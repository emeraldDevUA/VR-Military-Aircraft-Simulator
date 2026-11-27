using CesiumForUnity;
using System;
using System.Linq;
using System.Threading.Tasks;
using UnityEditor;
using UnityEditor.SceneManagement;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Rebuilds the generated Cesium cockpit scene in place, preserving the imported FBX
/// connection and the scene-level display, input, and mock-data setup.
/// </summary>
public static class InteractiveCockpitCesiumSceneBuilder
{
    private const string OutputScenePath = "Assets/Scenes/Interactive_Cockpit_Cesium_Scene.unity";
    private const string SkyboxPath = "Assets/Skyboxes/SkyDay.mat";
    private const string HudGlassPath = "Assets/Materials/F-2-HudGlass.mat";

    private const double RunwayLongitude = 141.35213;
    private const double RunwayLatitude = 40.70438;
    private const float RunwayHeading = 95.2f;
    private const double FallbackGroundHeight = 70d;
    private const float GroundClearance = 0.05f;
    private const float F2Length = 15.52f;
    private const float EyeBelowCanopyTop = 0.25f;
    private const float EyeForwardOfCanopyCenter = 0.2f;
    private const long CesiumWorldTerrainAssetId = 1;
    private const long GoogleSatelliteAssetId = 3830182;

    [MenuItem("Tools/Flight Sim/Build Interactive Cockpit Cesium Scene")]
    public static async void Build()
    {
        if (SceneManager.GetActiveScene().isDirty)
        {
            if (!EditorUtility.DisplayDialog("Save current scene?",
                    "Save the current scene before building the Cesium cockpit copy?", "Save", "Cancel"))
                return;
            if (!EditorSceneManager.SaveOpenScenes())
                return;
        }
        if (AssetDatabase.LoadAssetAtPath<SceneAsset>(OutputScenePath) == null)
        {
            EditorUtility.DisplayDialog("Cockpit scene builder",
                "The generated cockpit scene was not found:\n" + OutputScenePath +
                "\nKeep this scene in the project to rebuild it.", "OK");
            return;
        }
        if (AssetDatabase.LoadAssetAtPath<SceneAsset>(OutputScenePath) != null &&
            !EditorUtility.DisplayDialog("Replace generated scene?", OutputScenePath + " already exists. Replace it with a fresh build?", "Replace", "Cancel"))
            return;

        Scene scene = EditorSceneManager.OpenScene(OutputScenePath, OpenSceneMode.Single);
        try
        {
            RemoveSampleEnvironment(scene);
            GameObject cockpit = FindInScene(scene, "F2_with_interactive_cockpit");
            if (cockpit == null)
                throw new InvalidOperationException("F2_with_interactive_cockpit was not found in the template scene.");

            ConfigureSun(scene);
            RenderSettings.skybox = AssetDatabase.LoadAssetAtPath<Material>(SkyboxPath);
            CesiumGeoreference georeference = FindComponentInScene<CesiumGeoreference>(scene);
            if (georeference == null)
                georeference = CreateGeoreference();
            Cesium3DTileset terrain = georeference.GetComponentInChildren<Cesium3DTileset>(true);
            if (terrain == null)
                terrain = CreateTerrain(georeference.transform);
            double groundHeight = await SampleGroundHeight(terrain);
            georeference.SetOriginLongitudeLatitudeHeight(RunwayLongitude, RunwayLatitude, groundHeight);

            cockpit.transform.SetParent(georeference.transform, false);
            cockpit.transform.SetLocalPositionAndRotation(Vector3.zero, Quaternion.identity);
            cockpit.transform.localScale = Vector3.one;
            float bottom = ScaleToLength(cockpit, F2Length);
            MoveCameraToCockpit(cockpit);
            SetMaterial(cockpit, "F-2-hud", HudGlassPath);

            CesiumGlobeAnchor anchor = cockpit.GetComponent<CesiumGlobeAnchor>();
            if (anchor == null)
                anchor = cockpit.AddComponent<CesiumGlobeAnchor>();
            anchor.longitudeLatitudeHeight = new double3(RunwayLongitude, RunwayLatitude,
                groundHeight - bottom + GroundClearance);
            anchor.rotationEastUpNorth = Quaternion.Euler(0f, RunwayHeading + 90f, 0f);

            CesiumOriginShift originShift = cockpit.GetComponent<CesiumOriginShift>();
            if (originShift == null)
                originShift = cockpit.AddComponent<CesiumOriginShift>();
            originShift.distance = 2000f;

            ConfigurePilotCamera(cockpit);
            EditorSceneManager.SaveScene(scene, OutputScenePath);
            // Save first so Unity has created the scene's .meta file and GUID.
            // Registering it before the first save records an all-zero GUID.
            AssetDatabase.Refresh();
            RegisterInBuildSettings(OutputScenePath);
            Debug.Log("Interactive cockpit Cesium scene saved to " + OutputScenePath);
        }
        catch (Exception exception)
        {
            Debug.LogException(exception);
            Debug.LogError("Interactive cockpit Cesium scene rebuild failed. The saved scene file was not replaced.");
        }
    }

