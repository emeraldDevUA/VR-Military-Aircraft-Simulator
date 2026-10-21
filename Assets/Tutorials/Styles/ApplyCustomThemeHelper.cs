using System.Collections;
using UnityEngine;
#if UNITY_EDITOR
using Unity.EditorCoroutines.Editor;
using UnityEditor;

#endif

[CreateAssetMenu(fileName = "ApplyCustomThemeHelper", menuName = "Scriptable Objects/ApplyCustomThemeHelper")]
public class ApplyCustomThemeHelper : ScriptableObject
{
#if UNITY_EDITOR
    public void ApplyCustomTheme()
    {
        EditorCoroutineUtility.StartCoroutineOwnerless(WaitToApply());
    }

    IEnumerator WaitToApply()
    {
        yield return null;

        Unity.Tutorials.Editor.TutorialWindow.Instance.rootVisualElement.styleSheets.Add(EditorGUIUtility.isProSkin ?
            Unity.Tutorials.Editor.TutorialProjectSettings.Instance.TutorialStyle.DarkThemeStyleSheet :
            Unity.Tutorials.Editor.TutorialProjectSettings.Instance.TutorialStyle.LightThemeStyleSheet);
    }
#endif
}
