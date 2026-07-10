#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(OccluderBuildingVolume))]
public class OccluderBuildingVolumeEditor : Editor
{
    public override void OnInspectorGUI()
    {
        DrawDefaultInspector();

        EditorGUILayout.Space(4f);
        EditorGUILayout.HelpBox(
            "건물 루트(또는 자식)에 Trigger BoxCollider를 두고, 카메라가 들어올 실내 공간만큼 크기를 맞춥니다.\n" +
            "Mesh Collider(벽)는 그대로 두고, 이 볼륨은 페이드 판정 전용입니다.\n" +
            "Renderers Root: 페이드할 메쉬가 모인 부모 (보통 건물 루트).",
            MessageType.Info);

        if (GUILayout.Button("Renderer 목록 새로고침"))
        {
            foreach (Object t in targets)
            {
                if (t is OccluderBuildingVolume volume)
                    volume.RefreshRenderers();
            }
        }
    }
}
#endif
