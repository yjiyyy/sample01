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
        data.weaponCategory = (WeaponCategory)EditorGUILayout.EnumPopup("무기 분류", data.weaponCategory);

        EditorGUILayout.Space();
        EditorGUILayout.LabelField("애니메이션", EditorStyles.boldLabel);
        data.overrideController = (AnimatorOverrideController)EditorGUILayout.ObjectField("AOC", data.overrideController, typeof(AnimatorOverrideController), false);

        EditorGUILayout.Space();
        EditorGUILayout.LabelField("전투 관련", EditorStyles.boldLabel);
        data.cooldown = EditorGUILayout.FloatField("쿨타임", data.cooldown);
        data.damage = EditorGUILayout.FloatField("데미지", data.damage);
        data.range = EditorGUILayout.FloatField("사거리", data.range);
        data.projectileCount = EditorGUILayout.IntField("발사체 개수", data.projectileCount);
        data.isMelee = EditorGUILayout.Toggle("근접 무기 여부", data.isMelee);
        data.AutoAttackDelay = EditorGUILayout.FloatField("자동 공격 딜레이", data.AutoAttackDelay);
        data.hitBoxLifetime = EditorGUILayout.FloatField("히트박스 지속 시간", data.hitBoxLifetime);
        data.criticalChance = EditorGUILayout.FloatField("크리티컬 확률", data.criticalChance);
        data.aoeRadius = EditorGUILayout.FloatField("광역 반경", data.aoeRadius);

        EditorGUILayout.Space();
        EditorGUILayout.LabelField("히트박스 타이밍", EditorStyles.boldLabel);
        data.hitboxSpawnDelay = EditorGUILayout.FloatField("히트박스 생성 딜레이", data.hitboxSpawnDelay);

        EditorGUILayout.Space();
        EditorGUILayout.LabelField("넉백 / 저크", EditorStyles.boldLabel);
        data.knockbackDuration = EditorGUILayout.FloatField("넉백 지속 시간", data.knockbackDuration);
        data.knockbackPower = EditorGUILayout.FloatField("넉백 파워", data.knockbackPower);
        data.jerkIntensity = EditorGUILayout.FloatField("저크 강도", data.jerkIntensity);
        data.jerkDuration = EditorGUILayout.FloatField("저크 지속 시간", data.jerkDuration);

        EditorGUILayout.Space();
        EditorGUILayout.LabelField("스턴", EditorStyles.boldLabel);
        data.stunDuration = EditorGUILayout.FloatField("스턴 지속 시간", data.stunDuration);

        EditorGUILayout.Space();
        EditorGUILayout.LabelField("투사체", EditorStyles.boldLabel);
        data.projectileLifetime = EditorGUILayout.FloatField("수명", data.projectileLifetime);
        data.projectileSpeed = EditorGUILayout.FloatField("속도", data.projectileSpeed);
        data.pierceCount = EditorGUILayout.IntField("관통 횟수", data.pierceCount);
        data.isExplosiveProjectile = EditorGUILayout.Toggle("폭발성 여부", data.isExplosiveProjectile);
        data.explosiveRadius = EditorGUILayout.FloatField("폭발 반경", data.explosiveRadius);
        data.explosiveEdgeMul = EditorGUILayout.Slider("폭발 가장자리 배수", data.explosiveEdgeMul, 0f, 1f);

        EditorGUILayout.Space();
        EditorGUILayout.LabelField("부채꼴 감지", EditorStyles.boldLabel);
        data.viewAngle = EditorGUILayout.FloatField("시야각", data.viewAngle);
        data.viewDistance = EditorGUILayout.FloatField("시야 거리", data.viewDistance);

        EditorGUILayout.Space();
        EditorGUILayout.LabelField("랙돌 / 슬라이스", EditorStyles.boldLabel);
        data.ragdollImpulse = EditorGUILayout.FloatField("랙돌 임펄스", data.ragdollImpulse);
        data.upwardImpulse = EditorGUILayout.FloatField("상향 임펄스", data.upwardImpulse);
        data.torqueImpulse = EditorGUILayout.FloatField("토크 임펄스", data.torqueImpulse);
        data.sliceForce = EditorGUILayout.FloatField("슬라이스 힘", data.sliceForce);
        data.deathType = (EnemyDeathType)EditorGUILayout.EnumPopup("죽음 타입", data.deathType);

        SerializedProperty slicePartsProp = serializedObject.FindProperty("possibleSliceParts");
        EditorGUILayout.PropertyField(slicePartsProp, new GUIContent("절단 가능한 부위"), true);

        EditorGUILayout.Space();
        EditorGUILayout.LabelField("샷건", EditorStyles.boldLabel);
        data.shotgunRadius = EditorGUILayout.FloatField("샷건 반경", data.shotgunRadius);
        data.shotgunAngle = EditorGUILayout.Slider("샷건 각도", data.shotgunAngle, 1f, 360f);
        data.shotgunUseDistanceFalloff = EditorGUILayout.Toggle("거리 감쇠 사용", data.shotgunUseDistanceFalloff);
        data.shotgunFalloffMin = EditorGUILayout.Slider("최소 감쇠 배율", data.shotgunFalloffMin, 0f, 1f);
        data.shotgunDebugVisualize = EditorGUILayout.Toggle("디버그 시각화", data.shotgunDebugVisualize);
        data.shotgunDebugColor = EditorGUILayout.ColorField("디버그 색상", data.shotgunDebugColor);
        data.shotgunDebugActualColor = EditorGUILayout.ColorField("실제 판정 색상", data.shotgunDebugActualColor);

        EditorGUILayout.Space();
        EditorGUILayout.LabelField("데미지 판정 대상", EditorStyles.boldLabel);
        data.damageTargetType = (DamageTargetType)EditorGUILayout.EnumPopup("타겟 타입", data.damageTargetType);

        if (GUI.changed)
        {
            EditorUtility.SetDirty(data);
        }

        serializedObject.ApplyModifiedProperties();
    }
}
