/* 
    Diseñado por [GameDev]: twscraft {AMM}, from UAEH - ICBI
    Powered by the best: [AI.Eng] xSaidanMka {AAAI}, from ESCOM & CINVESTAV by IPN
    
    Hecho por: ChatGPT-6
*/

using UnityEngine;

// Traduce los guantes a las acciones del coche. Usa la clase Input clásica de Unity
// (la misma que usa tu ManualCarController). Si los guantes no están conectados,
// cae al teclado de siempre: W, S, A/D y Espacio.
[DefaultExecutionOrder(-50)]
public class GloveDrive : MonoBehaviour
{
    [Header("Referencias")]
    public GloveSerialReader reader;
    [Tooltip("Opcional: el Rigidbody del coche, para no apagar el motor en marcha")]
    public Rigidbody carBody;

    [Header("Modo y calibración")]
    public bool forceKeyboard = false;
    public float staleSeconds = 0.5f;
    [Tooltip("C ya apaga el motor en tu script, por eso la calibración usa V")]
    public KeyCode calibrateKey = KeyCode.V;
    public bool showStatus = true;

    [Header("Volante")]
    [Tooltip("Grados de giro de la muñeca para llegar al tope del volante")]
    public float wristDegreesForFullLock = 45f;
    public float deadZoneDegrees = 3f;
    [Tooltip("0 = sin suavizado, 0.9 = muy suave")]
    [Range(0f, 0.95f)] public float smoothing = 0.25f;
    public bool invertSteer = false;

    [Header("Palanca (mano derecha)")]
    public int gripThreshold = 1500;
    public float shiftAngle = 15f;
    public float shiftResetAngle = 5f;
    public bool invertShift = false;

    [Header("Encender o apagar con los dos botones")]
    public float ignitionHoldSeconds = 1.5f;
    public float ignitionMaxKmh = 5f;

    // Salidas que lee ManualCarController
    public bool Accelerating { get; private set; }
    public bool Braking { get; private set; }
    public bool Handbrake { get; private set; }
    public float Steer { get; private set; }            // -1 a 1
    public int ShiftRequest { get; private set; }       // +1 sube, -1 baja, 0 nada (dura un frame)
    public bool EngineToggleRequest { get; private set; }
    public bool UsingGloves { get; private set; }

    float _neutralL, _neutralR, _steerSmoothed, _shifterRefPitch, _bothTimer;
    bool _calibrated, _shifterGrabbed, _shiftArmed = true, _ignitionArmed = true;

    void Update()
    {
        ShiftRequest = 0;
        EngineToggleRequest = false;

        GloveState left = default(GloveState);
        GloveState right = default(GloveState);
        if (reader != null)
        {
            left = reader.Left;
            right = reader.Right;
        }

        UsingGloves = !forceKeyboard && reader != null && reader.IsConnected
                      && left.AgeSeconds < staleSeconds && right.AgeSeconds < staleSeconds;

        if (UsingGloves) ReadGloves(left, right);
        else ReadKeyboard();
    }

    void ReadGloves(GloveState left, GloveState right)
    {
        if (Input.GetKeyDown(calibrateKey)) Calibrate();

        // Giro de cada mano respecto a la posición «manos rectas»
        float dL = Mathf.DeltaAngle(_neutralL, left.roll);
        float dR = Mathf.DeltaAngle(_neutralR, right.roll);
        float degrees = (dL + dR) * 0.5f;
        if (invertSteer) degrees = -degrees;

        float target = 0f;
        if (_calibrated)
            target = Mathf.Clamp(ApplyDeadZone(degrees, deadZoneDegrees) / wristDegreesForFullLock, -1f, 1f);
        _steerSmoothed = Mathf.Lerp(_steerSmoothed, target, 1f - smoothing);
        Steer = _steerSmoothed;

        // Los dos botones juntos no aceleran ni frenan: sirven para encender o apagar
        bool both = left.button && right.button;
        Accelerating = left.button && !both;
        Braking = right.button && !both;
        Handbrake = Input.GetKey(KeyCode.Space);   // los guantes no tienen botón libre: sigue en el teclado

        UpdateIgnition(both);
        ReadShifter(right, right.grip < gripThreshold);
    }

    void UpdateIgnition(bool both)
    {
        if (!both)
        {
            _bothTimer = 0f;
            _ignitionArmed = true;
            return;
        }

        _bothTimer += Time.deltaTime;
        if (_ignitionArmed && _bothTimer >= ignitionHoldSeconds)
        {
            _ignitionArmed = false;   // una sola vez por pulsación
            float kmh = carBody != null ? carBody.linearVelocity.magnitude * 3.6f : 0f;
            if (kmh <= ignitionMaxKmh) EngineToggleRequest = true;
        }
    }

    void ReadShifter(GloveState right, bool grip)
    {
        if (!grip)
        {
            _shifterGrabbed = false;
            _shiftArmed = true;
            return;
        }

        if (!_shifterGrabbed)
        {
            // Al agarrar la palanca, la inclinación actual pasa a ser el punto de partida
            _shifterGrabbed = true;
            _shifterRefPitch = right.pitch;
            _shiftArmed = true;
            return;
        }

        float d = Mathf.DeltaAngle(_shifterRefPitch, right.pitch);
        if (_shiftArmed && Mathf.Abs(d) > shiftAngle)
        {
            ShiftRequest = ((d > 0f) != invertShift) ? 1 : -1;
            _shiftArmed = false;
        }
        else if (!_shiftArmed && Mathf.Abs(d) < shiftResetAngle)
        {
            _shiftArmed = true;   // regresaste la palanca al centro
        }
    }

    [ContextMenu("Calibrar")]
    public void Calibrate()
    {
        if (reader == null) return;
        _neutralL = reader.Left.roll;
        _neutralR = reader.Right.roll;
        _steerSmoothed = 0f;
        _calibrated = true;
        Debug.Log("Calibrado. Neutro izq=" + _neutralL.ToString("F1") + "  der=" + _neutralR.ToString("F1"));
    }

    static float ApplyDeadZone(float value, float deadZone)
    {
        if (Mathf.Abs(value) <= deadZone) return 0f;
        return value - Mathf.Sign(value) * deadZone;
    }

    // Respaldo: el teclado de tu script original
    void ReadKeyboard()
    {
        Accelerating = Input.GetKey(KeyCode.W);
        Braking = Input.GetKey(KeyCode.S);
        Handbrake = Input.GetKey(KeyCode.Space);
        Steer = Input.GetAxis("Horizontal");
    }

    // Mensaje de estado en pantalla (funciona en el editor y en el ejecutable)
    void OnGUI()
    {
        if (!showStatus) return;

        string text;
        if (!UsingGloves) text = "Modo teclado: W/S, A/D, Espacio, 1-5, E/C";
        else if (!_calibrated) text = "Manos rectas en el volante y presiona " + calibrateKey;
        else text = "Guantes conectados";

        GUI.Label(new Rect(10f, 10f, 700f, 24f), text);
    }
}