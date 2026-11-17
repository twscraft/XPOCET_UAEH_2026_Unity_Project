/* 
    Diseñado por [GameDev]: twscraft {AMM}, from UAEH - ICBI
    Powered by the best: [AI.Eng] xSaidanMka {AAAI}, from ESCOM & CINVESTAV by IPN
    
    Hecho por: ChatGPT-6
*/

using UnityEngine;
using TMPro;

public class ManualCarController : MonoBehaviour
{
    [Header("Guantes")] // GUANTES: arrastra aquí el objeto con GloveDrive
    public GloveDrive gloves;

    [Header("Ruedas (Wheel Colliders)")]
    public WheelCollider frontLeftWheel;
    public WheelCollider frontRightWheel;
    public WheelCollider rearLeftWheel;
    public WheelCollider rearRightWheel;

    [Header("Mallas Visuales de las Ruedas (Opcional)")]
    public Transform frontLeftMesh;
    public Transform frontRightMesh;
    public Transform rearLeftMesh;
    public Transform rearRightMesh;

    [Header("Configuración de Conducción")]
    public float motorPower = 4000f;
    public float maxSteerAngle = 32f;
    public float brakePower = 4000f;

    [Header("Direccion suave y segura en curvas")]
    [Tooltip("Cuantos grados por segundo pueden girar las ruedas delanteras. Menor = giro mas suave.")]
    [Min(1f)] public float steeringResponseDegreesPerSecond = 115f;

    [Tooltip("Hasta esta velocidad se utiliza el angulo maximo de direccion.")]
    [Min(0f)] public float steeringReductionStartKmh = 10f;

    [Tooltip("A partir de esta velocidad se utiliza highSpeedSteerAngle.")]
    [Min(1f)] public float steeringReductionEndKmh = 80f;

    [Tooltip("Angulo maximo de las ruedas al circular rapido. Menor = curvas menos bruscas a alta velocidad.")]
    [Range(1f, 32f)] public float highSpeedSteerAngle = 9f;

    [Tooltip("Ignora movimientos muy pequenos del volante o los guantes para evitar vibraciones.")]
    [Range(0f, 0.25f)] public float steeringDeadZone = 0.06f;

    [Header("Direccion con el auto detenido (teclado y guantes)")]
    [Tooltip("Conservar el angulo de las ruedas al soltar el control con el auto detenido.")]
    public bool holdSteeringWhenStopped = true;

    [Tooltip("A esta velocidad o menos (km/h) se considera estacionado.")]
    [Min(0.1f)] public float stoppedSteeringSpeedKmh = 1f;

    [Tooltip("Velocidad de giro de las ruedas con A/D cuando el auto esta parado (grados/segundo).")]
    [Min(1f)] public float stoppedKeyboardSteerDegreesPerSecond = 95f;

    [Tooltip("Movimiento minimo de los guantes para ORDENAR un nuevo angulo en parado. Evita que el regreso a neutro borre el giro.")]
    [Range(0.05f, 0.5f)] public float stoppedGloveCommandDeadZone = 0.12f;

    [Tooltip("Tiempo minimo en segundos que se conserva el giro al arrancar sin dar nuevas ordenes de direccion.")]
    [Min(0f)] public float startupSteeringHoldSeconds = 1.2f;

    [Tooltip("Mientras se arranca por debajo de esta velocidad (km/h), se conserva el giro si no das nuevas ordenes.")]
    [Min(0f)] public float startupSteeringHoldUntilKmh = 12f;

    [Tooltip("Opcional: pulsa esta tecla con el auto parado para dejar las ruedas rectas, tambien en modo guantes.")]
    public KeyCode centerStoppedSteeringKey = KeyCode.X;

    private float stoppedSteerTargetAngle = 0f;
    private bool wasStopped = false;
    private bool launchSteeringPresetActive = false;
    private float timeSinceMovingFromStop = 0f;

    [Header("Agarre lateral de las ruedas")]
    [Tooltip("Agarre delantero. Los valores muy altos pueden hacer bruscos los giros.")]
    [Range(0.5f, 3f)] public float frontSidewaysStiffness = 1.5f;

