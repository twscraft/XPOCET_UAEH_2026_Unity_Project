using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(XpocetDayNightCycle))]
public sealed class XpocetDayNightEditor : Editor
{
    bool advanced;
    public override void OnInspectorGUI()
    {
        var cycle=(XpocetDayNightCycle)target;
        var config=cycle.ActiveSettings;
        EditorGUILayout.LabelField("DIA Y NOCHE",EditorStyles.boldLabel);
        EditorGUILayout.HelpBox(Application.isPlaying ? "Controles en vivo. Los cambios durante Play son temporales. El reloj se conserva al cambiar entre MainMenu y MainScene." : "Configuracion compartida por MainMenu y MainScene. Dia: 06:00-18:00. Noche: 18:00-06:00. Duraciones en minutos reales.",MessageType.Info);
        if(config==null) { DrawDefaultInspector(); return; }
        var so=new SerializedObject(config); so.Update();
        Field(so,"dayMinutes","Duracion del dia (min)","Minutos reales de 06:00 a 18:00.");
        Field(so,"nightMinutes","Duracion de la noche (min)","Minutos reales de 18:00 a 06:00.");
        Field(so,"startHour","Hora al iniciar","Hora inicial al comenzar una nueva partida en Play.");
        Field(so,"pauseWithGame","Detener al pausar el juego","El reloj no avanza con Time.timeScale = 0.");
        EditorGUILayout.LabelField("Ciclo completo",$"{config.dayMinutes+config.nightMinutes:0.##} minutos reales");
        EditorGUILayout.Space();
        EditorGUILayout.LabelField("VISIBILIDAD DE NOCHE",EditorStyles.boldLabel);
        Field(so,"moonLux","Luz de luna (lux)","Luz azul suave para distinguir estructuras.");
        Field(so,"nightFillLux","Luz minima de apoyo (lux)","Relleno sin sombras para distinguir carreteras incluso bajo los arboles.");
        Field(so,"nightExposureCompensation","Brillo nocturno (EV)","Aumentar aclara la noche; reducir oscurece. La exposicion automatica tiene limites nocturnos.");
        if(Application.isPlaying) {
            EditorGUILayout.Space(); EditorGUILayout.LabelField("VISTA PREVIA EN PLAY",EditorStyles.boldLabel);
            EditorGUILayout.LabelField("Hora actual",$"{(int)cycle.Hour:00}:{(int)(cycle.Hour%1*60):00} · {(cycle.IsDay?"Dia":"Noche")}");
            EditorGUI.BeginChangeCheck(); float hour=EditorGUILayout.Slider("Cambiar hora",cycle.Hour,0,23.99f);
            if(EditorGUI.EndChangeCheck()) cycle.SetHour(hour);
            cycle.Running=EditorGUILayout.Toggle("Ciclo en marcha",cycle.Running);
            using(new EditorGUILayout.HorizontalScope()) {
                if(GUILayout.Button("Amanecer"))cycle.SetHour(6);
                if(GUILayout.Button("Mediodia"))cycle.SetHour(12);
                if(GUILayout.Button("Atardecer"))cycle.SetHour(18);
                if(GUILayout.Button("Medianoche"))cycle.SetHour(0);
            }
            Repaint();
        }
        advanced=EditorGUILayout.Foldout(advanced,"Ajustes avanzados y referencias",true);
        if(advanced) {
            Field(so,"sunLux","Intensidad solar (lux)"); Field(so,"sunAzimuth","Orientacion del recorrido solar");
            Field(so,"daySkyRadiance","Luminancia del cielo diurno"); Field(so,"nightSkyRadiance","Luminancia del cielo nocturno");
            Field(so,"skyRotation","Rotacion de las texturas del cielo");
            Field(so,"daySky","Cielo BOXOPHOBIC - dia"); Field(so,"nightSky","Cielo BOXOPHOBIC - noche");
            using(new EditorGUI.DisabledScope(true)) {
                EditorGUILayout.ObjectField("Configuracion guardada",cycle.settings,typeof(XpocetDayNightSettings),false);
                EditorGUILayout.ObjectField("Sol",cycle.sun,typeof(Light),true);
                EditorGUILayout.ObjectField("Luna",cycle.moon,typeof(Light),true);
                EditorGUILayout.ObjectField("Volumen HDRP",cycle.environmentVolume,typeof(UnityEngine.Rendering.Volume),true);
            }
        }
        so.ApplyModifiedProperties();
    }
    static void Field(SerializedObject so,string name,string label,string tip="") => EditorGUILayout.PropertyField(so.FindProperty(name),new GUIContent(label,tip));
}
