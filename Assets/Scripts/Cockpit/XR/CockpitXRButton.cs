using System;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;
using UnityEngine.XR.Interaction.Toolkit.Interactors;

[RequireComponent(typeof(XRSimpleInteractable), typeof(BoxCollider))]
public class CockpitXRButton : MonoBehaviour
{
    public CockpitMfdButton button;
    public CockpitButtonFeel feel;

    public event Action<IXRInteractor> Pressed;
    public event Action<IXRInteractor> Released;
    public event Action<IXRInteractor> Cancelled;

    public XRSimpleInteractable Interactable { get; private set; }

    private BoxCollider hitBox;
    private float lastReleaseTime = float.NegativeInfinity;
    private bool ownsPress;

    void Awake()
    {
        Interactable = GetComponent<XRSimpleInteractable>();
        Interactable.selectMode = InteractableSelectMode.Multiple;
        hitBox = GetComponent<BoxCollider>();
    }

    void OnEnable()
    {
        Interactable.hoverEntered.AddListener(OnHoverChanged);
        Interactable.hoverExited.AddListener(OnHoverChanged);
        Interactable.selectEntered.AddListener(OnSelectEntered);
        Interactable.selectExited.AddListener(OnSelectExited);
    }

    void OnDisable()
    {
        Interactable.hoverEntered.RemoveListener(OnHoverChanged);
        Interactable.hoverExited.RemoveListener(OnHoverChanged);
        Interactable.selectEntered.RemoveListener(OnSelectEntered);
        Interactable.selectExited.RemoveListener(OnSelectExited);
        if (ownsPress && button != null)
            button.CancelPress();
        ownsPress = false;
    }

    void Update()
    {
        if (button != null && !button.IsHovered && Interactable.interactorsHovering.Count > 0)
            button.SetHovered(true);
    }

    private void OnHoverChanged(BaseInteractionEventArgs args)
    {
        if (button != null)
            button.SetHovered(Interactable.interactorsHovering.Count > 0);
    }

    private void OnSelectEntered(SelectEnterEventArgs args)
    {
        if (button == null || button.IsPressed || Interactable.interactorsSelecting.Count > 1)
            return;
        if (Time.unscaledTime - lastReleaseTime < Debounce)
            return;

        button.Press();
        ownsPress = true;
        Pressed?.Invoke(args.interactorObject);
    }

    private void OnSelectExited(SelectExitEventArgs args)
    {
        if (button == null || !ownsPress || Interactable.interactorsSelecting.Count > 0)
            return;

        ownsPress = false;
        lastReleaseTime = Time.unscaledTime;
        if (!button.IsPressed)
            return;
        if (!args.isCanceled && IsOverFace(args.interactorObject))
        {
            button.Release(true);
            Released?.Invoke(args.interactorObject);
        }
        else
        {
            button.CancelPress();
            Cancelled?.Invoke(args.interactorObject);
        }
    }

    private bool IsOverFace(IXRInteractor interactor)
    {
        Vector3 local = transform.InverseTransformPoint(interactor.GetAttachTransform(Interactable).position) - hitBox.center;
        float margin = feel != null ? feel.releaseEdgeMargin : 0f;
        return Mathf.Abs(local.x) <= hitBox.size.x * 0.5f + margin && Mathf.Abs(local.y) <= hitBox.size.y * 0.5f + margin;
    }

    private float Debounce => feel != null ? feel.debounce : 0f;
}