    [Tooltip("Agarre trasero. Valores muy bajos pueden causar derrapes.")]
    [Range(0.5f, 3f)] public float rearSidewaysStiffness = 1.35f;

    [Header("Desaceleracion automatica al soltar el acelerador")]
    [Tooltip("Torque de frenado suave por rueda, en Nm. Menor = desaceleracion mas lenta.")]
    [Min(0f)] public float coastBrakeTorque = 110f;

    [Tooltip("Velocidad con la que aumenta el freno suave, en Nm por segundo.")]
    [Min(0f)] public float coastBrakeRampRate = 110f;

    [Tooltip("Debajo de esta velocidad (km/h), deja de aplicar el freno suave.")]
    [Min(0f)] public float coastBrakeMinSpeedKmh = 1f;

    private float currentCoastBrakeTorque = 0f;

    [Tooltip("Desde esta velocidad el freno suave alcanza su fuerza maxima; a velocidades bajas frena menos.")]
    [Min(2f)] public float coastBrakeFullStrengthSpeedKmh = 30f;

    [Header("Evitar frenazos en curvas")]
    [Tooltip("A partir de esta inclinacion normalizada del volante se empieza a reducir el frenado automatico.")]
    [Range(0f, 0.5f)] public float cornerBrakeStartSteer = 0.08f;

    [Tooltip("A partir de esta inclinacion se alcanza la reduccion maxima de freno en curvas.")]
    [Range(0.1f, 1f)] public float cornerBrakeFullSteer = 0.35f;

    [Tooltip("1 = quitar por completo el freno automatico al girar fuerte; 0 = no modificarlo.")]
    [Range(0f, 1f)] public float cornerBrakeReduction = 1f;

    private float currentSteerAngle = 0f;

    [Header("Sistema de Encendido")]
    public bool isEngineRunning = false;

    [Header("Ajuste de Velocidad (Máx 410 km/h)")]
    public float maxSpeedLimit = 410f;

    [Header("Transmisión Manual Realista")]
    public int currentGear = 0; // 0 = Neutral, -1 = Reversa, 1-5 = Marchas
    private int previousGear = 0;

    private float[] gearMaxSpeeds = { 0f, 40f, 75f, 120f, 170f, 250f };
    private float reverseMaxSpeed = 35f;

    private float clutchDelayTimer = 0f;
    private const float clutchDuration = 0.2f;

    [Header("Interfaz de Usuario (UI Separada)")]
    public TextMeshProUGUI gearText;
    public TextMeshProUGUI rpmText;
    public TextMeshProUGUI speedText;
    public TextMeshProUGUI suggestionText;

    private Rigidbody rb;
    private float engineRPM;
    private float maxRPM = 6000f;
    private float idleRPM = 800f;

    private float hudTimer = 0f;
    private float suggestionTimer = 0f;
    private float displaySpeed = 0f;
    private bool isRevLimiterActive = false;

    // GUANTES: lo que lee GloveHaptics
    public float EngineRPM { get { return engineRPM; } }
    public bool RevLimiterActive { get { return isRevLimiterActive; } }

    // GUANTES: true cuando los guantes son el control activo
    private bool GlovesActive { get { return gloves != null && gloves.UsingGloves; } }

    void Start()
    {
        rb = GetComponent<Rigidbody>();
        if (rb != null)
        {
            rb.centerOfMass = new Vector3(0, -0.6f, 0);
        }
        previousGear = currentGear;

        // Valores moderados: antes eran 3.0 delante y 2.5 detras.
        // Se aplican al iniciar y se pueden ajustar en el Inspector.
        ConfigureWheelFriction(frontLeftWheel, frontSidewaysStiffness);
        ConfigureWheelFriction(frontRightWheel, frontSidewaysStiffness);
        ConfigureWheelFriction(rearLeftWheel, rearSidewaysStiffness);
        ConfigureWheelFriction(rearRightWheel, rearSidewaysStiffness);
    }

    void ConfigureWheelFriction(WheelCollider wheel, float sidewaysStiffness)
    {
        if (wheel == null) return;
        WheelFrictionCurve sideways = wheel.sidewaysFriction;
        sideways.stiffness = sidewaysStiffness;
        wheel.sidewaysFriction = sideways;
    }

