using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

/// <summary>
/// 로비에 뒤 벽·경계 안개·단색/사진 재질을 넣고, 조명 프리셋을 연결합니다.
/// 메뉴: Tools → Setup Lobby Environment
/// </summary>
public static class SetupLobbyEnvironment
{
    private const string MenuPath = "Tools/Setup Lobby Environment";
    private const string ScenePath = "Assets/Scenes/03_Lobby.unity";
    private const string AutoRunFlagPath = "Assets/Editor/SetupLobbyEnvironment.run";
    private const string FolderPath = "Assets/Arts/Lobby";
    private const string FloorMatPath = FolderPath + "/Mat_LobbyFloor.mat";
    private const string BackdropMatPath = FolderPath + "/Mat_LobbyBackdrop.mat";
    private const string MistMatPath = FolderPath + "/Mat_LobbyHorizonMist.mat";

    [InitializeOnLoadMethod]
    private static void AutoRunIfFlagExists()
    {
        EditorApplication.delayCall += () =>
        {
            if (!File.Exists(AutoRunFlagPath))
                return;

            try { File.Delete(AutoRunFlagPath); }
            catch { /* ignore */ }

            Setup();
        };
    }

    [MenuItem(MenuPath)]
    public static void Setup()
    {
        var scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
        ApplyToOpenScene();
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        AssetDatabase.SaveAssets();
        Debug.Log("[SetupLobbyEnvironment] 로비 배경(뒤 벽 + 경계 안개 + 조명 프리셋) 구성 완료. 플레이 중 1/2/3 키로 테스트하세요.");
    }

    public static void ApplyToOpenScene()
    {
        EnsureMaterials(out var floorMat, out var backdropMat, out var mistMat);

        var floor = GameObject.Find("LobbyFloor");
        if (floor != null)
        {
            floor.transform.localScale = new Vector3(4f, 1f, 4f);
            var floorRenderer = floor.GetComponent<Renderer>();
            if (floorRenderer != null)
                floorRenderer.sharedMaterial = floorMat;
            EditorUtility.SetDirty(floor);
        }

        var backdrop = EnsureBackdrop(backdropMat);
        var mist = EnsureMist(mistMat);
        var env = EnsureEnvironment(floor, backdrop, mist);
        env.ApplyPreset(0, log: false);
        EditorUtility.SetDirty(env);
    }

    private static void EnsureMaterials(out Material floorMat, out Material backdropMat, out Material mistMat)
    {
        if (!AssetDatabase.IsValidFolder("Assets/Arts"))
            AssetDatabase.CreateFolder("Assets", "Arts");
        if (!AssetDatabase.IsValidFolder(FolderPath))
            AssetDatabase.CreateFolder("Assets/Arts", "Lobby");

        var litShader = Shader.Find("Universal Render Pipeline/Lit");
        if (litShader == null)
            litShader = Shader.Find("Universal Render Pipeline/Simple Lit");

        floorMat = LoadOrCreateLit(FloorMatPath, litShader, new Color(0.34f, 0.33f, 0.32f), 0.08f);

        var backdropShader = Shader.Find("Custom/Lobby/Backdrop");
        if (backdropShader == null)
            Debug.LogError("[SetupLobbyEnvironment] Custom/Lobby/Backdrop 셰이더를 찾지 못했습니다.");
        backdropMat = LoadOrCreateCustom(BackdropMatPath, backdropShader, new Color(0.54f, 0.56f, 0.60f));

        var mistShader = Shader.Find("Custom/Lobby/HorizonMist");
        if (mistShader == null)
            Debug.LogError("[SetupLobbyEnvironment] Custom/Lobby/HorizonMist 셰이더를 찾지 못했습니다.");
        mistMat = LoadOrCreateCustom(MistMatPath, mistShader, new Color(0.62f, 0.64f, 0.68f, 0.65f));
    }

    private static Material LoadOrCreateLit(string path, Shader shader, Color color, float smoothness)
    {
        var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (mat == null)
        {
            mat = new Material(shader);
            mat.SetColor("_BaseColor", color);
            mat.SetFloat("_Smoothness", smoothness);
            mat.SetFloat("_Metallic", 0f);
            mat.EnableKeyword("_EMISSION");
            mat.globalIlluminationFlags = MaterialGlobalIlluminationFlags.BakedEmissive;
            mat.SetColor("_EmissionColor", Color.black);
            AssetDatabase.CreateAsset(mat, path);
        }
        else
        {
            if (shader != null)
                mat.shader = shader;
            mat.SetFloat("_Smoothness", smoothness);
            mat.SetFloat("_Metallic", 0f);
            mat.EnableKeyword("_EMISSION");
            mat.globalIlluminationFlags = MaterialGlobalIlluminationFlags.BakedEmissive;
            EditorUtility.SetDirty(mat);
        }

        return mat;
    }

