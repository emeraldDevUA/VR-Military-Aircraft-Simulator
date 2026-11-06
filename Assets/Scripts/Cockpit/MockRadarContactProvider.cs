using UnityEngine;

/// <summary>
/// Separate demonstration target source. Targets have absolute local positions and move along
/// their own ground tracks; aircraft-relative plots are calculated by the display from the
/// shared aircraft navigation and heading sources.
/// </summary>
public sealed class MockRadarContactProvider : MonoBehaviour, IRadarContactDataSource
{
    [Header("Shared aircraft sources")]
    [SerializeField] private MonoBehaviour navigationDataSourceComponent;
    [SerializeField] private MonoBehaviour flightDataSourceComponent;

    private readonly RadarContact[] contacts = new RadarContact[4];
    private bool initialized;

    private INavigationDataSource navigationDataSource;
    private IFlightDataSource flightDataSource;

    public RadarContactDataSnapshot CurrentRadarSnapshot
    {
        get { return new RadarContactDataSnapshot(contacts); }
    }

    private void Awake()
    {
        navigationDataSource = navigationDataSourceComponent as INavigationDataSource;
        flightDataSource = flightDataSourceComponent as IFlightDataSource;
    }

    private void Start()
    {
        EnsureInitialized();
    }

    private void Update()
    {
        EnsureInitialized();
        if (!initialized)
            return;

        float deltaTime = Time.deltaTime;
        for (int i = 0; i < contacts.Length; i++)
        {
            RadarContact contact = contacts[i];
            float headingRadians = contact.HeadingDegrees * Mathf.Deg2Rad;
            float distanceNm = contact.SpeedKnots * deltaTime / 3600f;
            contact.NorthNm += Mathf.Cos(headingRadians) * distanceNm;
            contact.EastNm += Mathf.Sin(headingRadians) * distanceNm;
            contacts[i] = contact;
        }
    }

    private void EnsureInitialized()
    {
        if (initialized)
            return;
        if (navigationDataSource == null)
            navigationDataSource = navigationDataSourceComponent as INavigationDataSource;
        if (flightDataSource == null)
            flightDataSource = flightDataSourceComponent as IFlightDataSource;
        if (navigationDataSource == null || flightDataSource == null)
            return;

        NavigationDataSnapshot nav = navigationDataSource.CurrentNavigationSnapshot;
        FlightDataSnapshot aircraft = flightDataSource.CurrentSnapshot;
        float[] rangesNm = { 4.8f, 8.5f, 13.5f, 18f };
        float[] relativeBearings = { -34f, 20f, 50f, -48f };
        float[] altitudesFeet = { 15800f, 14600f, 17400f, 13200f };
        float[] targetHeadings = { 325f, 212f, 90f, 300f };
        float[] targetSpeeds = { 190f, 240f, 140f, 300f };

        for (int i = 0; i < contacts.Length; i++)
        {
            float bearing = (aircraft.HeadingDegrees + relativeBearings[i]) * Mathf.Deg2Rad;
            contacts[i] = new RadarContact(
                "T-0" + (i + 1),
                nav.PositionNorthNm + Mathf.Cos(bearing) * rangesNm[i],
                nav.PositionEastNm + Mathf.Sin(bearing) * rangesNm[i],
                altitudesFeet[i],
                targetHeadings[i],
                targetSpeeds[i]);
        }
        initialized = true;
    }
}