    private static void RemoveSampleEnvironment(Scene scene)
    {
        string[] removeNames =
        {
            "Ground", "01_Box_Yellow", "02_Box_Orange", "03_Box_Orange", "04_Box_Red",
            "Collectibles", "Tutorial", "AIM120"
        };

        foreach (string objectName in removeNames)
        {
            GameObject sampleObject = FindInScene(scene, objectName);
            if (sampleObject != null)
                UnityEngine.Object.DestroyImmediate(sampleObject);
        }
    }

    private static void ConfigureSun(Scene scene)
    {
        Light sun = scene.GetRootGameObjects()
            .SelectMany(root => root.GetComponentsInChildren<Light>(true))
            .FirstOrDefault(light => light.type == LightType.Directional);
        if (sun == null)
        {
            GameObject sunObject = new GameObject("Sun");
            sun = sunObject.AddComponent<Light>();
            sun.type = LightType.Directional;
        }
        sun.name = "Sun";
        sun.transform.rotation = Quaternion.Euler(50f, -30f, 0f);
        sun.shadows = LightShadows.Soft;
        RenderSettings.sun = sun;
    }

    private static CesiumGeoreference CreateGeoreference()
    {
        GameObject georeferenceObject = new GameObject("CesiumGeoreference");
        CesiumGeoreference georeference = georeferenceObject.AddComponent<CesiumGeoreference>();
        georeference.SetOriginLongitudeLatitudeHeight(RunwayLongitude, RunwayLatitude, FallbackGroundHeight);
        return georeference;
    }

    private static Cesium3DTileset CreateTerrain(Transform parent)
    {
        GameObject terrainObject = new GameObject("Cesium World Terrain");
        terrainObject.transform.SetParent(parent, false);
        Cesium3DTileset terrain = terrainObject.AddComponent<Cesium3DTileset>();
        terrain.ionAssetID = CesiumWorldTerrainAssetId;
        terrain.maximumScreenSpaceError = 4f;
        terrain.preloadAncestors = true;
        terrain.preloadSiblings = true;
        terrain.enableFrustumCulling = true;
        terrain.enableFogCulling = true;
        terrain.enforceCulledScreenSpaceError = true;
        terrain.culledScreenSpaceError = 64f;
        terrain.maximumSimultaneousTileLoads = 32;
        terrain.maximumCachedBytes = 2048L * 1024 * 1024;
        terrain.createPhysicsMeshes = true;

        CesiumIonRasterOverlay imagery = terrainObject.AddComponent<CesiumIonRasterOverlay>();
        imagery.ionAssetID = GoogleSatelliteAssetId;
        imagery.maximumScreenSpaceError = 1f;
        imagery.maximumTextureSize = 4096;
        imagery.maximumSimultaneousTileLoads = 32;
        imagery.subTileCacheBytes = 64L * 1024 * 1024;
        return terrain;
    }

    private static async Task<double> SampleGroundHeight(Cesium3DTileset terrain)
    {
        try
        {
            var position = new double3(RunwayLongitude, RunwayLatitude, FallbackGroundHeight);
            CesiumSampleHeightResult result = await terrain.SampleHeightMostDetailed(position);
            if (result.sampleSuccess != null && result.sampleSuccess.Length > 0 && result.sampleSuccess[0])
                return result.longitudeLatitudeHeightPositions[0].z;
            Debug.LogWarning("Cesium terrain sample failed; using fallback height. " + string.Join(" ", result.warnings));
        }
        catch (Exception exception)
        {
            Debug.LogWarning("Cesium terrain sample threw an exception; using fallback height. " + exception.Message);
        }
        return FallbackGroundHeight;
    }

