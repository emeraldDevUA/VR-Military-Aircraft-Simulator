using System.IO;
using System.Reflection;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Profile;
using UnityEngine;

public static class QuestBuildSetup
{
    private const string ProfilePath = "Assets/Settings/Build Profiles/Meta Quest.asset";
    private const string ScenePath = "Assets/Scenes/VR_Cockpit_Test.unity";
    private const string PackageName = "com.f2sim.vrcockpit";
    private const string ProductName = "F-2 Flight Sim";
    private const string ApkPath = "Builds/Quest/F2Cockpit.apk";

    [MenuItem("Tools/Flight Sim/Set Up Quest Build")]
    public static void SetUp()
    {
        BuildProfile profile = AssetDatabase.LoadAssetAtPath<BuildProfile>(ProfilePath) ?? CreateAndroidProfile();
        if (profile == null)
            return;

        var so = new SerializedObject(profile);
        so.FindProperty("m_OverrideGlobalSceneList").boolValue = true;
        SerializedProperty scenes = so.FindProperty("m_Scenes");
        scenes.arraySize = 1;
        scenes.GetArrayElementAtIndex(0).FindPropertyRelative("m_path").stringValue = ScenePath;
        scenes.GetArrayElementAtIndex(0).FindPropertyRelative("m_guid").stringValue = AssetDatabase.AssetPathToGUID(ScenePath);
        scenes.GetArrayElementAtIndex(0).FindPropertyRelative("m_enabled").boolValue = true;
        so.ApplyModifiedPropertiesWithoutUndo();

        PlayerSettings.productName = ProductName;
        PlayerSettings.SetApplicationIdentifier(NamedBuildTarget.Android, PackageName);
        PlayerSettings.Android.forceInternetPermission = true;
        PlayerSettings.defaultInterfaceOrientation = UIOrientation.LandscapeLeft;

        BuildProfile.SetActiveBuildProfile(profile);
        EditorUserBuildSettings.androidBuildSubtarget = MobileTextureSubtarget.ASTC;
        EditorUserBuildSettings.buildAppBundle = false;
        EditorUserBuildSettings.development = true;

        EditorUtility.SetDirty(profile);
        AssetDatabase.SaveAssets();
        Debug.Log($"Quest build profile active: {ProfilePath}, scene {ScenePath}, package {PackageName}");
    }

    [MenuItem("Tools/Flight Sim/Build Quest APK")]
    public static void BuildApk()
    {
        var profile = AssetDatabase.LoadAssetAtPath<BuildProfile>(ProfilePath);
        if (profile == null)
        {
            Debug.LogError("Run Tools/Flight Sim/Set Up Quest Build first");
            return;
        }

        var options = new BuildPlayerWithProfileOptions
        {
            buildProfile = profile,
            locationPathName = ApkPath,
            options = BuildOptions.None,
        };
        var report = BuildPipeline.BuildPlayer(options);
        Debug.Log($"Quest APK build {report.summary.result}: {ApkPath}, {report.summary.totalSize / (1024 * 1024)} MB, {report.summary.totalErrors} errors, {report.summary.totalTime}");
    }

    private static BuildProfile CreateAndroidProfile()
    {
        Directory.CreateDirectory(Path.GetDirectoryName(ProfilePath));
        const BindingFlags flags = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static;
        var getPlatformId = System.Type.GetType("UnityEditor.Build.Profile.BuildProfileModuleUtil, UnityEditor.CoreModule")?
            .GetMethod("GetPlatformId", flags, null, new[] { typeof(BuildTarget), typeof(StandaloneBuildSubtarget) }, null);
        var create = typeof(BuildProfile).GetMethod("CreateInstance", flags, null, new[] { typeof(GUID), typeof(string) }, null);
        if (getPlatformId == null || create == null)
        {
            Debug.LogError("Build profile API not found, create an Android profile in File > Build Profiles and run this again");
            return null;
        }

        var platformId = (GUID)getPlatformId.Invoke(null, new object[] { BuildTarget.Android, StandaloneBuildSubtarget.Default });
        create.Invoke(null, new object[] { platformId, ProfilePath });
        AssetDatabase.Refresh();
        return AssetDatabase.LoadAssetAtPath<BuildProfile>(ProfilePath);
    }
}
