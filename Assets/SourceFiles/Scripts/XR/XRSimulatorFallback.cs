using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.XR;
using UnityEngine.XR.Interaction.Toolkit.Inputs.Simulation;

public class XRSimulatorFallback : MonoBehaviour
{
    public GameObject simulatorPrefab;

#if UNITY_EDITOR
    private XRDeviceSimulator simulator;
    private float rotateX;
    private float rotateY;

    void Start()
    {
        var displays = new List<XRDisplaySubsystem>();
        SubsystemManager.GetSubsystems(displays);
        foreach (XRDisplaySubsystem display in displays)
        {
            if (display.running)
                return;
        }

        if (simulatorPrefab == null)
            return;

        simulator = Instantiate(simulatorPrefab).GetComponentInChildren<XRDeviceSimulator>();
        if (simulator == null)
            return;

        rotateX = simulator.mouseXRotateSensitivity;
        rotateY = simulator.mouseYRotateSensitivity;
    }

    void Update()
    {
        if (simulator == null || Mouse.current == null)
            return;

        bool look = Mouse.current.rightButton.isPressed;
        simulator.mouseXRotateSensitivity = look ? rotateX : 0f;
        simulator.mouseYRotateSensitivity = look ? rotateY : 0f;
    }
#endif
}
