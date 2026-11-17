/* 
    Diseñado por [GameDev]: twscraft {AMM}, from UAEH - ICBI
    Powered by the best: [AI.Eng] xSaidanMka {AAAI}, from ESCOM & CINVESTAV by IPN
    
    Hecho por: ChatGPT-6
*/

/*
    Manos al Volante - Fundido negro entre escenas.
    Colocar en Canvas_Transicion (objeto RAIZ de cada escena).
    Requiere Canvas + CanvasGroup + una Image hija negra a pantalla completa.
    No requiere Animator ni paquetes externos.
*/
using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;

[RequireComponent(typeof(Canvas))]
[RequireComponent(typeof(CanvasGroup))]
public class TransicionEscenas : MonoBehaviour
{
    public static TransicionEscenas Instancia { get; private set; }

    [Header("Cortina negra")]
    [SerializeField] private CanvasGroup cortina;

    [Header("Duracion del efecto (segundos)")]
    [SerializeField, Min(0.05f)] private float duracionFundido = 0.6f;

    [SerializeField] private bool mostrarFundidoAlIniciar = true;

    private bool cambiandoEscena;

    private void Awake()
    {
        // Habra un prefab en cada escena, pero solo una instancia sobrevivira.
        if (Instancia != null && Instancia != this)
        {
            Destroy(gameObject);
            return;
        }

        Instancia = this;
        DontDestroyOnLoad(gameObject); // IMPORTANTE: este Canvas debe estar en la raiz.

        if (cortina == null)
            cortina = GetComponent<CanvasGroup>();

        Canvas canvas = GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.overrideSorting = true;
        canvas.sortingOrder = 30000;

        cortina.alpha = mostrarFundidoAlIniciar ? 1f : 0f;
        cortina.interactable = false;
        cortina.blocksRaycasts = mostrarFundidoAlIniciar;
    }

    private IEnumerator Start()
    {
        if (Instancia != this) yield break;
        if (mostrarFundidoAlIniciar)
        {
            cambiandoEscena = true;
            yield return FundirHasta(0f);
        }
        cortina.blocksRaycasts = false;
        cambiandoEscena = false;
    }

    public void CambiarEscena(string nombreEscena)
    {
        if (Instancia != this || cambiandoEscena) return;
        if (string.IsNullOrWhiteSpace(nombreEscena) ||
            !Application.CanStreamedLevelBeLoaded(nombreEscena))
        {
            Debug.LogError("TransicionEscenas: no se encontro '" + nombreEscena +
                           "'. Revisa File > Build Profiles > Scene List.", this);
            return;
        }

        StartCoroutine(CambiarConFundido(nombreEscena));
    }

    private IEnumerator CambiarConFundido(string nombreEscena)
    {
        cambiandoEscena = true;
        cortina.blocksRaycasts = true; // Bloquea otros botones mientras cambia.

        // 1) Oscurece la escena actual.
        yield return FundirHasta(1f);

        // 2) Guarda la configuracion y permite que el nuevo menu funcione
        //    incluso si regresamos desde un juego pausado (timeScale = 0).
        PlayerPrefs.Save();
        Time.timeScale = 1f;
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;

        // 3) Carga sin quitar la pantalla negra.
        AsyncOperation carga = SceneManager.LoadSceneAsync(nombreEscena, LoadSceneMode.Single);
        if (carga == null)
        {
            Debug.LogError("TransicionEscenas: la carga no se pudo iniciar.", this);
            yield return FundirHasta(0f);
            cortina.blocksRaycasts = false;
            cambiandoEscena = false;
            yield break;
        }

        while (!carga.isDone)
            yield return null;

        // Deja inicializar la UI y camara de la nueva escena.
        yield return null;

        // 4) Revela gradualmente la nueva escena.
        yield return FundirHasta(0f);
        cortina.blocksRaycasts = false;
        cambiandoEscena = false;
    }

    private IEnumerator FundirHasta(float alphaFinal)
    {
        float alphaInicial = cortina.alpha;
        float tiempo = 0f;

        // Unscaled funciona aunque el menu de pausa haya detenido Time.timeScale.
        while (tiempo < duracionFundido)
        {
            tiempo += Time.unscaledDeltaTime;
            cortina.alpha = Mathf.Lerp(alphaInicial, alphaFinal,
                                       Mathf.Clamp01(tiempo / duracionFundido));
            yield return null;
        }
        cortina.alpha = alphaFinal;
    }

    private void OnDestroy()
    {
        if (Instancia == this) Instancia = null;
    }
}