    void Update()
    {
        GetIgnitionAndGearInput();
        CalculateEngineRPM();

        if (clutchDelayTimer > 0f)
        {
            clutchDelayTimer -= Time.deltaTime;
        }

        float realSpeedKmh = rb != null ? rb.linearVelocity.magnitude * 3.6f : 0f;
        float targetSpeed = Mathf.Min(realSpeedKmh, maxSpeedLimit);
        displaySpeed = Mathf.Lerp(displaySpeed, targetSpeed, Time.deltaTime * 8f);

        hudTimer += Time.deltaTime;
        if (hudTimer >= 0.1f)
        {
            UpdateFastHUD();
            hudTimer = 0f;
        }

        suggestionTimer += Time.deltaTime;
        if (suggestionTimer >= 1.0f)
        {
            UpdateSuggestionText();
            suggestionTimer = 0f;
        }
    }

    void LateUpdate()
    {
        UpdateWheelVisual(frontLeftWheel, frontLeftMesh);
        UpdateWheelVisual(frontRightWheel, frontRightMesh);
        UpdateWheelVisual(rearLeftWheel, rearLeftMesh);
        UpdateWheelVisual(rearRightWheel, rearRightMesh);
    }

    void UpdateWheelVisual(WheelCollider collider, Transform visualMesh)
    {
        if (collider == null || visualMesh == null) return;

        Vector3 pos;
        Quaternion rot;
        collider.GetWorldPose(out pos, out rot);
        visualMesh.position = pos;
        visualMesh.rotation = rot;
    }

    void DetectGearChange()
    {
        if (currentGear != previousGear)
        {
            clutchDelayTimer = clutchDuration;
            previousGear = currentGear;
        }
    }

