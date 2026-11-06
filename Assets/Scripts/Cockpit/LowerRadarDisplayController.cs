using System.Globalization;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>Forward-looking air-to-air radar presentation for the lower cockpit display.</summary>
public sealed class LowerRadarDisplayController : MonoBehaviour
{
    [Header("Shared flight and sensor sources")]
    [SerializeField] private MonoBehaviour flightDataSourceComponent;
    [SerializeField] private MonoBehaviour navigationDataSourceComponent;
    [SerializeField] private MonoBehaviour radarDataSourceComponent;
    [Header("Screen placement")]
    [SerializeField, Min(0.0005f)] private float surfaceOffsetMeters = 0.0018f;
    [SerializeField, Min(0.02f)] private float refreshIntervalSeconds = 0.08f;

    private const float CanvasUnits = 1000f;
    private const float AzimuthCoverageDegrees = 60f;
    private static readonly float[] SupportedRangesNm = { 10f, 20f, 40f, 80f };
    private static readonly Color32 Cyan = new Color32(104, 239, 223, 255);
    private static readonly Color32 Green = new Color32(90, 238, 154, 255);
    private static readonly Color32 DimGreen = new Color32(48, 138, 100, 255);
    private static readonly Color32 GridColor = new Color32(30, 93, 74, 255);
    private static readonly Color32 Amber = new Color32(255, 190, 76, 255);

    private IFlightDataSource flightDataSource;
    private INavigationDataSource navigationDataSource;
    private IRadarContactDataSource radarDataSource;
    private RadarScopeGraphic scopeGraphic;
    private RectTransform[] contactLabels;
    private TextMeshProUGUI headingLabel;
    private TextMeshProUGUI rangeLabel;
    private TextMeshProUGUI trackCountLabel;
    private TextMeshProUGUI selectedTrackLabel;
    private TextMeshProUGUI selectedRangeBearingLabel;
    private TextMeshProUGUI selectedAltitudeLabel;
    private float radarRangeNm = 20f;
    private int selectedContactIndex = -1;
    private readonly int[] visibleContactIndices = new int[16];
    private int visibleContactCount;
    private float nextRefreshTime;

    /// <summary>Current selected radar range in nautical miles.</summary>
    public float RadarRangeNm => radarRangeNm;

    /// <summary>Stable identifier of the selected in-range track, or an empty string.</summary>
    public string SelectedTargetIdentifier
    {
        get
        {
            RadarContact[] contacts = radarDataSource == null ? null : radarDataSource.CurrentRadarSnapshot.Contacts;
            return contacts != null && selectedContactIndex >= 0 && selectedContactIndex < contacts.Length
                ? contacts[selectedContactIndex].Identifier : string.Empty;
        }
    }

    private void Awake()
    {
        flightDataSource = flightDataSourceComponent as IFlightDataSource;
        navigationDataSource = navigationDataSourceComponent as INavigationDataSource;
        radarDataSource = radarDataSourceComponent as IRadarContactDataSource;
        if (flightDataSource == null || navigationDataSource == null || radarDataSource == null)
        {
            Debug.LogError("Lower radar requires shared flight/navigation sources and an IRadarContactDataSource component.", this);
            enabled = false;
            return;
        }
        BuildCanvas();
        UpdateDisplay();
    }

    private void Update()
    {
        if (Time.unscaledTime < nextRefreshTime || radarDataSource == null)
            return;
        nextRefreshTime = Time.unscaledTime + refreshIntervalSeconds;
        UpdateDisplay();
    }

    /// <summary>Select the nearest supported scope range: 10, 20, 40, or 80 NM.</summary>
    public void SetRadarRangeNm(float range)
    {
        float nearest = SupportedRangesNm[0];
        float difference = Mathf.Abs(range - nearest);
        for (int i = 1; i < SupportedRangesNm.Length; i++)
        {
            float candidateDifference = Mathf.Abs(range - SupportedRangesNm[i]);
            if (candidateDifference < difference)
            {
                nearest = SupportedRangesNm[i];
                difference = candidateDifference;
            }
        }
        radarRangeNm = nearest;
        UpdateDisplay();
    }

    /// <summary>Choose the next track within the current range and forward azimuth coverage.</summary>
    public void SelectNextTarget()
    {
        UpdateDisplay();
        CycleVisibleTarget(1);
    }

    /// <summary>Choose the previous track within the current range and forward azimuth coverage.</summary>
    public void SelectPreviousTarget()
    {
        UpdateDisplay();
        CycleVisibleTarget(-1);
    }

