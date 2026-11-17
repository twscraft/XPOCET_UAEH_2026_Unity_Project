using System;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.HighDefinition;

[DisallowMultipleComponent, AddComponentMenu("XPOCET/Ciclo de dia y noche")]
public sealed class XpocetDayNightCycle : MonoBehaviour
{
    public XpocetDayNightSettings settings;
    public Light sun, moon, nightFill;
    public Volume environmentVolume;
    [SerializeField, HideInInspector] bool running = true;
    static XpocetDayNightCycle owner;
    static XpocetDayNightSettings sessionSettings;
    static double phase;
    static bool clockInitialized;
    VolumeProfile runtimeProfile;
    XpocetCycleSky sky;
    Exposure exposure;
    Light originalSun;
    HDAdditionalLightData sunData, moonData;
    LightSnapshot savedSun, savedMoon, savedFill;

    public XpocetDayNightSettings ActiveSettings => Application.isPlaying && sessionSettings!=null ? sessionSettings : settings;
    public float Hour => (float)((phase*24+6)%24);
    public bool IsDay => phase<0.5;
    public bool Running { get=>running; set=>running=value; }
    public float Daylight { get; private set; }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetClock() { if(sessionSettings!=null) Destroy(sessionSettings); owner=null; sessionSettings=null; phase=0; clockInitialized=false; }
    void OnEnable()
    {
        if(!Application.isPlaying) return;
        if(owner!=null && owner!=this) { Debug.LogWarning("Ciclo dia/noche duplicado: se mantiene el controlador activo.",this); enabled=false; return; }
        if(settings==null || sun==null || moon==null || nightFill==null || environmentVolume==null || environmentVolume.sharedProfile==null) {
            Debug.LogError("Faltan referencias del ciclo dia/noche.",this); enabled=false; return;
        }
        owner=this;
        if(sessionSettings==null) { sessionSettings=Instantiate(settings); sessionSettings.hideFlags=HideFlags.HideAndDontSave; }
        if(!clockInitialized) { phase=HourToPhase(sessionSettings.startHour); clockInitialized=true; }
        savedSun=new LightSnapshot(sun); savedMoon=new LightSnapshot(moon); savedFill=new LightSnapshot(nightFill);
        originalSun=RenderSettings.sun;
        sunData=sun.GetComponent<HDAdditionalLightData>(); moonData=moon.GetComponent<HDAdditionalLightData>();
        // Volume.profile instantiates a private copy: Play never edits the saved asset.
        runtimeProfile=environmentVolume.profile;
        if(!runtimeProfile.TryGet(out sky) || !runtimeProfile.TryGet(out exposure)) {
            Debug.LogError("El perfil del ciclo no contiene cielo y exposicion.",this); enabled=false; return;
        }
        Apply();
    }
    void Update()
    {
        if(owner!=this || sky==null) return;
        var s=ActiveSettings;
        // Real seconds, independent of slow motion; explicitly freeze with pause.
        if(running && !(s.pauseWithGame && Time.timeScale==0))
            phase=AdvancePhase(phase,Time.unscaledDeltaTime,s.dayMinutes,s.nightMinutes);
        Apply();
    }
    public static double HourToPhase(double hour) => ((hour-6)%24+24)%24/24;
    public static double AdvancePhase(double current, double seconds, double dayMinutes, double nightMinutes)
    {
        double d=Math.Max(0.1,double.IsNaN(dayMinutes)||double.IsInfinity(dayMinutes)?10:dayMinutes)*60;
        double n=Math.Max(0.1,double.IsNaN(nightMinutes)||double.IsInfinity(nightMinutes)?5:nightMinutes)*60;
        current=(current%1+1)%1;
        if(double.IsNaN(seconds)||double.IsInfinity(seconds)||seconds<=0) return current;
        double elapsed=current<0.5?current*2*d:d+(current-0.5)*2*n;
        elapsed=(elapsed+seconds)%(d+n);
        return elapsed<d?elapsed/(2*d):0.5+(elapsed-d)/(2*n);
    }
    public void SetHour(float hour) { if(float.IsNaN(hour)||float.IsInfinity(hour)) return; phase=HourToPhase(hour); Apply(); }
    public void Apply()
    {
        if(sky==null || ActiveSettings==null) return;
        var s=ActiveSettings;
        float angle=(float)phase*360;
        float altitude=Mathf.Sin(angle*Mathf.Deg2Rad);
        Daylight=Mathf.SmoothStep(0,1,Mathf.InverseLerp(-0.18f,0.35f,altitude));
        float sunPower=Mathf.Pow(Mathf.Max(0,altitude),1.35f);
        float moonPower=1-Daylight;
        sun.transform.rotation=Quaternion.Euler(angle,s.sunAzimuth,0);
        moon.transform.rotation=Quaternion.Euler(angle+180,s.sunAzimuth,0);
        // Broad, shadow-free low-level fill keeps driving surfaces legible at night.
        nightFill.transform.rotation=Quaternion.Euler(70,s.sunAzimuth+140,0);
        sun.enabled=true; moon.enabled=true; nightFill.enabled=true;
        sun.lightUnit=LightUnit.Lux; moon.lightUnit=LightUnit.Lux; nightFill.lightUnit=LightUnit.Lux;
        sun.intensity=Mathf.Max(0,s.sunLux)*sunPower;
        moon.intensity=Mathf.Max(0,s.moonLux)*moonPower;
        nightFill.intensity=Mathf.Max(0,s.nightFillLux)*moonPower;
        // HDRP has one cascaded directional shadow atlas. Never let both
        // directional lights reserve it, including at dawn and dusk.
        float sunShare=sun.intensity/Mathf.Max(.001f,sun.intensity+moon.intensity);
        bool solarShadows=sunShare>=.5f;
        sun.shadows=solarShadows?LightShadows.Soft:LightShadows.None;
        moon.shadows=solarShadows?LightShadows.None:LightShadows.Soft;
        nightFill.shadows=LightShadows.None;
        if(sunData!=null) sunData.shadowDimmer=Mathf.SmoothStep(0,1,Mathf.InverseLerp(.5f,.7f,sunShare));
        if(moonData!=null) moonData.shadowDimmer=Mathf.SmoothStep(0,1,Mathf.InverseLerp(.5f,.3f,sunShare));
        sun.useColorTemperature=false; moon.useColorTemperature=false; nightFill.useColorTemperature=false;
        sun.color=Color.Lerp(new Color(1,0.38f,0.12f),new Color(1,0.96f,0.88f),Mathf.SmoothStep(0,1,Mathf.InverseLerp(0,0.45f,altitude)));
        moon.color=new Color(0.56f,0.69f,1); nightFill.color=new Color(0.48f,0.63f,1);
        RenderSettings.sun=Daylight>0.5f?sun:moon;
        sky.dayTexture.value=s.daySky; sky.nightTexture.value=s.nightSky;
        sky.daylight.value=Daylight;
        sky.radiance.value=Mathf.Exp(Mathf.Lerp(Mathf.Log(Mathf.Max(.01f,s.nightSkyRadiance)),Mathf.Log(Mathf.Max(.01f,s.daySkyRadiance)),Daylight));
        float twilight=1-Mathf.SmoothStep(0,1,Mathf.InverseLerp(0,0.3f,Mathf.Abs(altitude)));
        sky.tint.value=Color.Lerp(Color.Lerp(new Color(.42f,.55f,1),Color.white,Daylight),new Color(1,.48f,.26f),twilight*.65f);
        sky.sunDirection.value=-sun.transform.forward;
        sky.sunVisibility.value=Mathf.SmoothStep(0,1,Mathf.InverseLerp(-.03f,.08f,altitude));
        sky.moonVisibility.value=moonPower;
        sky.rotationDegrees.value=s.skyRotation;
        exposure.mode.Override(ExposureMode.Automatic);
        // Prevent auto-exposure from making midnight look like noon.
        exposure.limitMin.Override(Mathf.Lerp(4.5f,10.5f,Daylight));
        exposure.limitMax.Override(Mathf.Lerp(7,16,Daylight));
        exposure.compensation.Override(Mathf.Lerp(s.nightExposureCompensation,0,Daylight));
    }
    void OnDisable()
    {
        if(owner!=this) return;
        savedSun.Restore(sun); savedMoon.Restore(moon); savedFill.Restore(nightFill);
        RenderSettings.sun=originalSun;
        if(environmentVolume!=null) environmentVolume.profile=null;
        if(runtimeProfile!=null) {
            foreach(var c in runtimeProfile.components) if(c!=null) Destroy(c);
            Destroy(runtimeProfile);
        }
        runtimeProfile=null; sky=null; exposure=null; owner=null;
    }
    struct LightSnapshot
    {
        readonly Quaternion rotation; readonly Color color; readonly float intensity;
        readonly bool enabled, temperature; readonly LightUnit unit;
        readonly LightShadows shadows; readonly float shadowDimmer;
        public LightSnapshot(Light l) { rotation=l.transform.rotation; color=l.color; intensity=l.intensity; enabled=l.enabled; temperature=l.useColorTemperature; unit=l.lightUnit; shadows=l.shadows; shadowDimmer=l.TryGetComponent<HDAdditionalLightData>(out var hd)?hd.shadowDimmer:1; }
        public void Restore(Light l) { if(l==null)return; l.transform.rotation=rotation; l.color=color; l.lightUnit=unit; l.intensity=intensity; l.enabled=enabled; l.useColorTemperature=temperature; l.shadows=shadows; if(l.TryGetComponent<HDAdditionalLightData>(out var hd))hd.shadowDimmer=shadowDimmer; }
    }
}
