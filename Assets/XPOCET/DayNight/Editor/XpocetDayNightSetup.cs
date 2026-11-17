using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.HighDefinition;
using UnityEngine.SceneManagement;

public static class XpocetDayNightSetup
{
    public const string Root="Assets/XPOCET/DayNight";
    public const string SettingsPath=Root+"/Configuracion Dia y Noche.asset";
    const string SkyRoot="Assets/BOXOPHOBIC/Skybox Cubemap Extended/Demo/Textures/";
    public const string ObjectName="CICLO DIA Y NOCHE - Controles";
    static IEnumerable<T> All<T>(Scene s) where T:Component => s.GetRootGameObjects().SelectMany(g=>g.GetComponentsInChildren<T>(true));
    static T Add<T>(VolumeProfile p) where T:VolumeComponent {
        if(!p.TryGet<T>(out var c)){c=p.Add<T>(); AssetDatabase.AddObjectToAsset(c,p);}
        c.active=true; EditorUtility.SetDirty(c); return c;
    }
    [MenuItem("Tools/XPOCET/Instalar o verificar ciclo dia y noche")]
    public static void Install()
    {
        if(EditorApplication.isPlaying)throw new InvalidOperationException("Salir de Play antes de instalar.");
        EditorSceneManager.SaveOpenScenes();
        var sceneSetup=EditorSceneManager.GetSceneManagerSetup();
        var settings=AssetDatabase.LoadAssetAtPath<XpocetDayNightSettings>(SettingsPath);
        if(settings==null) {
            settings=ScriptableObject.CreateInstance<XpocetDayNightSettings>();
            settings.daySky=AssetDatabase.LoadAssetAtPath<Cubemap>(SkyRoot+"Polyverse Skies - Blue Sky.png");
            settings.nightSky=AssetDatabase.LoadAssetAtPath<Cubemap>(SkyRoot+"Polyverse Skies - Night Sky.exr");
            if(settings.daySky==null || settings.nightSky==null)throw new Exception("No se encontraron ambos cubemaps BOXOPHOBIC.");
            AssetDatabase.CreateAsset(settings,SettingsPath);
        }
        var template=AssetDatabase.LoadAssetAtPath<Material>(Root+"/Cielo HDRP.mat");
        if(template==null) {
            var shader=Shader.Find("XPOCET/HDRP/BOXOPHOBIC Cycle Sky");
            if(shader==null || !shader.isSupported) throw new Exception("Shader HDRP no disponible.");
            template=new Material(shader); AssetDatabase.CreateAsset(template,Root+"/Cielo HDRP.mat");
        }
        var report=new List<string>();
        try {
            foreach(string path in new[]{"Assets/Scenes/MainMenu.unity","Assets/Scenes/MainScene.unity"}) {
                var scene=EditorSceneManager.OpenScene(path);
                var cycle=All<XpocetDayNightCycle>(scene).FirstOrDefault();
                if(cycle==null) cycle=new GameObject(ObjectName).AddComponent<XpocetDayNightCycle>();
                cycle.settings=settings;
                cycle.sun=All<Light>(scene).First(l=>l.type==LightType.Directional && l.name=="Directional Light");
                cycle.sun.lightmapBakeType=LightmapBakeType.Realtime;
                cycle.sun.shadows=LightShadows.Soft;
                cycle.moon=CreateLight(cycle.transform,"Luna - Luz nocturna",cycle.moon,false);
                cycle.nightFill=CreateLight(cycle.transform,"Apoyo nocturno - Visibilidad",cycle.nightFill,false);
                // Edit mode also uses only one directional shadow caster.
                cycle.moon.shadows=LightShadows.None; cycle.nightFill.shadows=LightShadows.None;
                EditorUtility.SetDirty(cycle.moon); EditorUtility.SetDirty(cycle.nightFill);
                if(cycle.environmentVolume==null) {
                    var go=new GameObject("Volumen - Cielo y exposicion del ciclo"); go.transform.SetParent(cycle.transform);
                    cycle.environmentVolume=go.AddComponent<Volume>();
                }
                var v=cycle.environmentVolume; v.isGlobal=true; v.weight=1; v.priority=100;
                string profilePath=Root+"/"+scene.name+" Ciclo Volume.asset";
                var profile=AssetDatabase.LoadAssetAtPath<VolumeProfile>(profilePath);
                if(profile==null) {profile=ScriptableObject.CreateInstance<VolumeProfile>();AssetDatabase.CreateAsset(profile,profilePath);}
                v.sharedProfile=profile;
                var env=Add<VisualEnvironment>(profile); env.skyType.Override(XpocetCycleSky.UniqueId); env.skyAmbientMode.Override(SkyAmbientMode.Dynamic);
                var sky=Add<XpocetCycleSky>(profile); sky.rendererMaterial.Override(template);
                sky.dayTexture.Override(settings.daySky); sky.nightTexture.Override(settings.nightSky);
                sky.daylight.Override(1); sky.radiance.Override(settings.daySkyRadiance); sky.tint.Override(Color.white);
                sky.sunDirection.Override(-cycle.sun.transform.forward); sky.sunVisibility.Override(1); sky.moonVisibility.Override(0);
                sky.rotationDegrees.Override(settings.skyRotation); sky.updateMode.Override(EnvironmentUpdateMode.Realtime); sky.updatePeriod.Override(1);
                var e=Add<Exposure>(profile); e.mode.Override(ExposureMode.Automatic);
                e.limitMin.Override(10.5f);e.limitMax.Override(16);e.compensation.Override(0);
                var shadows=Add<HDShadowSettings>(profile); shadows.maxShadowDistance.Override(250);shadows.cascadeShadowSplitCount.Override(4);
                var tone=Add<Tonemapping>(profile);tone.mode.Override(TonemappingMode.ACES);
                foreach(var cam in All<Camera>(scene)) {
                    var hd=cam.GetComponent<HDAdditionalCameraData>(); if(hd!=null){hd.volumeLayerMask|=1;EditorUtility.SetDirty(hd);}
                }
                EditorUtility.SetDirty(cycle);EditorUtility.SetDirty(v);EditorUtility.SetDirty(profile);EditorUtility.SetDirty(cycle.sun);
                AssetDatabase.SaveAssets();EditorSceneManager.MarkSceneDirty(scene);
                if(!EditorSceneManager.SaveScene(scene))throw new Exception("No se guardo "+path);
                report.Add(path+" | controlador=1 | configuracion compartida | BOXOPHOBIC dia/noche | HDRP | sombras=250m");
            }
        } finally {EditorSceneManager.RestoreSceneManagerSetup(sceneSetup);}
        string directory=Path.GetFullPath("DayNightReports"); Directory.CreateDirectory(directory);
        File.WriteAllLines(Path.Combine(directory,"Instalacion.txt"),report);
        Debug.Log("XPOCET: ciclo dia/noche instalado en MainMenu y MainScene.");
        SelectControls();
    }
    static Light CreateLight(Transform parent,string name,Light existing,bool shadows) {
        if(existing!=null)return existing;
        var go=new GameObject(name);go.transform.SetParent(parent);
        var l=go.AddComponent<Light>();l.type=LightType.Directional;
        go.AddComponent<HDAdditionalLightData>();l.lightUnit=LightUnit.Lux;l.intensity=0;
        l.shadows=shadows?LightShadows.Soft:LightShadows.None;l.lightmapBakeType=LightmapBakeType.Realtime;
        return l;
    }
    [MenuItem("Tools/XPOCET/Seleccionar controles de dia y noche")]
    public static void SelectControls() { var c=UnityEngine.Object.FindAnyObjectByType<XpocetDayNightCycle>();if(c!=null){Selection.activeGameObject=c.gameObject;EditorGUIUtility.PingObject(c);} }