    private void CycleVisibleTarget(int direction)
    {
        if (visibleContactCount == 0)
        {
            selectedContactIndex = -1;
            UpdateDisplay();
            return;
        }

        int selectedVisibleIndex = -1;
        for (int i = 0; i < visibleContactCount; i++)
        {
            if (visibleContactIndices[i] == selectedContactIndex)
            {
                selectedVisibleIndex = i;
                break;
            }
        }
        int nextVisibleIndex = selectedVisibleIndex < 0
            ? (direction > 0 ? 0 : visibleContactCount - 1)
            : (selectedVisibleIndex + direction + visibleContactCount) % visibleContactCount;
        selectedContactIndex = visibleContactIndices[nextVisibleIndex];
        UpdateDisplay();
    }

    private void BuildCanvas()
    {
        MeshFilter screenMesh = transform.parent != null ? transform.parent.GetComponent<MeshFilter>() : null;
        if (screenMesh == null || screenMesh.sharedMesh == null)
        {
            Debug.LogError("Lower radar display parent must be the existing lower MFD screen mesh.", this);
            enabled = false;
            return;
        }

        Vector3 bounds = screenMesh.sharedMesh.bounds.size;
        if (bounds.x <= 0f || bounds.y <= 0f)
        {
            Debug.LogError("Lower MFD screen mesh has no usable local XY surface bounds.", this);
            enabled = false;
            return;
        }

        GameObject canvasObject = new GameObject("LowerRadar_WorldCanvas", typeof(RectTransform), typeof(Canvas));
        canvasObject.transform.SetParent(transform, false);
        RectTransform canvasRect = canvasObject.GetComponent<RectTransform>();
        canvasRect.sizeDelta = new Vector2(CanvasUnits, CanvasUnits);
        float scaleX = bounds.x / CanvasUnits;
        float scaleY = bounds.y / CanvasUnits;
        canvasRect.localScale = new Vector3(scaleX, scaleY, Mathf.Min(scaleX, scaleY));
        canvasRect.localPosition = new Vector3(0f, 0f, surfaceOffsetMeters);
        // Match the existing MFD canvases: the imported screen's readable face is its +Z side.
        canvasRect.localRotation = Quaternion.Euler(0f, 180f, 0f);
        Canvas canvas = canvasObject.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.WorldSpace;
        canvas.pixelPerfect = true;
        canvas.sortingOrder = 20;

        GameObject panel = CreateImage("Panel", canvasRect, Vector2.zero, new Vector2(958f, 958f),
            new Color32(2, 12, 14, 255));
        AddBorder(panel.GetComponent<RectTransform>(), 958f, new Color32(29, 121, 107, 255), 3f);
        CreateLabel("Header", panel.transform, "F2  /  AIR RADAR", new Vector2(0f, 426f),
            new Vector2(780f, 48f), 37f, Cyan, FontStyles.Bold);
        CreateLine("HeaderRule", panel.transform, new Vector2(0f, 392f), new Vector2(860f, 3f), GridColor);
        headingLabel = CreateLabel("Heading", panel.transform, "HDG 248°", new Vector2(-255f, 351f),
            new Vector2(280f, 38f), 26f, Cyan, FontStyles.Bold);
        rangeLabel = CreateLabel("Range", panel.transform, "RNG 20 NM", new Vector2(250f, 351f),
            new Vector2(280f, 38f), 26f, Cyan, FontStyles.Bold);
        CreateLabel("Mode", panel.transform, "AA SEARCH  /  AZ +/-60°", new Vector2(0f, 309f),
            new Vector2(520f, 32f), 20f, DimGreen, FontStyles.Bold);

        GameObject scopeObject = new GameObject("RadarScope", typeof(RectTransform), typeof(CanvasRenderer),
            typeof(RectMask2D), typeof(RadarScopeGraphic));
        scopeObject.transform.SetParent(panel.transform, false);
        RectTransform scopeRect = scopeObject.GetComponent<RectTransform>();
        SetRect(scopeRect, new Vector2(0f, -4f), new Vector2(820f, 620f));
        scopeGraphic = scopeObject.GetComponent<RadarScopeGraphic>();
        scopeGraphic.raycastTarget = false;

        CreateLabel("AzimuthMinus60", scopeObject.transform, "-60°", new Vector2(-348f, 239f),
            new Vector2(82f, 27f), 17f, DimGreen, FontStyles.Bold);
        CreateLabel("AzimuthMinus30", scopeObject.transform, "-30°", new Vector2(-180f, 289f),
            new Vector2(82f, 27f), 17f, DimGreen, FontStyles.Bold);
        CreateLabel("AzimuthCenter", scopeObject.transform, "0°", new Vector2(0f, 294f),
            new Vector2(58f, 27f), 17f, Green, FontStyles.Bold);
        CreateLabel("AzimuthPlus30", scopeObject.transform, "+30°", new Vector2(180f, 289f),
            new Vector2(82f, 27f), 17f, DimGreen, FontStyles.Bold);
        CreateLabel("AzimuthPlus60", scopeObject.transform, "+60°", new Vector2(348f, 239f),
            new Vector2(82f, 27f), 17f, DimGreen, FontStyles.Bold);

        contactLabels = new RectTransform[16];
        for (int i = 0; i < contactLabels.Length; i++)
        {
            TextMeshProUGUI label = CreateLabel("ContactLabel_" + i, scopeObject.transform, string.Empty,
                Vector2.zero, new Vector2(94f, 28f), 18f, Green, FontStyles.Bold);
            contactLabels[i] = label.rectTransform;
            label.gameObject.SetActive(false);
        }

        trackCountLabel = CreateLabel("TrackCount", panel.transform, "TRK 00", new Vector2(0f, -319f),
            new Vector2(220f, 30f), 20f, DimGreen, FontStyles.Bold);
        CreateLine("FooterRule", panel.transform, new Vector2(0f, -347f), new Vector2(860f, 3f), GridColor);
        selectedTrackLabel = CreateLabel("SelectedTrack", panel.transform, "TGT  --", new Vector2(-285f, -393f),
            new Vector2(220f, 34f), 23f, Amber, FontStyles.Bold);
        selectedRangeBearingLabel = CreateLabel("SelectedRangeBearing", panel.transform, "RNG --.-  BRG ---°",
            new Vector2(10f, -393f), new Vector2(390f, 34f), 22f, Cyan, FontStyles.Bold);
        selectedAltitudeLabel = CreateLabel("SelectedAltitude", panel.transform, "ALT ----- FT",
            new Vector2(306f, -393f), new Vector2(230f, 34f), 20f, Green, FontStyles.Bold);
    }

