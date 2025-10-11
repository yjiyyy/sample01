#if UNITY_EDITOR
using UnityEditor;

[CustomEditor(typeof(WeaponDataSO_Shotgun))]
public class WeaponDataSO_ShotgunEditor : Editor
{
    public override void OnInspectorGUI()
    {
        serializedObject.Update();

        DrawHeader("무기 기본");
        Draw("weaponName");
        Draw("overrideController");
        Draw("cooldown");
        Draw("damage");

        EditorGUILayout.Space();
        DrawHeader("샷건(섹터)");
        Draw("shotgunRadius");
        Draw("shotgunAngle");
        Draw("shotgunUseDistanceFalloff");
        Draw("shotgunFalloffMin");
        Draw("shotgunDebugVisualize");
        Draw("shotgunDebugColor");
        Draw("shotgunDebugActualColor");

        EditorGUILayout.Space();
        DrawHeader("히트박스/타이밍");
        Draw("hitBoxLifetime");
        Draw("hitboxSpawnDelay");

        EditorGUILayout.Space();
        DrawHeader("넉백/스턴");
        Draw("knockbackPower");
        Draw("knockbackDuration");
        Draw("stunDuration");

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