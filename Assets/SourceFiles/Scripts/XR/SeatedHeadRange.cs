using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.XR;
using UnityEngine.XR;

[RequireComponent(typeof(TrackedPoseDriver))]
public class SeatedHeadRange : MonoBehaviour
{
    public Vector3 minOffset = new Vector3(-0.25f, -0.15f, -0.05f);
    public Vector3 maxOffset = new Vector3(0.25f, 0.10f, 0.30f);

    private InputAction position;
    private Transform cameraOffset;
    private Vector3? neutral;
    private readonly List<XRInputSubsystem> subsystems = new List<XRInputSubsystem>();

    void Awake()
    {
        position = GetComponent<TrackedPoseDriver>().positionInput.action;
        cameraOffset = transform.parent;
    }

    void OnEnable()
    {
        InputSystem.onAfterUpdate += Apply;
        SubsystemManager.GetSubsystems(subsystems);
        foreach (XRInputSubsystem subsystem in subsystems)
            subsystem.trackingOriginUpdated += OnOriginUpdated;
    }

    void OnDisable()
    {
        InputSystem.onAfterUpdate -= Apply;
        foreach (XRInputSubsystem subsystem in subsystems)
            subsystem.trackingOriginUpdated -= OnOriginUpdated;
        subsystems.Clear();
        if (cameraOffset != null)
            cameraOffset.localPosition = Vector3.zero;
    }

    public void Recenter() => neutral = null;

    private void OnOriginUpdated(XRInputSubsystem subsystem) => Recenter();

    private void Apply()
    {
        if (cameraOffset == null || position == null || !position.enabled || position.controls.Count == 0)
            return;

        Vector3 tracked = position.ReadValue<Vector3>();
        neutral ??= tracked;
        Vector3 lean = tracked - neutral.Value;
        Vector3 limited = Vector3.Max(minOffset, Vector3.Min(maxOffset, lean));
        cameraOffset.localPosition = limited - tracked;
    }
}
