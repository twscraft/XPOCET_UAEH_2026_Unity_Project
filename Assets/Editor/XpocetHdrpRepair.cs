using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.HighDefinition;
using UnityEngine.SceneManagement;

// Explicit, repeatable editor utility. No player/build code.
public static class XpocetHdrpRepair
{
    static string Output => Path.GetFullPath(Path.Combine(Application.dataPath,"../HDRPRepairReports"));
    const string Pack = "Assets/SimplePoly City - Low Poly Assets";
    static readonly List<string> log = new List<string>();
    static T Component<T>(VolumeProfile p) where T : VolumeComponent
    {
        if (!p.TryGet<T>(out var c)) { c=p.Add<T>(false); AssetDatabase.AddObjectToAsset(c,p); }
        c.active=true; EditorUtility.SetDirty(c); return c;
    }
    static void Profile(VolumeProfile p)
    {
        var e=Component<Exposure>(p);
        e.mode.Override(ExposureMode.Automatic);
        e.limitMin.Override(-1); e.limitMax.Override(20); e.compensation.Override(0);
        var s=Component<HDShadowSettings>(p);
        s.maxShadowDistance.Override(250); s.cascadeShadowSplitCount.Override(4);
        EditorUtility.SetDirty(p);
    }
    static IEnumerable<T> All<T>(Scene s) where T: UnityEngine.Component => s.GetRootGameObjects().SelectMany(g=>g.GetComponentsInChildren<T>(true));
    [MenuItem("Tools/XPOCET/Repair HDRP")]
    public static void Repair()
    {
        if(EditorApplication.isPlaying) throw new Exception("Exit play mode before repair");
        Directory.CreateDirectory(Output);
        log.Clear();
        var setup=EditorSceneManager.GetSceneManagerSetup();
        EditorSceneManager.SaveOpenScenes();
        var shader=Shader.Find("HDRP/Lit");
        if(shader==null) throw new Exception("HDRP/Lit unavailable");
        var pipeline=AssetDatabase.LoadAssetAtPath<HDRenderPipelineAsset>("Assets/HDRPDefaultResources/HDRenderPipelineAsset.asset");
        if(pipeline==null) throw new Exception("HDRP asset unavailable");
        if (!(GraphicsSettings.defaultRenderPipeline is HDRenderPipelineAsset)) GraphicsSettings.defaultRenderPipeline=pipeline;
        int quality=QualitySettings.GetQualityLevel();
        for(int i=0;i<QualitySettings.names.Length;i++) { QualitySettings.SetQualityLevel(i,false); if (!(QualitySettings.renderPipeline is HDRenderPipelineAsset)) QualitySettings.renderPipeline=pipeline; }
        QualitySettings.SetQualityLevel(quality,false);
        var materials=AssetDatabase.FindAssets("t:Material",new[]{Pack}).Select(AssetDatabase.GUIDToAssetPath).ToArray();
        // Preflight every mapping before modifying materials.
        foreach(var path in materials)
        {
            string name=Path.GetFileNameWithoutExtension(path);
            if(name.StartsWith("Vehicle_")) name=Regex.Replace(name,@"_color0([123])$","_$1");
            if(AssetDatabase.LoadAssetAtPath<Texture2D>(Pack+"/Textures/"+name+".png")==null) throw new Exception("Missing palette: "+path);
        }
        foreach(var path in materials)
        {
            var m=AssetDatabase.LoadAssetAtPath<Material>(path);
            string name=m.name;
            if(name.StartsWith("Vehicle_")) name=Regex.Replace(name,@"_color0([123])$","_$1");
            var tex=AssetDatabase.LoadAssetAtPath<Texture2D>(Pack+"/Textures/"+name+".png");
            m.shader=shader; m.SetTexture("_BaseColorMap",tex); m.SetColor("_BaseColor",Color.white);
            m.SetTextureScale("_BaseColorMap",Vector2.one); m.SetTextureOffset("_BaseColorMap",Vector2.zero);
            HDMaterial.ValidateMaterial(m); EditorUtility.SetDirty(m);
            if(m.shader.name!="HDRP/Lit" || m.GetTexture("_BaseColorMap")!=tex || m.GetColor("_BaseColor")!=Color.white) throw new Exception("Material failed: "+path);
            log.Add("MATERIAL OK | "+path+" | "+AssetDatabase.GetAssetPath(tex));
        }
        AssetDatabase.SaveAssets();
        if(!AssetDatabase.IsValidFolder("Assets/HDRPSceneProfiles")) AssetDatabase.CreateFolder("Assets","HDRPSceneProfiles");
        var scenes=AssetDatabase.FindAssets("t:Scene",new[]{"Assets"}).Select(AssetDatabase.GUIDToAssetPath).OrderBy(x=>x).ToArray();
        try
        {
            foreach(var path in scenes)
            {
                EditorUtility.DisplayProgressBar("XPOCET HDRP",path,Array.IndexOf(scenes,path)/(float)scenes.Length);
                var scene=EditorSceneManager.OpenScene(path,OpenSceneMode.Single);
                var volumes=All<Volume>(scene).ToList();
                var global=volumes.FirstOrDefault(v=>v.isGlobal && v.enabled && v.gameObject.activeInHierarchy);
                if(global==null) { global=new GameObject("Global Volume").AddComponent<Volume>(); global.isGlobal=true; volumes.Add(global); }
                global.enabled=true; global.weight=1; global.priority=Mathf.Max(global.priority,volumes.Max(v=>v.priority));
                foreach(var v in volumes)
                {
                    if(v.sharedProfile==null)
                    {
                        var profile=ScriptableObject.CreateInstance<VolumeProfile>();
                        string dest=AssetDatabase.GenerateUniqueAssetPath("Assets/HDRPSceneProfiles/"+scene.name+"_"+v.name+".asset");
                        AssetDatabase.CreateAsset(profile,dest); v.sharedProfile=profile;
                    }
                    Profile(v.sharedProfile); EditorUtility.SetDirty(v);
                }
                foreach(var cam in All<Camera>(scene))
                {
                    var data=cam.GetComponent<HDAdditionalCameraData>() ?? cam.gameObject.AddComponent<HDAdditionalCameraData>();
                    data.volumeLayerMask |= 1 << global.gameObject.layer;
                    if(cam.farClipPlane<250) cam.farClipPlane=1000;
                    EditorUtility.SetDirty(data); EditorUtility.SetDirty(cam);
                }
                int suns=0;
                if (!All<Light>(scene).Any(l=>l.type==LightType.Directional))
                {
                    var named=All<Transform>(scene).FirstOrDefault(t=>t.name=="Directional Light");
                    var sunObject=named!=null?named.gameObject:new GameObject("Directional Light");
                    if(named==null) sunObject.transform.rotation=Quaternion.Euler(50,-30,0);
                    var sun=sunObject.GetComponent<Light>() ?? sunObject.AddComponent<Light>(); sun.type=LightType.Directional;
                    if(!sunObject.TryGetComponent<HDAdditionalLightData>(out _)) sunObject.AddComponent<HDAdditionalLightData>();
                    sun.lightUnit=LightUnit.Lux; sun.intensity=100000;
                    RenderSettings.sun=sun;
                }
                foreach(var light in All<Light>(scene).Where(l=>l.type==LightType.Directional))
                {
                    suns++;
                    if(!light.TryGetComponent<HDAdditionalLightData>(out var hd)) hd=light.gameObject.AddComponent<HDAdditionalLightData>();
                    // Legacy directional values were in the 0-10 range, not physical lux.
                    if(light.intensity<=10) { light.lightUnit=LightUnit.Lux; light.intensity=100000; }
                    light.shadows=LightShadows.Soft;
                    EditorUtility.SetDirty(light); EditorUtility.SetDirty(hd);
                }
                AssetDatabase.SaveAssets();
                EditorSceneManager.MarkSceneDirty(scene);
                if(!EditorSceneManager.SaveScene(scene)) throw new Exception("Scene save failed: "+path);
                foreach(var v in volumes)
                {
                    if(!v.sharedProfile.TryGet<Exposure>(out var e) || e.mode.value!=ExposureMode.Automatic || !e.mode.overrideState) throw new Exception("Exposure failed: "+path);
                    if(!v.sharedProfile.TryGet<HDShadowSettings>(out var s) || s.maxShadowDistance.value!=250 || !s.maxShadowDistance.overrideState) throw new Exception("Shadows failed: "+path);
                }
                log.Add("SCENE OK | "+path+" | volumes="+volumes.Count+" | directionalLights="+suns+" | Automatic | 250 m");
                File.WriteAllLines(Output+"/Unity_HDRP_verificacion.txt",log);
            }
        }
        finally { EditorUtility.ClearProgressBar(); EditorSceneManager.RestoreSceneManagerSetup(setup); }
        log.Add("COMPLETE | materials="+materials.Length+" | scenes="+scenes.Length);
        if(!scenes.Any(x=>Path.GetFileName(x)=="SampleScene.unity")) log.Add("SampleScene.unity absent; only its baked-data folder exists.");
        File.WriteAllLines(Output+"/Unity_HDRP_verificacion.txt",log);
        Debug.Log("XPOCET HDRP repair COMPLETE: "+materials.Length+" materials; "+scenes.Length+" scenes.");
    }
    [MenuItem("Tools/XPOCET/Balance Main Scenes Lighting")]
    public static void Balance()
    {
        if(EditorApplication.isPlaying) throw new Exception("Exit play mode");
        var setup=EditorSceneManager.GetSceneManagerSetup();
        try {
            foreach(var path in new[]{"Assets/Scenes/MainMenu.unity","Assets/Scenes/MainScene.unity"}) {
                var s=EditorSceneManager.OpenScene(path);
                foreach(var v in All<Volume>(s).Where(v=>v.isGlobal)) {
                    var env=Component<VisualEnvironment>(v.sharedProfile);
                    env.skyType.Override((int)SkyType.PhysicallyBased);
                    env.skyAmbientMode.Override(SkyAmbientMode.Dynamic);
                    var sky=Component<PhysicallyBasedSky>(v.sharedProfile);
                    sky.exposure.Override(0); sky.multiplier.Override(1);
                    var tone=Component<Tonemapping>(v.sharedProfile); tone.mode.Override(TonemappingMode.ACES);
                    EditorUtility.SetDirty(v.sharedProfile);
                }
                AssetDatabase.SaveAssets(); EditorSceneManager.SaveScene(s);
            }
        } finally {EditorSceneManager.RestoreSceneManagerSetup(setup);}
    }
}
