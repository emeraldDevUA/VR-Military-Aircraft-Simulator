using UnityEngine;

/// <summary>Device-independent mapping from a physical key to a display controller method.</summary>
[DisallowMultipleComponent]
public sealed class CockpitMfdButtonAction : MonoBehaviour
{
    public enum ActionKind
    {
        None,
        NextWaypoint,
        PreviousWaypoint,
        IncreaseMapRange,
        DecreaseMapRange,
        NextTarget,
        PreviousTarget,
        IncreaseRadarRange,
        DecreaseRadarRange,
        CentralDigit0,
        CentralDigit1,
        CentralDigit2,
        CentralDigit3,
        CentralDigit4,
        CentralDigit5,
        CentralDigit6,
        CentralDigit7,
        CentralDigit8,
        CentralDigit9,
        CentralEnter,
        CentralClear
    }

    private static readonly float[] SupportedRanges = { 10f, 20f, 40f, 80f };

    [SerializeField] private ActionKind actionKind;
    [SerializeField] private MonoBehaviour displayController;

    public ActionKind Kind => actionKind;

    /// <summary>Execute this mapping. Called by the button after a completed press.</summary>
    public void InvokeAction()
    {
        switch (actionKind)
        {
            case ActionKind.NextWaypoint:
                RightController()?.AdvanceWaypoint(1);
                break;
            case ActionKind.PreviousWaypoint:
                RightController()?.AdvanceWaypoint(-1);
                break;
            case ActionKind.IncreaseMapRange:
                ChangeMapRange(1);
                break;
            case ActionKind.DecreaseMapRange:
                ChangeMapRange(-1);
                break;
            case ActionKind.NextTarget:
                LowerController()?.SelectNextTarget();
                break;
            case ActionKind.PreviousTarget:
                LowerController()?.SelectPreviousTarget();
                break;
            case ActionKind.IncreaseRadarRange:
                ChangeRadarRange(1);
                break;
            case ActionKind.DecreaseRadarRange:
                ChangeRadarRange(-1);
                break;
            case ActionKind.CentralDigit0: CentralController()?.AppendDigit(0); break;
            case ActionKind.CentralDigit1: CentralController()?.AppendDigit(1); break;
            case ActionKind.CentralDigit2: CentralController()?.AppendDigit(2); break;
            case ActionKind.CentralDigit3: CentralController()?.AppendDigit(3); break;
            case ActionKind.CentralDigit4: CentralController()?.AppendDigit(4); break;
            case ActionKind.CentralDigit5: CentralController()?.AppendDigit(5); break;
            case ActionKind.CentralDigit6: CentralController()?.AppendDigit(6); break;
            case ActionKind.CentralDigit7: CentralController()?.AppendDigit(7); break;
            case ActionKind.CentralDigit8: CentralController()?.AppendDigit(8); break;
            case ActionKind.CentralDigit9: CentralController()?.AppendDigit(9); break;
            case ActionKind.CentralEnter: CentralController()?.ConfirmInput(); break;
            case ActionKind.CentralClear: CentralController()?.ClearInput(); break;
        }
    }

    private RightMfdHsdController RightController()
    {
        RightMfdHsdController controller = displayController as RightMfdHsdController;
        if (controller == null && actionKind != ActionKind.None)
            Debug.LogWarning("MFD button action requires a RightMfdHsdController reference.", this);
        return controller;
    }

    private LowerRadarDisplayController LowerController()
    {
        LowerRadarDisplayController controller = displayController as LowerRadarDisplayController;
        if (controller == null && actionKind != ActionKind.None)
            Debug.LogWarning("MFD button action requires a LowerRadarDisplayController reference.", this);
        return controller;
    }

    private CentralAvionicsControlPanelController CentralController()
    {
        CentralAvionicsControlPanelController controller = displayController as CentralAvionicsControlPanelController;
        if (controller == null)
            controller = FindFirstObjectByType<CentralAvionicsControlPanelController>();
        if (controller == null && actionKind != ActionKind.None)
            Debug.LogWarning("Central keypad action requires a CentralAvionicsControlPanelController.", this);
        return controller;
    }

    private void ChangeMapRange(int direction)
    {
        RightMfdHsdController controller = RightController();
        if (controller == null)
            return;
        controller.SetMapRangeNm(StepRange(controller.MapRangeNm, direction));
    }

    private void ChangeRadarRange(int direction)
    {
        LowerRadarDisplayController controller = LowerController();
        if (controller == null)
            return;
        controller.SetRadarRangeNm(StepRange(controller.RadarRangeNm, direction));
    }

    private static float StepRange(float current, int direction)
    {
        int index = 0;
        float bestDelta = Mathf.Abs(current - SupportedRanges[0]);
        for (int i = 1; i < SupportedRanges.Length; i++)
        {
            float delta = Mathf.Abs(current - SupportedRanges[i]);
            if (delta < bestDelta)
            {
                bestDelta = delta;
                index = i;
            }
        }
        index = (index + (direction < 0 ? -1 : 1) + SupportedRanges.Length) % SupportedRanges.Length;
        return SupportedRanges[index];
    }
}
