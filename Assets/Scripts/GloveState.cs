/*  
    Diseñado por [GameDev]: twscraft {AMM}, from UAEH - ICBI
    Powered by the best: [AI.Eng] xSaidanMka {AAAI}, from ESCOM & CINVESTAV by IPN
    
    Hecho por: ChatGPT-6
*/

using System.Diagnostics;

// Datos de un guante. No es un componente: solo una estructura.
public struct GloveState
{
    public float roll;      // grados: giro de la mano sobre el eje de los dedos
    public float pitch;     // grados: inclinación hacia adelante (+) o atrás (-)
    public bool button;     // izquierdo = acelerador, derecho = freno
    public int grip;        // 0 a 4095: FSR de la mano derecha (0 en la izquierda)
    public int batteryMv;   // batería en milivolts
    public long stamp;      // momento de la última lectura (0 = nunca llegó nada)

    public float AgeSeconds
    {
        get
        {
            if (stamp == 0) return float.PositiveInfinity;
            return (Stopwatch.GetTimestamp() - stamp) / (float)Stopwatch.Frequency;
        }
    }
}