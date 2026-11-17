using UnityEngine;

[CreateAssetMenu(menuName = "XPOCET/Configuracion de dia y noche")]
public sealed class XpocetDayNightSettings : ScriptableObject
{
    [Min(0.1f)] public float dayMinutes = 10;
    [Min(0.1f)] public float nightMinutes = 5;
    [Range(0, 23.99f)] public float startHour = 9;
    public bool pauseWithGame = true;
    [Range(-180,180)] public float sunAzimuth = -30;
    [Min(0)] public float sunLux = 100000;
    [Min(0)] public float moonLux = 80;
    [Min(0)] public float nightFillLux = 18;
    [Range(-3,1)] public float nightExposureCompensation = -0.8f;
    [Min(0.01f)] public float daySkyRadiance = 8000;
    [Min(0.01f)] public float nightSkyRadiance = 60;
    [Range(0,360)] public float skyRotation = 0;
    public Cubemap daySky;
    public Cubemap nightSky;

    void OnValidate()
    {
        dayMinutes = ValidDuration(dayMinutes); nightMinutes = ValidDuration(nightMinutes);
        if (float.IsNaN(startHour) || float.IsInfinity(startHour)) startHour=9;
        startHour = Mathf.Repeat(startHour,24);
    }
    public static float ValidDuration(float minutes) => float.IsNaN(minutes) || float.IsInfinity(minutes) ? 10 : Mathf.Max(0.1f,minutes);
}
