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
            "Play 전 Scene/Prefab 화면에서 Head·Hair를 보려면 미리보기를 켜 두세요.\n" +
            "미리보기 오브젝트는 프리팹에 저장되지 않습니다.",
            MessageType.None);

        using (new EditorGUILayout.HorizontalScope())
        {
            if (GUILayout.Button("미리보기 갱신"))
                slots.RefreshEditorPreview();

            if (GUILayout.Button("미리보기 제거"))
                slots.ClearEditorPreview();
        }
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
