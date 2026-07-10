using UnityEditor;
using UnityEngine;

/// <summary>
/// 메뉴: Tools > Create Spawn02 Ground Burst Effect
/// </summary>
public static class Spawn02EffectCreator
{
    private const string Folder = "Assets/Arts/FX/Spawn";
    private const string PrefabPath = Folder + "/Spawn02.prefab";
    private const string BlockMatPath = Folder + "/Mat_Spawn02_Block.mat";
    private const string DustMatPath = Folder + "/Mat_Spawn02_Dust.mat";

    [MenuItem("Tools/Create Spawn02 Ground Burst Effect")]
    public static void CreateSpawn02Effect()
    {
        EnsureFolder();

        Material blockMat = LoadOrCreateMaterial(BlockMatPath, new Color(0.62f, 0.54f, 0.45f, 1f));
        Material dustMat = LoadOrCreateMaterial(DustMatPath, new Color(0.75f, 0.67f, 0.55f, 0.55f), transparent: true);

        var root = new GameObject("Spawn02");
        root.AddComponent<Spawn02GroundBurst>();

        CreateGroundShards(root.transform, blockMat);
        CreateGroundDust(root.transform, dustMat);

        var prefab = PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
        Object.DestroyImmediate(root);

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Selection.activeObject = prefab;
        EditorGUIUtility.PingObject(prefab);
        Debug.Log($"[Spawn02EffectCreator] 프리팹 생성 완료: {PrefabPath}");
    }

    private static void EnsureFolder()
    {
        if (!AssetDatabase.IsValidFolder("Assets/Arts/FX"))
        {
            if (!AssetDatabase.IsValidFolder("Assets/Arts"))
            {
                Debug.LogError("[Spawn02EffectCreator] Assets/Arts 폴더가 없습니다.");
                return;
            }

            AssetDatabase.CreateFolder("Assets/Arts", "FX");
        }

        if (!AssetDatabase.IsValidFolder(Folder))
            AssetDatabase.CreateFolder("Assets/Arts/FX", "Spawn");
    }

    private static Material LoadOrCreateMaterial(string path, Color color, bool transparent = false)
    {
        var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (mat != null)
        {
            ApplyColor(mat, color);
            EditorUtility.SetDirty(mat);
            return mat;
        }

        Shader shader = Shader.Find("Universal Render Pipeline/Particles/Unlit");
        if (shader == null) shader = Shader.Find("Particles/Standard Unlit");
        if (shader == null) shader = Shader.Find("Sprites/Default");

        mat = new Material(shader);
        ApplyColor(mat, color);

        if (transparent)
        {
            if (mat.HasProperty("_Surface")) mat.SetFloat("_Surface", 1f);
            if (mat.HasProperty("_Blend")) mat.SetFloat("_Blend", 0f);
            mat.renderQueue = 3000;
        }

        AssetDatabase.CreateAsset(mat, path);
        return mat;
    }

    private static void ApplyColor(Material mat, Color color)
    {
        if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", color);
        mat.color = color;
    }

    private static void CreateGroundShards(Transform parent, Material material)
    {
        const int shardCount = 8;
        const float radius = 0.48f;
        for (int i = 0; i < shardCount; i++)
        {
            var shard = GameObject.CreatePrimitive(PrimitiveType.Cube);
            shard.name = $"GroundShard_{i:00}";
            shard.transform.SetParent(parent, false);

            float yaw = (360f / shardCount) * i;
            float rad = yaw * Mathf.Deg2Rad;
            Vector3 dir = new Vector3(Mathf.Cos(rad), 0f, Mathf.Sin(rad));

            shard.transform.localPosition = dir * radius;
            shard.transform.localRotation = Quaternion.Euler(18f, yaw, 0f);
            shard.transform.localScale = new Vector3(0.34f, 0.07f, 0.62f);

            // 균열 느낌을 위해 살짝 랜덤 오프셋
            shard.transform.localPosition += new Vector3(
                Random.Range(-0.035f, 0.035f),
                Random.Range(-0.01f, 0.01f),
                Random.Range(-0.035f, 0.035f));
            shard.transform.localRotation *= Quaternion.Euler(
                Random.Range(-6f, 6f),
                Random.Range(-8f, 8f),
                Random.Range(-4f, 4f));
            shard.transform.localScale += new Vector3(
                Random.Range(-0.06f, 0.06f),
                Random.Range(-0.015f, 0.015f),
                Random.Range(-0.12f, 0.12f));

            var mr = shard.GetComponent<MeshRenderer>();
            if (mr != null)
                mr.sharedMaterial = material;

            var col = shard.GetComponent<Collider>();
            if (col != null)
                Object.DestroyImmediate(col);
        }
    }

    private static void CreateGroundDust(Transform parent, Material material)
    {
        var go = new GameObject("GroundDust");
        go.transform.SetParent(parent, false);

        var ps = go.AddComponent<ParticleSystem>();
        var main = ps.main;
        main.duration = 1f;
        main.loop = false;
        main.playOnAwake = true;
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.startLifetime = 0.85f;
        main.startSpeed = new ParticleSystem.MinMaxCurve(1.2f, 2.8f);
        main.startSize = new ParticleSystem.MinMaxCurve(0.35f, 0.75f);
        main.startColor = new Color(0.72f, 0.64f, 0.52f, 0.55f);
        main.gravityModifier = 0.15f;
        main.maxParticles = 24;

        var emission = ps.emission;
        emission.rateOverTime = 0f;
        emission.SetBursts(new[] { new ParticleSystem.Burst(0f, 14) });

        var shape = ps.shape;
        shape.enabled = true;
        shape.shapeType = ParticleSystemShapeType.Circle;
        shape.radius = 0.12f;
        shape.arc = 360f;
        shape.rotation = new Vector3(-90f, 0f, 0f);

        var colorOverLifetime = ps.colorOverLifetime;
        colorOverLifetime.enabled = true;
        var grad = new Gradient();
        grad.SetKeys(
            new[]
            {
                new GradientColorKey(new Color(0.78f, 0.7f, 0.58f), 0f),
                new GradientColorKey(new Color(0.55f, 0.48f, 0.4f), 1f)
            },
            new[]
            {
                new GradientAlphaKey(0.65f, 0f),
                new GradientAlphaKey(0f, 1f)
            });
        colorOverLifetime.color = grad;

        var sizeOverLifetime = ps.sizeOverLifetime;
        sizeOverLifetime.enabled = true;
        sizeOverLifetime.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.EaseInOut(0f, 0.6f, 1f, 1.2f));

        var renderer = go.GetComponent<ParticleSystemRenderer>();
        renderer.renderMode = ParticleSystemRenderMode.Billboard;
        renderer.material = material;
        renderer.sortingFudge = -1f;
    }
}
