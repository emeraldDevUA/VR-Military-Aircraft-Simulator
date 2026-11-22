using CesiumForUnity;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

[InitializeOnLoad]
public static class CesiumReloadFix
{
    private const string Marker = "&reload=";
    private static string nonce = System.DateTime.UtcNow.Ticks.ToString();

    static CesiumReloadFix()
    {
        EditorApplication.delayCall += ApplyToOpenScenes;
        EditorSceneManager.sceneOpened += (scene, mode) => Apply(scene, nonce);
        EditorSceneManager.sceneSaving += (scene, path) => Apply(scene, null);
        EditorSceneManager.sceneSaved += scene => Apply(scene, nonce);
        EditorApplication.playModeStateChanged += state =>
        {
            if (state == PlayModeStateChange.EnteredPlayMode)
                ApplyToOpenScenes();
        };
    }

    [MenuItem("Tools/Flight Sim/Reload Terrain Imagery")]
    private static void ReloadImagery()
    {
        nonce = System.DateTime.UtcNow.Ticks.ToString();
        ApplyToOpenScenes();
    }

    private static void ApplyToOpenScenes()
    {
        for (int i = 0; i < SceneManager.sceneCount; i++)
            Apply(SceneManager.GetSceneAt(i), nonce);
    }

    private static void Apply(Scene scene, string key)
    {
        if (!scene.isLoaded)
            return;

        foreach (GameObject root in scene.GetRootGameObjects())
        {
            foreach (CesiumIonRasterOverlay overlay in root.GetComponentsInChildren<CesiumIonRasterOverlay>(true))
            {
                string defaultToken = (overlay.ionServer != null ? overlay.ionServer : CesiumIonServer.defaultServer).defaultIonAccessToken;
                string token = Strip(overlay.ionAccessToken);
                string original = token == defaultToken ? "" : token;
                string wanted = key == null ? original : (original == "" ? defaultToken : original) + Marker + key;
                if (overlay.ionAccessToken != wanted)
                    overlay.ionAccessToken = wanted;
            }
        }
    }

    private static string Strip(string token)
    {
        int i = token.IndexOf(Marker, System.StringComparison.Ordinal);
        return i < 0 ? token : token.Substring(0, i);
    }
}
