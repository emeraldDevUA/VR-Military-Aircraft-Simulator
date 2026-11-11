using System.Globalization;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>Central avionics entry panel, aligned to the existing central screen mesh.</summary>
[DisallowMultipleComponent]
public sealed class CentralAvionicsControlPanelController : MonoBehaviour
{
    [Header("Shared navigation")]
    [SerializeField] private MonoBehaviour navigationDataSourceComponent;
    [SerializeField] private NavigationSelectionState navigationSelectionState;
    [Header("Screen placement")]
    [SerializeField, Min(0.0005f)] private float surfaceOffsetMeters = 0.0018f;
    [SerializeField, Min(0.02f)] private float refreshIntervalSeconds = 0.08f;
    [SerializeField, Range(0.25f, 1f)] private float displayBrightness = 1f;

    private const float CanvasWidth = 1200f;
    private const float CanvasHeight = 400f;
    private INavigationDataSource navigationDataSource;
    private CanvasGroup canvasGroup;
    private TextMeshPro modeLabel;
    private TextMeshPro waypointLabel;
    private TextMeshPro navLabel;
    private TextMeshPro inputLabel;
    private TextMeshPro feedbackLabel;
    private Transform textRoot;
    private Material displayTextMaterial;
    private string inputBuffer = string.Empty;
    private string feedback = "READY";
    private float nextRefreshTime;

    public float DisplayBrightness => displayBrightness;
    public string InputBuffer => inputBuffer;

    private void Awake()
    {
        if (navigationDataSourceComponent == null)
            navigationDataSourceComponent = FindFirstObjectByType<MockFlightDataProvider>();
        navigationDataSource = navigationDataSourceComponent as INavigationDataSource;
        if (navigationSelectionState == null)
            navigationSelectionState = FindFirstObjectByType<NavigationSelectionState>();
        if (navigationDataSource == null || navigationSelectionState == null)
        {
            Debug.LogError("Central avionics panel requires the shared navigation source and NavigationSelectionState.", this);
            enabled = false;
            return;
        }

        BuildCanvas();
        UpdateDisplay();
    }

    private void Update()
    {
        if (Time.unscaledTime < nextRefreshTime)
            return;
        nextRefreshTime = Time.unscaledTime + refreshIntervalSeconds;
        UpdateDisplay();
    }

    private void OnDestroy()
    {
        if (displayTextMaterial == null)
            return;
        if (Application.isPlaying)
            Destroy(displayTextMaterial);
        else
            DestroyImmediate(displayTextMaterial);
    }

    /// <summary>Add one keypad digit to the numeric command buffer.</summary>
    public void AppendDigit(int digit)
    {
        if (digit < 0 || digit > 9)
            return;
        if (inputBuffer.Length >= 2)
        {
            feedback = "INPUT FULL - CLEAR";
            UpdateDisplay();
            return;
        }
        inputBuffer += digit.ToString(CultureInfo.InvariantCulture);
        feedback = "ENTER WAYPOINT 1-3";
        UpdateDisplay();
    }

    /// <summary>Clear the pending numeric input without changing navigation state.</summary>
    public void ClearInput()
    {
        inputBuffer = string.Empty;
        feedback = "INPUT CLEARED";
        UpdateDisplay();
    }

    /// <summary>Confirm a waypoint number (1-3); invalid input leaves selection unchanged.</summary>
    public void ConfirmInput()
    {
        ResolveNavigationDataSource();
        NavigationWaypoint[] waypoints = navigationDataSource != null
            ? navigationDataSource.CurrentNavigationSnapshot.Waypoints : null;
        int waypointNumber;
        if (waypoints == null || !int.TryParse(inputBuffer, NumberStyles.Integer,
                CultureInfo.InvariantCulture, out waypointNumber) || waypointNumber < 1 || waypointNumber > waypoints.Length)
        {
            feedback = "REJECTED - ENTER 1-3";
            inputBuffer = string.Empty;
            UpdateDisplay();
            return;
        }

        navigationSelectionState.SetActiveWaypoint(waypointNumber - 1, waypoints.Length);
        feedback = "WP" + waypointNumber.ToString(CultureInfo.InvariantCulture) + " ACCEPTED";
        inputBuffer = string.Empty;
        UpdateDisplay();
    }

