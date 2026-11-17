/* 
    Diseñado por [GameDev]: twscraft {AMM}, from UAEH - ICBI
    Powered by the best: [AI.Eng] xSaidanMka {AAAI}, from ESCOM & CINVESTAV by IPN
    
    Hecho por: ChatGPT-6
*/


using UnityEngine;

// Manos al Volante - haptica de bajo consumo para motores ERM (uno por mano).
// El script solamente transmite intensidades 0-100 al receptor ESP32.
// NO activa el motor si el firmware C3 mantiene ENABLE_MOTOR=false.
// Colocar en el MISMO GameObject que ManualCarController y Rigidbody.
public class GloveHaptics : MonoBehaviour
{
    [Header("Referencias existentes")]
    public GloveSerialReader reader;
    public ManualCarController car;

    [Header("Seguridad y frecuencia de comandos")]
    [Tooltip("Activar SOLO cuando se hayan verificado regulacion, transistor/MOSFET, diodo y corriente del motor.")]
    public bool enableHaptics = false;
    [Range(0f, 1f)] public float masterIntensity = 1f;
    [Min(0.02f)] public float sendInterval = 0.04f;
    [Min(0.1f)] public float keepAliveSeconds = 0.5f;

    [Header("Conduccion - pulsos SUAVES para ahorrar bateria")]
    public bool engineRumble = true;
    [Tooltip("No vibra al ralentí ni con el coche quieto.")]
    [Min(0f)] public float minimumDrivingKmh = 3f;
    [Tooltip("Tiempo entre impulsos. Mayor intervalo = menor consumo promedio.")]
    [Min(0.1f)] public float enginePulseInterval = 0.55f;
    [Tooltip("Duracion de cada impulso de conduccion.")]
    [Range(0.02f, 0.2f)] public float enginePulseDuration = 0.07f;
    [Range(0, 100)] public int enginePulseLow = 28;
    [Range(0, 100)] public int enginePulseHigh = 35;
    [Min(1f)] public float idleRpm = 800f;
    [Min(1f)] public float maxRpm = 6000f;

    [Header("Choques - cualquier magnitud, intensidad media")]
    [Tooltip("0 significa no filtrar por velocidad. Se activa al recibir OnCollisionEnter, no con Trigger.")]
    [Min(0f)] public float minimumCollisionSpeed = 0f;
    [Range(0, 100)] public int collisionIntensity = 65;
    [Range(0, 100)] public int strongCollisionIntensity = 75;
    [Min(0.1f)] public float fullImpactSpeed = 10f;
    [Range(0.05f, 0.6f)] public float collisionDuration = 0.2f;
    [Tooltip("Evita ráfagas continuas por contactos de fisica muy repetidos.")]
    [Min(0f)] public float collisionCooldown = 0.2f;
    [Tooltip("Por defecto se consideran todas las capas; desmarca suelo/carretera si provoca choques falsos.")]
    public LayerMask collisionLayers = ~0;

    [Header("Opcional - otros eventos")]
    public bool gearClick = true;
    [Range(0, 100)] public int gearClickIntensity = 42;
    [Range(0.03f, 0.2f)] public float gearClickDuration = 0.075f;
    public bool startPulse = false;
    [Range(0, 100)] public int startPulseIntensity = 40;
    [Range(0.03f, 0.3f)] public float startPulseDuration = 0.12f;
    public bool limiterPulse = false;
    [Range(0, 100)] public int limiterPulseIntensity = 45;

    private readonly float[] pulseStrength = new float[2];
    private readonly float[] pulseUntil = new float[2];
    private readonly int[] lastSent = { -1, -1 };
    private readonly float[] lastSentAt = { -999f, -999f };
    private Rigidbody vehicleBody;
    private int previousGear;
    private bool previousEngineRunning;
    private bool wasEnabled;
    private float nextSendAt;
    private float nextEnginePulse;
    private float lastCollisionAt = -999f;
    private float nextLimiterPulse;

    private void Start()
    {
        if (car == null) car = GetComponent<ManualCarController>();
        vehicleBody = GetComponent<Rigidbody>();
        if (car != null)
        {
            previousGear = car.currentGear;
            previousEngineRunning = car.isEngineRunning;
        }
    }

