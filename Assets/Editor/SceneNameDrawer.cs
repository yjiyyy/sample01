using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;

[CustomPropertyDrawer(typeof(SceneNameAttribute))]
public class SceneNameDrawer : PropertyDrawer
{
    public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
    {
        if (property.propertyType != SerializedPropertyType.String)
        {
            EditorGUI.PropertyField(position, property, label);
            return;
        }

        SceneAsset currentAsset = FindSceneAsset(property.stringValue);

        var newAsset = EditorGUI.ObjectField(position, label, currentAsset, typeof(SceneAsset), false) as SceneAsset;
        if (newAsset != currentAsset)
        {
            if (newAsset != null)
            {
                var path = AssetDatabase.GetAssetPath(newAsset);
                property.stringValue = System.IO.Path.GetFileNameWithoutExtension(path);
            }
            else
            {
                property.stringValue = "";
            }
        }
    }

    private static SceneAsset FindSceneAsset(string sceneName)
    {
        if (string.IsNullOrEmpty(sceneName))
            return null;

        foreach (EditorBuildSettingsScene scene in EditorBuildSettings.scenes)
        {
            if (System.IO.Path.GetFileNameWithoutExtension(scene.path) == sceneName)
                return AssetDatabase.LoadAssetAtPath<SceneAsset>(scene.path);
        }

        // Build Profiles에 아직 넣지 않은 씬도 드래그한 상태가 Inspector에 유지되게 합니다.
        foreach (string guid in AssetDatabase.FindAssets($"{sceneName} t:Scene"))
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            if (System.IO.Path.GetFileNameWithoutExtension(path) == sceneName)
                return AssetDatabase.LoadAssetAtPath<SceneAsset>(path);
        }

        return null;
    }
}
