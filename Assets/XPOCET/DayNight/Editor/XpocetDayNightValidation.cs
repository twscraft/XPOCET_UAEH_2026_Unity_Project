using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

public static class XpocetDayNightValidation
{
    static XpocetDayNightCycle cycle;
    static double started, previousClock;
    static float originalDay, originalNight, originalHour, originalScale;
    static bool originalRunning, lastDay;
    static int stage, frames, changes, maxShadowCasters;
    static readonly HashSet<string> failures=new HashSet<string>();
    [MenuItem("Tools/XPOCET/Probar ciclo completo en Play")]
    public static void Start()
    {
        if(!EditorApplication.isPlaying)throw new InvalidOperationException("Esta prueba necesita Play.");
        if(cycle!=null)return;
        cycle=UnityEngine.Object.FindAnyObjectByType<XpocetDayNightCycle>();
        if(cycle==null)throw new Exception("No hay controlador.");
        var s=cycle.ActiveSettings;
        originalDay=s.dayMinutes;originalNight=s.nightMinutes;originalHour=cycle.Hour;originalRunning=cycle.Running;originalScale=Time.timeScale;
        failures.Clear();frames=changes=maxShadowCasters=stage=0;
        if(s==cycle.settings)failures.Add("Play modifica la configuracion guardada.");
        s.dayMinutes=.1f;s.nightMinutes=.15f;Time.timeScale=1;
        cycle.SetHour(6);cycle.Running=true;lastDay=cycle.IsDay;
        started=previousClock=EditorApplication.timeSinceStartup;
        Application.logMessageReceived+=Log;
        EditorApplication.update+=Tick;
    }
    static void Log(string message,string stack,LogType type) {
        if(type==LogType.Error||type==LogType.Exception||type==LogType.Assert)failures.Add(message);
    }
    static void Tick()
    {
        if(!EditorApplication.isPlaying||cycle==null){Finish(false);return;}
        frames++;
        int shadows=UnityEngine.Object.FindObjectsByType<Light>().Count(l=>l.enabled&&l.gameObject.activeInHierarchy&&l.type==LightType.Directional&&l.shadows!=LightShadows.None);
        maxShadowCasters=Math.Max(maxShadowCasters,shadows);
        if(shadows>1)failures.Add("Mas de una luz direccional con sombras.");
        if(cycle.IsDay!=lastDay){changes++;lastDay=cycle.IsDay;}
        double now=EditorApplication.timeSinceStartup;
        if(stage==0 && now-started>=17) {
            if(changes<2)failures.Add("No se completaron amanecer y anochecer.");
            previousClock=cycle.Hour;Time.timeScale=0;stage=1;started=now;
        } else if(stage==1 && now-started>=2) {
            if(cycle.ActiveSettings.pauseWithGame && Math.Abs(cycle.Hour-previousClock)>.01)failures.Add("El reloj avanzo durante pausa.");
            Time.timeScale=1;cycle.Running=false;previousClock=cycle.Hour;stage=2;started=now;
        } else if(stage==2 && now-started>=1) {
            if(Math.Abs(cycle.Hour-previousClock)>.01)failures.Add("El reloj avanzo con ciclo detenido.");
            Finish(true);
        }
    }
    static void Finish(bool completed)
    {
        EditorApplication.update-=Tick;Application.logMessageReceived-=Log;
        if(!completed)failures.Add("Prueba interrumpida.");
        if(cycle!=null) {
            cycle.ActiveSettings.dayMinutes=originalDay;cycle.ActiveSettings.nightMinutes=originalNight;
            cycle.SetHour(originalHour);cycle.Running=originalRunning;
        }
        Time.timeScale=originalScale;
        var lines=new List<string>{failures.Count==0?"PASS":"FAIL","Frames inspeccionados="+frames,"Cambios dia/noche="+changes,"Maximo luces direccionales con sombras="+maxShadowCasters,"Prueba pausa y detener ciclo completada="+completed,"Configuracion original restaurada: dia="+originalDay+" noche="+originalNight};
        lines.AddRange(failures);
        Directory.CreateDirectory("DayNightReports");File.WriteAllLines("DayNightReports/Validacion_Play.txt",lines);
        Debug.Log("XPOCET PLAY VALIDATION "+lines[0]+"; frames="+frames+"; max shadow casters="+maxShadowCasters);
        cycle=null;
    }
}
