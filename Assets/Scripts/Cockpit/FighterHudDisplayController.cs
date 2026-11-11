using System.Globalization;
using TMPro;
using UnityEngine;
using UnityEngine.Rendering;

/// <summary>
/// Builds a lightweight, world-space F-2-style HUD on the pilot-facing side of the
/// imported combiner. The source mesh and its transform are never changed.
/// </summary>
[DisallowMultipleComponent]
public sealed class FighterHudDisplayController : MonoBehaviour
{
    [Header("Shared cockpit state")]
    [SerializeField] private MonoBehaviour flightDataSourceComponent;
    [SerializeField] private MonoBehaviour navigationDataSourceComponent;
    [SerializeField] private NavigationSelectionState navigationSelectionState;

    [Header("Existing cockpit geometry")]
    [SerializeField] private Transform combiner;
    [SerializeField] private Camera pilotCamera;

    [Header("Optical overlay")]
    [SerializeField, Range(0f, 0.35f)] private float combinerTintAlpha = 0.12f;
    [SerializeField, Min(0.0005f)] private float overlayOffsetMeters = 0.004f;
    [SerializeField, Min(0.02f)] private float refreshIntervalSeconds = 0.05f;

    private const float PitchScaleMetersPerDegree = 0.00125f;
    private const float TextTransformScale = 0.006f;
    private static readonly Color HudGreen = new Color32(140, 255, 173, 255);
    private static readonly Color HudDim = new Color32(66, 172, 112, 220);
    private static readonly Color HudBright = new Color32(194, 255, 211, 255);

    private IFlightDataSource flightDataSource;
    private INavigationDataSource navigationDataSource;
    private Transform graphicRoot;
    private Material privateLineMaterial;
    private Material privateTextMaterial;
    private Material privateCombinerMaterial;
    private Material originalCombinerMaterial;
    private float nextUpdateTime;
    private float halfWidth;
    private float halfHeight;
    private float topBankY;

    private TextMeshPro speedValue;
    private TextMeshPro altitudeValue;
    private TextMeshPro headingValue;
    private TextMeshPro verticalSpeedValue;
    private TextMeshPro machValue;
    private TextMeshPro gValue;
    private TextMeshPro navigationValue;
    private TextMeshPro[] headingLabels;
    private TextMeshPro[] pitchLabels;
    private HudLine[] headingTicks;
    private HudLine[] pitchLadder;
    private HudLine[] bankTicks;
    private HudLine bankPointer;
    private HudLine headingCaret;
    private HudLine waypointCue;
    private HudLine aircraftLeftWing;
    private HudLine aircraftRightWing;
    private HudLine aircraftNose;
    private HudLine aircraftTail;

    private void Awake()
    {
        ResolveReferences();
        if (!ValidateDependencies())
            return;

        BuildDisplay();
        UpdateDisplay();
    }

    private void Update()
    {
        if (Time.unscaledTime < nextUpdateTime)
            return;
        nextUpdateTime = Time.unscaledTime + refreshIntervalSeconds;
        UpdateDisplay();
    }

    private void OnDestroy()
    {
        if (combiner != null && originalCombinerMaterial != null)
        {
            MeshRenderer renderer = combiner.GetComponent<MeshRenderer>();
            if (renderer != null && privateCombinerMaterial != null && renderer.sharedMaterial == privateCombinerMaterial)
                renderer.sharedMaterial = originalCombinerMaterial;
        }
        DestroyOwnedMaterial(privateCombinerMaterial);
        DestroyOwnedMaterial(privateLineMaterial);
        DestroyOwnedMaterial(privateTextMaterial);
    }

