#if UNITY_EDITOR
using UnityEditor;

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
        DrawHeader("프로젝타일");
        Draw("projectileSpeed");
        Draw("projectileLifetime");
        Draw("pierceCount");

        EditorGUILayout.Space();
        DrawHeader("조준 스캔");
        Draw("aimScanAngle");
        Draw("aimScanDistance");

        EditorGUILayout.Space();
        DrawHeader("탄약 / 리로드");
        Draw("usesAmmo");
        if (serializedObject.FindProperty("usesAmmo").boolValue)
        {
            Draw("magazineSize");
            Draw("initialReserve");
            Draw("infiniteReserve");
            Draw("reloadTime");
            Draw("consumePerShot");
            Draw("autoReloadOnEmpty");
        }

        EditorGUILayout.Space();
        DrawHeader("히트박스/타이밍");
        Draw("hitboxSpawnDelay");
        Draw("hitBoxLifetime");

        EditorGUILayout.Space();
        DrawHeader("넉백/저크/스턴");
        Draw("knockbackPower");
        Draw("knockbackDuration");
        Draw("stunDuration");
        Draw("jerkIntensity");
        Draw("jerkDuration");

        EditorGUILayout.Space();
        DrawHeader("처치 연출");
        Draw("deathType");
        Draw("ragdollImpulse");
        Draw("upwardImpulse");
        Draw("torqueImpulse");
        Draw("sliceForce");
        EditorGUILayout.PropertyField(serializedObject.FindProperty("possibleSliceParts"), true);

        serializedObject.ApplyModifiedProperties();
    }

    private void Draw(string name) => EditorGUILayout.PropertyField(serializedObject.FindProperty(name));
    private void DrawHeader(string label) => EditorGUILayout.LabelField(label, EditorStyles.boldLabel);
}
#endif