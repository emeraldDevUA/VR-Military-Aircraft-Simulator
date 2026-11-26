using System.Collections.Generic;
using System.IO;
using Unity.XR.CoreUtils;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Filtering;
using UnityEngine.XR.Interaction.Toolkit.Interactables;
using UnityEngine.XR.Interaction.Toolkit.Interactors;

public static class CockpitInteractionSetup
{
    private const string FeelPath = "Assets/Settings/Cockpit/CockpitButtonFeel.asset";
    private const string PressClipPath = "Assets/SourceFiles/SoundFX/Cockpit/click_001.ogg";
    private const string ReleaseClipPath = "Assets/SourceFiles/SoundFX/Cockpit/click_004.ogg";
    private const string LayerSettingsPath = "Assets/XRI/Settings/Resources/InteractionLayerSettings.asset";
    private const string CockpitLayerName = "Cockpit";
    private const int CockpitLayerIndex = 1;
    private const string HitSuffix = "_Hit";
    private const float MinNeighbourGap = 0.001f;

    [MenuItem("Tools/Flight Sim/Set Up Cockpit Interactions")]
    public static void SetUpOpenScene()
    {
        int count = SetUp();
        EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
        Debug.Log($"Cockpit interactions set up on {count} buttons");
    }

    public static int SetUp()
    {
        EnsureCockpitLayer();
        CockpitButtonFeel feel = GetOrCreateFeel();
        InteractionLayerMask cockpitMask = 1 << CockpitLayerIndex;

        foreach (XRPokeInteractor poker in Object.FindObjectsByType<XRPokeInteractor>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            poker.interactionLayers = poker.interactionLayers.value | cockpitMask.value;
            poker.physicsTriggerInteraction = QueryTriggerInteraction.Collide;
            PrefabUtility.RecordPrefabInstancePropertyModifications(poker);
        }

        Vector3? eye = FindEye();
        if (eye == null)
            Debug.LogWarning("No XR Origin or main camera found, press directions may face the wrong way");

        var shapes = new List<KeyShape>();
        foreach (CockpitMfdButton button in Object.FindObjectsByType<CockpitMfdButton>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            if (TryMeasure(button, eye ?? button.transform.position - button.transform.forward, out KeyShape shape))
                shapes.Add(shape);
        }

        foreach (KeyShape shape in shapes)
            Apply(shape, PaddingFor(shape, shapes, feel.edgePadding), feel, cockpitMask);
        return shapes.Count;
    }

    private struct KeyShape
    {
        public CockpitMfdButton button;
        public Vector3 face;
        public Vector3 inward;
        public Quaternion rotation;
        public float width;
        public float height;
    }

    private static bool TryMeasure(CockpitMfdButton button, Vector3 eye, out KeyShape shape)
    {
        shape = default;
        Transform node = button.transform;
        if (node.parent == null)
            return false;

        Bounds local;
        if (node.TryGetComponent(out Renderer renderer))
            local = renderer.localBounds;
        else if (node.TryGetComponent(out BoxCollider collider))
            local = new Bounds(collider.center, collider.size);
        else
        {
            Debug.LogWarning($"{node.name} has no renderer or BoxCollider, skipped", node);
            return false;
        }

        Vector3[] axes = { node.right, node.up, node.forward };
        Vector3 lossy = node.lossyScale;
        float[] sizes = { Mathf.Abs(local.size.x * lossy.x), Mathf.Abs(local.size.y * lossy.y), Mathf.Abs(local.size.z * lossy.z) };
        int pressAxis = 0;
        for (int i = 1; i < 3; i++)
            if (sizes[i] < sizes[pressAxis])
                pressAxis = i;
        int upAxis = (pressAxis + 2) % 3;
        int sideAxis = (pressAxis + 1) % 3;

        Vector3 center = node.TransformPoint(local.center);
        Vector3 inward = axes[pressAxis] * (Vector3.Dot(axes[pressAxis], center - eye) >= 0f ? 1f : -1f);
        Quaternion rotation = Quaternion.LookRotation(inward, axes[upAxis]);
        bool sideIsRight = Mathf.Abs(Vector3.Dot(rotation * Vector3.right, axes[sideAxis])) > 0.5f;

        shape = new KeyShape
        {
            button = button,
            face = center - inward * (sizes[pressAxis] * 0.5f),
            inward = inward,
            rotation = rotation,
            width = sideIsRight ? sizes[sideAxis] : sizes[upAxis],
            height = sideIsRight ? sizes[upAxis] : sizes[sideAxis],
        };
        return true;
    }

