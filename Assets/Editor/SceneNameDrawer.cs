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

        // 현재 string 값으로부터 SceneAsset 로드 (표시용)
        SceneAsset currentAsset = null;
        if (!string.IsNullOrEmpty(property.stringValue))
        {
            foreach (var s in EditorBuildSettings.scenes)
            {
                if (System.IO.Path.GetFileNameWithoutExtension(s.path) == property.stringValue)
                {
                    currentAsset = AssetDatabase.LoadAssetAtPath<SceneAsset>(s.path);
                    break;
                }
            }
        }

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
}
