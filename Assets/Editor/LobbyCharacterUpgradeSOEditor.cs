using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(LobbyCharacterUpgradeSO))]
public class LobbyCharacterUpgradeSOEditor : Editor
{
    public override void OnInspectorGUI()
    {
        serializedObject.Update();
        EditorGUILayout.HelpBox("기본 능력은 대상 캐릭터의 PlayerConfig에서 읽습니다. 구매 기록은 캐릭터별로 따로 저장됩니다. 막대 끝은 최종 최대값입니다.", MessageType.Info);
        EditorGUILayout.PropertyField(serializedObject.FindProperty("character"));
        EditorGUILayout.PropertyField(serializedObject.FindProperty("stats"), true);
        serializedObject.ApplyModifiedProperties();
        var profile = (LobbyCharacterUpgradeSO)target;
        if (profile.Config == null)
        {
            EditorGUILayout.HelpBox("대상 캐릭터와 그 캐릭터의 PlayerConfig를 연결하세요.", MessageType.Error);
            return;
        }
        var catalog = LobbyStatUpgradeCatalogSO.Load();
        if (catalog == null || catalog.GetCharacter(profile.character) != profile)
            EditorGUILayout.HelpBox("공용 LobbyStatUpgradeCatalog의 캐릭터 목록에 이 SO를 등록하세요. 같은 캐릭터는 한 번만 등록합니다.", MessageType.Warning);

        var seen = new HashSet<LobbyStatId>();
        if (profile.stats != null)
            foreach (var entry in profile.stats)
            {
                if (entry == null) { EditorGUILayout.HelpBox("목록에 비어 있는 항목이 있습니다.", MessageType.Warning); continue; }
                if (!seen.Add(entry.id)) EditorGUILayout.HelpBox(entry.id + ": 같은 능력치가 중복되어 있습니다.", MessageType.Error);
                float basis = LobbyStatUpgradeApplier.GetConfigBase(entry, profile.Config);
                float reachable = basis + Mathf.Max(0, entry.maxLevel) * Mathf.Max(0f, entry.perLevel);
                EditorGUILayout.LabelField(entry.id + " 기본 / 최대", $"{basis:0.##} / {entry.maxValue:0.##}");
                if (entry.maxValue < basis || entry.perLevel <= 0f || entry.maxLevel < 0 || entry.baseCost < 0 || entry.costPerLevel < 0)
                    EditorGUILayout.HelpBox(entry.id + ": 최대값은 기본값 이상, 증가량은 0보다 크게, 횟수와 가격은 0 이상으로 설정하세요.", MessageType.Error);
                else if (Mathf.Abs(reachable - entry.maxValue) > 0.0001f)
                    EditorGUILayout.HelpBox($"{entry.id}: {entry.maxLevel}회 강화로 계산한 값은 {reachable:0.##}입니다. 현재 상한 설정에서는 실제 {LobbyStatUpgradeApplier.GetEffectiveMaxLevel(entry, profile.Config)}회 강화됩니다. 아래 버튼으로 횟수와 막대 끝을 맞출 수 있습니다.", MessageType.Warning);
            }
        if (GUILayout.Button("최대값을 기본값 + 증가량 × 횟수로 맞추기"))
        {
            Undo.RecordObject(profile, "업그레이드 최대값 맞추기");
            foreach (var entry in profile.stats)
                if (entry != null) entry.maxValue = LobbyStatUpgradeApplier.GetConfigBase(entry, profile.Config) + Mathf.Max(0, entry.maxLevel) * Mathf.Max(0f, entry.perLevel);
            EditorUtility.SetDirty(profile);
        }
        if (GUILayout.Button("열린 로비의 업그레이드 미리보기 갱신")) SetupLobbyUpgradePanel.Setup();
    }
}

[CustomPropertyDrawer(typeof(LobbyStatUpgradeEntry))]
public class LobbyStatUpgradeEntryDrawer : PropertyDrawer
{
    private static readonly string[] Fields = { "id", "displayName", "icon", "perLevel", "maxLevel", "maxValue", "baseCost", "costPerLevel", "selectLine" };
    private static readonly string[] Labels = { "능력 종류", "표시 이름", "아이콘", "1회 증가량", "최대 강화 횟수", "최종 최대값 (막대 끝)", "첫 구매 가격", "회당 가격 증가", "선택 시 NPC 대사" };
    public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
    {
        float height = EditorGUIUtility.singleLineHeight;
        if (property.isExpanded)
            foreach (string field in Fields) height += EditorGUI.GetPropertyHeight(property.FindPropertyRelative(field), true) + 3f;
        return height;
    }
    public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
    {
        EditorGUI.BeginProperty(position, label, property);
        position.height = EditorGUIUtility.singleLineHeight;
        var id = property.FindPropertyRelative("id");
        property.isExpanded = EditorGUI.Foldout(position, property.isExpanded, id.enumDisplayNames[id.enumValueIndex], true);
        if (property.isExpanded)
        {
            EditorGUI.indentLevel++;
            for (int i = 0; i < Fields.Length; i++)
            {
                position.y += position.height + 3f;
                var child = property.FindPropertyRelative(Fields[i]);
                position.height = EditorGUI.GetPropertyHeight(child, true);
                EditorGUI.PropertyField(position, child, new GUIContent(Labels[i]), true);
            }
            EditorGUI.indentLevel--;
        }
        EditorGUI.EndProperty();
    }
}
