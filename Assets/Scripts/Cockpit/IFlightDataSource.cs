/// <summary>
/// Read-only source for the latest aircraft state. Implement this interface on a mock
/// provider or an adapter around the aircraft physics model.
/// </summary>
public interface IFlightDataSource
{
    /// <summary>Latest flight state, expressed in the documented units of FlightDataSnapshot.</summary>
    FlightDataSnapshot CurrentSnapshot { get; }
}
