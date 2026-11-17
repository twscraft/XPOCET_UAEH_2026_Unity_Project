/* 
    Diseñado por [GameDev]: twscraft {AMM}, from UAEH - ICBI
    Powered by the best: [AI.Eng] xSaidanMka {AAAI}, from ESCOM & CINVESTAV by IPN
    
    Hecho por: ChatGPT-6
*/

using UnityEngine;
using UnityEngine.SceneManagement;

public class NavegacionMenu : MonoBehaviour
{
    [Header("Escenas (nombres exactos, sin .unity)")]
    public string escenaMenu = "MainMenu";
    public string escenaJuego = "MainScene";

    [Header("Contenedor principal - no se desactiva")]
    public GameObject menuPrincipal;

    [Header("Elementos que se ocultan al abrir un subpanel")]
    public GameObject titulo;
    public GameObject jugar;
    public GameObject ajustes;
    public GameObject creditos;
    public GameObject salir;

    [Header("Paneles secundarios (dentro de MenuPrincipal)")]
    public GameObject panelAjustes;
    public GameObject panelCreditos;

    private bool ajustesAbiertos;

    private void Start()
    {
        Time.timeScale = 1f;
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
        if (menuPrincipal != null) menuPrincipal.SetActive(true);
        CerrarPaneles();
    }

    public void AbrirCreditos()
    {
        if (ajustesAbiertos) Time.timeScale = 1f;
        ajustesAbiertos = false;
        MostrarElementosPrincipales(false);
        if (panelAjustes != null) panelAjustes.SetActive(false);
        if (panelCreditos != null) panelCreditos.SetActive(true);
    }

    public void AbrirAjustes()
    {
        MostrarElementosPrincipales(false);
        if (panelCreditos != null) panelCreditos.SetActive(false);
        if (panelAjustes != null) panelAjustes.SetActive(true);
        ajustesAbiertos = true;
        // Pausa la camara del menu durante los ajustes.
        Time.timeScale = 0f;
    }

    // VOLVER desde los paneles de MainMenu.
    public void CerrarPaneles()
    {
        // Confirma el guardado aunque el jugador no cierre el ejecutable.
        PlayerPrefs.Save();
        if (panelCreditos != null) panelCreditos.SetActive(false);
        if (panelAjustes != null) panelAjustes.SetActive(false);
        if (ajustesAbiertos) Time.timeScale = 1f;
        ajustesAbiertos = false;
        MostrarElementosPrincipales(true);
    }

    private void MostrarElementosPrincipales(bool visible)
    {
        if (titulo != null) titulo.SetActive(visible);
        if (jugar != null) jugar.SetActive(visible);
        if (ajustes != null) ajustes.SetActive(visible);
        if (creditos != null) creditos.SetActive(visible);
        if (salir != null) salir.SetActive(visible);
    }

    public void Jugar() { CargarEscena(escenaJuego); }
    public void VolverAlMenu() { CargarEscena(escenaMenu); }

    public void Salir()
    {
        // Los cambios del slider/dropdown/toggle ya se guardaron al modificarlos;
        // repetimos el guardado al pulsar SALIR como proteccion extra.
        PlayerPrefs.Save();
        Time.timeScale = 1f;
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }

    private void CargarEscena(string nombre)
    {
        if (string.IsNullOrWhiteSpace(nombre) || !Application.CanStreamedLevelBeLoaded(nombre))
        {
            Debug.LogError("Escena no disponible: '" + nombre +
                "'. Revisa Build Profiles > Scene List.", this);
            return;
        }

        PlayerPrefs.Save();

        if (TransicionEscenas.Instancia != null)
        {
            // El fundido se ejecuta con unscaledDeltaTime (funciona en pausa).
            TransicionEscenas.Instancia.CambiarEscena(nombre);
        }
        else
        {
            // Permite seguir navegando mientras aun no instalas el Canvas de transicion.
            Debug.LogWarning("Falta Canvas_Transicion: cambio de escena sin fundido.", this);
            Time.timeScale = 1f;
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
            SceneManager.LoadScene(nombre, LoadSceneMode.Single);
        }
    }
}