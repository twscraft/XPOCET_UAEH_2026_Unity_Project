/* 
    Diseñado por [GameDev]: twscraft {AMM}, from UAEH - ICBI
    Powered by the best: [AI.Eng] xSaidanMka {AAAI}, from ESCOM & CINVESTAV by IPN
    
    Hecho por: ChatGPT-6
*/

using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

// ===================================================
// PREFERENCIAS GLOBALES
// ===================================================

public static class PreferenciasManosAlVolante
{
    private const string Prefijo = "MAV.Settings.";

    private const string ClaveVolumen =
        Prefijo + "Volumen";

    private const string ClaveCalidad =
        Prefijo + "CalidadNombre";

    private const string ClavePantalla =
        Prefijo + "PantallaCompleta";

    // -----------------------------------------------
    // Leer preferencias guardadas
    // -----------------------------------------------

    public static float Volumen
    {
        get
        {
            return Mathf.Clamp01(
                PlayerPrefs.GetFloat(ClaveVolumen, 0.8f)
            );
        }
    }

    public static int IndiceCalidad
    {
        get
        {
            string[] nombres = QualitySettings.names;

            if (nombres == null || nombres.Length == 0)
                return 0;

            string guardado =
                PlayerPrefs.GetString(ClaveCalidad, "");

            int indice = Array.IndexOf(nombres, guardado);

            if (indice >= 0)
                return indice;

            return Mathf.Clamp(
                QualitySettings.GetQualityLevel(),
                0,
                nombres.Length - 1
            );
        }
    }

    public static bool PantallaCompleta
    {
        get
        {
            return PlayerPrefs.GetInt(
                ClavePantalla,
                Screen.fullScreen ? 1 : 0
            ) == 1;
        }
    }

    // -----------------------------------------------
    // Aplicar configuraciones al iniciar
    // -----------------------------------------------

