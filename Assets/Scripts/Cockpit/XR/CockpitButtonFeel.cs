using UnityEngine;

[CreateAssetMenu(menuName = "Flight Sim/Cockpit Button Feel", fileName = "CockpitButtonFeel")]
public class CockpitButtonFeel : ScriptableObject
{
    [Header("Travel")]
    [Min(0f)] public float travel = 0.003f;
    [Min(0f)] public float hoverDepth = 0.006f;
    [Min(0f)] public float edgePadding = 0.0015f;
    [Min(1f)] public float capSpeed = 40f;

    [Header("Press")]
    [Min(0f)] public float debounce = 0.08f;
    [Min(0f)] public float releaseEdgeMargin = 0.004f;

    [Header("Haptics")]
    [Range(0f, 1f)] public float pressAmplitude = 0.5f;
    [Min(0f)] public float pressDuration = 0.02f;
    [Range(0f, 1f)] public float releaseAmplitude = 0.25f;
    [Min(0f)] public float releaseDuration = 0.015f;

    [Header("Sound")]
    public AudioClip pressClip;
    public AudioClip releaseClip;
    [Range(0f, 1f)] public float volume = 0.8f;
}
