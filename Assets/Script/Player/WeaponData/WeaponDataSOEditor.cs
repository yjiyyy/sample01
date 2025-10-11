using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(WeaponDataSO))]
public class WeaponDataSOEditor : Editor
{
    public override void OnInspectorGUI()
    {
        WeaponDataSO data = (WeaponDataSO)target;
        serializedObject.Update();

        EditorGUILayout.Space();
        EditorGUILayout.LabelField("무기 기본 정보", EditorStyles.boldLabel);
        data.weaponName = EditorGUILayout.TextField("무기 이름", data.weaponName);

        EditorGUILayout.Space();
        EditorGUILayout.LabelField("애니메이션", EditorStyles.boldLabel);
        data.overrideController = (AnimatorOverrideController)EditorGUILayout.ObjectField("AOC", data.overrideController, typeof(AnimatorOverrideController), false);

        EditorGUILayout.Space();
        EditorGUILayout.LabelField("전투 관련", EditorStyles.boldLabel);
        data.cooldown = EditorGUILayout.FloatField("쿨타임", data.cooldown);
        data.damage = EditorGUILayout.FloatField("데미지", data.damage);
        data.range = EditorGUILayout.FloatField("사거리", data.range);

        EditorGUILayout.Space();
        EditorGUILayout.LabelField("히트박스", EditorStyles.boldLabel);
        data.hitboxSpawnDelay = EditorGUILayout.FloatField("생성 딜레이", data.hitboxSpawnDelay);
        data.hitBoxLifetime = EditorGUILayout.FloatField("지속 시간", data.hitBoxLifetime);

        EditorGUILayout.Space();
        EditorGUILayout.LabelField("넉백 / 저크", EditorStyles.boldLabel);
        data.knockbackDuration = EditorGUILayout.FloatField("넉백 지속", data.knockbackDuration);
        data.knockbackPower = EditorGUILayout.FloatField("넉백 파워", data.knockbackPower);
        data.jerkIntensity = EditorGUILayout.FloatField("저크 강도", data.jerkIntensity);
        data.jerkDuration = EditorGUILayout.FloatField("저크 지속", data.jerkDuration);

        EditorGUILayout.Space();
        EditorGUILayout.LabelField("스턴", EditorStyles.boldLabel);
        data.stunDuration = EditorGUILayout.FloatField("스턴 지속", data.stunDuration);

        EditorGUILayout.Space();
        EditorGUILayout.LabelField("랙돌 / 슬라이스", EditorStyles.boldLabel);
        data.ragdollImpulse = EditorGUILayout.FloatField("랙돌 임펄스", data.ragdollImpulse);
        data.upwardImpulse = EditorGUILayout.FloatField("상향 임펄스", data.upwardImpulse);
        data.torqueImpulse = EditorGUILayout.FloatField("토크 임펄스", data.torqueImpulse);
        data.sliceForce = EditorGUILayout.FloatField("슬라이스 힘", data.sliceForce);
        data.deathType = (EnemyDeathType)EditorGUILayout.EnumPopup("죽음 타입", data.deathType);

        SerializedProperty slicePartsProp = serializedObject.FindProperty("possibleSliceParts");
        EditorGUILayout.PropertyField(slicePartsProp, new GUIContent("절단 가능한 부위"), true);

        if (GUI.changed)
        {
            EditorUtility.SetDirty(data);
        }

        serializedObject.ApplyModifiedProperties();
    }
}