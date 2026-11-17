
/*
    Diseñado por [GameDev]: twscraft {AMM}, from UAEH - ICBI
    Powered by the best: [AI.Eng] xSaidanMka {AAAI}, from ESCOM & CINVESTAV by IPN

    Hecho por: ChatGPT-6
*/

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
    [Min(0.01f)]
    public float flashDuration = 0.15f;

    [Min(0f)]
    public float pauseDuration = 2f;

    [Header("Intensidad y Color HDRP")]
    [ColorUsage(true, true)]
    public Color beaconColor = Color.red;

    [Min(0f)]
    public float lightIntensity = 600f;

    [Min(0f)]
    public float emissionIntensity = 100f;

    [Header("Detección de Noche")]
    public bool onlyAtNight = true;

    [Tooltip("Permite probar el parpadeo durante el día.")]
    public bool forceFlashForTesting = false;

    [Tooltip("Margen para considerar que el sol está bajo el horizonte.")]
    public float nightThreshold = 0.02f;

    private Coroutine beaconRoutine;
    private string emissionProperty = "";

    void Start()
    {
        // Obtener componentes
        beaconLight = GetComponent<Light>();
        beaconRenderer = GetComponent<Renderer>();

        // Buscar la luz del sol
        if (sunLight == null)
        {
            GameObject sunObj =
                GameObject.Find("Directional Light");

            if (sunObj != null)
            {
                sunLight = sunObj.GetComponent<Light>();
            }
        }

        // Preparar la luz de la baliza
        if (beaconLight != null)
        {
            beaconLight.color = beaconColor;
            beaconLight.intensity = lightIntensity;

            // IMPORTANTE:
            // En HDRP, configurar Light Mode = Realtime
            // directamente en el Inspector de Unity.
            //
            // No utilizar Light.lightmapBakeType aquí,
            // porque impide compilar el ejecutable.
        }
        else
        {
            Debug.LogWarning(
                "Falta el componente Light en " + name,
                this
            );
        }

        // Preparar el material
        if (beaconRenderer != null &&
            beaconRenderer.sharedMaterial != null)
        {
            // Crear instancia independiente del material
            beaconMaterial = beaconRenderer.material;

            if (beaconMaterial.HasProperty("_EmissiveColor"))
            {
                // HDRP/Lit
                emissionProperty = "_EmissiveColor";
            }
            else if (beaconMaterial.HasProperty("_EmissionColor"))
            {
                // Shader Standard u otros compatibles
                emissionProperty = "_EmissionColor";

                beaconMaterial.EnableKeyword("_EMISSION");
            }
            else
            {
                Debug.LogWarning(
                    "El material de " + name +
                    " no tiene una propiedad de emisión compatible.",
                    this
                );
            }
        }
        else
        {
            Debug.LogWarning(
                "La baliza " + name +
                " necesita un material en su Mesh Renderer.",
                this
            );
        }

        // Comenzar con la baliza apagada
        SetState(false);
    }

    void Update()
    {
        bool shouldFlash = forceFlashForTesting;

        // Comprobar si debe parpadear
        if (!shouldFlash)
        {
            if (!onlyAtNight)
            {
                shouldFlash = true;
            }
            else if (sunLight != null)
            {
                // Determinar si el sol está bajo
                // el horizonte según su orientación
                float sunDirectionY =
                    sunLight.transform.forward.y;

                shouldFlash =
                    sunDirectionY > nightThreshold;
            }
        }

        if (shouldFlash)
        {
            // Iniciar el patrón si aún no está activo
            if (beaconRoutine == null)
            {
                beaconRoutine =
                    StartCoroutine(BeaconSequence());
            }
        }
        else
        {
            // Detener el patrón si es de día
            if (beaconRoutine != null)
            {
                StopCoroutine(beaconRoutine);
                beaconRoutine = null;
            }

            SetState(false);
        }
    }

    IEnumerator BeaconSequence()
    {
        while (true)
        {
            // Primer destello
            SetState(true);
            yield return new WaitForSeconds(flashDuration);

            // Apagado breve
            SetState(false);
            yield return new WaitForSeconds(flashDuration);

            // Segundo destello
            SetState(true);
            yield return new WaitForSeconds(flashDuration);

            // Pausa larga
            SetState(false);
            yield return new WaitForSeconds(pauseDuration);
        }
    }

    void SetState(bool state)
    {
        // Encender o apagar la luz física
        if (beaconLight != null)
        {
            beaconLight.enabled = state;
        }

        // Encender o apagar la emisión del material
        if (beaconMaterial != null &&
            !string.IsNullOrEmpty(emissionProperty))
        {
            Color finalEmission = state
                ? new Color(
                    beaconColor.r * emissionIntensity,
                    beaconColor.g * emissionIntensity,
                    beaconColor.b * emissionIntensity,
                    1f
                )
                : Color.black;

            beaconMaterial.SetColor(
                emissionProperty,
                finalEmission
            );
        }
    }

    void OnDisable()
    {
        // Detener la secuencia
        if (beaconRoutine != null)
        {
            StopCoroutine(beaconRoutine);
            beaconRoutine = null;
        }

        // Apagar luz y emisión
        SetState(false);
    }

    void OnDestroy()
    {
        // Liberar la instancia de material
        if (beaconMaterial != null)
        {
            Destroy(beaconMaterial);
            beaconMaterial = null;
        }
    }
}