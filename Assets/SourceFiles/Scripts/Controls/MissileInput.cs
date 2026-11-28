using UnityEngine;

[System.Serializable]
public class MissileInput
{
    // Normalized control commands: -1 to +1
    [Range(-1f, 1f)]
    public float roll;

    [Range(-1f, 1f)]
    public float pitch;

    [Range(-1f, 1f)]
    public float yaw;

    // 0 to 1
    [Range(0f, 1f)]
    public float throttle;

    public void Clear()
    {
        roll = 0f;
        pitch = 0f;
        yaw = 0f;
        throttle = 0f;
    }
}