    [RuntimeInitializeOnLoadMethod(
        RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void Inicializar()
    {
        // Evitar suscripciones repetidas.
        SceneManager.sceneLoaded -= AlCargarEscena;
        SceneManager.sceneLoaded += AlCargarEscena;

        AplicarGuardado();
    }

    private static void AlCargarEscena(
        Scene escena,
        LoadSceneMode modo)
    {
        AplicarGuardado();

        Debug.Log(
            "[MAV Ajustes] Preferencias aplicadas en: "
            + escena.name
        );
    }

    public static void AplicarGuardado()
    {
        AudioListener.volume = Volumen;

        if (PlayerPrefs.HasKey(ClaveCalidad) &&
            QualitySettings.names.Length > 0)
        {
            QualitySettings.SetQualityLevel(
                IndiceCalidad,
                true
            );
        }

        if (PlayerPrefs.HasKey(ClavePantalla))
        {
            Screen.fullScreen = PantallaCompleta;
        }
    }

    // -----------------------------------------------
    // Guardar modificaciones
    // -----------------------------------------------

    public static void GuardarVolumen(float valor)
    {
        valor = Mathf.Clamp01(valor);

        AudioListener.volume = valor;

        PlayerPrefs.SetFloat(ClaveVolumen, valor);
        PlayerPrefs.Save();

        Debug.Log(
            "[MAV Ajustes] Volumen guardado: "
            + valor.ToString("F2")
        );
    }

    public static void GuardarCalidad(int indice)
    {
        string[] nombres = QualitySettings.names;

        if (nombres == null || nombres.Length == 0)
            return;

        indice = Mathf.Clamp(
            indice,
            0,
            nombres.Length - 1
        );

        QualitySettings.SetQualityLevel(indice, true);

        PlayerPrefs.SetString(
            ClaveCalidad,
            nombres[indice]
        );

        PlayerPrefs.Save();

        Debug.Log(
            "[MAV Ajustes] Calidad guardada: "
            + nombres[indice]
        );
    }

    public static void GuardarPantallaCompleta(bool activa)
    {
        Screen.fullScreen = activa;

        PlayerPrefs.SetInt(
            ClavePantalla,
            activa ? 1 : 0
        );

        PlayerPrefs.Save();

        Debug.Log(
            "[MAV Ajustes] Pantalla completa guardada: "
            + activa
        );
    }

    // -----------------------------------------------
    // Diagnóstico
    // -----------------------------------------------

    public static void Diagnosticar()
    {
        Debug.Log(
            "[MAV Diagnostico]\n" +
            "Volumen guardado: " + Volumen +
            " | Existe: " + PlayerPrefs.HasKey(ClaveVolumen) +
            "\nCalidad guardada: " +
            PlayerPrefs.GetString(ClaveCalidad, "(sin guardar)") +
            " | Existe: " + PlayerPrefs.HasKey(ClaveCalidad) +
            "\nPantalla completa guardada: " +
            PlayerPrefs.GetInt(ClavePantalla, -1) +
            " | Existe: " + PlayerPrefs.HasKey(ClavePantalla)
        );
    }
}

// ===================================================
// CONTROL DE INTERFAZ DE AJUSTES
// ===================================================

public class AjustesPersistentes : MonoBehaviour
{
    [Header("Componentes UI del panel")]
    public Slider sliderVolumen;
    public TMP_Dropdown dropdownCalidad;
    public Toggle togglePantallaCompleta;

    private bool opcionesPreparadas;
    private bool eventosConectados;

    // -----------------------------------------------
    // Inicialización
    // -----------------------------------------------

    private void Awake()
    {
        BuscarReferencias();
        PrepararOpcionesCalidad();
    }

    private void OnEnable()
    {
        BuscarReferencias();
        PrepararOpcionesCalidad();

        // Aplicar valores previamente guardados.
        PreferenciasManosAlVolante.AplicarGuardado();

        // Actualizar interfaz sin disparar eventos.
        ActualizarControles();

        // Conectar los eventos automáticamente.
        ConectarEventos();

        Debug.Log(
            "[MAV Ajustes] Panel abierto en " +
            gameObject.scene.name
        );

        PreferenciasManosAlVolante.Diagnosticar();
    }

    // -----------------------------------------------
    // Buscar componentes si faltan referencias
    // -----------------------------------------------

    private void BuscarReferencias()
    {
        if (sliderVolumen == null)
        {
            sliderVolumen =
                GetComponentInChildren<Slider>(true);
        }

        if (dropdownCalidad == null)
        {
            dropdownCalidad =
                GetComponentInChildren<TMP_Dropdown>(true);
        }

        if (togglePantallaCompleta == null)
        {
            Toggle[] toggles =
                GetComponentsInChildren<Toggle>(true);

            foreach (Toggle toggle in toggles)
            {
                if (toggle.gameObject.name ==
                    "TogglePantallaCompleta")
                {
                    togglePantallaCompleta = toggle;
                    break;
                }
            }
        }

        if (sliderVolumen == null ||
            dropdownCalidad == null ||
            togglePantallaCompleta == null)
        {
            Debug.LogError(
                "[MAV Ajustes] Faltan referencias UI en " +
                gameObject.name,
                this
            );
        }
    }

    // -----------------------------------------------
    // Opciones gráficas
    // -----------------------------------------------

    private void PrepararOpcionesCalidad()
    {
        if (opcionesPreparadas || dropdownCalidad == null)
            return;

        List<string> opciones =
            new List<string>(QualitySettings.names);

        dropdownCalidad.ClearOptions();
        dropdownCalidad.AddOptions(opciones);

        opcionesPreparadas = true;
    }

    // -----------------------------------------------
    // Sincronizar interfaz con PlayerPrefs
    // -----------------------------------------------

    private void ActualizarControles()
    {
        if (sliderVolumen != null)
        {
            sliderVolumen.minValue = 0f;
            sliderVolumen.maxValue = 1f;
            sliderVolumen.wholeNumbers = false;

            sliderVolumen.SetValueWithoutNotify(
                PreferenciasManosAlVolante.Volumen
            );
        }

        if (dropdownCalidad != null &&
            dropdownCalidad.options.Count > 0)
        {
            int indice = Mathf.Clamp(
                PreferenciasManosAlVolante.IndiceCalidad,
                0,
                dropdownCalidad.options.Count - 1
            );

            dropdownCalidad.SetValueWithoutNotify(indice);
            dropdownCalidad.RefreshShownValue();
        }

        if (togglePantallaCompleta != null)
        {
            togglePantallaCompleta.SetIsOnWithoutNotify(
                PreferenciasManosAlVolante.PantallaCompleta
            );
        }
    }

    // -----------------------------------------------
    // Registrar eventos automáticos
    // -----------------------------------------------

    private void ConectarEventos()
    {
        if (eventosConectados)
            return;

        if (sliderVolumen != null)
        {
            sliderVolumen.onValueChanged.AddListener(
                CambiarVolumen
            );
        }

        if (dropdownCalidad != null)
        {
            dropdownCalidad.onValueChanged.AddListener(
                CambiarCalidad
            );
        }

        if (togglePantallaCompleta != null)
        {
            togglePantallaCompleta.onValueChanged.AddListener(
                CambiarPantallaCompleta
            );
        }

        eventosConectados = true;
    }

    private void DesconectarEventos()
    {
        if (!eventosConectados)
            return;

        if (sliderVolumen != null)
        {
            sliderVolumen.onValueChanged.RemoveListener(
                CambiarVolumen
            );
        }

        if (dropdownCalidad != null)
        {
            dropdownCalidad.onValueChanged.RemoveListener(
                CambiarCalidad
            );
        }

        if (togglePantallaCompleta != null)
        {
            togglePantallaCompleta.onValueChanged.RemoveListener(
                CambiarPantallaCompleta
            );
        }

        eventosConectados = false;
    }

    // -----------------------------------------------
    // Métodos de modificación
    // -----------------------------------------------

    public void CambiarVolumen(float volumen)
    {
        PreferenciasManosAlVolante.GuardarVolumen(volumen);
    }

    public void CambiarCalidad(int calidad)
    {
        PreferenciasManosAlVolante.GuardarCalidad(calidad);
    }

    public void CambiarPantallaCompleta(bool activada)
    {
        PreferenciasManosAlVolante
            .GuardarPantallaCompleta(activada);
    }

    // -----------------------------------------------
    // Al cerrar el panel
    // -----------------------------------------------

    private void OnDisable()
    {
        DesconectarEventos();

        PlayerPrefs.Save();
    }

    private void OnApplicationQuit()
    {
        PlayerPrefs.Save();
    }

    // -----------------------------------------------
    // Prueba desde el Inspector
    // -----------------------------------------------

    [ContextMenu("MAV - Diagnosticar preferencias")]
    private void DiagnosticarDesdeInspector()
    {
        PreferenciasManosAlVolante.Diagnosticar();
    }
}