    void FixedUpdate()
    {
        // GloveDrive sigue proporcionando acelerador, freno, direccion y freno de mano.
        // Si no hay GloveDrive asignado, utiliza el teclado directamente.
        // No se requiere cambiar firmware ESP32 ni otros scripts.
        bool accelerating = gloves != null ? gloves.Accelerating : Input.GetKey(KeyCode.W);
        bool braking = gloves != null ? gloves.Braking : Input.GetKey(KeyCode.S);
        float steerInput = gloves != null ? gloves.Steer : Input.GetAxis("Horizontal");
        bool handbrake = gloves != null ? gloves.Handbrake : Input.GetKey(KeyCode.Space);

        steerInput = Mathf.Clamp(steerInput, -1f, 1f);
        if (Mathf.Abs(steerInput) < steeringDeadZone)
            steerInput = 0f;

        float speedKmh = rb != null ? rb.linearVelocity.magnitude * 3.6f : 0f;

        // A baja velocidad se conserva el angulo original (maxSteerAngle).
        // A mayor velocidad disminuye para evitar que el coche se clave en la curva.
        float reduceAt = Mathf.Max(steeringReductionStartKmh + 1f, steeringReductionEndKmh);
        float speedRatio = Mathf.InverseLerp(steeringReductionStartKmh, reduceAt, speedKmh);
        float allowedSteer = Mathf.Lerp(maxSteerAngle,
            Mathf.Min(maxSteerAngle, highSpeedSteerAngle), speedRatio);

        // Direccion normal: conserva el comportamiento que ya tenia el auto
        // (progresiva segun la velocidad y centrado al soltar durante la marcha).
        float targetSteer = steerInput * allowedSteer;

        if (holdSteeringWhenStopped)
        {
            bool stoppedNow = speedKmh <= stoppedSteeringSpeedKmh;
            bool usingRealGloves = gloves != null && gloves.UsingGloves;

            if (stoppedNow)
            {
                if (!wasStopped)
                {
                    // Recuerda la posicion REAL de las ruedas al llegar a cero.
                    stoppedSteerTargetAngle = currentSteerAngle;
                }

                if (Input.GetKey(centerStoppedSteeringKey))
                {
                    // Centrado manual opcional (X). Tiene prioridad sobre A/D y guantes.
                    stoppedSteerTargetAngle = 0f;
                }
                else if (usingRealGloves)
                {
                    // GloveDrive.Steer ya entrega la orientacion de las manos (-1 a 1).
                    // En parado guardamos el MAYOR giro solicitado por cada lado.
                    // Asi, regresar las manos a neutro NO devuelve las ruedas a cero.
                    // Un gesto en sentido contrario permite cambiar de direccion.
                    float requested = Mathf.Clamp(steerInput, -1f, 1f) * maxSteerAngle;
                    if (Mathf.Abs(steerInput) >= stoppedGloveCommandDeadZone)
                    {
                        bool opposite = Mathf.Abs(stoppedSteerTargetAngle) > 0.01f &&
                                        Mathf.Sign(requested) != Mathf.Sign(stoppedSteerTargetAngle);
                        if (opposite || Mathf.Abs(requested) > Mathf.Abs(stoppedSteerTargetAngle))
                            stoppedSteerTargetAngle = requested;
                    }
                }
                else
                {
                    // Teclado: A y D giran las ruedas MIENTRAS se pulsan.
                    // GetAxisRaw evita que el suavizado del Input clasico
                    // siga girando las ruedas despues de soltar A/D.
                    float rawKeyboard = Input.GetAxisRaw("Horizontal");
                    stoppedSteerTargetAngle += rawKeyboard *
                        stoppedKeyboardSteerDegreesPerSecond * Time.fixedDeltaTime;
                }

                stoppedSteerTargetAngle = Mathf.Clamp(stoppedSteerTargetAngle,
                    -maxSteerAngle, maxSteerAngle);
                targetSteer = stoppedSteerTargetAngle;
                launchSteeringPresetActive = true;
                timeSinceMovingFromStop = 0f;
            }
            else
            {
                if (wasStopped)
                    timeSinceMovingFromStop = 0f;
                else
                    timeSinceMovingFromStop += Time.fixedDeltaTime;

                // Ignora el resto del suavizado de A/D despues de soltar la tecla.
                // Cuando hay guantes, usa una zona neutra para el ruido del sensor.
                bool driverSteers = usingRealGloves
                    ? Mathf.Abs(steerInput) >= stoppedGloveCommandDeadZone
                    : Mathf.Abs(Input.GetAxisRaw("Horizontal")) > 0.01f;

                if (launchSteeringPresetActive)
                {
                    if (driverSteers ||
                        (timeSinceMovingFromStop >= startupSteeringHoldSeconds &&
                         speedKmh >= startupSteeringHoldUntilKmh))
                    {
                        // El jugador retoma el control, o finaliza la fase de salida:
                        // vuelve el control de direccion habitual.
                        launchSteeringPresetActive = false;
                    }
                    else
                    {
                        // Arranca siguiendo la orientacion que dejaron las ruedas.
                        // Respeta igualmente la limitacion del angulo por velocidad.
                        targetSteer = Mathf.Clamp(stoppedSteerTargetAngle,
                            -allowedSteer, allowedSteer);
                    }
                }
            }

            wasStopped = stoppedNow;
        }
        else
        {
            wasStopped = false;
            launchSteeringPresetActive = false;
        }

        currentSteerAngle = Mathf.MoveTowards(currentSteerAngle, targetSteer,
            steeringResponseDegreesPerSecond * Time.fixedDeltaTime);

        frontLeftWheel.steerAngle = currentSteerAngle;
        frontRightWheel.steerAngle = currentSteerAngle;

        float motorTorque = 0f;
        float frontBrake = 0f;
        float rearBrake = 0f;

        // El freno automatico sigue funcionando en rectas al soltar acelerar,
        // pero disminuye progresivamente en curvas y a baja velocidad.
        bool shouldCoastBrake = isEngineRunning
            && !handbrake
            && !accelerating
            && !braking
            && clutchDelayTimer <= 0f
            && speedKmh > coastBrakeMinSpeedKmh;

        if (shouldCoastBrake)
        {
            // Cuanto mas lento va el coche, menor sera el freno automatico.
            float fullBrakeAt = Mathf.Max(coastBrakeMinSpeedKmh + 1f,
                coastBrakeFullStrengthSpeedKmh);
            float speedBrakeFactor = Mathf.InverseLerp(
                coastBrakeMinSpeedKmh, fullBrakeAt, speedKmh);

            // Usa la entrada solicitada (teclado o guante) y la direccion real
            // para liberar pronto el freno mientras se inicia o termina un giro.
            float actualTurnRatio = maxSteerAngle > 0.01f
                ? Mathf.Abs(currentSteerAngle) / maxSteerAngle : 0f;
            float turnAmount = Mathf.Max(Mathf.Abs(steerInput), actualTurnRatio);
            float fullTurnAt = Mathf.Max(cornerBrakeStartSteer + 0.01f,
                cornerBrakeFullSteer);
            float cornerFactor = Mathf.InverseLerp(
                cornerBrakeStartSteer, fullTurnAt, turnAmount);
            float cornerBrakeFactor = 1f - cornerFactor * cornerBrakeReduction;

            float targetCoastBrake = Mathf.Min(coastBrakeTorque, brakePower)
                * speedBrakeFactor * cornerBrakeFactor;

            // Se aplica despacio en recta, pero se suelta rapido al girar.
            float rampRate = targetCoastBrake < currentCoastBrakeTorque
                ? Mathf.Max(coastBrakeRampRate * 8f, 500f)
                : coastBrakeRampRate;
            currentCoastBrakeTorque = Mathf.MoveTowards(
                currentCoastBrakeTorque, targetCoastBrake,
                rampRate * Time.fixedDeltaTime);
        }
        else
        {
            // Al volver a acelerar, frenar, cambiar o casi detenerse,
            // el freno automatico se quita inmediatamente.
            currentCoastBrakeTorque = 0f;
        }

        if (handbrake)
        {
            frontBrake = brakePower;
            rearBrake = brakePower;
        }
        else if (!isEngineRunning)
        {
            rearBrake = 50f; // Freno leve con motor apagado (igual que antes).
        }
        else if (braking)
        {
            frontBrake = brakePower;
            rearBrake = brakePower;
        }
        else if (clutchDelayTimer > 0f)
        {
            motorTorque = 0f;
        }
        else if (accelerating)
        {
            if (isRevLimiterActive)
            {
                motorTorque = 0f;
            }
            else if (currentGear > 0)
            {
                float powerFactor = 1f / currentGear;
                motorTorque = motorPower * Mathf.Max(powerFactor, 0.3f);
            }
            else if (currentGear == -1)
            {
                motorTorque = -motorPower * 0.5f;
            }
        }
        else if (shouldCoastBrake)
        {
            // En curvas marcadas este valor tiende a cero y el coche rueda libre.
            frontBrake = currentCoastBrakeTorque;
            rearBrake = currentCoastBrakeTorque;
        }

        rearLeftWheel.motorTorque = motorTorque;
        rearRightWheel.motorTorque = motorTorque;

        frontLeftWheel.brakeTorque = frontBrake;
        frontRightWheel.brakeTorque = frontBrake;
        rearLeftWheel.brakeTorque = rearBrake;
        rearRightWheel.brakeTorque = rearBrake;
    }