    private void ResolveReferences()
    {
        if (combiner == null)
        {
            GameObject hud = GameObject.Find("F-2-hud");
            if (hud != null)
                combiner = hud.transform;
        }
        if (pilotCamera == null)
        {
            GameObject testCamera = GameObject.Find("CockpitTestCamera");
            pilotCamera = testCamera != null ? testCamera.GetComponent<Camera>() : Camera.main;
        }
        if (flightDataSourceComponent == null)
            flightDataSourceComponent = FindFirstObjectByType<MockFlightDataProvider>();
        if (navigationDataSourceComponent == null)
            navigationDataSourceComponent = flightDataSourceComponent;
        if (navigationSelectionState == null)
            navigationSelectionState = FindFirstObjectByType<NavigationSelectionState>();

        flightDataSource = flightDataSourceComponent as IFlightDataSource;
        navigationDataSource = navigationDataSourceComponent as INavigationDataSource;
    }

    private bool ValidateDependencies()
    {
        if (combiner == null || combiner.GetComponent<MeshFilter>() == null ||
            combiner.GetComponent<MeshRenderer>() == null || pilotCamera == null ||
            flightDataSource == null || navigationDataSource == null || navigationSelectionState == null)
        {
            Debug.LogError("Fighter HUD requires F-2-hud geometry, the pilot camera, shared flight/navigation data, and NavigationSelectionState.", this);
            enabled = false;
            return false;
        }
        MeshFilter meshFilter = combiner.GetComponent<MeshFilter>();
        if (meshFilter.sharedMesh == null || meshFilter.sharedMesh.vertexCount == 0)
        {
            Debug.LogError("F-2-hud has no readable imported mesh.", this);
            enabled = false;
            return false;
        }
        return true;
    }

    private void BuildDisplay()
    {
        MeshRenderer combinerRenderer = combiner.GetComponent<MeshRenderer>();
        ConfigurePrivateTransparentCombiner(combinerRenderer);

        Vector3 pilotDirection;
        Vector3 displayCenter;
        MeasurePilotFacingSurface(out displayCenter, out pilotDirection, out halfWidth, out halfHeight);
        halfWidth *= 0.88f;
        halfHeight *= 0.86f;
        if (halfWidth < 0.025f || halfHeight < 0.025f)
        {
            Debug.LogError("F-2-hud pilot-facing surface bounds are too small to place symbology safely.", this);
            enabled = false;
            return;
        }

        Vector3 up = Vector3.ProjectOnPlane(pilotCamera.transform.up, pilotDirection).normalized;
        Vector3 right = Vector3.ProjectOnPlane(pilotCamera.transform.right, pilotDirection).normalized;
        if (up.sqrMagnitude < 0.5f || right.sqrMagnitude < 0.5f)
        {
            Debug.LogError("F-2-hud surface orientation could not be aligned to the pilot view.", this);
            enabled = false;
            return;
        }

        GameObject root = new GameObject("FighterHUD_RuntimeSymbology");
        graphicRoot = root.transform;
        graphicRoot.SetParent(transform, false);
        graphicRoot.SetPositionAndRotation(displayCenter + pilotDirection * overlayOffsetMeters,
            Quaternion.LookRotation(Vector3.Cross(right, up).normalized, up));

        CreatePrivateMaterials();
        CreateSymbology();
    }

