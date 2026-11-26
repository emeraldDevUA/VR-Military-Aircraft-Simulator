using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit.Inputs.Haptics;
using UnityEngine.XR.Interaction.Toolkit.Interactors;

[RequireComponent(typeof(CockpitXRButton))]
public class CockpitButtonFeedback : MonoBehaviour
{
    public Transform cap;
    public Vector3 localPressDirection = Vector3.forward;

    private CockpitXRButton xrButton;
    private AudioSource audioSource;
    private Vector3 capRest;
    private float capDepth;

    void Awake()
    {
        xrButton = GetComponent<CockpitXRButton>();
        if (cap != null)
            capRest = cap.localPosition;

        audioSource = GetComponent<AudioSource>();
        if (audioSource == null)
            audioSource = gameObject.AddComponent<AudioSource>();
        audioSource.playOnAwake = false;
        audioSource.spatialBlend = 1f;
        audioSource.minDistance = 0.15f;
        audioSource.maxDistance = 3f;
        audioSource.rolloffMode = AudioRolloffMode.Linear;
    }

    void OnEnable()
    {
        xrButton.Pressed += OnPressed;
        xrButton.Released += OnReleased;
    }

    void OnDisable()
    {
        xrButton.Pressed -= OnPressed;
        xrButton.Released -= OnReleased;
        capDepth = 0f;
        if (cap != null)
            cap.localPosition = capRest;
    }

    void Update()
    {
        CockpitButtonFeel feel = xrButton.feel;
        if (cap == null || feel == null)
            return;

        bool pressed = xrButton.button != null && xrButton.button.IsPressed;
        float target = pressed ? 1f : FingerDepth(feel.travel);
        float next = Mathf.MoveTowards(capDepth, target, feel.capSpeed * Time.deltaTime);
        if (Mathf.Approximately(next, capDepth))
            return;

        capDepth = next;
        cap.localPosition = capRest + localPressDirection * (feel.travel * capDepth);
    }

    private float FingerDepth(float travel)
    {
        if (travel <= 0f)
            return 0f;

        float deepest = 0f;
        var hovering = xrButton.Interactable.interactorsHovering;
        for (int i = 0; i < hovering.Count; i++)
        {
            if (hovering[i] is not XRPokeInteractor poker)
                continue;
            Vector3 local = transform.InverseTransformPoint(poker.GetAttachTransform(xrButton.Interactable).position);
            float depth = Mathf.Clamp01(1f + (local.z + poker.pokeInteractionOffset) / travel);
            deepest = Mathf.Max(deepest, depth);
        }
        return deepest;
    }

    private void OnPressed(IXRInteractor interactor)
    {
        CockpitButtonFeel feel = xrButton.feel;
        if (feel == null)
            return;
        Play(feel.pressClip, feel.volume);
        Pulse(interactor, feel.pressAmplitude, feel.pressDuration);
    }

    private void OnReleased(IXRInteractor interactor)
    {
        CockpitButtonFeel feel = xrButton.feel;
        if (feel == null)
            return;
        Play(feel.releaseClip, feel.volume);
        Pulse(interactor, feel.releaseAmplitude, feel.releaseDuration);
    }

    private void Play(AudioClip clip, float volume)
    {
        if (clip != null)
            audioSource.PlayOneShot(clip, volume);
    }

    private static void Pulse(IXRInteractor interactor, float amplitude, float duration)
    {
        if (amplitude <= 0f || duration <= 0f || interactor == null)
            return;
        HapticImpulsePlayer player = interactor.transform.GetComponentInParent<HapticImpulsePlayer>();
        if (player != null)
            player.SendHapticImpulse(amplitude, duration);
    }
}