    [MenuItem("Tools/XPOCET/Verificar reloj dia y noche")]
    public static void TestClock()
    {
        var failures=new List<string>();
        void Check(string name,double actual,double expected) {if(Math.Abs(actual-expected)>1e-8)failures.Add(name+": "+actual+" != "+expected);}
        Check("06:00 inicio",XpocetDayNightCycle.HourToPhase(6),0);
        Check("18:00 inicio noche",XpocetDayNightCycle.HourToPhase(18),.5);
        Check("00:00",XpocetDayNightCycle.HourToPhase(0),.75);
        Check("10 min dia exactos",XpocetDayNightCycle.AdvancePhase(0,600,10,5),.5);
        Check("5 min noche exactos",XpocetDayNightCycle.AdvancePhase(.5,300,10,5),0);
        Check("cruce con resto",XpocetDayNightCycle.AdvancePhase(.49,18,10,5),.51);
        Check("cruce medianoche ciclo",XpocetDayNightCycle.AdvancePhase(.99,12,10,5),.005);
        Check("100 ciclos con resto",XpocetDayNightCycle.AdvancePhase(0,90000+300,10,5),.25);
        Check("pausa",XpocetDayNightCycle.AdvancePhase(.32,0,10,5),.32);
        Check("duracion minima",XpocetDayNightCycle.AdvancePhase(0,6,0,0),.5);
        double split=0;for(int i=0;i<9000;i++)split=XpocetDayNightCycle.AdvancePhase(split,.1,10,5);
        if(Math.Min(Math.Abs(split),Math.Abs(1-split))>1e-8)failures.Add("Independencia de frames");
        if(failures.Count>0)throw new Exception(string.Join("\n",failures));
        Debug.Log("XPOCET CLOCK TESTS PASS: 11 pruebas.");
    }
}