    /// <summary>
    /// Finds the actual pilot-facing mesh surface from its triangle winding, then measures
    /// that surface in the test camera's screen-right/up basis. This avoids assuming FBX axes.
    /// </summary>
    private void MeasurePilotFacingSurface(out Vector3 center, out Vector3 towardPilot,
        out float measuredHalfWidth, out float measuredHalfHeight)
    {
        Mesh mesh = combiner.GetComponent<MeshFilter>().sharedMesh;
        MeshRenderer renderer = combiner.GetComponent<MeshRenderer>();
        towardPilot = (pilotCamera.transform.position - renderer.bounds.center).normalized;
        Vector3 cameraRight = Vector3.ProjectOnPlane(pilotCamera.transform.right, towardPilot).normalized;
        Vector3 cameraUp = Vector3.ProjectOnPlane(pilotCamera.transform.up, towardPilot).normalized;

        Vector3 weightedCenter = Vector3.zero;
        float totalArea = 0f;
        float minX = float.PositiveInfinity;
        float maxX = float.NegativeInfinity;
        float minY = float.PositiveInfinity;
        float maxY = float.NegativeInfinity;
        Vector3[] vertices = mesh.vertices;
        int[] triangles = mesh.triangles;

        for (int i = 0; i < triangles.Length; i += 3)
        {
            Vector3 a = combiner.TransformPoint(vertices[triangles[i]]);
            Vector3 b = combiner.TransformPoint(vertices[triangles[i + 1]]);
            Vector3 c = combiner.TransformPoint(vertices[triangles[i + 2]]);
            Vector3 cross = Vector3.Cross(b - a, c - a);
            float area = cross.magnitude * 0.5f;
            if (area < 0.00001f || Vector3.Dot(cross.normalized, towardPilot) < 0.55f)
                continue;

            weightedCenter += ((a + b + c) / 3f) * area;
            totalArea += area;
            AccumulateProjection(a, renderer.bounds.center, cameraRight, cameraUp,
                ref minX, ref maxX, ref minY, ref maxY);
            AccumulateProjection(b, renderer.bounds.center, cameraRight, cameraUp,
                ref minX, ref maxX, ref minY, ref maxY);
            AccumulateProjection(c, renderer.bounds.center, cameraRight, cameraUp,
                ref minX, ref maxX, ref minY, ref maxY);
        }

        if (totalArea < 0.0001f || float.IsInfinity(minX) || float.IsInfinity(minY))
        {
            center = renderer.bounds.center;
            measuredHalfWidth = renderer.bounds.extents.magnitude * 0.35f;
            measuredHalfHeight = measuredHalfWidth;
            return;
        }

        center = weightedCenter / totalArea;
        measuredHalfWidth = (maxX - minX) * 0.5f;
        measuredHalfHeight = (maxY - minY) * 0.5f;
    }

    private static void AccumulateProjection(Vector3 point, Vector3 origin, Vector3 right, Vector3 up,
        ref float minX, ref float maxX, ref float minY, ref float maxY)
    {
        Vector3 offset = point - origin;
        float x = Vector3.Dot(offset, right);
        float y = Vector3.Dot(offset, up);
        minX = Mathf.Min(minX, x);
        maxX = Mathf.Max(maxX, x);
        minY = Mathf.Min(minY, y);
        maxY = Mathf.Max(maxY, y);
    }

    private void ConfigurePrivateTransparentCombiner(MeshRenderer renderer)
    {
        originalCombinerMaterial = renderer.sharedMaterial;
        if (originalCombinerMaterial == null)
            return;

        // The imported green combiner is opaque. Use a renderer-local runtime clone so
        // the pilot retains a tinted, see-through pane without changing the FBX material.
        privateCombinerMaterial = new Material(originalCombinerMaterial);
        privateCombinerMaterial.name = originalCombinerMaterial.name + " (HUD runtime transparent instance)";
        if (privateCombinerMaterial.HasProperty("_Surface"))
            privateCombinerMaterial.SetFloat("_Surface", 1f);
        if (privateCombinerMaterial.HasProperty("_Blend"))
            privateCombinerMaterial.SetFloat("_Blend", 0f);
        if (privateCombinerMaterial.HasProperty("_ZWrite"))
            privateCombinerMaterial.SetFloat("_ZWrite", 0f);
        if (privateCombinerMaterial.HasProperty("_SrcBlend"))
            privateCombinerMaterial.SetFloat("_SrcBlend", (float)BlendMode.SrcAlpha);
        if (privateCombinerMaterial.HasProperty("_DstBlend"))
            privateCombinerMaterial.SetFloat("_DstBlend", (float)BlendMode.OneMinusSrcAlpha);
        if (privateCombinerMaterial.HasProperty("_AlphaClip"))
            privateCombinerMaterial.SetFloat("_AlphaClip", 0f);
        Color tint = privateCombinerMaterial.HasProperty("_BaseColor")
            ? privateCombinerMaterial.GetColor("_BaseColor") : privateCombinerMaterial.color;
        tint.a = combinerTintAlpha;
        if (privateCombinerMaterial.HasProperty("_BaseColor"))
            privateCombinerMaterial.SetColor("_BaseColor", tint);
        if (privateCombinerMaterial.HasProperty("_Color"))
            privateCombinerMaterial.SetColor("_Color", tint);

        privateCombinerMaterial.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
        privateCombinerMaterial.DisableKeyword("_ALPHATEST_ON");
        privateCombinerMaterial.DisableKeyword("_ALPHAPREMULTIPLY_ON");
        privateCombinerMaterial.renderQueue = (int)RenderQueue.Transparent;
        renderer.sharedMaterial = privateCombinerMaterial;
    }

