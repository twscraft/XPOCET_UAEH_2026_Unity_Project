using UnityEngine;

public class CarCamera : MonoBehaviour
{
    [Header("Objetivo (El Carro)")]
    public Transform target; // Arrastra aquí a Vehicle_Car_color03_separate

    [Header("Configuración de Distancia y Suavizado")]
    public Vector3 offset = new Vector3(0f, 2.2f, -5.5f); // Posición relativa detrás del carro
    public float positionSmoothSpeed = 12f;                // Velocidad con la que persigue la posición
    public float rotationSmoothSpeed = 10f;                // Velocidad con la que rota siguiendo al carro

    [Header("Efecto de Sensación de Velocidad (FOV Dinámico)")]
    public Camera cam;
    public Rigidbody targetRigidbody;
    public float minFOV = 60f;                             // FOV base a baja velocidad
    public float maxFOV = 82f;                             // FOV amplio al ir a 250 km/h
    public float fovSmoothSpeed = 4f;

    void Start()
    {
        if (cam == null) cam = GetComponentInChildren<Camera>();
        if (target != null && targetRigidbody == null)
        {
            targetRigidbody = target.GetComponent<Rigidbody>();
        }
    }

    void FixedUpdate()
    {
        if (target == null) return;

        // 1. Posición fluida detrás del carro usando el espacio local del vehículo
        Vector3 targetPosition = target.position + target.TransformDirection(offset);
        transform.position = Vector3.Lerp(transform.position, targetPosition, positionSmoothSpeed * Time.fixedDeltaTime);

        // 2. Rotación suave (Slerp) apuntando ligeramente hacia arriba del vehículo para una perspectiva cinematográfica
        Vector3 lookAtTarget = target.position + Vector3.up * 1.2f;
        Quaternion targetRotation = Quaternion.LookRotation(lookAtTarget - transform.position, target.up);
        transform.rotation = Quaternion.Slerp(transform.rotation, targetRotation, rotationSmoothSpeed * Time.fixedDeltaTime);

        // 3. Campo de visión dinámico (FOV) que se estira con la velocidad real para dar adrenalina
        if (cam != null && targetRigidbody != null)
        {
            float speedKmh = targetRigidbody.linearVelocity.magnitude * 3.6f;
            float targetFOV = Mathf.Lerp(minFOV, maxFOV, speedKmh / 250f);
            cam.fieldOfView = Mathf.Lerp(cam.fieldOfView, targetFOV, fovSmoothSpeed * Time.fixedDeltaTime);
        }
    }
}