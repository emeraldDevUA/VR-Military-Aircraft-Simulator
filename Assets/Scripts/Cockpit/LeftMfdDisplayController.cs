using System.Globalization;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Builds and updates the first cockpit MFD. The canvas is sized from the screen mesh
/// bounds, placed in that mesh's local XY plane, and offset slightly along its front normal.
/// </summary>
public sealed class LeftMfdDisplayController : MonoBehaviour
{
    [Header("Data source")]
    [SerializeField] private MonoBehaviour flightDataSourceComponent;

    [Header("Screen placement")]
    [SerializeField, Min(0.0005f)] private float surfaceOffsetMeters = 0.0018f;
    [SerializeField, Min(0.02f)] private float refreshIntervalSeconds = 0.08f;

    private const float CanvasUnits = 1000f;
    private IFlightDataSource flightDataSource;
    private RectTransform attitudeContent;
    private TextMeshProUGUI airspeedValue;
    private TextMeshProUGUI altitudeValue;
    private TextMeshProUGUI headingValue;
    private TextMeshProUGUI pitchValue;
    private TextMeshProUGUI rollValue;
    private TextMeshProUGUI verticalSpeedValue;
    private TextMeshProUGUI machValue;
    private TextMeshProUGUI gLoadValue;
    private TextMeshProUGUI engineValue;
    private float nextRefreshTime;

    private void Awake()
    {
        flightDataSource = flightDataSourceComponent as IFlightDataSource;
        if (flightDataSource == null)
        {
            Debug.LogError("Left MFD requires a component implementing IFlightDataSource.", this);
            enabled = false;
            return;
        }

        BuildCanvas();
        UpdateDisplay(flightDataSource.CurrentSnapshot);
    }

    private void Update()
    {
        if (Time.unscaledTime < nextRefreshTime || flightDataSource == null)
            return;

        nextRefreshTime = Time.unscaledTime + refreshIntervalSeconds;
        UpdateDisplay(flightDataSource.CurrentSnapshot);
    }

