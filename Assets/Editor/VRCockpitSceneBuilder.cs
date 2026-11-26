using System.Collections.Generic;
using System.Linq;
using Unity.XR.CoreUtils;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

public static class VRCockpitSceneBuilder
{
    private const string SourceScenePath = "Assets/Scenes/Cockpit_Scene.unity";
    private const string ScenePath = "Assets/Scenes/VR_Cockpit_Test.unity";
    private const string SamplesPath = "Assets/Samples/XR Interaction Toolkit/3.7.0";
    private const string RigPath = SamplesPath + "/Starter Assets/Prefabs/XR Origin (XR Rig).prefab";
    private const string SimulatorPath = SamplesPath + "/XR Device Simulator/XR Device Simulator.prefab";

    private const string ModelName = "F2_with_interactive_cockpit";
    private const string TestCameraName = "CockpitTestCamera";
    private const string HudName = "F-2-hud";
    private const string StickName = "Control_Stick";
    private const float EyeBehindHud = 0.7f;
    private const float HeadrestSearchStart = 0.3f;
    private const float MaxSearchDistance = 2f;
    private const float EyeForwardOfHeadrest = 0.22f;
    private const float EyeAboveHud = 0.05f;
    private const float CanopyClearance = 0.25f;

    private static readonly string[] NonStructuralPrefixes = { "VR_", "F-2-hud", "F-2-instrGlass", "Control_Stick", "Throttle", "F-2-landing" };

    private static readonly string[] LocomotionOnlyObjects = { "Locomotion", "Teleport Interactor", "Climb Teleport" };

