using CesiumForUnity;
using System.Linq;
using System.Threading.Tasks;
using UnityEditor;
using UnityEditor.SceneManagement;
using Unity.Mathematics;
using UnityEngine;

public static class FlightSceneBuilder
{
    private const string ScenePath = "Assets/Scenes/Flight_Scene.unity";
    private const string SourceScenePath = "Assets/Scenes/GetStarted_Scene.unity";
    private const string JetPrefabPath = "Assets/Prefabs/F2_Player.prefab";
    private const string SkyboxPath = "Assets/Skyboxes/SkyDay.mat";
    private const string HudGlassPath = "Assets/Materials/F-2-HudGlass.mat";

    // Misawa Air Base, 150 m past the runway 10 threshold. Heading is true, derived from both thresholds.
    private const double RunwayLongitude = 141.35213;
    private const double RunwayLatitude = 40.70438;
    private const float RunwayHeading = 95.2f;
    // Ellipsoid height, not MSL. Only used if terrain sampling fails.
    private const double FallbackGroundHeight = 70;
    private const float GroundClearance = 0.05f;

    private const float F2Length = 15.52f;
    private const float EyeBelowCanopyTop = 0.25f;
    private const float EyeForwardOfCanopyCenter = 0.2f;

    private const long CesiumWorldTerrainAssetId = 1;
    // Bing Aerial (asset 2) hangs without reporting an error and stalls terrain refinement.
    private const long GoogleSatelliteAssetId = 3830182;

    [MenuItem("Tools/Flight Sim/Build Flight Scene")]
    public static async void Build()
    {
        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
            return;

        GameObject jetPrefab = GetOrCreateJetPrefab();
        if (jetPrefab == null)
            return;

        var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

        CreateSun();
        RenderSettings.skybox = AssetDatabase.LoadAssetAtPath<Material>(SkyboxPath);

        CesiumGeoreference georeference = CreateGeoreference();
        Cesium3DTileset terrain = CreateTerrain(georeference.transform);

        double groundHeight = await SampleGroundHeight(terrain);
        georeference.SetOriginLongitudeLatitudeHeight(RunwayLongitude, RunwayLatitude, groundHeight);
        CreateJet(jetPrefab, georeference.transform, groundHeight);

        EditorSceneManager.SaveScene(scene, ScenePath);
        AddToBuildSettings(ScenePath);
        // The headset streaming app holds focus, and tiles stop loading if the game pauses.
        PlayerSettings.runInBackground = true;
        Debug.Log($"Flight scene saved to {ScenePath}");
    }

    private static GameObject GetOrCreateJetPrefab()
    {
        var existing = AssetDatabase.LoadAssetAtPath<GameObject>(JetPrefabPath);
        if (existing != null)
            return existing;

        EditorSceneManager.OpenScene(SourceScenePath, OpenSceneMode.Single);
        var jet = Object.FindObjectsByType<PlayerControllerScript>(FindObjectsSortMode.None).FirstOrDefault();
        if (jet == null)
        {
            Debug.LogError($"No PlayerControllerScript found in {SourceScenePath}");
            return null;
        }

        return PrefabUtility.SaveAsPrefabAsset(jet.gameObject, JetPrefabPath);
    }

    private static void CreateSun()
    {
        var go = new GameObject("Sun");
        go.transform.rotation = Quaternion.Euler(50f, -30f, 0f);
        var light = go.AddComponent<Light>();
        light.type = LightType.Directional;
        light.shadows = LightShadows.Soft;
        RenderSettings.sun = light;
    }

    private static CesiumGeoreference CreateGeoreference()
    {
        var georeference = new GameObject("CesiumGeoreference").AddComponent<CesiumGeoreference>();
        georeference.SetOriginLongitudeLatitudeHeight(RunwayLongitude, RunwayLatitude, FallbackGroundHeight);
        return georeference;
    }

    private static Cesium3DTileset CreateTerrain(Transform parent)
    {
        var go = new GameObject("Cesium World Terrain");
        go.transform.SetParent(parent, false);

        var tileset = go.AddComponent<Cesium3DTileset>();
        tileset.ionAssetID = CesiumWorldTerrainAssetId;
        tileset.maximumScreenSpaceError = 4f;
        tileset.preloadAncestors = true;
        tileset.preloadSiblings = true;
        tileset.enableFrustumCulling = true;
        tileset.enableFogCulling = true;
        tileset.enforceCulledScreenSpaceError = true;
        tileset.culledScreenSpaceError = 64f;
        tileset.maximumSimultaneousTileLoads = 32;
        tileset.maximumCachedBytes = 2048L * 1024 * 1024;
        tileset.createPhysicsMeshes = true;

        var imagery = go.AddComponent<CesiumIonRasterOverlay>();
        imagery.ionAssetID = GoogleSatelliteAssetId;
        imagery.maximumScreenSpaceError = 1f;
        imagery.maximumTextureSize = 4096;
        imagery.maximumSimultaneousTileLoads = 32;
        imagery.subTileCacheBytes = 64L * 1024 * 1024;

        // Enable to see tile (chunk) boundaries. Needs its own material key or it fights the imagery for slot "0".
        var debugOverlay = go.AddComponent<CesiumDebugColorizeTilesRasterOverlay>();
        debugOverlay.materialKey = "1";
        debugOverlay.enabled = false;

        return tileset;
    }