    private void UpdateDisplay()
    {
        if (scopeGraphic == null || flightDataSource == null || navigationDataSource == null || radarDataSource == null)
            return;

        FlightDataSnapshot flight = flightDataSource.CurrentSnapshot;
        NavigationDataSnapshot nav = navigationDataSource.CurrentNavigationSnapshot;
        RadarContact[] contacts = radarDataSource.CurrentRadarSnapshot.Contacts;
        visibleContactCount = 0;
        if (contacts != null)
        {
            int count = Mathf.Min(contacts.Length, visibleContactIndices.Length);
            for (int i = 0; i < count; i++)
            {
                if (RadarDisplayMath.TryGetContactGeometry(contacts[i], nav, flight.HeadingDegrees,
                    radarRangeNm, AzimuthCoverageDegrees, out float rangeNm, out float trueBearing, out float relativeAzimuth))
                    visibleContactIndices[visibleContactCount++] = i;
            }
        }

        if (!IsVisibleIndex(selectedContactIndex))
            selectedContactIndex = visibleContactCount > 0 ? visibleContactIndices[0] : -1;

        scopeGraphic.SetData(nav, flight.HeadingDegrees, contacts, radarRangeNm,
            AzimuthCoverageDegrees, selectedContactIndex);
        headingLabel.text = "HDG " + Mathf.RoundToInt(Mathf.Repeat(flight.HeadingDegrees, 360f))
            .ToString("000", CultureInfo.InvariantCulture) + "°";
        rangeLabel.text = "RNG " + radarRangeNm.ToString("0", CultureInfo.InvariantCulture) + " NM";
        trackCountLabel.text = "TRK " + visibleContactCount.ToString("00", CultureInfo.InvariantCulture);

        for (int i = 0; i < contactLabels.Length; i++)
            contactLabels[i].gameObject.SetActive(false);
        if (contacts != null)
        {
            for (int i = 0; i < visibleContactCount; i++)
            {
                int contactIndex = visibleContactIndices[i];
                RadarContact contact = contacts[contactIndex];
                Vector2 point = RadarDisplayMath.ProjectContact(contact, nav, flight.HeadingDegrees, radarRangeNm);
                RectTransform label = contactLabels[i];
                label.anchoredPosition = point + new Vector2(24f, 14f);
                TextMeshProUGUI text = label.GetComponent<TextMeshProUGUI>();
                text.text = contact.Identifier;
                text.color = contactIndex == selectedContactIndex ? Amber : Green;
                label.gameObject.SetActive(true);
            }
        }

        if (selectedContactIndex >= 0 && contacts != null && selectedContactIndex < contacts.Length)
        {
            RadarContact target = contacts[selectedContactIndex];
            RadarDisplayMath.TryGetContactGeometry(target, nav, flight.HeadingDegrees,
                radarRangeNm, AzimuthCoverageDegrees, out float distance, out float trueBearing, out float relativeAzimuth);
            selectedTrackLabel.text = "TGT  " + target.Identifier;
            selectedRangeBearingLabel.text = "RNG " + distance.ToString("00.0", CultureInfo.InvariantCulture) +
                " NM  BRG " + Mathf.RoundToInt(trueBearing).ToString("000", CultureInfo.InvariantCulture) + "°T" +
                "  AZ " + Signed(relativeAzimuth, "00") + "°";
            selectedAltitudeLabel.text = "ALT " + Mathf.RoundToInt(target.AltitudeFeet).ToString("00000", CultureInfo.InvariantCulture) + " FT";
        }
        else
        {
            selectedTrackLabel.text = "TGT  --";
            selectedRangeBearingLabel.text = "NO TRACK IN SEARCH VOLUME";
            selectedAltitudeLabel.text = "ALT ----- FT";
        }
    }