    private static float PaddingFor(KeyShape key, List<KeyShape> all, float wanted)
    {
        float padding = wanted;
        Quaternion toLocal = Quaternion.Inverse(key.rotation);
        foreach (KeyShape other in all)
        {
            if (other.button == key.button || Vector3.Angle(other.inward, key.inward) > 10f)
                continue;
            Vector3 offset = toLocal * (other.face - key.face);
            if (Mathf.Abs(offset.z) > 0.005f || offset.magnitude > 0.05f)
                continue;
            float gapX = Mathf.Abs(offset.x) - (key.width + other.width) * 0.5f;
            float gapY = Mathf.Abs(offset.y) - (key.height + other.height) * 0.5f;
            float gap = Mathf.Max(gapX, gapY);
            padding = Mathf.Min(padding, Mathf.Max(0f, (gap - MinNeighbourGap) * 0.5f));
        }
        return padding;
    }

    private static void Apply(KeyShape key, float padding, CockpitButtonFeel feel, InteractionLayerMask cockpitMask)
    {
        Transform node = key.button.transform;
        string hitName = node.name + HitSuffix;
        Transform hit = node.parent.Find(hitName);
        if (hit == null)
        {
            hit = new GameObject(hitName).transform;
            hit.SetParent(node.parent, false);
        }
        hit.SetPositionAndRotation(key.face + key.inward * feel.travel, key.rotation);
        Vector3 parentScale = node.parent.lossyScale;
        hit.localScale = new Vector3(1f / parentScale.x, 1f / parentScale.y, 1f / parentScale.z);

        BoxCollider box = GetOrAdd<BoxCollider>(hit.gameObject);
        box.isTrigger = true;
        box.center = Vector3.zero;
        box.size = new Vector3(key.width + padding * 2f, key.height + padding * 2f, (feel.travel + feel.hoverDepth) * 2f);

        XRSimpleInteractable interactable = GetOrAdd<XRSimpleInteractable>(hit.gameObject);
        interactable.interactionLayers = cockpitMask;
        interactable.selectMode = InteractableSelectMode.Multiple;
        var interactableSo = new SerializedObject(interactable);
        SerializedProperty colliders = interactableSo.FindProperty("m_Colliders");
        colliders.arraySize = 1;
        colliders.GetArrayElementAtIndex(0).objectReferenceValue = box;
        interactableSo.ApplyModifiedPropertiesWithoutUndo();

        XRPokeFilter filter = GetOrAdd<XRPokeFilter>(hit.gameObject);
        var filterSo = new SerializedObject(filter);
        filterSo.FindProperty("m_PokeCollider").objectReferenceValue = box;
        filterSo.ApplyModifiedPropertiesWithoutUndo();

        CockpitXRButton xrButton = GetOrAdd<CockpitXRButton>(hit.gameObject);
        xrButton.button = key.button;
        xrButton.feel = feel;

        CockpitButtonFeedback feedback = GetOrAdd<CockpitButtonFeedback>(hit.gameObject);
        feedback.cap = node;
        feedback.localPressDirection = node.parent.InverseTransformVector(key.inward);

        EditorUtility.SetDirty(hit.gameObject);
    }

    private static T GetOrAdd<T>(GameObject go) where T : Component
    {
        T component = go.GetComponent<T>();
        return component != null ? component : go.AddComponent<T>();
    }

    private static Vector3? FindEye()
    {
        XROrigin origin = Object.FindFirstObjectByType<XROrigin>(FindObjectsInactive.Include);
        if (origin != null && origin.Camera != null)
            return origin.Camera.transform.position;
        return Camera.main != null ? Camera.main.transform.position : null;
    }

    private static void EnsureCockpitLayer()
    {
        var settings = AssetDatabase.LoadMainAssetAtPath(LayerSettingsPath);
        if (settings == null)
        {
            Debug.LogWarning($"{LayerSettingsPath} not found, interaction layer {CockpitLayerIndex} left unnamed");
            return;
        }
        var so = new SerializedObject(settings);
        SerializedProperty names = so.FindProperty("m_LayerNames");
        SerializedProperty entry = names.GetArrayElementAtIndex(CockpitLayerIndex);
        if (entry.stringValue == CockpitLayerName)
            return;
        if (!string.IsNullOrEmpty(entry.stringValue))
            Debug.LogWarning($"Interaction layer {CockpitLayerIndex} was '{entry.stringValue}', renamed to {CockpitLayerName}");
        entry.stringValue = CockpitLayerName;
        so.ApplyModifiedPropertiesWithoutUndo();
        AssetDatabase.SaveAssets();
    }

    private static CockpitButtonFeel GetOrCreateFeel()
    {
        var feel = AssetDatabase.LoadAssetAtPath<CockpitButtonFeel>(FeelPath);
        if (feel != null)
            return feel;

        Directory.CreateDirectory(Path.GetDirectoryName(FeelPath));
        feel = ScriptableObject.CreateInstance<CockpitButtonFeel>();
        feel.pressClip = AssetDatabase.LoadAssetAtPath<AudioClip>(PressClipPath);
        feel.releaseClip = AssetDatabase.LoadAssetAtPath<AudioClip>(ReleaseClipPath);
        AssetDatabase.CreateAsset(feel, FeelPath);
        AssetDatabase.SaveAssets();
        return feel;
    }
}