    private void CreatePrivateMaterials()
    {
        Shader unlit = Shader.Find("Universal Render Pipeline/Unlit");
        if (unlit == null)
            unlit = Shader.Find("Unlit/Color");
        if (unlit == null)
        {
            Debug.LogError("No suitable unlit shader is available for HUD line geometry.", this);
            enabled = false;
            return;
        }

        privateLineMaterial = new Material(unlit);
        ConfigureTransparentUnlit(privateLineMaterial, HudGreen);
        TMP_FontAsset font = TMP_Settings.defaultFontAsset;
        if (font == null)
        {
            Debug.LogError("Fighter HUD requires the project's default TextMesh Pro font asset.", this);
            enabled = false;
            return;
        }

        Shader overlay = Shader.Find("TextMeshPro/Mobile/Distance Field Overlay");
        if (overlay == null)
            overlay = Shader.Find("TextMeshPro/Distance Field Overlay");
        privateTextMaterial = new Material(font.material);
        privateTextMaterial.name = font.material.name + " (Fighter HUD private overlay)";
        if (overlay != null)
            privateTextMaterial.shader = overlay;
        if (privateTextMaterial.HasProperty("_CullMode"))
            privateTextMaterial.SetFloat("_CullMode", 0f);
    }

    private static void ConfigureTransparentUnlit(Material material, Color color)
    {
        material.color = color;
        if (material.HasProperty("_BaseColor"))
            material.SetColor("_BaseColor", color);
        if (material.HasProperty("_Surface"))
            material.SetFloat("_Surface", 1f);
        if (material.HasProperty("_ZWrite"))
            material.SetFloat("_ZWrite", 0f);
        material.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
        material.renderQueue = (int)RenderQueue.Transparent;
    }

