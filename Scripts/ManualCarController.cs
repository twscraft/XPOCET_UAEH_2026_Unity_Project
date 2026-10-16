using UnityEngine;
using TMPro;

public class ManualCarController : MonoBehaviour
{
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

    [Header("Sistema de Encendido")]
    public bool isEngineRunning = false;

    [Header("Ajuste de Velocidad (Máx 250 km/h)")]
    public float maxSpeedLimit = 250f;

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

    void Start()
    {
        rb = GetComponent<Rigidbody>();
        if (rb != null)
        {
            rb.centerOfMass = new Vector3(0, -0.6f, 0);
        }
        previousGear = currentGear;

        ConfigureWheelFriction(frontLeftWheel, 3.0f);
        ConfigureWheelFriction(frontRightWheel, 3.0f);
        ConfigureWheelFriction(rearLeftWheel, 2.5f);
        ConfigureWheelFriction(rearRightWheel, 2.5f);
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
        bool accelerating = Input.GetKey(KeyCode.W);
        bool braking = Input.GetKey(KeyCode.S);
        float steerInput = Input.GetAxis("Horizontal"); // A / D

        float targetSteer = steerInput * maxSteerAngle;
        frontLeftWheel.steerAngle = Mathf.Lerp(frontLeftWheel.steerAngle, targetSteer, Time.fixedDeltaTime * 12f);
        frontRightWheel.steerAngle = Mathf.Lerp(frontRightWheel.steerAngle, targetSteer, Time.fixedDeltaTime * 12f);

        float motorTorque = 0f;
        float frontBrake = 0f;
        float rearBrake = 0f;

        if (Input.GetKey(KeyCode.Space))
        {
            frontBrake = brakePower;
            rearBrake = brakePower;
        }
        else if (!isEngineRunning)
        {
            rearBrake = 50f; // Freno leve si está apagado
        }
        else
        {
            if (clutchDelayTimer > 0f)
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
                    float powerFactor = 1.0f / currentGear;
                    motorTorque = motorPower * Mathf.Max(powerFactor, 0.3f);
                }
                else if (currentGear == -1) // Reversa
                {
                    motorTorque = motorPower * -0.5f;
                }
            }
            else if (braking)
            {
                frontBrake = brakePower;
                rearBrake = brakePower;
            }
            else
            {
                // Al soltar el acelerador: 0 torque y 0 freno. El carro rueda libremente por inercia y mantiene el control de giro.
                motorTorque = 0f;
                frontBrake = 0f;
                rearBrake = 0f;
            }
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

        if (!isEngineRunning) return;

        DetectGearChange();

        if (Input.GetKeyDown(KeyCode.Alpha1)) currentGear = 1;
        if (Input.GetKeyDown(KeyCode.Alpha2)) currentGear = 2;
        if (Input.GetKeyDown(KeyCode.Alpha3)) currentGear = 3;
        if (Input.GetKeyDown(KeyCode.Alpha4)) currentGear = 4;
        if (Input.GetKeyDown(KeyCode.Alpha5)) currentGear = 5;
        if (Input.GetKeyDown(KeyCode.R)) currentGear = -1;
        if (Input.GetKeyDown(KeyCode.N)) currentGear = 0;
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
            bool accelerating = Input.GetKey(KeyCode.W);
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
                rpmText.text = "RPM: APAGADO [E]";
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
                suggestionText.text = "[Presiona 'E' para encender el motor]";
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
                suggestionText.text = "[Presiona 1 para arrancar]";
            }
            else
            {
                suggestionText.text = "";
            }
        }
    }
}