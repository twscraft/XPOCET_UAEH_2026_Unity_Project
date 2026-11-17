using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.HighDefinition;
using UnityEngine.SceneManagement;

// Editor-only configuration and integration checks. Never included in a player.
[InitializeOnLoad]
public static class XpocetQualitySetup
{
    const string Key = "MAV.Settings.CalidadNombre";
    const string TestKey = "XPOCET.QualityTest";
    const string Folder = "Assets/HDRPDefaultResources/";
    static readonly string[] Names = { "Bajo", "Medio", "Alto" };
    static readonly int[] Resolutions = { 1024, 2048, 4096 };
    static readonly float[] Lod = { .7f, 1f, 1.5f };
    static string Reports => Path.GetFullPath("QualityReports");
    static string Report => Path.Combine(Reports, "verification.txt");
    static int cursor;
    static bool waiting;
    static double next;
    static int frames;

    static XpocetQualitySetup()
    {
        EditorApplication.playModeStateChanged += OnPlayState;
        EditorApplication.update += Tick;
        Application.logMessageReceived += OnLog;
        next = EditorApplication.timeSinceStartup + 5;
    }

    static SerializedProperty Required(SerializedObject so, string path)
    {
        return so.FindProperty(path) ?? throw new Exception("Missing property: " + path);
    }
    static void Int(SerializedObject so, string path, int value) => Required(so, path).intValue = value;
    static void Ints(SerializedObject so, string path, params int[] values)
    {
        var p = Required(so, path); p.arraySize = values.Length;
        for (int i = 0; i < values.Length; i++) p.GetArrayElementAtIndex(i).intValue = values[i];
    }
    static HDRenderPipelineAsset Asset(int i) => AssetDatabase.LoadAssetAtPath<HDRenderPipelineAsset>(Folder + "HDRP_" + Names[i] + ".asset");
    static void Check(bool condition, string message) { if (!condition) throw new Exception(message); }
    static void Log(string message) { Directory.CreateDirectory(Reports); File.AppendAllText(Report, message + Environment.NewLine); }
    static T[] All<T>(Scene scene) where T : Component => scene.GetRootGameObjects().SelectMany(g => g.GetComponentsInChildren<T>(true)).ToArray();