    private void Update()
    {
        if (!enableHaptics || reader == null || car == null)
        {
            if (wasEnabled) SendStop();
            wasEnabled = false;
            return;
        }
        wasEnabled = true;
        float now = Time.unscaledTime;

        // Pausa del juego: silencio para gastar menos y evitar vibraciones persistentes.
        if (Time.timeScale <= 0f)
        {
            if (now >= nextSendAt)
            {
                nextSendAt = now + Mathf.Max(0.02f, sendInterval);
                Send(0, 'L', 0, now);
                Send(1, 'R', 0, now);
            }
            return;
        }

        if (car.isEngineRunning && !previousEngineRunning && startPulse)
        {
            Pulse(0, startPulseIntensity / 100f, startPulseDuration, now);
            Pulse(1, startPulseIntensity / 100f, startPulseDuration, now);
        }
        previousEngineRunning = car.isEngineRunning;

        if (car.currentGear != previousGear)
        {
            previousGear = car.currentGear;
            if (gearClick) Pulse(1, gearClickIntensity / 100f, gearClickDuration, now);
        }

        if (limiterPulse && car.RevLimiterActive && now >= nextLimiterPulse)
        {
            nextLimiterPulse = now + 0.25f;
            Pulse(0, limiterPulseIntensity / 100f, 0.06f, now);
            Pulse(1, limiterPulseIntensity / 100f, 0.06f, now);
        }

        // Ahorro de bateria: breve vibracion a intervalos, NO PWM continuo.
        float speedKmh = vehicleBody != null ? vehicleBody.linearVelocity.magnitude * 3.6f : 0f;
        bool driving = engineRumble && car.isEngineRunning && speedKmh >= minimumDrivingKmh;
        if (driving && now >= nextEnginePulse)
        {
            nextEnginePulse = now + Mathf.Max(0.1f, enginePulseInterval);
            float rpm = Mathf.InverseLerp(idleRpm, Mathf.Max(idleRpm + 1f, maxRpm), car.EngineRPM);
            float strength = Mathf.Lerp(enginePulseLow, enginePulseHigh, rpm) / 100f;
            Pulse(0, strength, enginePulseDuration, now);
            Pulse(1, strength, enginePulseDuration, now);
        }
        else if (!driving)
        {
            nextEnginePulse = now;
        }

        if (now < nextSendAt) return;
        nextSendAt = now + Mathf.Max(0.02f, sendInterval);
        Send(0, 'L', ComputeLevel(0, now), now);
        Send(1, 'R', ComputeLevel(1, now), now);
    }

    private int ComputeLevel(int hand, float now)
    {
        float strength = now < pulseUntil[hand] ? pulseStrength[hand] : 0f;
        return Mathf.RoundToInt(Mathf.Clamp01(strength * masterIntensity) * 100f);
    }

    private void Pulse(int hand, float strength, float seconds, float now)
    {
        strength = Mathf.Clamp01(strength);
        float end = now + Mathf.Max(0.01f, seconds);
        // Una colision fuerte mantiene prioridad frente a un pulso de motor mas debil.
        if (now < pulseUntil[hand] && pulseStrength[hand] > strength) return;
        pulseStrength[hand] = strength;
        pulseUntil[hand] = end;
    }

    private void Send(int hand, char tag, int level, float now)
    {
        if (level == lastSent[hand] && now - lastSentAt[hand] < keepAliveSeconds) return;
        lastSent[hand] = level;
        lastSentAt[hand] = now;
        reader.SendHaptics(tag, level);
    }

    private void OnCollisionEnter(Collision collision)
    {
        if (!enableHaptics || reader == null || car == null) return;
        if (Time.timeScale <= 0f) return;
        if ((collisionLayers.value & (1 << collision.gameObject.layer)) == 0) return;

        float speed = collision.relativeVelocity.magnitude;
        if (speed < minimumCollisionSpeed) return;
        float now = Time.unscaledTime;
        if (now - lastCollisionAt < collisionCooldown) return;
        lastCollisionAt = now;

        // AUN UN TOQUE LEVE produce una vibracion media.
        float t = Mathf.Clamp01(speed / Mathf.Max(0.1f, fullImpactSpeed));
        float intensity = Mathf.Lerp(collisionIntensity, strongCollisionIntensity, t) / 100f;
        Pulse(0, intensity, collisionDuration, now);
        Pulse(1, intensity, collisionDuration, now);
    }

    private void SendStop()
    {
        if (reader != null)
        {
            reader.SendHaptics('L', 0);
            reader.SendHaptics('R', 0);
        }
        lastSent[0] = lastSent[1] = 0;
        lastSentAt[0] = lastSentAt[1] = Time.unscaledTime;
        pulseUntil[0] = pulseUntil[1] = 0f;
    }

    private void OnDisable() { SendStop(); }
    private void OnApplicationQuit() { SendStop(); }
}