    /// <summary>Step shared active waypoint selection; used by the first rotary control.</summary>
    public void StepActiveWaypoint(int direction)
    {
        ResolveNavigationDataSource();
        NavigationWaypoint[] waypoints = navigationDataSource != null
            ? navigationDataSource.CurrentNavigationSnapshot.Waypoints : null;
        if (waypoints == null || waypoints.Length == 0)
            return;
        navigationSelectionState.SetActiveWaypoint(navigationSelectionState.GetValidIndex(waypoints.Length) +
            (direction < 0 ? -1 : 1), waypoints.Length);
        feedback = "ACTIVE WAYPOINT UPDATED";
        UpdateDisplay();
    }

    /// <summary>Adjust the central display background and its private 3D TMP labels.</summary>
    public void AdjustBrightness(int direction)
    {
        displayBrightness = Mathf.Clamp(displayBrightness + (direction < 0 ? -0.1f : 0.1f), 0.25f, 1f);
        if (canvasGroup != null)
            canvasGroup.alpha = displayBrightness;
        ApplyTextBrightness();
        feedback = "BRIGHTNESS " + Mathf.RoundToInt(displayBrightness * 100f).ToString(CultureInfo.InvariantCulture) + "%";
        UpdateDisplay();
    }

    private void BuildCanvas()
    {
        MeshFilter screenMesh = transform.parent != null ? transform.parent.GetComponent<MeshFilter>() : null;
        if (screenMesh == null || screenMesh.sharedMesh == null)
        {
            Debug.LogError("Central avionics display parent must be VR_Central_Screen.001.", this);
            enabled = false;
            return;
        }

        Vector3 bounds = screenMesh.sharedMesh.bounds.size;
        if (bounds.x <= 0f || bounds.y <= 0f)
        {
            Debug.LogError("Central screen mesh has no usable local XY surface bounds.", this);
            enabled = false;
            return;
        }

        GameObject canvasObject = new GameObject("CentralAvionics_WorldCanvas", typeof(RectTransform), typeof(Canvas), typeof(CanvasGroup));
        canvasObject.transform.SetParent(transform, false);
        RectTransform canvasRect = canvasObject.GetComponent<RectTransform>();
        canvasRect.sizeDelta = new Vector2(CanvasWidth, CanvasHeight);
        float scaleX = bounds.x / CanvasWidth;
        float scaleY = bounds.y / CanvasHeight;
        canvasRect.localScale = new Vector3(scaleX, scaleY, Mathf.Min(scaleX, scaleY));
        canvasRect.localPosition = new Vector3(0f, 0f, surfaceOffsetMeters);
        // The central screen mesh's local +Z normal points toward the pilot. Keep the
        // Canvas on that same face so TMP geometry is front-facing to the Game camera.
        canvasRect.localRotation = Quaternion.identity;
        Canvas canvas = canvasObject.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.WorldSpace;
        canvas.pixelPerfect = true;
        canvas.sortingOrder = 24;
        canvasGroup = canvasObject.GetComponent<CanvasGroup>();
        canvasGroup.alpha = displayBrightness;

        // Keep the existing world-space Canvas for its background and rules. Text uses
        // 3D TMP meshes, parented to the screen transform and scaled from the measured
        // mesh bounds so the layout occupies the same physical screen area.
        GameObject textRootObject = new GameObject("CentralAvionics_3DText");
        textRootObject.transform.SetParent(transform, false);
        textRootObject.transform.localPosition = new Vector3(0f, 0f, surfaceOffsetMeters);
        textRootObject.transform.localRotation = Quaternion.identity;
        textRootObject.transform.localScale = new Vector3(scaleX, scaleY, 1f);
        textRoot = textRootObject.transform;

        TMP_FontAsset font = TMP_Settings.defaultFontAsset;
        if (font == null)
        {
            Debug.LogError("Central avionics display requires the project's default TMP font asset.", this);
            enabled = false;
            return;
        }

        Shader overlayShader = Shader.Find("TextMeshPro/Mobile/Distance Field Overlay");
        if (overlayShader == null)
            overlayShader = Shader.Find("TextMeshPro/Distance Field Overlay");

        // This is a controller-owned material instance. The shared TMP font material
        // remains untouched; the overlay shader lets the text sit on the measured screen
        // plane even where the cockpit bezel would otherwise win the depth test.
        displayTextMaterial = new Material(font.material);
        if (overlayShader != null)
            displayTextMaterial.shader = overlayShader;
        if (displayTextMaterial.HasProperty("_CullMode"))
            displayTextMaterial.SetFloat("_CullMode", 0f);

        GameObject panel = CreateImage("Panel", canvasRect, new Vector2(CanvasWidth, CanvasHeight), new Color32(2, 12, 15, 255));
        AddBorder(panel.GetComponent<RectTransform>(), CanvasWidth, CanvasHeight, new Color32(29, 122, 111, 255), 3f);
        modeLabel = CreateLabel("Mode", "F2  /  CENTRAL AVIONICS", new Vector2(0f, 150f),
            new Vector2(1120f, 48f), 38f, Cyan, FontStyles.Bold);
        CreateLine(panel.transform, new Vector2(0f, 116f), new Vector2(1100f, 3f), DimLine);
        waypointLabel = CreateLabel("Waypoint", "ACTIVE WP1", new Vector2(-355f, 65f),
            new Vector2(420f, 48f), 36f, Amber, FontStyles.Bold);
        navLabel = CreateLabel("Navigation", "HDG ---°     BRG ---°     DIST --.- NM", new Vector2(230f, 65f),
            new Vector2(620f, 48f), 28f, Cyan, FontStyles.Bold);
        CreateLine(panel.transform, new Vector2(0f, 15f), new Vector2(1100f, 2f), DimLine);
        CreateLabel("InputCaption", "WP SELECT", new Vector2(-390f, -46f), new Vector2(250f, 40f), 24f, Green, FontStyles.Bold);
        inputLabel = CreateLabel("Input", "_", new Vector2(-150f, -46f), new Vector2(250f, 54f), 42f, Green, FontStyles.Bold);
        feedbackLabel = CreateLabel("Feedback", "READY", new Vector2(335f, -46f), new Vector2(500f, 40f), 23f, Green, FontStyles.Bold);
        CreateLine(panel.transform, new Vector2(0f, -100f), new Vector2(1100f, 2f), DimLine);
        CreateLabel("KeyLegend", "R1  1 2 3 ENT    R2  4 5 6 0    R3  7 8 9 CLR       KNOB 1: WP    KNOB 2: BRIGHT",
            new Vector2(0f, -144f), new Vector2(1100f, 38f), 18f, DimGreen, FontStyles.Normal);
        ApplyTextBrightness();
    }

