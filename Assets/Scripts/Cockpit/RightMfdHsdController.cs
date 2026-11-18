using System.Globalization;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>Heading-up horizontal situation display for the right cockpit MFD.</summary>
public sealed class RightMfdHsdController : MonoBehaviour
{
    [Header("Shared data source")]
    [SerializeField] private MonoBehaviour navigationDataSourceComponent;
    [SerializeField] private MonoBehaviour flightDataSourceComponent;
    [SerializeField] private NavigationSelectionState navigationSelectionState;
    [Header("Screen placement")]
    [SerializeField, Min(0.0005f)] private float surfaceOffsetMeters = 0.0018f;
    [SerializeField, Min(0.02f)] private float refreshIntervalSeconds = 0.08f;

    private const float CanvasUnits = 1000f;
    private static readonly float[] SupportedRanges = { 10f, 20f, 40f, 80f };
    private INavigationDataSource navigationDataSource;
    private IFlightDataSource flightDataSource;
    private HsdMapGraphic mapGraphic;
    private RectTransform[] waypointLabels;
    private TextMeshProUGUI headingLabel;
    private TextMeshProUGUI rangeLabel;
    private TextMeshProUGUI activeWaypointLabel;
    private TextMeshProUGUI distanceLabel;
    private TextMeshProUGUI bearingLabel;
    private RectTransform northReferenceLabel;
    private RectTransform[] rangeMarkLabels;
    private float mapRangeNm = 20f;
    private int activeWaypointIndex;
    private float nextRefreshTime;

    /// <summary>Current map range, one of 10, 20, 40, or 80 nautical miles.</summary>
    public float MapRangeNm => mapRangeNm;

    private void Awake()
    {
        navigationDataSource = navigationDataSourceComponent as INavigationDataSource;
        flightDataSource = flightDataSourceComponent as IFlightDataSource;
        if (navigationSelectionState == null)
            navigationSelectionState = FindFirstObjectByType<NavigationSelectionState>();
        if (navigationDataSource == null || flightDataSource == null)
        {
            Debug.LogError("Right MFD HSD requires the shared component to implement both flight and navigation data interfaces.", this);
            enabled = false;
            return;
        }

        BuildCanvas();
        UpdateDisplay();
    }

    private void Update()
    {
        if (Time.unscaledTime < nextRefreshTime || navigationDataSource == null || flightDataSource == null)
            return;
        nextRefreshTime = Time.unscaledTime + refreshIntervalSeconds;
        UpdateDisplay();
    }

    /// <summary>Set the active route waypoint by zero-based index; values wrap over the route.</summary>
    public void SetActiveWaypoint(int index)
    {
        NavigationWaypoint[] waypoints = navigationDataSource != null
            ? navigationDataSource.CurrentNavigationSnapshot.Waypoints : null;
        if (waypoints == null || waypoints.Length == 0)
            return;
        activeWaypointIndex = ((index % waypoints.Length) + waypoints.Length) % waypoints.Length;
        if (navigationSelectionState != null)
            navigationSelectionState.SetActiveWaypoint(activeWaypointIndex, waypoints.Length);
        UpdateDisplay();
    }

    /// <summary>Select the nearest supported map range: 10, 20, 40, or 80 NM.</summary>
    public void SetMapRangeNm(float rangeNm)
    {
        float nearest = SupportedRanges[0];
        float bestDelta = Mathf.Abs(rangeNm - nearest);
        for (int i = 1; i < SupportedRanges.Length; i++)
        {
            float delta = Mathf.Abs(rangeNm - SupportedRanges[i]);
            if (delta < bestDelta)
            {
                nearest = SupportedRanges[i];
                bestDelta = delta;
            }
        }
        mapRangeNm = nearest;
        UpdateDisplay();
    }

