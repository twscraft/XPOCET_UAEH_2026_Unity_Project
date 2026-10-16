using UnityEngine;
using System.Collections;

public class BuildingBeacon : MonoBehaviour
{
    [Header("Referencias")]
    public Light sunLight;
    private Light beaconLight;
    private Renderer beaconRenderer;
    private Material beaconMaterial;

    [Header("Configuración de Patrón")]
    public float flashDuration = 0.15f; // Duración de cada destello rápido
    public float pauseDuration = 2.0f;  // Tiempo apagado al final del ciclo

    [Header("Intensidad y Color")]
    public Color beaconColor = Color.red;
    public float lightIntensity = 15.0f;   // Aumentado para mayor alcance
    public float emissionIntensity = 5.0f; // Mayor brillo HDR

    private Coroutine beaconRoutine;
    private bool wasNight = false;

    void Start()
    {
        beaconLight = GetComponent<Light>();
        beaconRenderer = GetComponent<Renderer>();

        if (beaconLight != null)
        {
            beaconLight.color = beaconColor;
            beaconLight.intensity = lightIntensity;
        }

        if (beaconRenderer != null)
        {
            beaconMaterial = beaconRenderer.material;
            beaconMaterial.EnableKeyword("_EMISSION");
        }

        if (sunLight == null)
        {
            GameObject sunObj = GameObject.Find("Directional Light");
            if (sunObj != null) sunLight = sunObj.GetComponent<Light>();
        }
    }

    void Update()
    {
        bool isNight = true;
        if (sunLight != null)
        {
            float sunRotationX = sunLight.transform.eulerAngles.x;
            isNight = sunRotationX > 180f && sunRotationX < 360f;
        }

        // Control de activación según el ciclo de día/noche
        if (isNight && !wasNight)
        {
            wasNight = true;
            if (beaconRoutine != null) StopCoroutine(beaconRoutine);
            beaconRoutine = StartCoroutine(BeaconSequence());
        }
        else if (!isNight && wasNight)
        {
            wasNight = false;
            if (beaconRoutine != null) StopCoroutine(beaconRoutine);
            TurnOffBeacon();
        }

        // Por si inicia directamente de noche
        if (isNight && beaconRoutine == null)
        {
            wasNight = true;
            beaconRoutine = StartCoroutine(BeaconSequence());
        }
    }

    IEnumerator BeaconSequence()
    {
        while (true)
        {
            // --- Primer destello ---
            SetState(true);
            yield return new WaitForSeconds(flashDuration);

            // --- Apagado breve entre destellos ---
            SetState(false);
            yield return new WaitForSeconds(flashDuration);

            // --- Segundo destello ---
            SetState(true);
            yield return new WaitForSeconds(flashDuration);

            // --- Pausa larga apagada (2 segundos) ---
            SetState(false);
            yield return new WaitForSeconds(pauseDuration);
        }
    }

    void SetState(bool state)
    {
        if (beaconLight != null) beaconLight.enabled = state;
        if (beaconMaterial != null)
        {
            if (state)
            {
                beaconMaterial.SetColor("_EmissionColor", beaconColor * emissionIntensity);
            }
            else
            {
                beaconMaterial.SetColor("_EmissionColor", Color.black);
            }
        }
    }

    void TurnOffBannerOrBeacon() { TurnOffBeacon(); }

    void TurnOffBeacon()
    {
        if (beaconLight != null) beaconLight.enabled = false;
        if (beaconMaterial != null)
        {
            beaconMaterial.SetColor("_EmissionColor", Color.black);
        }
        beaconRoutine = null;
    }
}