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

    private TrackedPoseDriver driver;
    private InputAction position;
    private Vector3? neutral;
    private readonly List<XRInputSubsystem> subsystems = new List<XRInputSubsystem>();

    void Awake()
    {
        driver = GetComponent<TrackedPoseDriver>();
        driver.trackingType = TrackedPoseDriver.TrackingType.RotationOnly;
        position = driver.positionInput.action;
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
    }

    public void Recenter() => neutral = null;

    private void OnOriginUpdated(XRInputSubsystem subsystem) => Recenter();

    private void Apply()
    {
        if (position == null || !position.enabled || position.controls.Count == 0)
            return;

        Vector3 tracked = position.ReadValue<Vector3>();
        neutral ??= tracked;
        Vector3 lean = tracked - neutral.Value;
        transform.localPosition = Vector3.Max(minOffset, Vector3.Min(maxOffset, lean));
    }
}