    /// <summary>Advance or reverse the active waypoint, suitable for a future physical button callback.</summary>
    public void AdvanceWaypoint(int direction = 1)
    {
        int current = navigationSelectionState != null
            ? navigationSelectionState.ActiveWaypointIndex : activeWaypointIndex;
        SetActiveWaypoint(current + (direction < 0 ? -1 : 1));
    }

    private void BuildCanvas()
    {
        MeshFilter screenMesh = transform.parent != null ? transform.parent.GetComponent<MeshFilter>() : null;
        if (screenMesh == null || screenMesh.sharedMesh == null)
        {
            Debug.LogError("Right MFD HSD display parent must be the existing screen mesh.", this);
            enabled = false;
            return;
        }

        Vector3 bounds = screenMesh.sharedMesh.bounds.size;
        if (bounds.x <= 0f || bounds.y <= 0f)
        {
            Debug.LogError("Right MFD screen mesh has no usable local XY surface bounds.", this);
            enabled = false;
            return;
        }

        GameObject canvasObject = new GameObject("RightMFD_HSD_WorldCanvas", typeof(RectTransform), typeof(Canvas));
        canvasObject.transform.SetParent(transform, false);
        RectTransform canvasRect = canvasObject.GetComponent<RectTransform>();
        canvasRect.sizeDelta = new Vector2(CanvasUnits, CanvasUnits);
        float scaleX = bounds.x / CanvasUnits;
        float scaleY = bounds.y / CanvasUnits;
        canvasRect.localScale = new Vector3(scaleX, scaleY, Mathf.Min(scaleX, scaleY));
        canvasRect.localPosition = new Vector3(0f, 0f, surfaceOffsetMeters);
        canvasRect.localRotation = Quaternion.Euler(0f, 180f, 0f);
        Canvas canvas = canvasObject.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.WorldSpace;
        canvas.pixelPerfect = true;
        canvas.sortingOrder = 20;

        GameObject panel = CreateImage("Panel", canvasRect, Vector2.zero, new Vector2(958f, 958f),
            new Color32(2, 12, 15, 255));
        AddBorder(panel.GetComponent<RectTransform>(), 958f, new Color32(29, 122, 111, 255), 3f);
        CreateLabel("Header", panel.transform, "F2  /  HORIZONTAL SITUATION", new Vector2(0f, 426f),
            new Vector2(850f, 48f), 34f, Cyan, FontStyles.Bold);
        CreateLine("HeaderRule", panel.transform, new Vector2(0f, 392f), new Vector2(860f, 3f), DimLine);
        headingLabel = CreateLabel("Heading", panel.transform, "HDG 248°", new Vector2(-274f, 350f),
            new Vector2(300f, 40f), 27f, Cyan, FontStyles.Bold);
        rangeLabel = CreateLabel("Range", panel.transform, "RNG 20 NM", new Vector2(274f, 350f),
            new Vector2(300f, 40f), 27f, Cyan, FontStyles.Bold);

        GameObject viewportObject = new GameObject("NavigationMap", typeof(RectTransform), typeof(CanvasRenderer),
            typeof(RectMask2D), typeof(HsdMapGraphic));
        viewportObject.transform.SetParent(panel.transform, false);
        RectTransform viewport = viewportObject.GetComponent<RectTransform>();
        SetRect(viewport, new Vector2(0f, 10f), new Vector2(610f, 610f));
        mapGraphic = viewportObject.GetComponent<HsdMapGraphic>();
        mapGraphic.raycastTarget = false;

        waypointLabels = new RectTransform[3];
        for (int i = 0; i < waypointLabels.Length; i++)
        {
            TextMeshProUGUI label = CreateLabel("WaypointLabel" + (i + 1), viewportObject.transform,
                "WP" + (i + 1), Vector2.zero, new Vector2(116f, 36f), 25f, Green, FontStyles.Bold);
            waypointLabels[i] = label.rectTransform;
        }

        TextMeshProUGUI north = CreateLabel("NorthReference", viewportObject.transform, "N", Vector2.zero,
            new Vector2(34f, 32f), 22f, Green, FontStyles.Bold);
        northReferenceLabel = north.rectTransform;
        rangeMarkLabels = new RectTransform[2];
        rangeMarkLabels[0] = CreateLabel("RangeMarkInner", viewportObject.transform, "— NM", Vector2.zero,
            new Vector2(100f, 27f), 16f, DimGreen, FontStyles.Normal).rectTransform;
        rangeMarkLabels[1] = CreateLabel("RangeMarkOuter", viewportObject.transform, "— NM", Vector2.zero,
            new Vector2(100f, 27f), 16f, DimGreen, FontStyles.Normal).rectTransform;
        CreateLabel("AircraftCaption", panel.transform, "OWN SHIP", new Vector2(0f, -310f),
            new Vector2(180f, 30f), 20f, DimGreen, FontStyles.Bold);
        CreateLine("FooterRule", panel.transform, new Vector2(0f, -350f), new Vector2(860f, 3f), DimLine);
        activeWaypointLabel = CreateLabel("ActiveWaypoint", panel.transform, "ACTIVE  WP1", new Vector2(-280f, -393f),
            new Vector2(300f, 34f), 23f, Amber, FontStyles.Bold);
        distanceLabel = CreateLabel("Distance", panel.transform, "DIST  00.0 NM", new Vector2(0f, -393f),
            new Vector2(300f, 34f), 23f, Cyan, FontStyles.Bold);
        bearingLabel = CreateLabel("Bearing", panel.transform, "BRG 000°", new Vector2(280f, -393f),
            new Vector2(260f, 34f), 23f, Cyan, FontStyles.Bold);
    }

