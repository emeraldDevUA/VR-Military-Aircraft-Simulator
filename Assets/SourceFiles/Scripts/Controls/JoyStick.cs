using UnityEngine;

[System.Serializable]
public class FlightInput
{
    // Axes (-1 to 1)
    public float aileron;    // roll
    public float elevator;   // pitch
    public float rudder;     // yaw

    // 0 to 1
    public float throttle;

    // -1 to 1
    public float trim;

    // Device info (optional)
    public int numAxes, numHats, numButtons;

    public void Clear()
    {
        aileron = elevator = rudder = trim = 0f;
        throttle = 0f;
    }
}