    private void CreateSymbology()
    {
        halfWidth = Mathf.Max(0.01f, halfWidth);
        halfHeight = Mathf.Max(0.01f, halfHeight);
        // Reserve a separate top band for the bank arc. The previous .69 position
        // made its lower half cross the heading ticks and heading labels.
        topBankY = halfHeight * 0.93f;

        CreateText("HUD_Speed", "---", new Vector2(-halfWidth * 0.69f, 0.015f), 0.047f, 0.012f, 22f, HudBright);
        CreateText("HUD_SpeedUnits", "KTS", new Vector2(-halfWidth * 0.69f, 0.002f), 0.04f, 0.009f, 15f, HudDim);
        CreateText("HUD_Altitude", "-----", new Vector2(halfWidth * 0.69f, 0.015f), 0.05f, 0.012f, 21f, HudBright);
        CreateText("HUD_AltitudeUnits", "FT", new Vector2(halfWidth * 0.69f, 0.002f), 0.04f, 0.009f, 15f, HudDim);
        CreateText("HUD_Heading", "---°", new Vector2(0f, halfHeight * 0.77f), 0.062f, 0.014f, 22f, HudBright);
        CreateText("HUD_Navigation", "WP1   BRG ---°   ---.- NM", new Vector2(0f, -halfHeight * 0.48f),
            halfWidth * 1.45f, 0.011f, 14f, HudGreen);
        // Keep the secondary data row above the lower edge of the measured combiner.
        CreateText("HUD_VerticalSpeed", "+---- FPM", new Vector2(-halfWidth * 0.57f, -halfHeight * 0.62f),
            halfWidth * 0.7f, 0.009f, 13f, HudGreen);
        CreateText("HUD_Mach", "M 0.00", new Vector2(0f, -halfHeight * 0.62f), 0.045f, 0.009f, 13f, HudGreen);
        CreateText("HUD_GLoad", "1.0 G", new Vector2(halfWidth * 0.57f, -halfHeight * 0.62f),
            halfWidth * 0.6f, 0.009f, 13f, HudGreen);

        headingLabels = new TextMeshPro[5];
        headingTicks = new HudLine[13];
        for (int i = 0; i < headingTicks.Length; i++)
            headingTicks[i] = CreateLine("HeadingTick_" + i, 0.00065f, HudDim);
        for (int i = 0; i < headingLabels.Length; i++)
            headingLabels[i] = CreateText("HeadingMark_" + i, "000", Vector2.zero, 0.025f, 0.009f, 13f, HudGreen);
        headingCaret = CreateLine("HeadingIndex", 0.0011f, HudBright);

        pitchLadder = new HudLine[16];
        pitchLabels = new TextMeshPro[8];
        for (int i = 0; i < pitchLadder.Length; i++)
            pitchLadder[i] = CreateLine("PitchLadder_" + i, 0.00075f, HudGreen);
        for (int i = 0; i < pitchLabels.Length; i++)
            pitchLabels[i] = CreateText("PitchMark_" + i, "05", Vector2.zero, 0.018f, 0.008f, 12f, HudDim);

        bankTicks = new HudLine[13];
        for (int i = 0; i < bankTicks.Length; i++)
            bankTicks[i] = CreateLine("BankTick_" + i, i % 3 == 0 ? 0.00085f : 0.00055f, HudDim);
        bankPointer = CreateLine("BankPointer", 0.0013f, HudBright);
        aircraftLeftWing = CreateLine("AircraftReference_Left", 0.0013f, HudBright);
        aircraftRightWing = CreateLine("AircraftReference_Right", 0.0013f, HudBright);
        aircraftNose = CreateLine("AircraftReference_Nose", 0.0013f, HudBright);
        aircraftTail = CreateLine("AircraftReference_Tail", 0.0009f, HudBright);
        waypointCue = CreateLine("WaypointSteeringCue", 0.001f, HudBright);
        DrawBankScale(0f);
        DrawFixedAircraftSymbol();
    }

    private void UpdateDisplay()
    {
        if (flightDataSource == null || navigationDataSource == null || graphicRoot == null)
            return;

        FlightDataSnapshot flight = flightDataSource.CurrentSnapshot;
        NavigationDataSnapshot nav = navigationDataSource.CurrentNavigationSnapshot;
        speedValue.text = Mathf.RoundToInt(flight.IndicatedAirspeedKnots).ToString("000", CultureInfo.InvariantCulture);
        altitudeValue.text = Mathf.RoundToInt(flight.AltitudeFeet).ToString("00000", CultureInfo.InvariantCulture);
        headingValue.text = Mathf.RoundToInt(Mathf.Repeat(flight.HeadingDegrees, 360f)).ToString("000", CultureInfo.InvariantCulture) + "°";
        verticalSpeedValue.text = Signed(Mathf.RoundToInt(flight.VerticalSpeedFeetPerMinute), "0000") + " FPM";
        machValue.text = "M " + flight.MachNumber.ToString("0.00", CultureInfo.InvariantCulture);
        gValue.text = flight.GLoad.ToString("0.0", CultureInfo.InvariantCulture) + " G";

        UpdateHeadingTape(flight.HeadingDegrees);
        UpdatePitchLadder(flight.PitchDegrees, flight.RollDegrees);
        DrawBankScale(flight.RollDegrees);
        UpdateNavigationCue(flight, nav);
    }

