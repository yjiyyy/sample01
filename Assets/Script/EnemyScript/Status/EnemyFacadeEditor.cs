#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(EnemyFacade))]
public class EnemyFacadeEditor : Editor
{
    private EnemyFacade facade;
    private SerializedProperty configProp;
    private SerializedProperty autoSyncProp;
    private SerializedProperty targetEnemyProp;
    private SerializedProperty extraTargetsProp;

    private void OnEnable()
    {
        facade = (EnemyFacade)target;
        configProp = serializedObject.FindProperty("config");
        autoSyncProp = serializedObject.FindProperty("autoSync");
        targetEnemyProp = serializedObject.FindProperty("targetEnemy");
        extraTargetsProp = serializedObject.FindProperty("extraTargets");
    }

    public override void OnInspectorGUI()
    {
        serializedObject.Update();

        EditorGUILayout.PropertyField(configProp, new GUIContent("EnemyConfig (SO)"));
        EditorGUILayout.PropertyField(autoSyncProp, new GUIContent("Auto Sync (OnValidate/Awake)"));
        EditorGUILayout.PropertyField(targetEnemyProp, new GUIContent("Target Enemy (optional)"));
        EditorGUILayout.PropertyField(extraTargetsProp, true);

        EditorGUILayout.Space();

        if (facade.config != null)
        {
            // show selected config summary (trimmed)
            EditorGUILayout.LabelField("Config preview", EditorStyles.boldLabel);
            EditorGUILayout.LabelField("Name", facade.config.displayName);
            EditorGUILayout.LabelField("HP", facade.config.maxHealth.ToString());
            EditorGUILayout.LabelField("Move Speed", facade.config.baseMoveSpeed.ToString());
            if (facade.config.useShield)
            {
                EditorGUILayout.LabelField("Shield", $"{facade.config.maxShield} (break {facade.config.shieldBreakDuration}s)");
            }
            EditorGUILayout.Space();
        }
        else
        {
            EditorGUILayout.HelpBox("Assign an EnemyConfig ScriptableObject to apply to components.", MessageType.Info);
        }

        EditorGUILayout.BeginHorizontal();
        if (GUILayout.Button("Apply to Components"))
        {
            Undo.RecordObject(facade.gameObject, "EnemyFacade Apply");
            facade.ApplyToComponents();
            if (!Application.isPlaying)
                UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(facade.gameObject.scene);
        }

        if (GUILayout.Button("Revert (no-op)"))
        {
            Undo.RecordObject(facade.gameObject, "EnemyFacade Revert");
            facade.RevertFromComponents();
            if (!Application.isPlaying)
                UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(facade.gameObject.scene);
        }
        EditorGUILayout.EndHorizontal();

        EditorGUILayout.Space();

        EditorGUILayout.HelpBox("AutoSync will run ApplyToComponents in OnValidate (editor) and Awake (runtime). If you want to keep custom per-instance overrides, turn AutoSync off and use Apply manually.", MessageType.Info);

        serializedObject.ApplyModifiedProperties();
    }
}
#endif