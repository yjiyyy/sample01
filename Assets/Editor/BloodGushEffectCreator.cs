using UnityEngine;
using UnityEditor;
using System.IO;

/// <summary>
/// 메뉴: Tools > Create Blood Gush Effect
/// 동맥이 잘린 듯한, 한 방향으로 퍼지는 피 이펙트. 콘 1개. 총 3초 재생.
/// </summary>
public static class BloodGushEffectCreator
{
    [MenuItem("Tools/Create Blood Gush Effect")]
    public static void CreateBloodGushEffect()
    {
        string folder = "Assets/Arts/FX";
        if (!AssetDatabase.IsValidFolder("Assets/Arts"))
        {
            Debug.LogError("[BloodGushEffectCreator] Assets/Arts 폴더가 없습니다.");
            return;
        }
        if (!AssetDatabase.IsValidFolder("Assets/Arts/FX"))
        {
            AssetDatabase.CreateFolder("Assets/Arts", "FX");
        }

        var mat = AssetDatabase.LoadAssetAtPath<Material>(Path.Combine(folder, "Mat_BloodGush.mat"));
        if (mat == null) mat = GetDefaultParticleMaterial();

        GameObject root = new GameObject("BloodGushEffect");

        // 콘 1개: 동맥이 잘린 느낌 — 전방으로 명확하게 퍼지는 제트
        var ps = root.AddComponent<ParticleSystem>();
        var main = ps.main;
        main.duration = 1.8f;
        main.loop = false;
        main.startLifetime = 1.4f;
        main.startSpeed = new ParticleSystem.MinMaxCurve(6f, 11f);  // 빠른 속도로 뿜어짐
        main.startSize = new ParticleSystem.MinMaxCurve(0.1f, 0.28f);
        main.startColor = new Color(0.9f, 0.06f, 0.06f, 0.95f);
        main.gravityModifier = 0.85f;
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.playOnAwake = true;
        main.stopAction = ParticleSystemStopAction.None;

        var emission = ps.emission;
        emission.enabled = true;
        emission.rateOverTime = new ParticleSystem.MinMaxCurve(1f, new AnimationCurve(
            new Keyframe(0f, 90f),
            new Keyframe(0.3f, 45f),
            new Keyframe(0.6f, 18f),
            new Keyframe(1f, 2f)
        ));  // 처음 강하게 → 서서히 감쇠
        emission.SetBursts(new ParticleSystem.Burst[0]);

        var shape = ps.shape;
        shape.enabled = true;
        shape.shapeType = ParticleSystemShapeType.Cone;
        shape.angle = 22f;          // 좁은 각도 → 전방 방향이 뚜렷함
        shape.radius = 0.015f;      // 작은 반경 → 한 점에서 뿜어짐
        shape.arc = 360f;
        shape.arcMode = ParticleSystemShapeMultiModeValue.Random;

        var colorOverLifetime = ps.colorOverLifetime;
        colorOverLifetime.enabled = true;
        var grad = new Gradient();
        grad.SetKeys(
            new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
            new[] { new GradientAlphaKey(0.95f, 0f), new GradientAlphaKey(0.35f, 0.5f), new GradientAlphaKey(0f, 1f) }
        );
        colorOverLifetime.color = grad;

        var sizeOverLifetime = ps.sizeOverLifetime;
        sizeOverLifetime.enabled = true;
        sizeOverLifetime.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.EaseInOut(0f, 1f, 1f, 0.2f));

        var renderer = root.GetComponent<ParticleSystemRenderer>();
        renderer.material = mat;
        renderer.renderMode = ParticleSystemRenderMode.Billboard;
        renderer.sortingFudge = 0f;

        // 3초 후 자동 파괴
        var destroyer = root.AddComponent<BloodGushEffectAutoDestroy>();
        destroyer.lifetime = 3f;

        string prefabPath = Path.Combine(folder, "BloodGushEffect.prefab").Replace("\\", "/");
        GameObject prefab = PrefabUtility.SaveAsPrefabAsset(root, prefabPath);
        Object.DestroyImmediate(root);

        AssetDatabase.Refresh();
        Selection.activeObject = prefab;
        EditorGUIUtility.PingObject(prefab);
        Debug.Log($"✅ 피 뿜는 이펙트 프리팹 생성 완료: {prefabPath}");
    }

    static Material GetDefaultParticleMaterial()
    {
        var goop = AssetDatabase.LoadAssetAtPath<Material>(
            "Assets/UnityTechnologies/ParticlePack/EffectExamples/Goop Effects/Materials/GoopSplashParticle.mat");
        if (goop != null)
        {
            var mat = new Material(goop);
            mat.color = new Color(0.8f, 0.05f, 0.05f, 0.9f);
            return mat;
        }
        return null;
    }
}