    private void UpdateHeadingTape(float headingDegrees)
    {
        float headingScale = halfWidth * 0.61f / 60f;
        int baseHeading = Mathf.RoundToInt(Mathf.Repeat(headingDegrees, 360f) / 10f) * 10;
        for (int i = 0; i < headingTicks.Length; i++)
        {
            int step = i - headingTicks.Length / 2;
            float value = Mathf.Repeat(baseHeading + step * 10f, 360f);
            float delta = Mathf.DeltaAngle(headingDegrees, value);
            float x = delta * headingScale;
            float y = halfHeight * 0.61f;
            float length = Mathf.Abs(step) % 3 == 0 ? 0.006f : 0.003f;
            headingTicks[i].Set(new Vector2(x, y), new Vector2(x, y - length));
        }
        for (int i = 0; i < headingLabels.Length; i++)
        {
            int step = (i - headingLabels.Length / 2) * 3;
            float value = Mathf.Repeat(baseHeading + step * 10f, 360f);
            float delta = Mathf.DeltaAngle(headingDegrees, value);
            headingLabels[i].text = Mathf.RoundToInt(value / 10f).ToString("00", CultureInfo.InvariantCulture);
            headingLabels[i].transform.localPosition = new Vector3(delta * headingScale,
                halfHeight * 0.61f - 0.011f, -0.0002f);
        }
        headingCaret.Set(new Vector2(0f, halfHeight * 0.61f + 0.002f),
            new Vector2(0f, halfHeight * 0.61f - 0.002f));
    }

    private void UpdatePitchLadder(float pitchDegrees, float rollDegrees)
    {
        float angle = -rollDegrees * Mathf.Deg2Rad;
        float cos = Mathf.Cos(angle);
        float sin = Mathf.Sin(angle);
        int line = 0;
        int label = 0;
        for (int pitchMark = -20; pitchMark <= 20; pitchMark += 5)
        {
            if (pitchMark == 0)
                continue;
            float y = (pitchMark - pitchDegrees) * PitchScaleMetersPerDegree;
            float halfSegment = pitchMark % 10 == 0 ? 0.016f : 0.011f;
            float gap = 0.004f;
            Vector2 leftA = RotatePoint(-halfSegment, y, cos, sin);
            Vector2 leftB = RotatePoint(-gap, y, cos, sin);
            Vector2 rightA = RotatePoint(gap, y, cos, sin);
            Vector2 rightB = RotatePoint(halfSegment, y, cos, sin);
            pitchLadder[line++].Set(leftA, leftB);
            pitchLadder[line++].Set(rightA, rightB);

            if (pitchMark % 10 == 0 && label + 1 < pitchLabels.Length)
            {
                string text = Mathf.Abs(pitchMark).ToString("00", CultureInfo.InvariantCulture);
                pitchLabels[label].text = text;
                pitchLabels[label].transform.localPosition = new Vector3(RotatePoint(-halfSegment - 0.006f, y, cos, sin).x,
                    RotatePoint(-halfSegment - 0.006f, y, cos, sin).y, -0.0002f);
                pitchLabels[label + 1].text = text;
                pitchLabels[label + 1].transform.localPosition = new Vector3(RotatePoint(halfSegment + 0.006f, y, cos, sin).x,
                    RotatePoint(halfSegment + 0.006f, y, cos, sin).y, -0.0002f);
                label += 2;
            }
        }
        for (; line < pitchLadder.Length; line++)
            pitchLadder[line].Set(Vector2.zero, Vector2.zero);
        for (; label < pitchLabels.Length; label++)
            pitchLabels[label].text = string.Empty;
    }

