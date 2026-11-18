using System;
using UnityEngine;

/// <summary>Shared active-waypoint selection used by every navigation display.</summary>
[DisallowMultipleComponent]
public sealed class NavigationSelectionState : MonoBehaviour
{
    [SerializeField, Min(0)] private int activeWaypointIndex;

    /// <summary>Zero-based index into the current navigation source's waypoint array.</summary>
    public int ActiveWaypointIndex => activeWaypointIndex;
    public event Action<int> SelectionChanged;

    /// <summary>Set a waypoint index, wrapping it into the available route.</summary>
    public void SetActiveWaypoint(int index, int waypointCount)
    {
        if (waypointCount <= 0)
            return;
        int normalized = ((index % waypointCount) + waypointCount) % waypointCount;
        if (activeWaypointIndex == normalized)
            return;
        activeWaypointIndex = normalized;
        SelectionChanged?.Invoke(activeWaypointIndex);
    }

    /// <summary>Clamp the current selection when a provider supplies a shorter route.</summary>
    public int GetValidIndex(int waypointCount)
    {
        if (waypointCount <= 0)
            return 0;
        activeWaypointIndex = Mathf.Clamp(activeWaypointIndex, 0, waypointCount - 1);
        return activeWaypointIndex;
    }
}
