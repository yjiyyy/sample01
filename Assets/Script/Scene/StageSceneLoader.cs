using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// 스테이지 씬 로드 후 플레이어 스폰·스테이지 시작 처리.
/// 로비 진입·에디터에서 스테이지 씬 단독 Play 모두 지원.
/// </summary>
public static class StageSceneLoader
{
    public const string PlayerSpawnPointName = "PlayerSpawnPoint";

    public static bool IsLoading { get; private set; }

    internal static void SetLoading(bool value) => IsLoading = value;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void RegisterSceneLoaded()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
        SceneManager.sceneLoaded += OnSceneLoaded;
    }

    private static void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        if (!StageSceneNames.IsStageEnvironmentScene(scene.name))
            return;

        if (IsLoading)
            return;

        var runnerObject = new GameObject(nameof(StageSceneLoader));
        Object.DontDestroyOnLoad(runnerObject);
        runnerObject.AddComponent<StageSceneLoaderRunner>().Begin(scene);
    }

    public static StageData ResolveStageData(string stageSceneName)
    {
        var list = Resources.Load<StageListSO>("StageList");
        if (list == null || list.stages == null)
            return null;

        for (int i = 0; i < list.stages.Count; i++)
        {
            var info = list.stages[i];
            if (info != null && info.sceneName == stageSceneName)
                return info.stageData;
        }

        return null;
    }

    public static bool TryGetSpawnPose(Scene stageScene, out Vector3 position, out Quaternion rotation)
    {
        position = Vector3.zero;
        rotation = Quaternion.identity;

        if (!stageScene.IsValid())
            return false;

        foreach (var root in stageScene.GetRootGameObjects())
        {
            var spawnPoint = FindChildByName(root.transform, PlayerSpawnPointName);
            if (spawnPoint == null)
                continue;

            position = spawnPoint.position;
            rotation = spawnPoint.rotation;
            return true;
        }

        position = Vector3.zero;
        return true;
    }

    private static Transform FindChildByName(Transform parent, string childName)
    {
        if (parent.name == childName)
            return parent;

        for (int i = 0; i < parent.childCount; i++)
        {
            var found = FindChildByName(parent.GetChild(i), childName);
            if (found != null)
                return found;
        }

        return null;
    }
}

internal sealed class StageSceneLoaderRunner : MonoBehaviour
{
    public void Begin(Scene stageScene)
    {
        StageSceneLoader.SetLoading(true);
        StartCoroutine(CoBootstrap(stageScene));
    }

    private IEnumerator CoBootstrap(Scene stageScene)
    {
        yield return null;

        var spawnManager = SpawnManager.Instance;
        if (spawnManager == null)
            spawnManager = Object.FindFirstObjectByType<SpawnManager>();

        if (spawnManager == null)
        {
            Debug.LogError("[StageSceneLoader] SpawnManager를 찾을 수 없습니다. 스테이지 씬에 SpawnManager가 있는지 확인하세요.");
            Cleanup();
            yield break;
        }

        StageSceneLoader.TryGetSpawnPose(stageScene, out Vector3 spawnPos, out Quaternion spawnRot);
        var followCamera = StageFollowCamera.FindInScene(stageScene);

        if (followCamera == null)
            Debug.LogWarning("[StageSceneLoader] Main Camera에 DiabloStyleCamera가 없습니다. 스테이지 씬 Main Camera를 확인하세요.");

        spawnManager.SpawnInitialPlayer(spawnPos, spawnRot, followCamera);

        var stageManager = Object.FindFirstObjectByType<StageManager>();
        if (stageManager != null)
            stageManager.BeginStage(StageSceneLoader.ResolveStageData(stageScene.name));
        else
            Debug.LogWarning("[StageSceneLoader] StageManager를 찾을 수 없습니다.");

        if (GameManager.Instance != null)
            GameManager.Instance.AssignExistingPlayerUIs();

        Cleanup();
    }

    private void Cleanup()
    {
        StageSceneLoader.SetLoading(false);
        Destroy(gameObject);
    }
}