    void GetIgnitionAndGearInput()
    {
        if (Input.GetKeyDown(KeyCode.E))
        {
            isEngineRunning = true;
        }
        if (Input.GetKeyDown(KeyCode.C))
        {
            isEngineRunning = false;
            currentGear = 0;
        }

        // GUANTES: los dos botones juntos durante 1.5 s encienden o apagan el motor
        if (gloves != null && gloves.EngineToggleRequest)
        {
            isEngineRunning = !isEngineRunning;
            if (!isEngineRunning) currentGear = 0;
        }

        if (!isEngineRunning) return;

        // GUANTES: la palanca de la mano derecha sube o baja una marcha (R, N, 1, 2, 3, 4, 5)
        if (gloves != null && gloves.ShiftRequest != 0)
        {
            currentGear = Mathf.Clamp(currentGear + gloves.ShiftRequest, -1, 5);
        }

        if (Input.GetKeyDown(KeyCode.Alpha1)) currentGear = 1;
        if (Input.GetKeyDown(KeyCode.Alpha2)) currentGear = 2;
        if (Input.GetKeyDown(KeyCode.Alpha3)) currentGear = 3;
        if (Input.GetKeyDown(KeyCode.Alpha4)) currentGear = 4;
        if (Input.GetKeyDown(KeyCode.Alpha5)) currentGear = 5;
        if (Input.GetKeyDown(KeyCode.R)) currentGear = -1;
        if (Input.GetKeyDown(KeyCode.N)) currentGear = 0;

        // Detectar el cambio inmediatamente (antes se detectaba un frame despues).
        DetectGearChange();
    }