    [MenuItem("Tools/XPOCET/Quality/Configure HDRP Quality")]
    public static void Configure()
    {
        Check(!EditorApplication.isPlayingOrWillChangePlaymode, "Exit Play Mode first.");
        Check(!Enumerable.Range(0, SceneManager.sceneCount).Any(i => SceneManager.GetSceneAt(i).isDirty), "Save open scene changes before configuring quality.");
        for (int i = 0; i < 3; i++) Check(Asset(i) != null, "Missing HDRP_" + Names[i]);
        var oldNames = QualitySettings.names;
        Check(oldNames.SequenceEqual(new[] { "Very Low", "Low", "Medium", "High", "Very High", "Ultra" }) || oldNames.SequenceEqual(Names), "Unexpected quality levels; review before editing.");
        string backup = Path.Combine(Reports, "Backup-" + DateTime.Now.ToString("yyyyMMdd-HHmmss"));
        foreach (string path in new[] { "ProjectSettings/QualitySettings.asset", "ProjectSettings/GraphicsSettings.asset", Folder + "HDRP_Bajo.asset", Folder + "HDRP_Medio.asset", Folder + "HDRP_Alto.asset", "Assets/Scenes/MainMenu.unity", "Assets/Scenes/MainScene.unity" })
        {
            string dest = Path.Combine(backup, path); Directory.CreateDirectory(Path.GetDirectoryName(dest)); File.Copy(path, dest);
        }
        for (int i = 0; i < 3; i++)
        {
            var asset = Asset(i); var so = new SerializedObject(asset);
            const string root = "m_RenderPipelineSettings.";
            const string shadows = root + "hdShadowInitParams.";
            Int(so, shadows + "directionalShadowFilteringQuality", i);
            Int(so, shadows + "punctualShadowFilteringQuality", i);
            Int(so, shadows + "maxDirectionalShadowMapResolution", Resolutions[i]);
            Ints(so, shadows + "shadowResolutionDirectional.m_Values", Resolutions[i] / 2, Resolutions[i], Resolutions[i], Resolutions[i]);
            int punctual = 512 << i;
            Int(so, shadows + "maxPunctualShadowMapResolution", punctual);
            Ints(so, shadows + "shadowResolutionPunctual.m_Values", punctual / 2, punctual, punctual, punctual);
            var lod = Required(so, root + "lodBias.m_Values");
            for (int j = 0; j < lod.arraySize; j++) lod.GetArrayElementAtIndex(j).floatValue = Lod[i];
            // Preserve exposure, sky, fog and palette textures across all levels.
            so.ApplyModifiedPropertiesWithoutUndo(); EditorUtility.SetDirty(asset);
        }
        var quality = new SerializedObject(AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/QualitySettings.asset")[0]);
        var levels = Required(quality, "m_QualitySettings");
        if (levels.arraySize == 6) { levels.DeleteArrayElementAtIndex(5); levels.DeleteArrayElementAtIndex(4); levels.DeleteArrayElementAtIndex(0); }
        for (int i = 0; i < 3; i++)
        {
            var level = levels.GetArrayElementAtIndex(i);
            level.FindPropertyRelative("name").stringValue = Names[i];
            level.FindPropertyRelative("customRenderPipeline").objectReferenceValue = Asset(i);
            level.FindPropertyRelative("excludedTargetPlatforms").ClearArray();
            level.FindPropertyRelative("vSyncCount").intValue = 1;
            level.FindPropertyRelative("globalTextureMipmapLimit").intValue = 0;
            level.FindPropertyRelative("lodBias").floatValue = Lod[i];
            level.FindPropertyRelative("anisotropicTextures").intValue = i == 2 ? 2 : 1;
        }
        Int(quality, "m_CurrentQuality", 1);
        var defaults = Required(quality, "m_PerPlatformDefaultQuality");
        // Unity serializes this dictionary as string/int pairs. Do not walk
        // recursively into strings: their characters are integer properties.
        string[] platforms = { "Android", "GameCoreScarlett", "GameCoreXboxOne", "Lumin", "Nintendo 3DS", "Nintendo Switch", "Nintendo Switch 2", "PS4", "PS5", "Stadia", "Standalone", "WebGL", "Windows Store Apps", "XboxOne", "iPhone", "tvOS" };
        Check(defaults.isArray, "Unexpected platform defaults serialization.");
        defaults.arraySize = platforms.Length;
        for (int i = 0; i < platforms.Length; i++)
        {
            var pair = defaults.GetArrayElementAtIndex(i);
            var first = pair.FindPropertyRelative("first");
            var second = pair.FindPropertyRelative("second");
            Check(first != null && second != null, "Unexpected platform default pair.");
            first.stringValue = platforms[i]; second.intValue = 1;
        }
        quality.ApplyModifiedPropertiesWithoutUndo();
        GraphicsSettings.defaultRenderPipeline = Asset(1);
        QualitySettings.SetQualityLevel(1, true);
        AssetDatabase.SaveAssets();
        var setup = EditorSceneManager.GetSceneManagerSetup();
        try
        {
            foreach (string name in new[] { "MainMenu", "MainScene" })
            {
                var scene = EditorSceneManager.OpenScene("Assets/Scenes/" + name + ".unity");
                foreach (var light in All<HDAdditionalLightData>(scene))
                {
                    if (light.GetComponent<Light>().type != LightType.Directional) continue;
                    light.SetShadowResolutionOverride(false); light.SetShadowResolutionLevel(1);
                    EditorUtility.SetDirty(light);
                    PrefabUtility.RecordPrefabInstancePropertyModifications(light);
                }
                var panels = All<AjustesPersistentes>(scene);
                Check(panels.Length > 0, "No settings panel: " + name);
                foreach (var panel in panels)
                {
                    Check(panel.dropdownCalidad != null, "Missing quality dropdown: " + name);
                    var ev = panel.dropdownCalidad.onValueChanged;
                    Check(Enumerable.Range(0, ev.GetPersistentEventCount()).Any(i => ev.GetPersistentTarget(i) == panel && ev.GetPersistentMethodName(i) == "CambiarCalidad"), "Quality UI is not connected: " + name);
                }
                EditorSceneManager.MarkSceneDirty(scene);
                Check(EditorSceneManager.SaveScene(scene), "Could not save: " + name);
            }
        }
        finally { EditorSceneManager.RestoreSceneManagerSetup(setup); }
        AssetDatabase.SaveAssets();
        Log("CONFIGURED " + DateTime.Now + " | Bajo/Medio/Alto | directional shadows 1024/2048/4096 | default Medio | backups " + backup);
        Debug.Log("XPOCET HDRP quality configured: Bajo, Medio, Alto. Both scene menus verified.");
    }

    [MenuItem("Tools/XPOCET/Quality/Validate In Play Mode")]
    public static void Validate()
    {
        Check(!EditorApplication.isPlayingOrWillChangePlaymode, "Exit Play Mode first.");
        Check(QualitySettings.names.SequenceEqual(Names), "Configure quality first.");
        Check(SceneManager.GetActiveScene().name == "MainMenu", "Open MainMenu before validation.");
        SessionState.SetString(TestKey + ".old", PlayerPrefs.GetString(Key, ""));
        SessionState.SetBool(TestKey + ".had", PlayerPrefs.HasKey(Key));
        SessionState.SetInt(TestKey + ".phase", 0);
        SessionState.SetInt(TestKey + ".errors", 0);
        SessionState.SetBool(TestKey, true);
        Log("BEGIN PLAY VALIDATION " + DateTime.Now);
        EditorApplication.isPlaying = true;
    }
    static void OnLog(string text, string stack, LogType type)
    {
        if (!SessionState.GetBool(TestKey, false)) return;
        if (type == LogType.Error || type == LogType.Exception || type == LogType.Assert)
        { SessionState.SetInt(TestKey + ".errors", SessionState.GetInt(TestKey + ".errors", 0) + 1); Log("UNITY ERROR " + text + "\n" + stack); }
    }
    static void OnPlayState(PlayModeStateChange state)
    {
        if (!SessionState.GetBool(TestKey, false)) return;
        if (state == PlayModeStateChange.EnteredPlayMode) { cursor = 0; waiting = false; next = EditorApplication.timeSinceStartup + 5; }
        if (state != PlayModeStateChange.EnteredEditMode) return;
        if (SessionState.GetInt(TestKey + ".phase", 0) == 1)
        { EditorApplication.delayCall += () => EditorApplication.isPlaying = true; return; }
        if (SessionState.GetBool(TestKey + ".had", false)) PlayerPrefs.SetString(Key, SessionState.GetString(TestKey + ".old", ""));
        else PlayerPrefs.DeleteKey(Key);
        PlayerPrefs.Save(); QualitySettings.SetQualityLevel(1, true);
        SessionState.SetBool(TestKey, false);
        Debug.Log("XPOCET quality validation finished. Report: " + Report);
    }
    static void Tick()
    {
        if (!SessionState.GetBool(TestKey, false) || !EditorApplication.isPlaying || EditorApplication.isPaused || EditorApplication.timeSinceStartup < next) return;
        try
        {
            if (SessionState.GetInt(TestKey + ".phase", 0) == 1)
            {
                Check(QualitySettings.GetQualityLevel() == 2 && PreferenciasManosAlVolante.IndiceCalidad == 2, "Saved quality was not restored on new Play session.");
                Check(GraphicsSettings.currentRenderPipeline == Asset(2), "HDRP asset not restored on startup.");
                Check(SessionState.GetInt(TestKey + ".errors", 0) == 0, "Unity reported errors during validation.");
                Log("PASS: second Play session restored Alto from PlayerPrefs; zero errors.");
                SessionState.SetInt(TestKey + ".phase", 2); EditorApplication.isPlaying = false; return;
            }
            if (cursor == 6)
            { Log("PASS: both scenes and all three quality levels."); SessionState.SetInt(TestKey + ".phase", 1); EditorApplication.isPlaying = false; return; }
            int index = cursor % 3;
            string sceneName = cursor < 3 ? "MainMenu" : "MainScene";
            if (SceneManager.GetActiveScene().name != sceneName)
            {
                Check(PlayerPrefs.GetString(Key) == "Alto", "Preference missing before scene transition.");
                Time.timeScale = 1;
                SceneManager.LoadScene(sceneName);
                next = EditorApplication.timeSinceStartup + 5;
                return;
            }
            var panel = All<AjustesPersistentes>(SceneManager.GetActiveScene()).First();
            if (!waiting)
            {
                if (cursor == 3) Check(QualitySettings.GetQualityLevel() == 2 && PlayerPrefs.GetString(Key) == "Alto", "Quality changed during scene transition.");
                for (Transform t = panel.transform; t != null; t = t.parent) t.gameObject.SetActive(true);
                Check(panel.dropdownCalidad.options.Select(o => o.text).SequenceEqual(Names), "Dropdown names differ from quality settings.");
                panel.dropdownCalidad.value = index;
                waiting = true; frames = Time.frameCount;
                next = EditorApplication.timeSinceStartup + 3;
                return;
            }
            if (Time.frameCount < frames + 3) return;
            Check(QualitySettings.GetQualityLevel() == index, "Dropdown did not change quality.");
            Check(PlayerPrefs.GetString(Key) == Names[index], "Dropdown did not save preference.");
            Check(GraphicsSettings.currentRenderPipeline == Asset(index), "Wrong active HDRP Asset.");
            Check(RenderPipelineManager.currentPipeline is HDRenderPipeline, "HDRP renderer inactive.");
            var settings = Asset(index).currentPlatformRenderPipelineSettings.hdShadowInitParams;
            Check(settings.maxDirectionalShadowMapResolution == Resolutions[index], "Wrong shadow resolution.");
            foreach (var cycle in All<XpocetDayNightCycle>(SceneManager.GetActiveScene()))
                foreach (var light in new[] { cycle.sun, cycle.moon })
                {
                    var data = light.GetComponent<HDAdditionalLightData>();
                    Check(!data.shadowResolution.useOverride && data.shadowResolution.level == 1, "Light overrides quality tier.");
                }
            Log("PASS " + sceneName + " | " + Names[index] + " | " + Asset(index).name + " | shadows " + Resolutions[index] + " | UI callback + PlayerPrefs + renderer OK");
            cursor++; waiting = false; next = EditorApplication.timeSinceStartup + 1;
        }
        catch (Exception ex)
        {
            Log("FAILED " + ex); SessionState.SetInt(TestKey + ".phase", 2); EditorApplication.isPlaying = false;
        }
    }
}
