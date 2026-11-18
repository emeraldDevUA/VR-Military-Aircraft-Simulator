using System;

/// <summary>A navigation waypoint in the provider's local flat map frame.</summary>
[Serializable]
public struct NavigationWaypoint
{
    public string Label;
    /// <summary>Northing in nautical miles from the local navigation origin.</summary>
    public float NorthNm;
    /// <summary>Easting in nautical miles from the local navigation origin.</summary>
    public float EastNm;

    public NavigationWaypoint(string label, float northNm, float eastNm)
    {
        Label = label;
        NorthNm = northNm;
        EastNm = eastNm;
    }
}

/// <summary>Aircraft position in a local tangent-plane navigation frame.</summary>
[Serializable]
public struct NavigationDataSnapshot
{
    /// <summary>North position relative to the local origin, in nautical miles.</summary>
    public float PositionNorthNm;
    /// <summary>East position relative to the local origin, in nautical miles.</summary>
    public float PositionEastNm;
    /// <summary>Route waypoints in the same local North/East nautical-mile frame.</summary>
    public NavigationWaypoint[] Waypoints;

    public NavigationDataSnapshot(float positionNorthNm, float positionEastNm, NavigationWaypoint[] waypoints)
    {
        PositionNorthNm = positionNorthNm;
        PositionEastNm = positionEastNm;
        Waypoints = waypoints;
    }
}

/// <summary>Read-only navigation state compatible with a future aircraft navigation adapter.</summary>
public interface INavigationDataSource
{
    NavigationDataSnapshot CurrentNavigationSnapshot { get; }
}
