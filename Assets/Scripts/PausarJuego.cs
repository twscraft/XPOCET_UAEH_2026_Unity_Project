/* 
    Diseñado por [GameDev]: twscraft {AMM}, from UAEH - ICBI
    Powered by the best: [AI.Eng] xSaidanMka {AAAI}, from ESCOM & CINVESTAV by IPN
    
    Hecho por: ChatGPT-6
*/

using UnityEngine;
using UnityEngine.Rendering.PostProcessing;

#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

// Colocar en PauseManager, SIEMPRE ACTIVO, nunca dentro de MenuPausa.
public class PausarJuego : MonoBehaviour
{
    [Header("Pantallas del juego")]
    public GameObject menuPausa;
    [Tooltip("Debe ser hermano de MenuPausa dentro del Canvas; NO hijo de MenuPausa.")]
    public GameObject panelAjustes;

    [Header("Estado de pausa (solo lectura en Play)")]
    public bool juegoPausado = false;

    [Header("Postprocesado del fondo (opcional)")]
    public PostProcessVolume postProcessVolume;

    private CursorLockMode modoCursorAnterior;
    private bool cursorVisibleAnterior;
    private bool ajustesAbiertos;

    private void Awake()
    {
        if (menuPausa != null && transform.IsChildOf(menuPausa.transform))
            Debug.LogError("Mueve PausarJuego fuera de MenuPausa y usa PauseManager.", this);
        if (menuPausa != null && panelAjustes != null &&
            panelAjustes.transform.IsChildOf(menuPausa.transform))
            Debug.LogError("Panel_Ajustes NO puede ser hijo de MenuPausa: al ocultarlo tambien desaparece Ajustes.", this);
    }

    private void Start()
    {
        Time.timeScale = 1f;
        juegoPausado = false;
        ajustesAbiertos = false;
        if (menuPausa != null) menuPausa.SetActive(false);
        if (panelAjustes != null) panelAjustes.SetActive(false);
        if (postProcessVolume != null) postProcessVolume.enabled = false;
    }

    private void Update()
    {
        bool escape = false;
#if ENABLE_INPUT_SYSTEM
        if (Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame)
            escape = true;
#endif
#if ENABLE_LEGACY_INPUT_MANAGER
        if (Input.GetKeyDown(KeyCode.Escape))
            escape = true;
#endif
        if (!escape) return;

        if (ajustesAbiertos)
            CerrarAjustes(); // ESC vuelve primero al menu de pausa.
        else
            AlternarPausa(); // Otro ESC reanuda el juego.
    }

    public void AlternarPausa()
    {
        if (ajustesAbiertos) CerrarAjustes();
        else if (juegoPausado) ReanudarJuego();
        else Pausar();
    }

    public void Pausar()
    {
        if (juegoPausado) return;
        modoCursorAnterior = Cursor.lockState;
        cursorVisibleAnterior = Cursor.visible;
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
        juegoPausado = true;
        ajustesAbiertos = false;
        if (panelAjustes != null) panelAjustes.SetActive(false);
        if (menuPausa != null) menuPausa.SetActive(true);
        if (postProcessVolume != null) postProcessVolume.enabled = true;
        Time.timeScale = 0f;
    }

    // Abrir desde boton Ajustes del menu de pausa, o desde un boton de ajustes del HUD.
    public void AbrirAjustes()
    {
        if (!juegoPausado) Pausar();
        ajustesAbiertos = true;
        if (menuPausa != null) menuPausa.SetActive(false);
        if (panelAjustes != null) panelAjustes.SetActive(true);
        Time.timeScale = 0f;
    }

    // Volver desde Ajustes al menu de pausa, SIN reanudar el juego.
    public void CerrarAjustes()
    {
        PlayerPrefs.Save(); // Confirma los cambios al salir de Ajustes.
        ajustesAbiertos = false;
        if (panelAjustes != null) panelAjustes.SetActive(false);
        if (juegoPausado && menuPausa != null) menuPausa.SetActive(true);
        // No restaurar Time.timeScale: sigue pausado hasta pulsar Continuar.
    }

    public void ReanudarJuego()
    {
        if (!juegoPausado) return;
        PlayerPrefs.Save(); // Tambien confirma los cambios al continuar.
        ajustesAbiertos = false;
        juegoPausado = false;
        if (panelAjustes != null) panelAjustes.SetActive(false);
        if (menuPausa != null) menuPausa.SetActive(false);
        if (postProcessVolume != null) postProcessVolume.enabled = false;
        Time.timeScale = 1f;
        Cursor.lockState = modoCursorAnterior;
        Cursor.visible = cursorVisibleAnterior;
    }

    private void OnDestroy()
    {
        if (juegoPausado) Time.timeScale = 1f;
    }
}