    private bool IsVisibleIndex(int index)
    {
        for (int i = 0; i < visibleContactCount; i++)
            if (visibleContactIndices[i] == index)
                return true;
        return false;
    }

    private static string Signed(float value, string format)
    {
        return (value >= 0f ? "+" : "-") + Mathf.Abs(value).ToString(format, CultureInfo.InvariantCulture);
    }

    private static GameObject CreateImage(string name, Transform parent, Vector2 position, Vector2 size, Color color)
    {
        GameObject go = new GameObject(name, typeof(RectTransform), typeof(Image));
        go.transform.SetParent(parent, false);
        SetRect(go.GetComponent<RectTransform>(), position, size);
        Image image = go.GetComponent<Image>();
        image.color = color;
        image.raycastTarget = false;
        return go;
    }

    private static TextMeshProUGUI CreateLabel(string name, Transform parent, string value, Vector2 position,
        Vector2 size, float fontSize, Color color, FontStyles style)
    {
        GameObject go = new GameObject(name, typeof(RectTransform), typeof(TextMeshProUGUI));
        go.transform.SetParent(parent, false);
        SetRect(go.GetComponent<RectTransform>(), position, size);
        TextMeshProUGUI text = go.GetComponent<TextMeshProUGUI>();
        text.text = value;
        text.font = TMP_Settings.defaultFontAsset;
        text.fontSize = fontSize;
        text.fontStyle = style;
        text.color = color;
        text.alignment = TextAlignmentOptions.Center;
        text.textWrappingMode = TextWrappingModes.NoWrap;
        text.overflowMode = TextOverflowModes.Truncate;
        text.raycastTarget = false;
        return text;
    }

    private static void CreateLine(string name, Transform parent, Vector2 position, Vector2 size, Color color) =>
        CreateImage(name, parent, position, size, color);

    private static void AddBorder(RectTransform parent, float size, Color color, float thickness)
    {
        CreateLine("BorderTop", parent, new Vector2(0f, size * 0.5f - thickness * 0.5f), new Vector2(size, thickness), color);
        CreateLine("BorderBottom", parent, new Vector2(0f, -size * 0.5f + thickness * 0.5f), new Vector2(size, thickness), color);
        CreateLine("BorderLeft", parent, new Vector2(-size * 0.5f + thickness * 0.5f, 0f), new Vector2(thickness, size), color);
        CreateLine("BorderRight", parent, new Vector2(size * 0.5f - thickness * 0.5f, 0f), new Vector2(thickness, size), color);
    }

    private static void SetRect(RectTransform rect, Vector2 position, Vector2 size)
    {
        rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = position;
        rect.sizeDelta = size;
        rect.localScale = Vector3.one;
        rect.localRotation = Quaternion.identity;
    }
}

/// <summary>Geometry helpers for a forward-looking, heading-relative radar scope.</summary>
public static class RadarDisplayMath
{
    public const float ScopeRadiusUnits = 400f;
    public const float OwnshipOriginY = -190f;

