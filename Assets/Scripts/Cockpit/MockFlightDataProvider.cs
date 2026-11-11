using UnityEngine;

/// <summary>
/// Smooth, repeatable flight demonstration values. Replace this component reference
/// with a MonoBehaviour implementing IFlightDataSource when aircraft telemetry is ready.
/// </summary>
public sealed class MockFlightDataProvider : MonoBehaviour, IFlightDataSource, INavigationDataSource
{
    [SerializeField, Min(0.02f)] private float sampleIntervalSeconds = 0.08f;
    [SerializeField] private float startingAltitudeFeet = 14500f;
    [SerializeField] private float startingHeadingDegrees = 248f;

    private float elapsed;
    private float nextSampleTime;
    private float lastSampleElapsed;
    private FlightDataSnapshot currentSnapshot;
    private float positionNorthNm;
    private float positionEastNm;
    private readonly NavigationWaypoint[] demonstrationWaypoints =
    {
        new NavigationWaypoint("WP1", -4f, -8f),
        new NavigationWaypoint("WP2", -11f, -10f),
        new NavigationWaypoint("WP3", -14f, -2f)
    };

    public FlightDataSnapshot CurrentSnapshot => currentSnapshot;
    public NavigationDataSnapshot CurrentNavigationSnapshot =>
        new NavigationDataSnapshot(positionNorthNm, positionEastNm, demonstrationWaypoints);

    private void OnEnable()
    {
        elapsed = 0f;
        nextSampleTime = 0f;
        lastSampleElapsed = 0f;
        positionNorthNm = 0f;
        positionEastNm = 0f;
        Sample();
    }

    private void Update()
    {
        elapsed += Time.deltaTime;
        if (elapsed >= nextSampleTime)
        {
            Sample();
            nextSampleTime = elapsed + sampleIntervalSeconds;
        }
    }

    private void Sample()
    {
        float deltaTime = elapsed - lastSampleElapsed;
        if (deltaTime < 0f || deltaTime > sampleIntervalSeconds * 2f)
            deltaTime = 0f;
        lastSampleElapsed = elapsed;
        float t = elapsed;
        float airspeed = 318f + 26f * Mathf.Sin(t * 0.19f);
        float altitude = startingAltitudeFeet + 210f * Mathf.Sin(t * 0.075f);
        float heading = Mathf.Repeat(startingHeadingDegrees + 18f * Mathf.Sin(t * 0.105f), 360f);
        // Integrate the reported heading and indicated speed into the local map frame.
        // Heading is clockwise from north; easting is positive to the right/east.
        float headingRadians = heading * Mathf.Deg2Rad;
        float traveledNm = airspeed * deltaTime / 3600f;
        positionNorthNm += Mathf.Cos(headingRadians) * traveledNm;
        positionEastNm += Mathf.Sin(headingRadians) * traveledNm;
        float pitch = 5f * Mathf.Sin(t * 0.41f);
        float roll = 24f * Mathf.Sin(t * 0.29f);
        float verticalSpeed = 945f * Mathf.Cos(t * 0.075f);
        float mach = 0.55f + (airspeed - 318f) * 0.0018f;
        float gLoad = 1f + 0.42f * Mathf.Sin(t * 0.37f + 0.4f);
        float throttle = Mathf.Clamp01(0.68f + 0.08f * Mathf.Sin(t * 0.16f));

        currentSnapshot = new FlightDataSnapshot(
            airspeed,
            altitude,
            heading,
            pitch,
            roll,
            verticalSpeed,
            mach,
            gLoad,
            throttle,
            EngineState.Running);
    }
}