    private void BuildCanvas()
    {
        MeshFilter screenMesh = transform.parent != null ? transform.parent.GetComponent<MeshFilter>() : null;
        if (screenMesh == null || screenMesh.sharedMesh == null)
        {
            Debug.LogError("Left MFD display parent must be the existing screen mesh.", this);
            enabled = false;
            return;
        }

        Vector3 bounds = screenMesh.sharedMesh.bounds.size;
        if (bounds.x <= 0f || bounds.y <= 0f)
        {
            Debug.LogError("Left MFD screen mesh has no usable local XY surface bounds.", this);
            enabled = false;
            return;
        }

        GameObject canvasObject = new GameObject("LeftMFD_WorldCanvas", typeof(RectTransform), typeof(Canvas));
        canvasObject.transform.SetParent(transform, false);
        RectTransform canvasRect = canvasObject.GetComponent<RectTransform>();
        canvasRect.sizeDelta = new Vector2(CanvasUnits, CanvasUnits);
        float scaleX = bounds.x / CanvasUnits;
        float scaleY = bounds.y / CanvasUnits;
        canvasRect.localScale = new Vector3(scaleX, scaleY, Mathf.Min(scaleX, scaleY));
        canvasRect.localPosition = new Vector3(0f, 0f, surfaceOffsetMeters);
        // World-space Canvas graphics face local -Z; flip the UI plane so its readable
        // side matches the imported screen mesh's measured +Z/front normal.
        canvasRect.localRotation = Quaternion.Euler(0f, 180f, 0f);

        Canvas canvas = canvasObject.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.WorldSpace;
        canvas.pixelPerfect = true;
        canvas.sortingOrder = 20;

        GameObject panel = CreateImage("Panel", canvasRect, Vector2.zero, new Vector2(958f, 958f),
            new Color32(3, 13, 15, 255));
        AddBorder(panel.GetComponent<RectTransform>(), 958f, new Color32(28, 110, 103, 255), 3f);

        CreateLabel("Header", panel.transform, "F2  /  FLIGHT", new Vector2(0f, 421f),
            new Vector2(700f, 54f), 38f, new Color32(111, 255, 213, 255), FontStyles.Bold);
        CreateLabel("HeaderMode", panel.transform, "NAV  •  SIM", new Vector2(0f, 375f),
            new Vector2(500f, 36f), 25f, new Color32(51, 186, 161, 255), FontStyles.Normal);
        CreateLine("HeaderRule", panel.transform, new Vector2(0f, 348f), new Vector2(860f, 3f),
            new Color32(27, 100, 95, 255));

        CreateLabel("AirspeedCaption", panel.transform, "IAS  /  KT", new Vector2(-310f, 296f),
            new Vector2(250f, 38f), 25f, DimGreen, FontStyles.Bold);
        airspeedValue = CreateLabel("AirspeedValue", panel.transform, "318", new Vector2(-310f, 246f),
            new Vector2(270f, 68f), 54f, BrightGreen, FontStyles.Bold);
        CreateLabel("AltitudeCaption", panel.transform, "ALT  /  FT", new Vector2(310f, 296f),
            new Vector2(250f, 38f), 25f, DimGreen, FontStyles.Bold);
        altitudeValue = CreateLabel("AltitudeValue", panel.transform, "14500", new Vector2(310f, 246f),
            new Vector2(300f, 68f), 48f, BrightGreen, FontStyles.Bold);
        CreateLabel("HeadingCaption", panel.transform, "HDG", new Vector2(0f, 296f),
            new Vector2(130f, 38f), 24f, DimGreen, FontStyles.Bold);
        headingValue = CreateLabel("HeadingValue", panel.transform, "248°", new Vector2(0f, 255f),
            new Vector2(175f, 50f), 35f, new Color32(126, 237, 222, 255), FontStyles.Bold);

        CreateLine("LeftDataSeparator", panel.transform, new Vector2(-310f, 85f), new Vector2(250f, 2f), DimLine);
        CreateLine("RightDataSeparator", panel.transform, new Vector2(310f, 85f), new Vector2(250f, 2f), DimLine);
        CreateLabel("AttitudeCaption", panel.transform, "ATTITUDE  /  DEG", new Vector2(0f, 173f),
            new Vector2(480f, 38f), 25f, DimGreen, FontStyles.Bold);

        GameObject viewportObject = new GameObject("AttitudeViewport", typeof(RectTransform), typeof(RectMask2D));
        viewportObject.transform.SetParent(panel.transform, false);
        RectTransform viewport = viewportObject.GetComponent<RectTransform>();
        SetRect(viewport, new Vector2(0f, 16f), new Vector2(418f, 250f));
        CreateImage("AttitudeFrame", panel.transform, new Vector2(0f, 16f), new Vector2(438f, 270f),
            new Color32(13, 49, 47, 255), siblingBehind: true);

        GameObject moving = new GameObject("MovingHorizon", typeof(RectTransform));
        moving.transform.SetParent(viewportObject.transform, false);
        attitudeContent = moving.GetComponent<RectTransform>();
        SetRect(attitudeContent, Vector2.zero, new Vector2(850f, 650f));
        CreateImage("Sky", moving.transform, new Vector2(0f, 200f), new Vector2(850f, 450f),
            new Color32(8, 31, 42, 255));
        CreateImage("Ground", moving.transform, new Vector2(0f, -225f), new Vector2(850f, 450f),
            new Color32(20, 37, 27, 255));
        CreateLine("HorizonLine", moving.transform, Vector2.zero, new Vector2(850f, 5f), BrightGreen);
        for (int i = 1; i <= 3; i++)
        {
            float y = i * 48f;
            float halfWidth = 42f + i * 13f;
            CreateLine("PitchLadderUp_" + i, moving.transform, new Vector2(0f, y),
                new Vector2(halfWidth * 2f, 3f), DimGreen);
            CreateLine("PitchLadderDown_" + i, moving.transform, new Vector2(0f, -y),
                new Vector2(halfWidth * 2f, 3f), DimGreen);
        }

        CreateLine("FixedWingLeft", panel.transform, new Vector2(-45f, 16f), new Vector2(72f, 6f), BrightGreen);
        CreateLine("FixedWingRight", panel.transform, new Vector2(45f, 16f), new Vector2(72f, 6f), BrightGreen);
        CreateLine("FixedNose", panel.transform, new Vector2(0f, 16f), new Vector2(6f, 30f), BrightGreen);
        CreateLine("RollIndex", panel.transform, new Vector2(0f, 157f), new Vector2(18f, 5f), BrightGreen);

        pitchValue = CreateLabel("PitchValue", panel.transform, "PITCH  +0.0°", new Vector2(-130f, -143f),
            new Vector2(260f, 38f), 23f, BrightGreen, FontStyles.Bold);
        rollValue = CreateLabel("RollValue", panel.transform, "ROLL  +0.0°", new Vector2(130f, -143f),
            new Vector2(260f, 38f), 23f, BrightGreen, FontStyles.Bold);

        CreateLine("FooterRule", panel.transform, new Vector2(0f, -200f), new Vector2(860f, 3f), DimLine);
        CreateLabel("VerticalSpeedCaption", panel.transform, "V/S  FPM", new Vector2(-300f, -245f),
            new Vector2(250f, 34f), 24f, DimGreen, FontStyles.Bold);
        verticalSpeedValue = CreateLabel("VerticalSpeedValue", panel.transform, "+000", new Vector2(-300f, -291f),
            new Vector2(260f, 58f), 42f, BrightGreen, FontStyles.Bold);
        CreateLabel("MachCaption", panel.transform, "MACH", new Vector2(0f, -245f),
            new Vector2(200f, 34f), 24f, DimGreen, FontStyles.Bold);
        machValue = CreateLabel("MachValue", panel.transform, "0.55", new Vector2(0f, -291f),
            new Vector2(200f, 58f), 42f, BrightGreen, FontStyles.Bold);
        CreateLabel("GLoadCaption", panel.transform, "G", new Vector2(300f, -245f),
            new Vector2(150f, 34f), 24f, DimGreen, FontStyles.Bold);
        gLoadValue = CreateLabel("GLoadValue", panel.transform, "1.0", new Vector2(300f, -291f),
            new Vector2(200f, 58f), 42f, BrightGreen, FontStyles.Bold);
        engineValue = CreateLabel("EngineState", panel.transform, "ENG  RUN", new Vector2(0f, -405f),
            new Vector2(400f, 34f), 22f, new Color32(51, 186, 161, 255), FontStyles.Bold);
    }