    private void UpdateDisplay()
    {
        if (mapGraphic == null || navigationDataSource == null || flightDataSource == null)
            return;
        NavigationDataSnapshot nav = navigationDataSource.CurrentNavigationSnapshot;
        FlightDataSnapshot flight = flightDataSource.CurrentSnapshot;
        NavigationWaypoint[] waypoints = nav.Waypoints;
        if (waypoints == null || waypoints.Length == 0)
            return;
        activeWaypointIndex = navigationSelectionState != null
            ? navigationSelectionState.GetValidIndex(waypoints.Length)
            : Mathf.Clamp(activeWaypointIndex, 0, waypoints.Length - 1);

        mapGraphic.SetData(nav, flight.HeadingDegrees, mapRangeNm, activeWaypointIndex);
        headingLabel.text = "HDG " + Mathf.RoundToInt(Mathf.Repeat(flight.HeadingDegrees, 360f)).ToString("000", CultureInfo.InvariantCulture) + "°";
        rangeLabel.text = "RNG " + mapRangeNm.ToString("0", CultureInfo.InvariantCulture) + " NM";
        NavigationWaypoint active = waypoints[activeWaypointIndex];
        activeWaypointLabel.text = "ACTIVE  " + active.Label;
        float deltaNorth = active.NorthNm - nav.PositionNorthNm;
        float deltaEast = active.EastNm - nav.PositionEastNm;
        float distance = Mathf.Sqrt(deltaNorth * deltaNorth + deltaEast * deltaEast);
        float bearing = distance < 0.001f ? flight.HeadingDegrees : Mathf.Repeat(Mathf.Atan2(deltaEast, deltaNorth) * Mathf.Rad2Deg, 360f);
        distanceLabel.text = "DIST  " + distance.ToString("00.0", CultureInfo.InvariantCulture) + " NM";
        bearingLabel.text = "BRG " + Mathf.RoundToInt(bearing).ToString("000", CultureInfo.InvariantCulture) + "°";

        float mapRadius = 232f;
        float scale = mapRadius / mapRangeNm;
        Vector2 northPosition = MapPoint(1f, 0f, flight.HeadingDegrees, mapRadius) .normalized * (mapRadius - 22f);
        northReferenceLabel.anchoredPosition = northPosition;
        rangeMarkLabels[0].anchoredPosition = new Vector2(13f, mapRadius / 3f + 2f);
        rangeMarkLabels[1].anchoredPosition = new Vector2(13f, mapRadius * 2f / 3f + 2f);
        rangeMarkLabels[0].GetComponent<TextMeshProUGUI>().text = (mapRangeNm / 3f).ToString("0.0", CultureInfo.InvariantCulture) + " NM";
        rangeMarkLabels[1].GetComponent<TextMeshProUGUI>().text = (mapRangeNm * 2f / 3f).ToString("0.0", CultureInfo.InvariantCulture) + " NM";
        int count = Mathf.Min(waypointLabels.Length, waypoints.Length);
        for (int i = 0; i < count; i++)
        {
            Vector2 p = MapPoint(waypoints[i].NorthNm - nav.PositionNorthNm,
                waypoints[i].EastNm - nav.PositionEastNm, flight.HeadingDegrees, scale);
            waypointLabels[i].anchoredPosition = p + new Vector2(0f, 22f);
            TextMeshProUGUI tmp = waypointLabels[i].GetComponent<TextMeshProUGUI>();
            tmp.text = waypoints[i].Label;
            tmp.color = i == activeWaypointIndex ? Amber : Green;
            waypointLabels[i].gameObject.SetActive(p.sqrMagnitude <= mapRadius * mapRadius * 1.08f);
        }
    }

