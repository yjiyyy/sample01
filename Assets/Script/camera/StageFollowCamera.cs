using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// 스테이지 씬 Main Camera의 DiabloStyleCamera를 찾습니다.
/// </summary>
public static class StageFollowCamera
{
    public static DiabloStyleCamera FindInScene(Scene scene)
    {
        if (!scene.IsValid())
            return null;

        foreach (var root in scene.GetRootGameObjects())
        {
            var camera = root.GetComponentInChildren<DiabloStyleCamera>(true);
            if (camera != null)
                return camera;
        }

        return null;
    }
}