    [MenuItem("Tools/Flight Sim/Build VR Cockpit Scene")]
    public static void Build()
    {
        if (!Application.isBatchMode && !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
            return;

        var rigPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(RigPath);
        var simulatorPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(SimulatorPath);
        if (rigPrefab == null || simulatorPrefab == null)
        {
            Debug.LogError("XR Interaction Toolkit samples missing, run Tools/Flight Sim/Configure XR first");
            return;
        }

        var scene = EditorSceneManager.OpenScene(SourceScenePath, OpenSceneMode.Single);
        GameObject model = GameObject.Find(ModelName);
        if (model == null)
        {
            Debug.LogError($"{ModelName} not found in {SourceScenePath}");
            return;
        }

        if (!TryGetPilotView(model, out Vector3 eye, out Vector3 forward))
            return;

        GameObject rig = CreateRig(rigPrefab, model.transform, eye, forward);
        Camera xrCamera = rig.GetComponent<XROrigin>().Camera;
        xrCamera.gameObject.AddComponent<SeatedHeadRange>();
        MoveTestCameraInputs(xrCamera);
        PointHudAt(xrCamera);

        var fallback = new GameObject("XR Simulator Fallback").AddComponent<XRSimulatorFallback>();
        fallback.simulatorPrefab = simulatorPrefab;

        EditorSceneManager.SaveScene(scene, ScenePath);
        AddToBuildSettings(ScenePath);
        Debug.Log($"VR cockpit scene saved to {ScenePath}");
    }

    private static bool TryGetPilotView(GameObject model, out Vector3 eye, out Vector3 forward)
    {
        eye = forward = Vector3.zero;
        Renderer[] renderers = model.GetComponentsInChildren<Renderer>(true);
        Renderer hud = renderers.FirstOrDefault(r => r.name == HudName);
        Transform stick = model.GetComponentsInChildren<Transform>(true).FirstOrDefault(t => t.name == StickName);
        if (hud == null || stick == null)
        {
            Debug.LogError($"{HudName} or {StickName} not found on {model.name}");
            return false;
        }

        Vector3 stickCenter = stick.GetComponentsInChildren<Renderer>(true).Select(r => r.bounds.center).DefaultIfEmpty(stick.position).First();
        forward = Vector3.ProjectOnPlane(hud.bounds.center - stickCenter, Vector3.up).normalized;
        Vector3 hudCenter = hud.bounds.center;

        var colliders = new List<MeshCollider>();
        foreach (MeshFilter filter in model.GetComponentsInChildren<MeshFilter>(true))
        {
            if (filter.sharedMesh == null || NonStructuralPrefixes.Any(p => filter.name.StartsWith(p)))
                continue;
            var collider = filter.gameObject.AddComponent<MeshCollider>();
            collider.sharedMesh = filter.sharedMesh;
            colliders.Add(collider);
        }
        Physics.SyncTransforms();

        try
        {
            Vector3 probe = hudCenter - forward * HeadrestSearchStart;
            float backDistance = Cast(colliders, probe, -forward, MaxSearchDistance, out string backHit);
            Vector3 station = backDistance > 0f ? probe - forward * (backDistance - EyeForwardOfHeadrest) : hudCenter - forward * EyeBehindHud;

            Vector3 above = station + Vector3.up * MaxSearchDistance;
            float down = Cast(colliders, above, Vector3.down, MaxSearchDistance, out string canopyHit);
            float canopyY = down > 0f ? above.y - down : float.PositiveInfinity;

            eye = new Vector3(station.x, Mathf.Min(hudCenter.y + EyeAboveHud, canopyY - CanopyClearance), station.z);
            Debug.Log($"Pilot eye {eye}: headrest {backHit} {backDistance:F2} m behind probe, canopy {canopyHit} at y {canopyY:F2}, HUD {hudCenter}, eye {Vector3.Dot(hudCenter - eye, forward):F2} m behind HUD, forward {forward}, model scale {model.transform.lossyScale}");
        }
        finally
        {
            foreach (MeshCollider collider in colliders)
                Object.DestroyImmediate(collider);
        }
        return true;
    }

    private static float Cast(List<MeshCollider> colliders, Vector3 origin, Vector3 direction, float distance, out string hitName)
    {
        float best = -1f;
        hitName = "none";
        foreach (MeshCollider collider in colliders)
        {
            if (collider.Raycast(new Ray(origin, direction), out RaycastHit hit, distance) && (best < 0f || hit.distance < best))
            {
                best = hit.distance;
                hitName = collider.name;
            }
        }
        return best;
    }

    private static GameObject CreateRig(GameObject prefab, Transform model, Vector3 eye, Vector3 forward)
    {
        var rig = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
        rig.name = "XR Origin (Cockpit)";
        rig.transform.SetPositionAndRotation(eye, Quaternion.LookRotation(forward, Vector3.up));
        rig.transform.SetParent(model, true);
        Vector3 s = model.lossyScale;
        rig.transform.localScale = new Vector3(1f / s.x, 1f / s.y, 1f / s.z);

        var origin = rig.GetComponent<XROrigin>();
        origin.RequestedTrackingOriginMode = XROrigin.TrackingOriginMode.Device;
        origin.CameraYOffset = 0f;
        Transform cameraOffset = origin.CameraFloorOffsetObject.transform;
        cameraOffset.localPosition = Vector3.zero;
        PrefabUtility.RecordPrefabInstancePropertyModifications(cameraOffset);

        var characterController = rig.GetComponent<CharacterController>();
        if (characterController != null)
            characterController.enabled = false;

        foreach (Transform t in rig.GetComponentsInChildren<Transform>(true))
        {
            if (LocomotionOnlyObjects.Contains(t.name))
                t.gameObject.SetActive(false);
        }

        foreach (MonoBehaviour behaviour in rig.GetComponentsInChildren<MonoBehaviour>(true))
        {
            if (behaviour == null || behaviour.GetType().Name != "ControllerInputActionManager")
                continue;
            var so = new SerializedObject(behaviour);
            so.FindProperty("m_TeleportMode").objectReferenceValue = null;
            so.FindProperty("m_TeleportModeCancel").objectReferenceValue = null;
            so.FindProperty("m_TeleportInteractor").objectReferenceValue = null;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        return rig;
    }

    private static void MoveTestCameraInputs(Camera xrCamera)
    {
        GameObject testCamera = GameObject.Find(TestCameraName);
        if (testCamera == null)
            return;

        foreach (MonoBehaviour source in new MonoBehaviour[] { testCamera.GetComponent<CockpitMouseRayInput>(), testCamera.GetComponent<CockpitMouseRotaryInput>() })
        {
            if (source == null)
                continue;
            Component copy = xrCamera.gameObject.AddComponent(source.GetType());
            EditorUtility.CopySerialized(source, copy);
            var so = new SerializedObject(copy);
            so.FindProperty("rayCamera").objectReferenceValue = xrCamera;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        Object.DestroyImmediate(testCamera);
    }

    private static void PointHudAt(Camera xrCamera)
    {
        foreach (FighterHudDisplayController hud in Object.FindObjectsByType<FighterHudDisplayController>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            var so = new SerializedObject(hud);
            so.FindProperty("pilotCamera").objectReferenceValue = xrCamera;
            so.ApplyModifiedPropertiesWithoutUndo();
        }
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