    internal static Vector2 MapPoint(float northDeltaNm, float eastDeltaNm, float headingDegrees, float pixelsPerNm)
    {
        float heading = headingDegrees * Mathf.Deg2Rad;
        float right = eastDeltaNm * Mathf.Cos(heading) - northDeltaNm * Mathf.Sin(heading);
        float forward = northDeltaNm * Mathf.Cos(heading) + eastDeltaNm * Mathf.Sin(heading);
        return new Vector2(right, forward) * pixelsPerNm;
    }

    private static readonly Color32 Cyan = new Color32(101, 239, 223, 255);
    private static readonly Color32 Green = new Color32(95, 240, 158, 255);
    private static readonly Color32 DimGreen = new Color32(43, 127, 92, 255);
    private static readonly Color32 DimLine = new Color32(20, 77, 69, 255);
    private static readonly Color32 Amber = new Color32(255, 195, 83, 255);

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

/// <summary>Small procedural HSD symbology graphic; uses one UI mesh and no extra camera or texture.</summary>
public sealed class HsdMapGraphic : MaskableGraphic
{
    private const float Radius = 232f;
    private NavigationDataSnapshot nav;
    private float heading;
    private float rangeNm = 20f;
    private int activeWaypoint;
    private bool hasData;

    public void SetData(NavigationDataSnapshot navigation, float headingDegrees, float selectedRangeNm, int activeIndex)
    {
        nav = navigation;
        heading = headingDegrees;
        rangeNm = Mathf.Max(1f, selectedRangeNm);
        activeWaypoint = activeIndex;
        hasData = true;
        SetVerticesDirty();
    }