    void CalculateEngineRPM()
    {
        if (!isEngineRunning)
        {
            engineRPM = 0f;
            isRevLimiterActive = false;
            return;
        }

        if (currentGear == 0)
        {
            // GUANTES: en neutral, el botón izquierdo (o la W) revoluciona el motor
            bool accelerating = gloves != null ? gloves.Accelerating : Input.GetKey(KeyCode.W);
            engineRPM = Mathf.MoveTowards(engineRPM, idleRPM + (accelerating ? 3000f : 0f), Time.deltaTime * 3000f);
            isRevLimiterActive = false;
        }
        else
        {
            float realSpeedKmh = rb != null ? rb.linearVelocity.magnitude * 3.6f : 0f;
            float targetMaxSpeed = (currentGear == -1) ? reverseMaxSpeed : gearMaxSpeeds[currentGear];

            if (realSpeedKmh >= targetMaxSpeed)
            {
                engineRPM = maxRPM + Random.Range(-80f, 80f);
                isRevLimiterActive = true;
            }
            else
            {
                isRevLimiterActive = false;

                float speedRatio = Mathf.Clamp01(realSpeedKmh / targetMaxSpeed);
                float calculatedRPM = idleRPM + speedRatio * (maxRPM - idleRPM);

                float finalTargetRPM = (clutchDelayTimer > 0f) ? Mathf.Max(idleRPM, calculatedRPM * 0.5f) : calculatedRPM;
                engineRPM = Mathf.MoveTowards(engineRPM, finalTargetRPM, Time.deltaTime * 7000f);
            }
        }
    }

    void UpdateFastHUD()
    {
        if (gearText != null)
        {
            string gearStr = currentGear == 0 ? "N" : (currentGear == -1 ? "R" : currentGear.ToString());
            gearText.text = "Marcha: " + gearStr;
        }

        if (rpmText != null)
        {
            if (!isEngineRunning)
            {
                // GUANTES: el aviso cambia según qué control esté activo
                rpmText.text = GlovesActive ? "RPM: APAGADO [2 botones]" : "RPM: APAGADO [E]";
            }
            else
            {
                string limitTag = isRevLimiterActive ? " <color=red><b>!CORTA!</b></color>" : "";
                rpmText.text = "RPM: " + Mathf.Round(engineRPM) + limitTag;
            }
        }

        if (speedText != null)
        {
            int finalSpeed = Mathf.RoundToInt(displaySpeed);
            if (finalSpeed < 1) finalSpeed = 0;
            speedText.text = "Velocidad: " + finalSpeed + " km/h";
        }
    }

    void UpdateSuggestionText()
    {
        if (suggestionText != null)
        {
            if (!isEngineRunning)
            {
                // GUANTES: el aviso cambia según qué control esté activo
                suggestionText.text = GlovesActive
                    ? "[Mantén los dos botones 1.5 s para encender el motor]"
                    : "[Presiona 'E' para encender el motor]";
                return;
            }

            if (currentGear == -1)
            {
                suggestionText.text = "[Modo Reversa R]";
                return;
            }

            float targetMaxSpeed = gearMaxSpeeds[currentGear];
            float realSpeedKmh = rb != null ? rb.linearVelocity.magnitude * 3.6f : 0f;

            if (currentGear > 0 && currentGear < 5 && realSpeedKmh >= (targetMaxSpeed * 0.85f))
            {
                suggestionText.text = "¡Sube de marcha! (-> " + (currentGear + 1) + ")";
            }
            else if (currentGear > 1 && realSpeedKmh <= (gearMaxSpeeds[currentGear - 1] * 0.4f))
            {
                suggestionText.text = "¡Baja de marcha! (-> " + (currentGear - 1) + ")";
            }
            else if (currentGear == 0)
            {
                suggestionText.text = GlovesActive
                    ? "[Aprieta la palanca e inclina la mano derecha hacia adelante para meter 1]"
                    : "[Presiona 1 para arrancar]";
            }
            else
            {
                suggestionText.text = "";
            }
        }
    }
}