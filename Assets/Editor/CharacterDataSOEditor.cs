using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(CharacterDataSO))]
public class CharacterDataSOEditor : Editor
{
    private readonly struct StatRow
    {
        public readonly string TierProperty;
        public readonly string Tier3Property;
        public readonly string PerTierProperty;
        public readonly string Label;

        public StatRow(string tierProperty, string tier3Property, string perTierProperty, string label)
        {
            TierProperty = tierProperty;
            Tier3Property = tier3Property;
            PerTierProperty = perTierProperty;
            Label = label;
        }
    }

    private static readonly StatRow[] StatRows =
    {
        new StatRow("hpTiers", "hpTier3Value", "hpPerTier", "HP"),
        new StatRow("stTiers", "staminaTier3Value", "staminaPerTier", "ST"),
        new StatRow("spdTiers", "speedTier3Value", "speedPerTier", "SPD"),
        new StatRow("strTiers", "strengthTier3Value", "strengthPerTier", "STR"),
        new StatRow("meleeAtkTiers", "meleeAttackTier3Value", "meleeAttackPerTier", "근접 ATK"),
        new StatRow("rangedAtkTiers", "rangedAttackTier3Value", "rangedAttackPerTier", "원거리 ATK")
    };

    public override void OnInspectorGUI()
    {
        serializedObject.Update();

        DrawProperty("m_Script", disabled: true);
        DrawProperty("portrait");
        DrawProperty("illustration");
        DrawProperty("displayName");

        EditorGUILayout.Space();
        EditorGUILayout.LabelField("스탯 표시 (1~5칸)", EditorStyles.boldLabel);
        int total = 0;
        foreach (StatRow row in StatRows)
        {
            total += DrawStatRow(row);
        }
        DrawStatTotal(total);

        EditorGUILayout.Space();
        EditorGUILayout.LabelField("초기 스탯 연동", EditorStyles.boldLabel);
        DrawProperty("playerConfig");
        DrawProperty("autoSyncPlayerConfig");

        CharacterDataSO data = (CharacterDataSO)target;
        DrawSyncMessages(data);

        using (new EditorGUI.DisabledScope(data.playerConfig == null || Application.isPlaying))
        {
            if (GUILayout.Button("지금 PlayerConfig에 적용"))
            {
                serializedObject.ApplyModifiedProperties();
                SyncPlayerConfig(data);
                serializedObject.Update();
            }
        }

        EditorGUILayout.Space();
        DrawProperty("description");
        DrawProperty("isLocked");
        DrawProperty("previewPrefab");
        DrawProperty("modelPrefab");

        bool changed = serializedObject.ApplyModifiedProperties();
        if (changed && data.autoSyncPlayerConfig && !Application.isPlaying)
            SyncPlayerConfig(data);
    }

    private int DrawStatRow(StatRow row)
    {
        SerializedProperty tier = serializedObject.FindProperty(row.TierProperty);
        SerializedProperty tier3 = serializedObject.FindProperty(row.Tier3Property);
        SerializedProperty perTier = serializedObject.FindProperty(row.PerTierProperty);

        EditorGUILayout.PropertyField(tier);

        float actualValue = Mathf.Max(
            row.TierProperty == "stTiers" ? 1f : 0f,
            tier3.floatValue + (Mathf.Clamp(tier.intValue, 1, 5) - 3) * perTier.floatValue);

        EditorGUI.indentLevel++;
        using (new EditorGUILayout.HorizontalScope())
        {
            EditorGUILayout.PropertyField(tier3, new GUIContent("3티어 값"));
            EditorGUILayout.PropertyField(perTier, new GUIContent("티어당"));
            using (new EditorGUI.DisabledScope(true))
                EditorGUILayout.FloatField(new GUIContent("실제값"), actualValue);
        }
        EditorGUI.indentLevel--;

        return tier.intValue;
    }

    private static void DrawStatTotal(int total)
    {
        using (new EditorGUI.DisabledScope(true))
        {
            EditorGUILayout.IntField(new GUIContent("스탯 합계", "현재 표시 스탯 6개의 합계입니다."), total);
        }
    }

    private void DrawProperty(string propertyName, bool disabled = false)
    {
        SerializedProperty property = serializedObject.FindProperty(propertyName);
        if (property == null)
            return;

        using (new EditorGUI.DisabledScope(disabled))
            EditorGUILayout.PropertyField(property, true);
    }

    private static void DrawSyncMessages(CharacterDataSO data)
    {
        if (Application.isPlaying)
        {
            EditorGUILayout.HelpBox("Play 모드에서는 원본 PlayerConfig를 자동으로 변경하지 않습니다.", MessageType.Info);
            return;
        }

        if (data.playerConfig == null)
        {
            EditorGUILayout.HelpBox("이 캐릭터에 대응하는 PlayerConfig를 연결하세요.", MessageType.Warning);
            return;
        }

        string[] characterGuids = AssetDatabase.FindAssets("t:CharacterDataSO");
        foreach (string guid in characterGuids)
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            CharacterDataSO other = AssetDatabase.LoadAssetAtPath<CharacterDataSO>(path);
            if (other != null && other != data && other.playerConfig == data.playerConfig)
            {
                EditorGUILayout.HelpBox(
                    $"'{other.name}'도 같은 PlayerConfig를 사용하고 있습니다. 캐릭터마다 Config를 하나씩 연결하는 것을 권장합니다.",
                    MessageType.Warning);
                break;
            }
        }
    }

    private static void SyncPlayerConfig(CharacterDataSO data)
    {
        if (data == null || data.playerConfig == null || Application.isPlaying)
            return;

        Undo.RecordObject(data.playerConfig, "CharacterDataSO 스탯 동기화");
        data.ApplyInitialStatsTo(data.playerConfig);
        EditorUtility.SetDirty(data.playerConfig);
    }
}