    private static async Task<double> SampleGroundHeight(Cesium3DTileset terrain)
    {
        var position = new double3(RunwayLongitude, RunwayLatitude, FallbackGroundHeight);
        CesiumSampleHeightResult result = await terrain.SampleHeightMostDetailed(position);
        if (result.sampleSuccess[0])
            return result.longitudeLatitudeHeightPositions[0].z;

        Debug.LogWarning($"Terrain height sampling failed, using {FallbackGroundHeight} m. {string.Join(" ", result.warnings)}");
        return FallbackGroundHeight;
    }

    private static void CreateJet(GameObject prefab, Transform parent, double groundHeight)
    {
        var jet = (GameObject)PrefabUtility.InstantiatePrefab(prefab, parent);
        jet.name = "F-2";
        jet.transform.SetLocalPositionAndRotation(Vector3.zero, Quaternion.identity);
        float bottom = ScaleToLength(jet, F2Length);
        MoveCameraToCockpit(jet);
        SetMaterial(jet, "F-2-hud", HudGlassPath);

        var anchor = jet.AddComponent<CesiumGlobeAnchor>();
        anchor.longitudeLatitudeHeight = new double3(RunwayLongitude, RunwayLatitude, groundHeight - bottom + GroundClearance);
        // Nose points along -X, so yaw is heading + 90.
        anchor.rotationEastUpNorth = Quaternion.Euler(0f, RunwayHeading + 90f, 0f);
        jet.AddComponent<CesiumOriginShift>().distance = 2000;

        var body = jet.GetComponent<Rigidbody>();
        // Off until there is lift, and so the jet doesn't drop before the runway colliders stream in.
        body.useGravity = false;
        body.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
        body.interpolation = RigidbodyInterpolation.Interpolate;

        var camera = jet.GetComponentInChildren<Camera>();
        camera.tag = "MainCamera";
        camera.nearClipPlane = 0.05f;
        camera.farClipPlane = 100000f;
    }

    // Returns the lowest point of the scaled model relative to its pivot.
    private static float ScaleToLength(GameObject jet, float length)
    {
        Renderer[] renderers = jet.GetComponentsInChildren<Renderer>();
        Bounds bounds = renderers[0].bounds;
        foreach (Renderer r in renderers)
            bounds.Encapsulate(r.bounds);

        bounds.center -= jet.transform.position;
        float scale = length / bounds.size.x;
        jet.transform.localScale *= scale;
        Debug.Log($"F-2 model size {bounds.size} scaled by {scale:F3} to {length} m long");
        return bounds.min.y * scale;
    }

    // Must run while the jet is still unrotated at the origin, so world bounds match its local axes.
    private static void MoveCameraToCockpit(GameObject jet)
    {
        foreach (Renderer r in jet.GetComponentsInChildren<Renderer>().Where(r => r.name.Contains("cockpit") || r.name.Contains("canopy")))
            Debug.Log($"{r.name} bounds center {r.bounds.center} size {r.bounds.size}");

        Renderer canopy = jet.GetComponentsInChildren<Renderer>().FirstOrDefault(r => r.name == "F-2-canopy");
        if (canopy == null)
        {
            Debug.LogWarning("F-2-canopy not found, camera left at chase position");
            return;
        }

        Bounds b = canopy.bounds;
        Transform cameraPoint = jet.GetComponent<PlayerControllerScript>().cameraPoint;
        cameraPoint.position = new Vector3(b.center.x - EyeForwardOfCanopyCenter, b.max.y - EyeBelowCanopyTop, b.center.z);
    }

    private static void SetMaterial(GameObject jet, string rendererName, string materialPath)
    {
        Renderer renderer = jet.GetComponentsInChildren<Renderer>().FirstOrDefault(r => r.name == rendererName);
        if (renderer == null)
        {
            Debug.LogWarning($"{rendererName} not found on jet");
            return;
        }
        renderer.sharedMaterial = AssetDatabase.LoadAssetAtPath<Material>(materialPath);
    }

    private static void AddToBuildSettings(string path)
    {
        var scenes = EditorBuildSettings.scenes.ToList();
        if (scenes.Any(s => s.path == path))
            return;
        scenes.Add(new EditorBuildSettingsScene(path, true));
        EditorBuildSettings.scenes = scenes.ToArray();
    }
}
