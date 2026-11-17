/* 
    Diseñado por [GameDev]: twscraft {AMM}, from UAEH - ICBI
    Powered by the best: [AI.Eng] xSaidanMka {AAAI}, from ESCOM & CINVESTAV by IPN
    
    Hecho por: ChatGPT-6
*/

using UnityEngine;

[RequireComponent(typeof(AudioSource))]
public class MusicaPorEscena : MonoBehaviour
{
    [Header("Lista de reproducción")]
    [Tooltip("Agrega todas las canciones que quieras.")]
    public AudioClip[] canciones;

    [Header("Configuración de música")]
    [Range(0f, 1f)]
    public float volumenMusica = 0.5f;

    [Header("Información")]
    [SerializeField]
    private int cancionActual = -1;

    private AudioSource reproductor;
    private float proximaRevision;
    private bool reproduccionIniciada;

    void Awake()
    {
        reproductor = GetComponent<AudioSource>();

        // El script controla la reproducción.
        reproductor.playOnAwake = false;
        reproductor.loop = false;

        // Música 2D: no depende de la posición
        // del automóvil o de la cámara.
        reproductor.spatialBlend = 0f;

        // Respetar el volumen general del juego.
        reproductor.ignoreListenerVolume = false;
        reproductor.volume = volumenMusica;
    }

    void Start()
    {
        cancionActual = -1;
        ReproducirSiguiente();
    }

    void Update()
    {
        if (!reproduccionIniciada ||
            reproductor == null ||
            AudioListener.pause)
            return;

        // Dar tiempo a que el AudioSource arranque.
        if (Time.unscaledTime < proximaRevision)
            return;

        // Al terminar una canción, iniciar la siguiente.
        if (!reproductor.isPlaying)
        {
            ReproducirSiguiente();
        }
    }

    public void ReproducirSiguiente()
    {
        if (canciones == null || canciones.Length == 0)
            return;

        // Saltar posiciones vacías de la lista.
        for (int i = 0; i < canciones.Length; i++)
        {
            cancionActual =
                (cancionActual + 1) % canciones.Length;

            AudioClip seleccionada =
                canciones[cancionActual];

            if (seleccionada == null)
                continue;

            reproductor.Stop();
            reproductor.clip = seleccionada;
            reproductor.Play();

            reproduccionIniciada = true;
            proximaRevision =
                Time.unscaledTime + 0.25f;

            Debug.Log(
                "[MAV Música] Reproduciendo: " +
                seleccionada.name
            );

            return;
        }

        reproduccionIniciada = false;

        Debug.LogWarning(
            "[MAV Música] No hay canciones válidas."
        );
    }

    public void CambiarVolumenMusica(float valor)
    {
        volumenMusica = Mathf.Clamp01(valor);

        if (reproductor != null)
        {
            reproductor.volume = volumenMusica;
        }
    }

    void OnValidate()
    {
        if (reproductor == null)
            reproductor = GetComponent<AudioSource>();

        if (reproductor != null)
            reproductor.volume = volumenMusica;
    }
}