    private void UpdateDisplay()
    {
        ResolveNavigationDataSource();
        if (navigationDataSource == null || navigationSelectionState == null || modeLabel == null)
            return;
        NavigationDataSnapshot nav = navigationDataSource.CurrentNavigationSnapshot;
        NavigationWaypoint[] waypoints = nav.Waypoints;
        FlightDataSnapshot flight = default;
        IFlightDataSource flightSource = navigationDataSourceComponent as IFlightDataSource;
        if (flightSource != null)
            flight = flightSource.CurrentSnapshot;
        int activeIndex = navigationSelectionState.GetValidIndex(waypoints != null ? waypoints.Length : 0);
        if (waypoints != null && waypoints.Length > 0)
        {
            NavigationWaypoint active = waypoints[activeIndex];
            float north = active.NorthNm - nav.PositionNorthNm;
            float east = active.EastNm - nav.PositionEastNm;
            float distance = Mathf.Sqrt(north * north + east * east);
            float bearing = distance < 0.001f ? flight.HeadingDegrees : Mathf.Repeat(Mathf.Atan2(east, north) * Mathf.Rad2Deg, 360f);
            waypointLabel.text = "ACTIVE " + active.Label;
            navLabel.text = "HDG " + Mathf.RoundToInt(Mathf.Repeat(flight.HeadingDegrees, 360f)).ToString("000", CultureInfo.InvariantCulture) +
                "°     BRG " + Mathf.RoundToInt(bearing).ToString("000", CultureInfo.InvariantCulture) + "°     DIST " +
                distance.ToString("00.0", CultureInfo.InvariantCulture) + " NM";
        }
        else
        {
            waypointLabel.text = "NO ROUTE";
            navLabel.text = "NAV DATA UNAVAILABLE";
        }
        modeLabel.text = inputBuffer.Length == 0 ? "F2  /  CENTRAL AVIONICS  /  NAV" : "F2  /  CENTRAL AVIONICS  /  WP ENTRY";
        inputLabel.text = inputBuffer.Length == 0 ? "_" : inputBuffer + "_";
        feedbackLabel.text = feedback;
    }

