using System.Collections.Generic;
using UnityEngine;
using UnityEngine.XR;
using UnityEngine.XR.Interaction.Toolkit.Inputs.Simulation;

public class XRSimulatorFallback : MonoBehaviour
{
    public GameObject simulatorPrefab;

    void Start()
    {
#if UNITY_EDITOR
        var displays = new List<XRDisplaySubsystem>();
        SubsystemManager.GetSubsystems(displays);
        foreach (XRDisplaySubsystem display in displays)
        {
            if (display.running)
                return;
        }

        if (simulatorPrefab == null)
            return;

        var simulator = Instantiate(simulatorPrefab).GetComponentInChildren<XRDeviceSimulator>();
        if (simulator == null)
            return;

        simulator.keyboardXTranslateSpeed = 0f;
        simulator.keyboardYTranslateSpeed = 0f;
        simulator.keyboardZTranslateSpeed = 0f;
        simulator.manipulateLeftAction = null;
        simulator.rotateModeOverrideAction = null;
        simulator.toggleMouseTransformationModeAction = null;
#endif
    }
}