    private void DrawBankScale(float rollDegrees)
    {
        const int intervals = 12;
        float radiusX = halfWidth * 0.34f;
        // Fit the complete bank arc into the reserved top strip of the combiner.
        // Line widths and the horizontal scale are unchanged.
        float radiusY = halfHeight * 0.12f;
        float centerY = topBankY - radiusY * 0.55f;
        for (int i = 0; i <= intervals; i++)
        {
            float degrees = -60f + i * 10f;
            float radians = degrees * Mathf.Deg2Rad;
            Vector2 p = new Vector2(Mathf.Sin(radians) * radiusX, centerY + Mathf.Cos(radians) * radiusY);
            Vector2 radial = new Vector2(Mathf.Sin(radians), Mathf.Cos(radians));
            float len = degrees % 30f == 0f ? 0.006f : 0.0035f;
            bankTicks[i].Set(p, p - radial * len);
        }

        float bankAngle = Mathf.Clamp(rollDegrees, -60f, 60f) * Mathf.Deg2Rad;
        Vector2 pointer = new Vector2(Mathf.Sin(bankAngle) * radiusX, centerY + Mathf.Cos(bankAngle) * radiusY);
        bankPointer.Set(pointer + new Vector2(-0.003f, 0.004f), pointer,
            pointer + new Vector2(0.003f, 0.004f));
    }

    private void UpdateNavigationCue(FlightDataSnapshot flight, NavigationDataSnapshot nav)
    {
        NavigationWaypoint[] waypoints = nav.Waypoints;
        if (waypoints == null || waypoints.Length == 0)
        {
            navigationValue.text = "NAV DATA --";
            waypointCue.Set(Vector2.zero, Vector2.zero);
            return;
        }

        int index = navigationSelectionState.GetValidIndex(waypoints.Length);
        NavigationWaypoint target = waypoints[index];
        float north = target.NorthNm - nav.PositionNorthNm;
        float east = target.EastNm - nav.PositionEastNm;
        float distanceNm = Mathf.Sqrt(north * north + east * east);
        float bearing = distanceNm < 0.001f ? Mathf.Repeat(flight.HeadingDegrees, 360f) :
            Mathf.Repeat(Mathf.Atan2(east, north) * Mathf.Rad2Deg, 360f);
        float relative = Mathf.DeltaAngle(flight.HeadingDegrees, bearing);
        navigationValue.text = target.Label + "   BRG " + Mathf.RoundToInt(bearing).ToString("000", CultureInfo.InvariantCulture) +
            "°   " + distanceNm.ToString("00.0", CultureInfo.InvariantCulture) + " NM";

        // Keep the steering cue in the clear lateral gutters beside the pitch ladder.
        // The previous full-width sweep crossed the pitch labels and could touch the
        // airspeed/altitude text at large steering angles.
        float cueX;
        float absoluteRelative = Mathf.Abs(relative);
        if (absoluteRelative < 3f)
        {
            cueX = 0f;
        }
        else
        {
            float cueMagnitude = Mathf.Lerp(halfWidth * 0.34f, halfWidth * 0.46f,
                Mathf.InverseLerp(3f, 60f, absoluteRelative));
            cueX = Mathf.Sign(relative) * cueMagnitude;
        }
        float cueY = halfHeight * 0.42f;
        waypointCue.Set(new Vector2(cueX - 0.003f, cueY - 0.003f), new Vector2(cueX, cueY + 0.003f),
            new Vector2(cueX + 0.003f, cueY - 0.003f));
    }

    private void DrawFixedAircraftSymbol()
    {
        aircraftLeftWing.Set(new Vector2(-0.018f, 0f), new Vector2(-0.005f, 0f));
        aircraftRightWing.Set(new Vector2(0.005f, 0f), new Vector2(0.018f, 0f));
        aircraftNose.Set(new Vector2(-0.005f, 0f), new Vector2(0f, 0.0045f), new Vector2(0.005f, 0f));
        aircraftTail.Set(new Vector2(-0.007f, -0.004f), new Vector2(0.007f, -0.004f));
    }