    /// <summary>Returns true when a contact lies inside the selected range and azimuth sector.</summary>
    public static bool TryGetContactGeometry(RadarContact contact, NavigationDataSnapshot aircraft,
        float aircraftHeadingDegrees, float maxRangeNm, float azimuthCoverageDegrees,
        out float rangeNm, out float trueBearingDegrees, out float relativeAzimuthDegrees)
    {
        float northDelta = contact.NorthNm - aircraft.PositionNorthNm;
        float eastDelta = contact.EastNm - aircraft.PositionEastNm;
        rangeNm = Mathf.Sqrt(northDelta * northDelta + eastDelta * eastDelta);
        trueBearingDegrees = rangeNm < 0.001f
            ? Mathf.Repeat(aircraftHeadingDegrees, 360f)
            : Mathf.Repeat(Mathf.Atan2(eastDelta, northDelta) * Mathf.Rad2Deg, 360f);
        relativeAzimuthDegrees = Mathf.DeltaAngle(aircraftHeadingDegrees, trueBearingDegrees);
        return rangeNm <= maxRangeNm && Mathf.Abs(relativeAzimuthDegrees) <= azimuthCoverageDegrees;
    }

    /// <summary>Projects a contact into ownship-centered radar coordinates, in canvas units.</summary>
    public static Vector2 ProjectContact(RadarContact contact, NavigationDataSnapshot aircraft,
        float aircraftHeadingDegrees, float maxRangeNm)
    {
        float northDelta = contact.NorthNm - aircraft.PositionNorthNm;
        float eastDelta = contact.EastNm - aircraft.PositionEastNm;
        float rangeNm = Mathf.Sqrt(northDelta * northDelta + eastDelta * eastDelta);
        float bearing = rangeNm < 0.001f ? aircraftHeadingDegrees : Mathf.Atan2(eastDelta, northDelta) * Mathf.Rad2Deg;
        float relativeRadians = Mathf.DeltaAngle(aircraftHeadingDegrees, bearing) * Mathf.Deg2Rad;
        float pixelsPerNm = ScopeRadiusUnits / Mathf.Max(1f, maxRangeNm);
        return new Vector2(Mathf.Sin(relativeRadians), Mathf.Cos(relativeRadians)) *
               (rangeNm * pixelsPerNm) + new Vector2(0f, OwnshipOriginY);
    }
}

/// <summary>One-mesh radar grid and track renderer; contact text remains in lightweight TMP labels.</summary>
public sealed class RadarScopeGraphic : MaskableGraphic
{
    private const float Coverage = 60f;
    private NavigationDataSnapshot aircraft;
    private RadarContact[] contacts;
    private float headingDegrees;
    private float rangeNm = 20f;
    private float coverageDegrees = Coverage;
    private int selectedIndex = -1;
    private bool hasData;

    public void SetData(NavigationDataSnapshot nav, float heading, RadarContact[] tracks,
        float selectedRangeNm, float azimuthCoverageDegrees, int selectedContact)
    {
        aircraft = nav;
        headingDegrees = heading;
        contacts = tracks;
        rangeNm = Mathf.Max(1f, selectedRangeNm);
        coverageDegrees = Mathf.Clamp(azimuthCoverageDegrees, 1f, 89f);
        selectedIndex = selectedContact;
        hasData = true;
        SetVerticesDirty();
    }