    protected override void OnPopulateMesh(VertexHelper vh)
    {
        vh.Clear();
        if (!hasData)
            return;
        float scale = Radius / rangeNm;
        Color ringColor = new Color32(46, 130, 105, 255);
        DrawCircle(vh, Vector2.zero, Radius, 4f, new Color32(69, 190, 151, 255));
        DrawCircle(vh, Vector2.zero, Radius * 2f / 3f, 3f, ringColor);
        DrawCircle(vh, Vector2.zero, Radius / 3f, 3f, ringColor);
        DrawLine(vh, new Vector2(-Radius, 0f), new Vector2(Radius, 0f), 2.5f, ringColor);
        DrawLine(vh, new Vector2(0f, -Radius), new Vector2(0f, Radius), 2.5f, ringColor);

        NavigationWaypoint[] waypoints = nav.Waypoints;
        if (waypoints != null && waypoints.Length > 0)
        {
            Vector2 previous = RightMfdHsdController.MapPoint(waypoints[0].NorthNm - nav.PositionNorthNm,
                waypoints[0].EastNm - nav.PositionEastNm, heading, scale);
            for (int i = 1; i < waypoints.Length; i++)
            {
                Vector2 next = RightMfdHsdController.MapPoint(waypoints[i].NorthNm - nav.PositionNorthNm,
                    waypoints[i].EastNm - nav.PositionEastNm, heading, scale);
                DrawLine(vh, previous, next, i - 1 == activeWaypoint ? 5f : 4f,
                    i - 1 == activeWaypoint ? new Color32(255, 195, 83, 255) : new Color32(74, 205, 128, 255));
                previous = next;
            }
            for (int i = 0; i < waypoints.Length; i++)
            {
                Vector2 p = RightMfdHsdController.MapPoint(waypoints[i].NorthNm - nav.PositionNorthNm,
                    waypoints[i].EastNm - nav.PositionEastNm, heading, scale);
                float marker = i == activeWaypoint ? 11f : 8f;
                DrawLine(vh, p + new Vector2(-marker, 0f), p + new Vector2(marker, 0f), 4f,
                    i == activeWaypoint ? new Color32(255, 195, 83, 255) : new Color32(95, 240, 158, 255));
                DrawLine(vh, p + new Vector2(0f, -marker), p + new Vector2(0f, marker), 4f,
                    i == activeWaypoint ? new Color32(255, 195, 83, 255) : new Color32(95, 240, 158, 255));
            }
        }

        // North reference rotates around the fixed ownship to preserve heading-up orientation.
        Vector2 north = RightMfdHsdController.MapPoint(1f, 0f, heading, Radius);
        if (north.sqrMagnitude > 0.01f)
            DrawLine(vh, north.normalized * (Radius - 18f), north.normalized * (Radius - 3f), 4f,
                new Color32(115, 191, 175, 255));

        // Fixed ownship symbol at map center, pointing toward the top of the heading-up display.
        DrawTriangle(vh, new Vector2(0f, 19f), new Vector2(-12f, -11f), new Vector2(0f, -5f), Color.white);
        DrawTriangle(vh, new Vector2(0f, 19f), new Vector2(0f, -5f), new Vector2(12f, -11f), Color.white);
    }

    private static void DrawCircle(VertexHelper vh, Vector2 center, float radius, float thickness, Color color)
    {
        const int segments = 64;
        Vector2 previous = center + new Vector2(0f, radius);
        for (int i = 1; i <= segments; i++)
        {
            float angle = i * Mathf.PI * 2f / segments;
            Vector2 next = center + new Vector2(Mathf.Sin(angle), Mathf.Cos(angle)) * radius;
            DrawLine(vh, previous, next, thickness, color);
            previous = next;
        }
    }

    private static void DrawLine(VertexHelper vh, Vector2 a, Vector2 b, float thickness, Color color)
    {
        Vector2 delta = b - a;
        if (delta.sqrMagnitude < 0.001f)
            return;
        Vector2 side = new Vector2(-delta.y, delta.x).normalized * (thickness * 0.5f);
        AddQuad(vh, a - side, a + side, b + side, b - side, color);
    }

    private static void DrawTriangle(VertexHelper vh, Vector2 a, Vector2 b, Vector2 c, Color color)
    {
        int start = vh.currentVertCount;
        AddVertex(vh, a, color);
        AddVertex(vh, b, color);
        AddVertex(vh, c, color);
        vh.AddTriangle(start, start + 1, start + 2);
    }

    private static void AddQuad(VertexHelper vh, Vector2 a, Vector2 b, Vector2 c, Vector2 d, Color color)
    {
        int start = vh.currentVertCount;
        AddVertex(vh, a, color);
        AddVertex(vh, b, color);
        AddVertex(vh, c, color);
        AddVertex(vh, d, color);
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
