#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(WeaponDataSO_Gun))]
public class WeaponDataSO_GunEditor : Editor
{
    public override void OnInspectorGUI()
    {
        serializedObject.Update();

        DrawHeader("무기 기본");
        Draw("weaponName");
        Draw("overrideController");
        Draw("cooldown");
        Draw("damage");
        Draw("range");

        EditorGUILayout.Space();
        DrawHeader("투사체");
        Draw("projectileCount");
        Draw("projectileSpeed");
        Draw("projectileLifetime");
        Draw("pierceCount");

        EditorGUILayout.Space();
        DrawHeader("히트박스/타이밍");
        Draw("hitboxSpawnDelay");

        EditorGUILayout.Space();
        DrawHeader("넉백/저크/스턴");
        Draw("knockbackPower");
        Draw("knockbackDuration");
        Draw("stunDuration");
        Draw("jerkIntensity");
        Draw("jerkDuration");

        EditorGUILayout.Space();
        DrawHeader("시야(FOV 표시용, 선택)");
        Draw("viewAngle");
        Draw("viewDistance");

        EditorGUILayout.Space();
        DrawHeader("처치 연출");
        Draw("deathType");
        Draw("ragdollImpulse");
        Draw("upwardImpulse");
        Draw("torqueImpulse");
        Draw("sliceForce");
        EditorGUILayout.PropertyField(serializedObject.FindProperty("possibleSliceParts"), true);

        EditorGUILayout.Space();
        DrawHeader("기타(읽기전용 상태)");
        EditorGUI.BeginDisabledGroup(true);
        EditorGUILayout.EnumPopup("weaponCategory", WeaponCategory.Gun);
        EditorGUILayout.Toggle("isMelee", false);
        EditorGUILayout.Toggle("isExplosiveProjectile", false);
        EditorGUI.EndDisabledGroup();

        serializedObject.ApplyModifiedProperties();
    }

    private void Draw(string name) => EditorGUILayout.PropertyField(serializedObject.FindProperty(name));
    private void DrawHeader(string label) => EditorGUILayout.LabelField(label, EditorStyles.boldLabel);
}
#endif