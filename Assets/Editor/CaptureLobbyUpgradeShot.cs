using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 로비 업그레이드 화면을 카메라로 찍어서 PNG로 저장합니다.
/// Play를 켜지 않아도 됩니다.
/// 메뉴: Tools → Capture Lobby Upgrade Shot
/// </summary>
public static class CaptureLobbyUpgradeShot
{
    public const string OutputPath = "Logs/LobbyUpgrade/upgrade-preview.png";
    private const string ScenePath = "Assets/Scenes/03_Lobby.unity";
    private const string AutoRunFlagPath = "Assets/Editor/CaptureLobbyUpgradeShot.run";

    [InitializeOnLoadMethod]
    private static void AutoRunIfFlagExists()
    {
        EditorApplication.delayCall += () =>
        {
            if (!File.Exists(AutoRunFlagPath))
                return;
            try { File.Delete(AutoRunFlagPath); }
            catch { /* ignore */ }
            Run();
        };
    }

    [MenuItem("Tools/Capture Lobby Upgrade Shot")]
    public static void Run()
    {
        SetupLobbyUpgradePanel.ImportSprites();
        SetupLobbyUpgradePanel.CreateOrUpdateCatalog();
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);

        var camera = Camera.main;
        if (camera == null)
            camera = Object.FindFirstObjectByType<Camera>();
        if (camera == null)
            throw new System.Exception("[CaptureLobbyUpgradeShot] 카메라가 없습니다.");

        var canvasGo = GameObject.Find("LobbyCanvas");
        if (canvasGo == null)
            throw new System.Exception("[CaptureLobbyUpgradeShot] LobbyCanvas가 없습니다.");

        var canvas = canvasGo.GetComponent<Canvas>();
        GameObject spawned = SpawnPreviewCharacter();
        SetupLobbyShopPanel.RemoveOldPopupShop(canvasGo.transform);

        var panel = LobbyUpgradePanelBuilder.Build(canvasGo.GetComponent<RectTransform>());
        panel.Show();
        Canvas.ForceUpdateCanvases();

        var previousMode = canvas.renderMode;
        var previousCamera = canvas.worldCamera;
        var previousScaler = canvas.GetComponent<CanvasScaler>();
        float previousScale = previousScaler != null ? previousScaler.scaleFactor : 1f;

        try
        {
            canvas.renderMode = RenderMode.ScreenSpaceCamera;
            canvas.worldCamera = camera;
            canvas.planeDistance = Mathf.Max(0.5f, camera.nearClipPlane + 0.4f);
            if (previousScaler != null)
            {
                previousScaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
                previousScaler.referenceResolution = new Vector2(1920f, 1080f);
                previousScaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
                previousScaler.matchWidthOrHeight = 0.5f;
            }
            Canvas.ForceUpdateCanvases();
            Capture(camera, 1920, 1080, OutputPath);
            Debug.Log("[CaptureLobbyUpgradeShot] 저장: " + Path.GetFullPath(OutputPath));
        }
        finally
        {
            canvas.renderMode = previousMode;
            canvas.worldCamera = previousCamera;
            if (previousScaler != null)
                previousScaler.scaleFactor = previousScale;
            if (spawned != null)
                Object.DestroyImmediate(spawned);
            if (panel != null)
                panel.Hide();
        }
    }

    private static GameObject SpawnPreviewCharacter()
    {
        var spawn = GameObject.Find("CharacterSpawnPoint");
        if (spawn == null)
            return null;

        for (int i = 0; i < spawn.transform.childCount; i++)
        {
            if (spawn.transform.GetChild(i).name.StartsWith("Player_"))
                return null;
        }

        var lobby = Object.FindFirstObjectByType<LobbyController>();
        if (lobby == null)
            return null;

        var so = new SerializedObject(lobby);
        var data = so.FindProperty("fallbackCharacter").objectReferenceValue as CharacterDataSO;
        var prefab = data != null ? data.GetPreviewPrefab() : null;
        if (prefab == null)
            prefab = so.FindProperty("fallbackModelPrefab").objectReferenceValue as GameObject;
        if (prefab == null)
            return null;

        var instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
        instance.transform.SetParent(spawn.transform, false);
        instance.transform.SetPositionAndRotation(spawn.transform.position, spawn.transform.rotation);
        instance.name = "Player_CapturePreview";
        return instance;
    }

    private static void Capture(Camera camera, int width, int height, string relativePath)
    {
        var target = new RenderTexture(width, height, 24, RenderTextureFormat.ARGB32);
        var image = new Texture2D(width, height, TextureFormat.RGB24, false);
        var previousActive = RenderTexture.active;
        var previousTarget = camera.targetTexture;
        try
        {
            camera.targetTexture = target;
            camera.Render();
            RenderTexture.active = target;
            image.ReadPixels(new Rect(0, 0, width, height), 0, 0);
            image.Apply();
            Directory.CreateDirectory(Path.GetDirectoryName(relativePath) ?? "Logs");
            File.WriteAllBytes(relativePath, image.EncodeToPNG());
        }
        finally
        {
            camera.targetTexture = previousTarget;
            RenderTexture.active = previousActive;
            Object.DestroyImmediate(image);
            Object.DestroyImmediate(target);
        }
    }
}
