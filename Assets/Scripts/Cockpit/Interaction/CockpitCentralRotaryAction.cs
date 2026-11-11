using UnityEngine;

/// <summary>Device-independent action mapping for the central panel's rotary controls.</summary>
[DisallowMultipleComponent]
public sealed class CockpitCentralRotaryAction : MonoBehaviour
{
    public enum RotaryFunction { None, ActiveWaypoint, DisplayBrightness }

    [SerializeField] private RotaryFunction function;
    [SerializeField] private CentralAvionicsControlPanelController panelController;

    public void Invoke(int direction)
    {
        if (panelController == null)
            panelController = FindFirstObjectByType<CentralAvionicsControlPanelController>();
        if (panelController == null)
            return;
        switch (function)
        {
            case RotaryFunction.ActiveWaypoint: panelController.StepActiveWaypoint(direction); break;
            case RotaryFunction.DisplayBrightness: panelController.AdjustBrightness(direction); break;
        }
    }
}
