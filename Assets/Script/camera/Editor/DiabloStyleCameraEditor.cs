#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(DiabloStyleCamera))]
public class DiabloStyleCameraEditor : Editor
{
    public override void OnInspectorGUI()
    {
        DrawDefaultInspector();

        EditorGUILayout.Space(4f);
        EditorGUILayout.HelpBox(
            "Pitch: 낮을수록 뒤에서, 90에 가까울수록 탑뷰\n" +
            "Distance: 캐릭터와 카메라 유지 거리\n" +
            "Dead Zone X: 화면 너비 비율 / Y: 화면 높이 비율 (0.05 = 5%)\n" +
            "Recenter To Screen Center: 켜면 존 밖 → 화면 정중앙까지 추적\n" +
            "Uniform Screen Dead Zone: X/Y 같을 때 픽셀 크기 맞춤 (가로만 축소)\n" +
            "Follow Point: 캐릭터 자식 빈 오브젝트를 넣으면 Dead Zone 판정 기준점",
            MessageType.Info);
    }

    private void OnSceneGUI()
    {
        var camera = (DiabloStyleCamera)target;
        if (!camera.ShowDeadZoneGizmo)
            return;

        Camera sceneCamera = camera.GetComponent<Camera>();
        if (sceneCamera == null)
            return;

        Vector3 anchor = camera.ResolveAnchorForGizmo();
        Vector3 camPos = anchor + camera.GetCameraOffset();

        Handles.color = new Color(0.3f, 0.8f, 1f, 0.9f);
        Handles.DrawLine(camPos, anchor);
        Handles.DrawWireDisc(anchor, Vector3.up, 0.25f);

        DrawScreenFrame(sceneCamera, anchor);

        if (!camera.TryGetDeadZoneGroundCorners(out Vector3 bl, out Vector3 br, out Vector3 tr, out Vector3 tl))
            return;

        Handles.color = new Color(1f, 0.85f, 0.1f, 0.25f);
        Handles.DrawAAConvexPolygon(bl, br, tr, tl);

        Handles.color = new Color(1f, 0.85f, 0.1f, 1f);
        Handles.DrawLine(bl, br);
        Handles.DrawLine(br, tr);
        Handles.DrawLine(tr, tl);
        Handles.DrawLine(tl, bl);

        Vector3 center = (bl + tr) * 0.5f;
        Handles.Label(center + Vector3.up * 0.35f, "Dead Zone");
    }

    private static void DrawScreenFrame(Camera sceneCamera, Vector3 anchor)
    {
        Vector3 viewport = sceneCamera.WorldToViewportPoint(anchor);
        float depth = viewport.z;
        if (depth <= 0f)
            return;

        float groundY = anchor.y;
        Vector3 fbl = ProjectToGround(sceneCamera.ViewportToWorldPoint(new Vector3(0f, 0f, depth)), groundY);
        Vector3 fbr = ProjectToGround(sceneCamera.ViewportToWorldPoint(new Vector3(1f, 0f, depth)), groundY);
        Vector3 ftr = ProjectToGround(sceneCamera.ViewportToWorldPoint(new Vector3(1f, 1f, depth)), groundY);
        Vector3 ftl = ProjectToGround(sceneCamera.ViewportToWorldPoint(new Vector3(0f, 1f, depth)), groundY);

        Handles.color = new Color(1f, 1f, 1f, 0.35f);
        Handles.DrawLine(fbl, fbr);
        Handles.DrawLine(fbr, ftr);
        Handles.DrawLine(ftr, ftl);
        Handles.DrawLine(ftl, fbl);
    }

    private static Vector3 ProjectToGround(Vector3 worldPoint, float groundY)
    {
        return new Vector3(worldPoint.x, groundY, worldPoint.z);
    }
}
#endif
