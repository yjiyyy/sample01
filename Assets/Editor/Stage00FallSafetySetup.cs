using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

public static class Stage00FallSafetySetup
{
    private const string StagePath = "Assets/Scenes/Stage/Stage00.unity";

    [MenuItem("Tools/Stage/Stage00 낙하 안전 설정 적용")]
    public static void Apply()
    {
        Scene scene = EditorSceneManager.OpenScene(StagePath, OpenSceneMode.Single);
        StageManager manager = Object.FindFirstObjectByType<StageManager>();
        if (manager == null)
        {
            Debug.LogError("[Stage00FallSafetySetup] Stage00에서 StageManager를 찾지 못했습니다.");
            return;
        }

        SerializedObject serializedManager = new SerializedObject(manager);
        serializedManager.FindProperty("killY").floatValue = -10f;
        serializedManager.FindProperty("fallCheckGraceSeconds").floatValue = 1f;
        serializedManager.ApplyModifiedPropertiesWithoutUndo();

        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        Debug.Log("[Stage00FallSafetySetup] Kill Y=-10, 스폰 유예=1초 적용 완료.");
    }
}