    private static GameObject CreateImage(string name, Transform parent, Vector2 size, Color color)
    {
        GameObject obj = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        obj.transform.SetParent(parent, false);
        RectTransform rect = obj.GetComponent<RectTransform>();
        rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.sizeDelta = size;
        obj.GetComponent<Image>().color = color;
        obj.GetComponent<Image>().raycastTarget = false;
        return obj;
    }

    private static void AddBorder(RectTransform parent, float width, float height, Color color, float thickness)
    {
        CreateLine(parent, new Vector2(0f, height * 0.5f - thickness), new Vector2(width, thickness), color);
        CreateLine(parent, new Vector2(0f, -height * 0.5f + thickness), new Vector2(width, thickness), color);
        GameObject left = CreateImage("BorderLeft", parent, new Vector2(thickness, height), color);
        left.GetComponent<RectTransform>().anchoredPosition = new Vector2(-width * 0.5f + thickness * 0.5f, 0f);
        GameObject right = CreateImage("BorderRight", parent, new Vector2(thickness, height), color);
        right.GetComponent<RectTransform>().anchoredPosition = new Vector2(width * 0.5f - thickness * 0.5f, 0f);
    }

    private TextMeshPro CreateLabel(string name, string text, Vector2 position,
        Vector2 size, float fontSize, Color color, FontStyles style)
    {
        GameObject obj = new GameObject(name, typeof(RectTransform), typeof(MeshFilter), typeof(MeshRenderer), typeof(TextMeshPro));
        obj.transform.SetParent(textRoot, false);
        obj.transform.localPosition = new Vector3(position.x, position.y, 0f);
        // TMP 3D meshes use font-size units roughly one tenth of the UI geometry units.
        // This scale factor keeps existing font sizes and layout proportions unchanged.
        obj.transform.localScale = Vector3.one * 10f;
        obj.transform.localRotation = Quaternion.Euler(0f, 180f, 0f);
        RectTransform rect = obj.GetComponent<RectTransform>();
        rect.sizeDelta = size / 10f;
        TextMeshPro label = obj.GetComponent<TextMeshPro>();
        label.font = TMP_Settings.defaultFontAsset;
        label.text = text;
        label.fontSize = fontSize;
        label.fontStyle = style;
        label.color = color;
        label.fontSharedMaterial = displayTextMaterial;
        label.alignment = TextAlignmentOptions.Center;
        label.textWrappingMode = TextWrappingModes.NoWrap;
        label.overflowMode = TextOverflowModes.Truncate;
        label.ForceMeshUpdate();
        MeshRenderer renderer = obj.GetComponent<MeshRenderer>();
        renderer.sortingOrder = 25;
        renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        renderer.receiveShadows = false;
        return label;
    }

    private void ApplyTextBrightness()
    {
        if (textRoot == null)
            return;
        TextMeshPro[] labels = textRoot.GetComponentsInChildren<TextMeshPro>(true);
        for (int i = 0; i < labels.Length; i++)
        {
            if (labels[i] == null)
                continue;
            Color color = labels[i].color;
            color.a = displayBrightness;
            labels[i].color = color;
        }
    }

    private void ResolveNavigationDataSource()
    {
        if (navigationDataSource == null && navigationDataSourceComponent != null)
            navigationDataSource = navigationDataSourceComponent as INavigationDataSource;
        if (navigationDataSource == null)
        {
            MockFlightDataProvider mockProvider = FindFirstObjectByType<MockFlightDataProvider>();
            if (mockProvider != null)
            {
                navigationDataSourceComponent = mockProvider;
                navigationDataSource = mockProvider;
            }
        }
    }

    private static void CreateLine(Transform parent, Vector2 position, Vector2 size, Color color)
    {
        GameObject line = CreateImage("Rule", parent, size, color);
        line.GetComponent<RectTransform>().anchoredPosition = position;
    }

    private static readonly Color Cyan = new Color32(91, 229, 212, 255);
    private static readonly Color Green = new Color32(109, 235, 150, 255);
    private static readonly Color DimGreen = new Color32(51, 130, 91, 255);
    private static readonly Color Amber = new Color32(255, 187, 72, 255);
    private static readonly Color DimLine = new Color32(29, 122, 111, 255);
}
