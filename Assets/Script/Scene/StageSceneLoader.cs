using System.Collections;
using Unity.Cinemachine;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Stage_Core + 배경 씬 Additive 로드 및 스테이지 시작 처리.
/// 로비 진입·에디터에서 아트 씬 단독 Play 모두 지원.
/// </summary>
public static class StageSceneLoader
{
    public const string PlayerSpawnPointName = "PlayerSpawnPoint";

    public static bool IsLoading { get; private set; }

    internal static void SetLoading(bool value) => IsLoading = value;

    public static void LoadStage(string environmentSceneName)
    {
        if (string.IsNullOrEmpty(environmentSceneName))
        {
            Debug.LogError("[StageSceneLoader] environmentSceneName이 비어 있습니다.");
            return;
        }

        if (!StageSceneNames.IsStageEnvironmentScene(environmentSceneName))
        {
            Debug.LogError($"[StageSceneLoader] 스테이지 배경 씬이 아닙니다: {environmentSceneName}");
            return;
        }

        var runnerObject = new GameObject(nameof(StageSceneLoader));
        Object.DontDestroyOnLoad(runnerObject);
        runnerObject.AddComponent<StageSceneLoaderRunner>()
            .BeginFromLobby(environmentSceneName, ResolveStageData(environmentSceneName));
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void AutoBootstrapWhenPlayingEnvironmentScene()
    {
        var activeScene = SceneManager.GetActiveScene();
        if (!StageSceneNames.IsStageEnvironmentScene(activeScene.name))
            return;

        if (IsLoading || IsCoreSceneLoaded())
            return;

        var runnerObject = new GameObject(nameof(StageSceneLoader));
        Object.DontDestroyOnLoad(runnerObject);
        runnerObject.AddComponent<StageSceneLoaderRunner>()
            .BeginFromEditorPlay(activeScene);
    }

    public static bool IsCoreSceneLoaded()
    {
        return SceneManager.GetSceneByName(StageSceneNames.Core).isLoaded;
    }

    public static StageData ResolveStageData(string environmentSceneName)
    {
        var list = Resources.Load<StageListSO>("StageList");
        if (list == null || list.stages == null)
            return null;

        for (int i = 0; i < list.stages.Count; i++)
        {
            var info = list.stages[i];
            if (info != null && info.sceneName == environmentSceneName)
                return info.stageData;
        }

        return null;
    }

    public static bool TryGetSpawnPose(Scene environmentScene, out Vector3 position, out Quaternion rotation)
    {
        position = Vector3.zero;
        rotation = Quaternion.identity;

        if (!environmentScene.IsValid())
            return false;

        foreach (var root in environmentScene.GetRootGameObjects())
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

    public static CinemachineCamera FindFollowCameraInScene(Scene scene)
    {
        if (!scene.IsValid())
            return null;

        foreach (var root in scene.GetRootGameObjects())
        {
            var camera = root.GetComponentInChildren<CinemachineCamera>(true);
            if (camera != null)
                return camera;
        }

        return null;
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
    public void BeginFromLobby(string environmentSceneName, StageData stageData)
    {
        StageSceneLoader.SetLoading(true);
        StartCoroutine(CoLoadFromLobby(environmentSceneName, stageData));
    }

    public void BeginFromEditorPlay(Scene environmentScene)
    {
        StageSceneLoader.SetLoading(true);
        StartCoroutine(CoLoadFromEditorPlay(environmentScene));
    }

    private IEnumerator CoLoadFromLobby(string environmentSceneName, StageData stageData)
    {
        yield return SceneManager.LoadSceneAsync(StageSceneNames.Core, LoadSceneMode.Single);
        yield return SceneManager.LoadSceneAsync(environmentSceneName, LoadSceneMode.Additive);

        var environmentScene = SceneManager.GetSceneByName(environmentSceneName);
        if (!environmentScene.IsValid())
        {
            Debug.LogError($"[StageSceneLoader] 배경 씬 로드 실패: {environmentSceneName}");
            Cleanup();
            yield break;
        }

        SceneManager.SetActiveScene(environmentScene);
        yield return FinalizeStageStart(environmentScene, stageData);
        Cleanup();
    }

    private IEnumerator CoLoadFromEditorPlay(Scene environmentScene)
    {
        if (!StageSceneLoader.IsCoreSceneLoaded())
            yield return SceneManager.LoadSceneAsync(StageSceneNames.Core, LoadSceneMode.Additive);

        if (environmentScene.IsValid())
            SceneManager.SetActiveScene(environmentScene);

        yield return null;
        yield return FinalizeStageStart(environmentScene, StageSceneLoader.ResolveStageData(environmentScene.name));
        Cleanup();
    }

    private IEnumerator FinalizeStageStart(Scene environmentScene, StageData stageData)
    {
        yield return null;

        var spawnManager = SpawnManager.Instance;
        if (spawnManager == null)
            spawnManager = Object.FindFirstObjectByType<SpawnManager>();

        if (spawnManager == null)
        {
            Debug.LogError("[StageSceneLoader] SpawnManager를 찾을 수 없습니다. Stage_Core 씬을 확인하세요.");
            Cleanup();
            yield break;
        }

        StageSceneLoader.TryGetSpawnPose(environmentScene, out Vector3 spawnPos, out Quaternion spawnRot);
        var followCamera = StageSceneLoader.FindFollowCameraInScene(environmentScene);

        spawnManager.SpawnInitialPlayer(spawnPos, spawnRot, followCamera);

        var stageManager = Object.FindFirstObjectByType<StageManager>();
        if (stageManager != null)
            stageManager.BeginStage(stageData);
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