    private static Material LoadOrCreateCustom(string path, Shader shader, Color color)
    {
        var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (mat == null)
        {
            mat = shader != null ? new Material(shader) : new Material(Shader.Find("Hidden/InternalErrorShader"));
            mat.SetColor("_BaseColor", color);
            mat.SetColor("_Color", color);
            AssetDatabase.CreateAsset(mat, path);
        }
        else if (shader != null && mat.shader != shader)
        {
            mat.shader = shader;
            mat.SetColor("_BaseColor", color);
            mat.SetColor("_Color", color);
            EditorUtility.SetDirty(mat);
        }

        return mat;
    }

    private static GameObject EnsureBackdrop(Material mat)
    {
        var go = GameObject.Find("LobbyBackdrop");
        if (go == null)
            go = GameObject.CreatePrimitive(PrimitiveType.Quad);

        go.name = "LobbyBackdrop";
        LobbyEnvironment.ApplyBackdropTransform(go.transform);

        var collider = go.GetComponent<Collider>();
        if (collider != null)
            Object.DestroyImmediate(collider);

        var renderer = go.GetComponent<Renderer>();
        if (renderer != null)
        {
            renderer.sharedMaterial = mat;
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            renderer.receiveShadows = false;
        }

        EditorUtility.SetDirty(go);
        return go;
    }

    private static GameObject EnsureMist(Material mat)
    {
        var go = GameObject.Find("LobbyHorizonMist");
        if (go == null)
            go = GameObject.CreatePrimitive(PrimitiveType.Quad);

        go.name = "LobbyHorizonMist";
        LobbyEnvironment.EnsureQuadMesh(go);
        LobbyEnvironment.ApplyMistTransform(go.transform);

        var collider = go.GetComponent<Collider>();
        if (collider != null)
            Object.DestroyImmediate(collider);

        var renderer = go.GetComponent<Renderer>();
        if (renderer != null)
        {
            renderer.sharedMaterial = mat;
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            renderer.receiveShadows = false;
        }

        EditorUtility.SetDirty(go);
        return go;
    }

    private static LobbyEnvironment EnsureEnvironment(GameObject floor, GameObject backdrop, GameObject mist)
    {
        var env = Object.FindFirstObjectByType<LobbyEnvironment>();
        GameObject envGo;
        if (env == null)
        {
            envGo = new GameObject("LobbyEnvironment");
            env = envGo.AddComponent<LobbyEnvironment>();
        }
        else
        {
            envGo = env.gameObject;
        }

        var so = new SerializedObject(env);
        so.FindProperty("currentPreset").intValue = 0;
        so.FindProperty("targetCamera").objectReferenceValue = Camera.main;
        var lightGo = GameObject.Find("Directional Light");
        so.FindProperty("keyLight").objectReferenceValue = lightGo != null ? lightGo.GetComponent<Light>() : null;
        so.FindProperty("floorRenderer").objectReferenceValue = floor != null ? floor.GetComponent<Renderer>() : null;
        so.FindProperty("backdropRenderer").objectReferenceValue = backdrop != null ? backdrop.GetComponent<Renderer>() : null;
        so.FindProperty("mistRenderer").objectReferenceValue = mist != null ? mist.GetComponent<Renderer>() : null;

        var presetsProp = so.FindProperty("presets");
        var defaults = LobbyEnvironment.CreateDefaultPresets();
        presetsProp.arraySize = defaults.Length;
        for (int i = 0; i < defaults.Length; i++)
            WritePreset(presetsProp.GetArrayElementAtIndex(i), defaults[i]);

        so.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(envGo);
        return env;
    }

    private static void WritePreset(SerializedProperty prop, LobbyEnvironment.LightingPreset preset)
    {
        prop.FindPropertyRelative("displayName").stringValue = preset.displayName;
        prop.FindPropertyRelative("cameraBackground").colorValue = preset.cameraBackground;
        prop.FindPropertyRelative("ambient").colorValue = preset.ambient;
        prop.FindPropertyRelative("fog").colorValue = preset.fog;
        prop.FindPropertyRelative("fogStart").floatValue = preset.fogStart;
        prop.FindPropertyRelative("fogEnd").floatValue = preset.fogEnd;
        prop.FindPropertyRelative("lightColor").colorValue = preset.lightColor;
        prop.FindPropertyRelative("lightIntensity").floatValue = preset.lightIntensity;
        prop.FindPropertyRelative("lightEuler").vector3Value = preset.lightEuler;
        prop.FindPropertyRelative("floorColor").colorValue = preset.floorColor;
        prop.FindPropertyRelative("backdropColor").colorValue = preset.backdropColor;
    }
}
