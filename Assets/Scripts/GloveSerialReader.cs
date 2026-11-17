/*  
    Diseñado por [GameDev]: twscraft {AMM}, from UAEH - ICBI
    Powered by the best: [AI.Eng] xSaidanMka {AAAI}, from ESCOM & CINVESTAV by IPN
    
    Hecho por: ChatGPT-6
*/


using System;
using System.Diagnostics;
using System.Globalization;
using System.IO.Ports;
using System.Threading;
using UnityEngine;
using Debug = UnityEngine.Debug;

// Lee los guantes por el puerto serie del receptor y manda la vibración.
//
// Protocolo (460800 baudios, líneas terminadas en LF):
//   Receptor -> PC:  G,<L|R>,<roll>,<pitch>,<boton>,<grip>,<bateria_mV>
//                    HB   (latido, 2 veces por segundo; sirve para encontrar el puerto solo)
//   PC -> Receptor:  H,<L|R>,<intensidad>       (0 a 100; un solo motor por guante)
//
// Necesita: Player Settings > Api Compatibility Level = .NET Framework
//           y Scripting Backend = Mono (para el ejecutable de Windows).
[DefaultExecutionOrder(-100)]
public class GloveSerialReader : MonoBehaviour
{
    [Header("Conexión con el receptor")]
    [Tooltip("Vacío = buscar el receptor automáticamente. Ejemplo: COM5")]
    public string portName = "";
    public int baudRate = 460800;
    public bool connectOnStart = true;
    public float retrySeconds = 3f;

    public bool IsConnected { get { return _connected; } }
    public string ConnectedPort { get; private set; }

    readonly object _lock = new object();
    GloveState _left, _right;
    SerialPort _port;
    Thread _thread;
    volatile bool _stop;
    volatile bool _connected;
    float _nextTry;

    public GloveState Left { get { lock (_lock) { return _left; } } }
    public GloveState Right { get { lock (_lock) { return _right; } } }

    void Awake()
    {
        Application.runInBackground = true;   // sigue leyendo aunque la ventana pierda el foco
        ConnectedPort = "";

        // En el ejecutable puedes fijar el puerto con un acceso directo:  juego.exe -gloveport COM5
        string[] args = Environment.GetCommandLineArgs();
        for (int i = 0; i < args.Length - 1; i++)
        {
            if (args[i] == "-gloveport") portName = args[i + 1];
        }
    }

    void Update()
    {
        if (!connectOnStart || _connected) return;
        if (_thread != null && _thread.IsAlive) return;
        if (Time.unscaledTime < _nextTry) return;

        _nextTry = Time.unscaledTime + retrySeconds;
        _stop = false;
        _thread = new Thread(Worker) { IsBackground = true };
        _thread.Start();
    }

    [ContextMenu("Conectar ahora")]
    public void Connect()
    {
        connectOnStart = true;
        _nextTry = 0f;
    }

    // Hilo de fondo: busca el puerto, se conecta y lee sin frenar el juego
    void Worker()
    {
        string[] candidates = string.IsNullOrEmpty(portName)
            ? SerialPort.GetPortNames()
            : new[] { portName };

        foreach (string name in candidates)
        {
            if (_stop) return;

            SerialPort p = TryOpen(name);
            if (p == null) continue;

            // Con búsqueda automática, solo nos quedamos con el puerto que manda G, o HB
            if (string.IsNullOrEmpty(portName) && !LooksLikeReceiver(p))
            {
                CloseQuietly(p);
                continue;
            }

            _port = p;
            ConnectedPort = name;
            _connected = true;
            Debug.Log("Receptor conectado en " + name);

            ReadLoop(p);

            _connected = false;
            _port = null;
            CloseQuietly(p);
            Debug.Log("Receptor desconectado de " + name);
            return;
        }
    }

    SerialPort TryOpen(string name)
    {
        try
        {
            SerialPort p = new SerialPort(name, baudRate);
            p.NewLine = ((char)10).ToString();   // el receptor termina cada línea con LF
            p.ReadTimeout = 200;
            p.WriteTimeout = 200;
            p.DtrEnable = false;                 // evita reiniciar la placa al abrir el puerto
            p.RtsEnable = false;
            p.Open();
            return p;
        }
        catch (Exception)
        {
            return null;
        }
    }

    bool LooksLikeReceiver(SerialPort p)
    {
        long end = Stopwatch.GetTimestamp() + (long)(1.5 * Stopwatch.Frequency);
        while (Stopwatch.GetTimestamp() < end && !_stop)
        {
            try
            {
                string line = p.ReadLine().Trim();
                if (line.StartsWith("G,") || line.StartsWith("HB")) return true;
            }
            catch (TimeoutException)
            {
                // nada en 200 ms: seguimos esperando
            }
            catch (Exception)
            {
                return false;
            }
        }
        return false;
    }

    void ReadLoop(SerialPort p)
    {
        while (!_stop)
        {
            try
            {
                string line = p.ReadLine();
                Parse(line.Trim());
            }
            catch (TimeoutException)
            {
                // nada en 200 ms: seguimos esperando
            }
            catch (Exception)
            {
                return;   // se desconectó el cable
            }
        }
    }

    // Formato: G,<L|R>,<roll>,<pitch>,<boton>,<grip>,<bateria_mV>
    void Parse(string line)
    {
        string[] p = line.Split(',');
        if (p.Length < 7 || p[0] != "G") return;

        CultureInfo inv = CultureInfo.InvariantCulture;
        float roll, pitch;
        int btn, grip, batt;
        if (!float.TryParse(p[2], NumberStyles.Float, inv, out roll)) return;
        if (!float.TryParse(p[3], NumberStyles.Float, inv, out pitch)) return;
        if (!int.TryParse(p[4], out btn)) return;
        if (!int.TryParse(p[5], out grip)) return;
        if (!int.TryParse(p[6], out batt)) return;

        GloveState s = new GloveState
        {
            roll = roll,
            pitch = pitch,
            button = btn != 0,
            grip = grip,
            batteryMv = batt,
            stamp = Stopwatch.GetTimestamp()
        };

        lock (_lock)
        {
            if (p[1] == "L") _left = s;
            else if (p[1] == "R") _right = s;
        }
    }

    // Intensidad de 0 a 100 para el motor de un guante ('L' o 'R')
    public void SendHaptics(char hand, int level)
    {
        SerialPort p = _port;
        if (!_connected || p == null) return;
        try { p.WriteLine("H," + hand + "," + level); }
        catch (Exception) { }
    }

    static void CloseQuietly(SerialPort p)
    {
        try { if (p != null && p.IsOpen) p.Close(); }
        catch (Exception) { }
    }

    public void Disconnect()
    {
        _stop = true;
        _connected = false;
        if (_thread != null && _thread.IsAlive) _thread.Join(800);

        SerialPort p = _port;
        _port = null;
        CloseQuietly(p);
    }

    void OnDisable() { Disconnect(); }
    void OnApplicationQuit() { Disconnect(); }
}
