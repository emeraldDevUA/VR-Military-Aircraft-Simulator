using System.Linq;
using Unity.XR.CoreUtils;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

public static class VRCockpitSceneBuilder
{
    private const string SourceScenePath = "Assets/Scenes/AircraftPhysicsScene.unity";
    private const string ScenePath = "Assets/Scenes/VR_Cockpit_Test.unity";
    private const string SamplesPath = "Assets/Samples/XR Interaction Toolkit/3.7.0";
    private const string RigPath = SamplesPath + "/Starter Assets/Prefabs/XR Origin (XR Rig).prefab";
    private const string SimulatorPath = SamplesPath + "/XR Device Simulator/XR Device Simulator.prefab";

    private const string CanopyName = "F-2-canopy";
    private const float EyeBelowCanopyTop = 0.25f;
    private const float EyeForwardOfCanopyCenter = 0.2f;

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
        var jet = Object.FindObjectsByType<PlayerControllerScript>(FindObjectsSortMode.None).FirstOrDefault();
        if (jet == null)
        {
            Debug.LogError($"No PlayerControllerScript found in {SourceScenePath}");
            return;
        }

        RemoveChaseCamera(jet);
        CreateRig(rigPrefab, jet.transform, GetEyePosition(jet));

        var fallback = new GameObject("XR Simulator Fallback").AddComponent<XRSimulatorFallback>();
        fallback.simulatorPrefab = simulatorPrefab;

        EditorSceneManager.SaveScene(scene, ScenePath);
        AddToBuildSettings(ScenePath);
        Debug.Log($"VR cockpit scene saved to {ScenePath}");
    }

    private static void RemoveChaseCamera(PlayerControllerScript jet)
    {
        Camera chase = jet.playerCamera;
        jet.playerCamera = null;
        if (chase != null && chase.gameObject != jet.gameObject)
            Object.DestroyImmediate(chase.gameObject);
    }

    private static Vector3 GetEyePosition(PlayerControllerScript jet)
    {
        Transform root = jet.transform;
        Renderer canopy = jet.GetComponentsInChildren<Renderer>(true).FirstOrDefault(r => r.name == CanopyName);
        if (canopy == null)
        {
            Debug.LogWarning($"{CanopyName} not found, using cameraPoint");
            return jet.cameraPoint != null ? root.InverseTransformPoint(jet.cameraPoint.position) : Vector3.zero;
        }

        Bounds local = canopy.localBounds;
        Bounds inJet = new Bounds(root.InverseTransformPoint(canopy.transform.TransformPoint(local.center)), Vector3.zero);
        for (int i = 0; i < 8; i++)
        {
            Vector3 corner = local.center + Vector3.Scale(local.extents, new Vector3(i & 1, (i >> 1) & 1, (i >> 2) & 1) * 2 - Vector3.one);
            inJet.Encapsulate(root.InverseTransformPoint(canopy.transform.TransformPoint(corner)));
        }

        Vector3 eye = new Vector3(inJet.center.x + EyeForwardOfCanopyCenter, inJet.max.y - EyeBelowCanopyTop, inJet.center.z);
        Debug.Log($"Canopy bounds in jet space {inJet}, eye at {eye}, jet scale {root.lossyScale}");
        return eye;
    }

    private static void CreateRig(GameObject prefab, Transform jet, Vector3 eye)
    {
        var rig = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
        rig.name = "XR Origin (Cockpit)";
        rig.transform.SetParent(jet, false);
        rig.transform.localPosition = eye;
        rig.transform.localRotation = Quaternion.Euler(0f, 90f, 0f);
        Vector3 s = jet.lossyScale;
        rig.transform.localScale = new Vector3(1f / s.x, 1f / s.y, 1f / s.z);

        var origin = rig.GetComponent<XROrigin>();
        origin.RequestedTrackingOriginMode = XROrigin.TrackingOriginMode.Device;
        origin.CameraYOffset = 0f;

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