    private void UpdateDisplay(FlightDataSnapshot data)
    {
        airspeedValue.text = Mathf.RoundToInt(data.IndicatedAirspeedKnots).ToString("000", CultureInfo.InvariantCulture);
        altitudeValue.text = Mathf.RoundToInt(data.AltitudeFeet).ToString("00000", CultureInfo.InvariantCulture);
        headingValue.text = Mathf.RoundToInt(Mathf.Repeat(data.HeadingDegrees, 360f)).ToString("000", CultureInfo.InvariantCulture) + "°";
        pitchValue.text = "PITCH  " + Signed(data.PitchDegrees, "0.0") + "°";
        rollValue.text = "ROLL  " + Signed(data.RollDegrees, "0.0") + "°";
        verticalSpeedValue.text = Signed(Mathf.RoundToInt(data.VerticalSpeedFeetPerMinute), "0000");
        machValue.text = data.MachNumber.ToString("0.00", CultureInfo.InvariantCulture);
        gLoadValue.text = data.GLoad.ToString("0.0", CultureInfo.InvariantCulture);
        engineValue.text = "ENG  " + data.Engine.ToString().ToUpperInvariant() + "   THR " +
                           Mathf.RoundToInt(Mathf.Clamp01(data.Throttle01) * 100f).ToString("00", CultureInfo.InvariantCulture);

        attitudeContent.localRotation = Quaternion.Euler(0f, 0f, -data.RollDegrees);
        attitudeContent.anchoredPosition = new Vector2(0f, -data.PitchDegrees * 3.2f);
    }

    private static string Signed(float value, string format)
    {
        return (value >= 0f ? "+" : "-") + Mathf.Abs(value).ToString(format, CultureInfo.InvariantCulture);
    }

    private static readonly Color32 BrightGreen = new Color32(107, 255, 179, 255);
    private static readonly Color32 DimGreen = new Color32(54, 174, 126, 255);
    private static readonly Color32 DimLine = new Color32(21, 76, 61, 255);

    private static GameObject CreateImage(string name, Transform parent, Vector2 position, Vector2 size,
        Color color, bool siblingBehind = false)
    {
        GameObject go = new GameObject(name, typeof(RectTransform), typeof(Image));
        go.transform.SetParent(parent, false);
        RectTransform rect = go.GetComponent<RectTransform>();
        SetRect(rect, position, size);
        Image image = go.GetComponent<Image>();
        image.color = color;
        image.raycastTarget = false;
        if (siblingBehind)
            go.transform.SetAsFirstSibling();
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

    private static GameObject CreateLine(string name, Transform parent, Vector2 position, Vector2 size, Color color)
    {
        return CreateImage(name, parent, position, size, color);
    }

    private static void AddBorder(RectTransform parent, float size, Color color, float thickness)
    {
        CreateLine("BorderTop", parent, new Vector2(0f, size * 0.5f - thickness * 0.5f), new Vector2(size, thickness), color);
        CreateLine("BorderBottom", parent, new Vector2(0f, -size * 0.5f + thickness * 0.5f), new Vector2(size, thickness), color);
        CreateLine("BorderLeft", parent, new Vector2(-size * 0.5f + thickness * 0.5f, 0f), new Vector2(thickness, size), color);
        CreateLine("BorderRight", parent, new Vector2(size * 0.5f - thickness * 0.5f, 0f), new Vector2(thickness, size), color);
    }

    private static void SetRect(RectTransform rect, Vector2 position, Vector2 size)
    {
        rect.anchorMin = new Vector2(0.5f, 0.5f);
        rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = position;
        rect.sizeDelta = size;
        rect.localScale = Vector3.one;
        rect.localRotation = Quaternion.identity;
    }
}
