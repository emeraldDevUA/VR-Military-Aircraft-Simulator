using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.PackageManager.UI;
using UnityEditor.XR.Management;
using UnityEditor.XR.Management.Metadata;
using UnityEditor.XR.OpenXR.Features;
using UnityEngine;
using UnityEngine.XR.Hands.OpenXR;
using UnityEngine.XR.Management;
using UnityEngine.XR.OpenXR;
using UnityEngine.XR.OpenXR.Features;
using UnityEngine.XR.OpenXR.Features.Interactions;
using UnityEngine.XR.OpenXR.Features.MetaQuestSupport;

public static class XRSetup
{
    private const string SettingsPath = "Assets/XR/XRGeneralSettingsPerBuildTarget.asset";
    private const string OpenXRLoader = "UnityEngine.XR.OpenXR.OpenXRLoader";
    private const string XriPackage = "com.unity.xr.interaction.toolkit";
    private static readonly string[] XriSamples = { "Starter Assets", "XR Device Simulator" };

    private static readonly System.Type[] PcFeatures =
    {
        typeof(OculusTouchControllerProfile), typeof(MetaQuestTouchPlusControllerProfile), typeof(MetaQuestTouchProControllerProfile),
        typeof(ValveIndexControllerProfile), typeof(HTCViveControllerProfile), typeof(KHRSimpleControllerProfile),
        typeof(HandInteractionProfile), typeof(HandTracking),
    };

    private static readonly System.Type[] QuestFeatures =
    {
        typeof(MetaQuestFeature), typeof(OculusTouchControllerProfile), typeof(MetaQuestTouchPlusControllerProfile),
        typeof(MetaQuestTouchProControllerProfile), typeof(HandInteractionProfile), typeof(HandTracking),
    };

    [MenuItem("Tools/Flight Sim/Configure XR")]
    public static void Configure()
    {
        XRGeneralSettingsPerBuildTarget perTarget = GetOrCreateSettings();
        EnableOpenXR(perTarget, BuildTargetGroup.Standalone, PcFeatures);
        EnableOpenXR(perTarget, BuildTargetGroup.Android, QuestFeatures);

        PlayerSettings.SetScriptingBackend(NamedBuildTarget.Android, ScriptingImplementation.IL2CPP);
        PlayerSettings.Android.targetArchitectures = AndroidArchitecture.ARM64;
        PlayerSettings.Android.minSdkVersion = AndroidSdkVersions.AndroidApiLevel32;

        ImportSamples();
        AssetDatabase.SaveAssets();
        Debug.Log("XR configured");
    }

    private static XRGeneralSettingsPerBuildTarget GetOrCreateSettings()
    {
        if (EditorBuildSettings.TryGetConfigObject(XRGeneralSettings.settingsKey, out XRGeneralSettingsPerBuildTarget existing) && existing != null)
            return existing;

        Directory.CreateDirectory(Path.GetDirectoryName(SettingsPath));
        var settings = AssetDatabase.LoadAssetAtPath<XRGeneralSettingsPerBuildTarget>(SettingsPath);
        if (settings == null)
        {
            settings = ScriptableObject.CreateInstance<XRGeneralSettingsPerBuildTarget>();
            AssetDatabase.CreateAsset(settings, SettingsPath);
        }
        EditorBuildSettings.AddConfigObject(XRGeneralSettings.settingsKey, settings, true);
        return settings;
    }

    private static void EnableOpenXR(XRGeneralSettingsPerBuildTarget perTarget, BuildTargetGroup group, System.Type[] features)
    {
        if (perTarget.ManagerSettingsForBuildTarget(group) == null)
            perTarget.CreateDefaultManagerSettingsForBuildTarget(group);

        perTarget.SettingsForBuildTarget(group).InitManagerOnStart = true;
        XRPackageMetadataStore.AssignLoader(perTarget.ManagerSettingsForBuildTarget(group), OpenXRLoader, group);

        FeatureHelpers.RefreshFeatures(group);
        OpenXRSettings openXR = OpenXRSettings.GetSettingsForBuildTargetGroup(group);
        foreach (System.Type type in features)
        {
            OpenXRFeature feature = openXR.GetFeatures().FirstOrDefault(f => f.GetType() == type);
            if (feature == null)
                Debug.LogWarning($"OpenXR feature {type.Name} not found for {group}");
            else
                feature.enabled = true;
        }
        EditorUtility.SetDirty(openXR);
    }

    private static void ImportSamples()
    {
        foreach (Sample sample in Sample.FindByPackage(XriPackage, ""))
        {
            if (XriSamples.Contains(sample.displayName) && !sample.isImported)
                sample.Import(Sample.ImportOptions.HideImportWindow | Sample.ImportOptions.OverridePreviousImports);
        }
    }
}