    private TextMeshPro CreateText(string name, string value, Vector2 position, float widthMeters,
        float heightMeters, float fontSize, Color color)
    {
        GameObject obj = new GameObject(name, typeof(RectTransform), typeof(MeshFilter), typeof(MeshRenderer), typeof(TextMeshPro));
        obj.transform.SetParent(graphicRoot, false);
        obj.transform.localPosition = new Vector3(position.x, position.y, -0.0002f);
        obj.transform.localScale = Vector3.one * TextTransformScale;
        RectTransform rect = obj.GetComponent<RectTransform>();
        rect.sizeDelta = new Vector2(widthMeters / TextTransformScale, heightMeters / TextTransformScale);
        TextMeshPro text = obj.GetComponent<TextMeshPro>();
        text.font = TMP_Settings.defaultFontAsset;
        text.text = value;
        text.fontSize = fontSize;
        text.color = color;
        text.alignment = TextAlignmentOptions.Center;
        text.textWrappingMode = TextWrappingModes.NoWrap;
        text.overflowMode = TextOverflowModes.Truncate;
        text.fontSharedMaterial = privateTextMaterial;
        text.ForceMeshUpdate();
        // TMP measures glyphs in font units. Expand the text container to the measured
        // glyph height so a short display row cannot truncate every character.
        rect.sizeDelta = new Vector2(
            Mathf.Max(widthMeters / TextTransformScale, text.preferredWidth * 1.05f),
            Mathf.Max(heightMeters / TextTransformScale, text.preferredHeight * 1.05f));
        MeshRenderer renderer = obj.GetComponent<MeshRenderer>();
        renderer.shadowCastingMode = ShadowCastingMode.Off;
        renderer.receiveShadows = false;
        renderer.sortingOrder = 80;
        if (name == "HUD_Speed") speedValue = text;
        else if (name == "HUD_Altitude") altitudeValue = text;
        else if (name == "HUD_Heading") headingValue = text;
        else if (name == "HUD_VerticalSpeed") verticalSpeedValue = text;
        else if (name == "HUD_Mach") machValue = text;
        else if (name == "HUD_GLoad") gValue = text;
        else if (name == "HUD_Navigation") navigationValue = text;
        return text;
    }

    private HudLine CreateLine(string name, float width, Color color)
    {
        GameObject obj = new GameObject(name, typeof(LineRenderer));
        obj.transform.SetParent(graphicRoot, false);
        LineRenderer line = obj.GetComponent<LineRenderer>();
        line.useWorldSpace = false;
        line.loop = false;
        line.positionCount = 2;
        line.startWidth = width;
        line.endWidth = width;
        line.numCapVertices = 0;
        line.numCornerVertices = 0;
        line.sharedMaterial = privateLineMaterial;
        line.startColor = color;
        line.endColor = color;
        line.shadowCastingMode = ShadowCastingMode.Off;
        line.receiveShadows = false;
        line.sortingOrder = 80;
        return new HudLine(line);
    }

    private static Vector2 RotatePoint(float x, float y, float cos, float sin) =>
        new Vector2(cos * x - sin * y, sin * x + cos * y);

    private static string Signed(int value, string format) =>
        (value >= 0 ? "+" : "-") + Mathf.Abs(value).ToString(format, CultureInfo.InvariantCulture);

    private void DestroyOwnedMaterial(Material material)
    {
        if (material == null)
            return;
        if (Application.isPlaying)
            Destroy(material);
        else
            DestroyImmediate(material);
    }

    private struct HudLine
    {
        private readonly LineRenderer renderer;
        public HudLine(LineRenderer lineRenderer) { renderer = lineRenderer; }
        public void Set(Vector2 a, Vector2 b)
        {
            if (renderer == null) return;
            renderer.positionCount = 2;
            renderer.SetPosition(0, new Vector3(a.x, a.y, 0f));
            renderer.SetPosition(1, new Vector3(b.x, b.y, 0f));
        }
        public void Set(Vector2 a, Vector2 b, Vector2 c)
        {
            if (renderer == null) return;
            renderer.positionCount = 3;
            renderer.SetPosition(0, new Vector3(a.x, a.y, 0f));
            renderer.SetPosition(1, new Vector3(b.x, b.y, 0f));
            renderer.SetPosition(2, new Vector3(c.x, c.y, 0f));
        }
    }
}
