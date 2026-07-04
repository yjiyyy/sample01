#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(EnemyBodyPartSlots))]
public class EnemyBodyPartSlotsEditor : Editor
{
    // 각 톤 버튼 색 (미리보기용)
    private static readonly Color ColorAmerican = new Color(240f / 255f, 209f / 255f, 178f / 255f, 1f); // 원본
    private static readonly Color ColorAsian    = new Color(0xFF / 255f, 0xC1 / 255f, 0x86 / 255f, 1f); // #FFC186
    private static readonly Color ColorAfrican  = new Color(0x43 / 255f, 0x1D / 255f, 0x00 / 255f, 1f); // #431D00

    public override void OnInspectorGUI()
    {
        DrawDefaultInspector();

        var slots = (EnemyBodyPartSlots)target;
        if (slots == null) return;

        EditorGUILayout.Space();
        EditorGUILayout.LabelField("피부 톤 프리셋", EditorStyles.boldLabel);

        if (!Application.isPlaying)
        {
            using (new EditorGUILayout.HorizontalScope())
            {
                DrawToneButton(slots, ColorAmerican, "American", "원본색 (텍스처 컬러 그대로)");
                DrawToneButton(slots, ColorAsian,    "Asian",    "#FFC186");
                DrawToneButton(slots, ColorAfrican,  "African",  "#431D00");
            }
        }

        EditorGUILayout.Space();
        EditorGUILayout.LabelField("피부 마스크 (B 방식)", EditorStyles.boldLabel);
        EditorGUILayout.HelpBox(
            "Body 알베도에서 #F0D1B2(240,209,178)에 가까운 픽셀을 찾아 마스크 PNG를 만듭니다.\n" +
            "bodySkinMaterial에 Mat_PC_*를 넣으면 가장 확실합니다. 비우면 M_Body 렌더러·프리팹 이름으로 자동 탐색합니다.\n" +
            "톤 프리셋 선택 → 피부 마스크 Bake → 피부색 적용 순으로 진행하세요.",
            MessageType.Info);

        if (!Application.isPlaying)
        {
            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("피부 마스크 Bake"))
                {
                    if (EnemyBodySkinMaskBaker.TryBakeForEnemyBodyPartSlots(
                            slots, slots.maskBakeColorThreshold, out string msg))
                    {
                        Debug.Log($"[EnemyBodyPartSlots] {msg}");
                        slots.ApplyBodySkinTint();
                        EditorUtility.SetDirty(slots);
                    }
                    else
                    {
                        Debug.LogWarning($"[EnemyBodyPartSlots] {msg}");
                    }
                }

                if (GUILayout.Button("피부색 적용"))
                    slots.ApplyBodySkinTint();
            }
        }

        EditorGUILayout.Space();
        EditorGUILayout.LabelField("에디터 미리보기", EditorStyles.boldLabel);

        if (Application.isPlaying)
        {
            EditorGUILayout.HelpBox("Play 중에는 런타임으로 파츠가 붙습니다.", MessageType.Info);
            return;
        }

        EditorGUILayout.HelpBox(
            "Head·Hair는 자동으로 붙지 않습니다. 씬에 배치한 적을 선택한 뒤 「미리보기 갱신」을 누르세요.\n" +
            "Project 프리팹·Prefab Mode에서는 미리보기할 수 없습니다.\n" +
            "미리보기 오브젝트는 프리팹·씬에 저장되지 않습니다.",
            MessageType.Info);

        bool canPreview = slots.previewInEditor && slots.gameObject.scene.IsValid();

        using (new EditorGUILayout.HorizontalScope())
        {
            using (new EditorGUI.DisabledScope(!canPreview))
            {
                if (GUILayout.Button("미리보기 갱신"))
                    slots.RefreshEditorPreview();
            }

            if (GUILayout.Button("미리보기 제거"))
                slots.ClearEditorPreview();
        }

        if (!slots.previewInEditor)
        {
            EditorGUILayout.HelpBox("previewInEditor를 켜야 미리보기 버튼을 사용할 수 있습니다.", MessageType.None);
        }
        else if (!slots.gameObject.scene.IsValid())
        {
            EditorGUILayout.HelpBox("씬에 배치된 적 인스턴스에서만 미리보기할 수 있습니다.", MessageType.None);
        }

        EditorGUILayout.Space();
        if (GUILayout.Button("씬 고아 미리보기 전부 제거"))
            EnemyBodyPartSlots.ClearAllEditorPreviewOrphansInOpenScenes();
    }

    private static void DrawToneButton(EnemyBodyPartSlots slots, Color toneColor, string label, string tooltip)
    {
        var prevBg = GUI.backgroundColor;
        GUI.backgroundColor = toneColor;

        var style = new GUIStyle(GUI.skin.button);
        style.richText = true;

        if (GUILayout.Button(new GUIContent($"<b>{label}</b>", tooltip), style))
        {
            Undo.RecordObject(slots, "Set Skin Tone Color");
            slots.bodySkinColor = toneColor;
            slots.ApplyBodySkinTint();
            EditorUtility.SetDirty(slots);
        }

        GUI.backgroundColor = prevBg;
    }
}
#endif
