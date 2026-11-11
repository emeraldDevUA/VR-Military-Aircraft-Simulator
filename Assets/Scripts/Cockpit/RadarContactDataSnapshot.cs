using System;

/// <summary>A tracked contact's position and ground-plane motion in the local map frame.</summary>
[Serializable]
public struct RadarContact
{
    /// <summary>Stable display identifier, such as T-01.</summary>
    public string Identifier;
    /// <summary>North coordinate from the shared local origin, in nautical miles.</summary>
    public float NorthNm;
    /// <summary>East coordinate from the shared local origin, in nautical miles.</summary>
    public float EastNm;
    /// <summary>Contact altitude in feet.</summary>
    public float AltitudeFeet;
    /// <summary>Ground-track heading, clockwise from north, in degrees.</summary>
    public float HeadingDegrees;
    /// <summary>Ground speed in knots (nautical miles per hour).</summary>
    public float SpeedKnots;

    public RadarContact(string identifier, float northNm, float eastNm, float altitudeFeet,
        float headingDegrees, float speedKnots)
    {
        Identifier = identifier;
        NorthNm = northNm;
        EastNm = eastNm;
        AltitudeFeet = altitudeFeet;
        HeadingDegrees = headingDegrees;
        SpeedKnots = speedKnots;
    }
}

/// <summary>Latest radar contact set; positions use the same local North/East nautical-mile frame as HSD.</summary>
[Serializable]
public struct RadarContactDataSnapshot
{
    /// <summary>Tracked contacts with stable identifiers and absolute local positions.</summary>
    public RadarContact[] Contacts;

    public RadarContactDataSnapshot(RadarContact[] contacts)
    {
        Contacts = contacts;
    }
}

/// <summary>Replaceable read-only source for radar or sensor track data.</summary>
public interface IRadarContactDataSource
{
    RadarContactDataSnapshot CurrentRadarSnapshot { get; }
}
