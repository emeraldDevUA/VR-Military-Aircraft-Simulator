using System;

/// <summary>
/// Immutable-by-convention set of flight values consumed by cockpit displays.
/// All values use the units stated on each field so mock and aircraft adapters agree.
/// </summary>
[Serializable]
public struct FlightDataSnapshot
{
    /// <summary>Indicated airspeed in knots (nautical miles per hour).</summary>
    public float IndicatedAirspeedKnots;

    /// <summary>Pressure altitude in feet.</summary>
    public float AltitudeFeet;

    /// <summary>Compass heading in degrees clockwise from north, normalized to [0, 360).</summary>
    public float HeadingDegrees;

    /// <summary>Pitch in degrees; positive means nose up.</summary>
    public float PitchDegrees;

    /// <summary>Roll/bank in degrees; positive means right wing down.</summary>
    public float RollDegrees;

    /// <summary>Vertical speed in feet per minute; positive means climbing.</summary>
    public float VerticalSpeedFeetPerMinute;

    /// <summary>Mach number, dimensionless.</summary>
    public float MachNumber;

    /// <summary>Normal acceleration in G, where 1.0 is standard gravity.</summary>
    public float GLoad;

    /// <summary>Throttle position as a normalized fraction in [0, 1].</summary>
    public float Throttle01;

    /// <summary>Current engine operating state.</summary>
    public EngineState Engine;

    public FlightDataSnapshot(
        float indicatedAirspeedKnots,
        float altitudeFeet,
        float headingDegrees,
        float pitchDegrees,
        float rollDegrees,
        float verticalSpeedFeetPerMinute,
        float machNumber,
        float gLoad,
        float throttle01,
        EngineState engine)
    {
        IndicatedAirspeedKnots = indicatedAirspeedKnots;
        AltitudeFeet = altitudeFeet;
        HeadingDegrees = headingDegrees;
        PitchDegrees = pitchDegrees;
        RollDegrees = rollDegrees;
        VerticalSpeedFeetPerMinute = verticalSpeedFeetPerMinute;
        MachNumber = machNumber;
        GLoad = gLoad;
        Throttle01 = throttle01;
        Engine = engine;
    }
}

/// <summary>Broad operating state shared with displays, independent of any physics implementation.</summary>
public enum EngineState
{
    Off,
    Starting,
    Idle,
    Running,
    Afterburner,
    Fault
}