    protected override void OnPopulateMesh(VertexHelper vh)
    {
        vh.Clear();
        if (!hasData)
            return;

        Color arcColor = new Color32(47, 143, 103, 255);
        Vector2 origin = new Vector2(0f, RadarDisplayMath.OwnshipOriginY);
        for (int ring = 1; ring <= 4; ring++)
        {
            float radius = RadarDisplayMath.ScopeRadiusUnits * ring / 4f;
            DrawArc(vh, origin, radius, -coverageDegrees, coverageDegrees, 3f, ring == 4
                ? new Color32(72, 185, 132, 255) : arcColor);
        }

        for (int az = -2; az <= 2; az++)
        {
            float angle = az * coverageDegrees / 2f * Mathf.Deg2Rad;
            Vector2 direction = new Vector2(Mathf.Sin(angle), Mathf.Cos(angle));
            DrawLine(vh, origin, origin + direction * RadarDisplayMath.ScopeRadiusUnits, az == 0 ? 3.2f : 2.4f,
                az == 0 ? arcColor : new Color32(30, 95, 71, 255));
        }

        // Azimuth ticks at five-degree intervals along the outer arc.
        for (int az = -60; az <= 60; az += 5)
        {
            float angle = az * Mathf.Deg2Rad;
            Vector2 direction = new Vector2(Mathf.Sin(angle), Mathf.Cos(angle));
            float tickLength = az % 30 == 0 ? 17f : (az % 10 == 0 ? 11f : 6f);
            DrawLine(vh, origin + direction * (RadarDisplayMath.ScopeRadiusUnits - tickLength),
                origin + direction * RadarDisplayMath.ScopeRadiusUnits, az % 10 == 0 ? 3f : 2f, arcColor);
        }

        if (contacts != null)
        {
            for (int i = 0; i < contacts.Length; i++)
            {
                if (!RadarDisplayMath.TryGetContactGeometry(contacts[i], aircraft, headingDegrees,
                    rangeNm, coverageDegrees, out float contactRange, out float bearing, out float azimuth))
                    continue;

                Vector2 point = RadarDisplayMath.ProjectContact(contacts[i], aircraft, headingDegrees, rangeNm);
                if (i == selectedIndex)
                    DrawDiamond(vh, point, 17f, new Color32(255, 190, 76, 255));
                else
                    DrawDiamond(vh, point, 11f, new Color32(105, 244, 162, 255));
                DrawLine(vh, point + new Vector2(-15f, 0f), point + new Vector2(15f, 0f), 2.5f,
                    i == selectedIndex ? new Color32(255, 190, 76, 255) : new Color32(105, 244, 162, 255));
            }
        }

        // Ownship is fixed at the lower edge of the forward search sector.
        DrawTriangle(vh, origin + new Vector2(0f, 19f), origin + new Vector2(-12f, -13f),
            origin + new Vector2(0f, -7f), new Color32(220, 255, 236, 255));
        DrawTriangle(vh, origin + new Vector2(0f, 19f), origin + new Vector2(0f, -7f),
            origin + new Vector2(12f, -13f), new Color32(220, 255, 236, 255));
    }

    private static void DrawArc(VertexHelper vh, Vector2 center, float radius, float minDegrees,
        float maxDegrees, float thickness, Color color)
    {
        const int segments = 56;
        Vector2 previous = PointOnArc(center, radius, minDegrees);
        for (int i = 1; i <= segments; i++)
        {
            float angle = Mathf.Lerp(minDegrees, maxDegrees, (float)i / segments);
            Vector2 next = PointOnArc(center, radius, angle);
            DrawLine(vh, previous, next, thickness, color);
            previous = next;
        }
    }

    private static Vector2 PointOnArc(Vector2 center, float radius, float angleDegrees)
    {
        float angle = angleDegrees * Mathf.Deg2Rad;
        return center + new Vector2(Mathf.Sin(angle), Mathf.Cos(angle)) * radius;
    }

    private static void DrawDiamond(VertexHelper vh, Vector2 center, float radius, Color color)
    {
        DrawLine(vh, center + new Vector2(0f, radius), center + new Vector2(radius, 0f), 3f, color);
        DrawLine(vh, center + new Vector2(radius, 0f), center + new Vector2(0f, -radius), 3f, color);
        DrawLine(vh, center + new Vector2(0f, -radius), center + new Vector2(-radius, 0f), 3f, color);
        DrawLine(vh, center + new Vector2(-radius, 0f), center + new Vector2(0f, radius), 3f, color);
    }

    private static void DrawTriangle(VertexHelper vh, Vector2 a, Vector2 b, Vector2 c, Color color)
    {
        int start = vh.currentVertCount;
        AddVertex(vh, a, color);
        AddVertex(vh, b, color);
        AddVertex(vh, c, color);
        vh.AddTriangle(start, start + 1, start + 2);
    }

    private static void DrawLine(VertexHelper vh, Vector2 a, Vector2 b, float thickness, Color color)
    {
        Vector2 delta = b - a;
        if (delta.sqrMagnitude < 0.001f)
            return;
        Vector2 side = new Vector2(-delta.y, delta.x).normalized * (thickness * 0.5f);
        int start = vh.currentVertCount;
        AddVertex(vh, a - side, color);
        AddVertex(vh, a + side, color);
        AddVertex(vh, b + side, color);
        AddVertex(vh, b - side, color);
        vh.AddTriangle(start, start + 1, start + 2);
        vh.AddTriangle(start, start + 2, start + 3);
    }

    private static void AddVertex(VertexHelper vh, Vector2 position, Color color)
    {
        UIVertex vertex = UIVertex.simpleVert;
        vertex.position = position;
        vertex.color = color;
        vh.AddVert(vertex);
    }
}
