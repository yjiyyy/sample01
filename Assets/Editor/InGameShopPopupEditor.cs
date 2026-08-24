using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(InGameShopPopup))]
public class InGameShopPopupEditor : Editor
{
    public override void OnInspectorGUI()
    {
        DrawDefaultInspector();

        EditorGUILayout.Space();
        if (GUILayout.Button("목록 위 3장으로 미리보기"))
        {
            var popup = (InGameShopPopup)target;
            popup.RefreshCards();
            EditorUtility.SetDirty(popup);
            if (!Application.isPlaying)
                UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(popup.gameObject.scene);
        }
    }
}