    private static float ScaleToLength(GameObject aircraft, float lengthMeters)
    {
        Renderer[] renderers = aircraft.GetComponentsInChildren<Renderer>();
        if (renderers.Length == 0)
            throw new InvalidOperationException("The imported cockpit model has no renderers to measure.");

        Bounds bounds = renderers[0].bounds;
        foreach (Renderer renderer in renderers)
            bounds.Encapsulate(renderer.bounds);
        bounds.center -= aircraft.transform.position;
        if (bounds.size.x <= 0f)
            throw new InvalidOperationException("The imported cockpit model has no usable length bounds.");

        float scale = lengthMeters / bounds.size.x;
        aircraft.transform.localScale *= scale;
        Debug.Log("F-2 model bounds " + bounds.size + " scaled by " + scale.ToString("F3") + " to " + lengthMeters + " m long");
        return bounds.min.y * scale;
    }

    private static void MoveCameraToCockpit(GameObject cockpit)
    {
        Renderer canopy = cockpit.GetComponentsInChildren<Renderer>(true)
            .FirstOrDefault(renderer => renderer.name == "F-2-canopy");
        Camera camera = cockpit.GetComponentsInChildren<Camera>(true).FirstOrDefault();
        if (canopy == null || camera == null)
        {
            Debug.LogWarning("Canopy or cockpit camera not found; keeping the camera's saved cockpit position.");
            return;
        }

        Bounds bounds = canopy.bounds;
        camera.transform.position = new Vector3(bounds.center.x - EyeForwardOfCanopyCenter,
            bounds.max.y - EyeBelowCanopyTop, bounds.center.z);
    }

    private static void ConfigurePilotCamera(GameObject cockpit)
    {
        Camera camera = cockpit.GetComponentsInChildren<Camera>(true).FirstOrDefault();
        if (camera == null)
        {
            Debug.LogWarning("No camera was found under the cockpit; the generated scene was saved without a pilot camera.");
            return;
        }
        camera.tag = "MainCamera";
        camera.nearClipPlane = 0.05f;
        camera.farClipPlane = 100000f;
    }

    private static void SetMaterial(GameObject root, string rendererName, string materialPath)
    {
        Renderer renderer = root.GetComponentsInChildren<Renderer>(true)
            .FirstOrDefault(candidate => candidate.name == rendererName);
        Material material = AssetDatabase.LoadAssetAtPath<Material>(materialPath);
        if (renderer == null || material == null)
        {
            Debug.LogWarning("Could not apply the HUD glass material; preserving the current renderer material.");
            return;
        }
        renderer.sharedMaterial = material;
    }

    private static GameObject FindInScene(Scene scene, string objectName)
    {
        foreach (GameObject root in scene.GetRootGameObjects())
        {
            if (root.name == objectName)
                return root;
            Transform[] descendants = root.GetComponentsInChildren<Transform>(true);
            Transform match = descendants.FirstOrDefault(transform => transform.name == objectName);
            if (match != null)
                return match.gameObject;
        }
        return null;
    }

    private static T FindComponentInScene<T>(Scene scene) where T : Component
    {
        foreach (GameObject root in scene.GetRootGameObjects())
        {
            T component = root.GetComponentInChildren<T>(true);
            if (component != null)
                return component;
        }
        return null;
    }

    private static void RegisterInBuildSettings(string scenePath)
    {
        EditorBuildSettingsScene[] scenes = EditorBuildSettings.scenes;
        string guid = AssetDatabase.AssetPathToGUID(scenePath);
        if (string.IsNullOrEmpty(guid))
            throw new InvalidOperationException("Unity could not resolve the saved scene GUID for " + scenePath);
        if (!GUID.TryParse(guid, out GUID sceneGuid))
            throw new InvalidOperationException("Unity returned an invalid scene GUID for " + scenePath);

        int existingIndex = Array.FindIndex(scenes, scene => scene.path == scenePath);
        var entry = new EditorBuildSettingsScene(sceneGuid, true);
        if (existingIndex >= 0)
            scenes[existingIndex] = entry;
        else
            scenes = scenes.Concat(new[] { entry }).ToArray();
        EditorBuildSettings.scenes = scenes;
    }
}
