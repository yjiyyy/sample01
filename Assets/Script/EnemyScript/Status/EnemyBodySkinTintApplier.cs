using UnityEngine;
#if UNITY_EDITOR
using System.IO;
using UnityEditor;
#endif

/// <summary>
/// M_Body 머티리얼에 마스크·피부색 틴트 적용.
/// 에디터: Generated SkinTint 머티리얼 에셋 + MaterialPropertyBlock.
/// Play: MaterialPropertyBlock + 필요 시 머티리얼 인스턴스(SkinTint 셰이더).
/// </summary>
public static class EnemyBodySkinTintApplier
{
#if UNITY_EDITOR
    private const string GeneratedMatFolder = "Assets/Arts/Characters/FBX/Body/Mat/Generated";
#endif

    public static void Apply(
        GameObject root,
        Color skinTintColor,
        Texture2D skinMask,
        bool enabled,
        Material bodySourceMaterial = null)
    {
        if (root == null) return;

#if UNITY_EDITOR
        if (!Application.isPlaying && PrefabUtility.IsPartOfPrefabAsset(root))
        {
            Debug.LogWarning(
                "[EnemyBodySkinTint] Project 창의 프리팹 에셋에는 적용할 수 없습니다. " +
                "Prefab Mode로 열거나 Hierarchy의 인스턴스에서 시도하세요.");
            return;
        }
#endif

        Shader skinShader = EnemyBodySkinTint.GetShader();
        if (enabled && skinShader == null)
        {
            Debug.LogWarning("[EnemyBodySkinTint] Custom/EnemyBodySkinTint 셰이더를 찾지 못했습니다.");
            return;
        }

        foreach (var smr in root.GetComponentsInChildren<SkinnedMeshRenderer>(true))
        {
            if (smr == null) continue;

            Material[] sharedMats = smr.sharedMaterials;
            if (sharedMats == null) continue;

            for (int i = 0; i < sharedMats.Length; i++)
            {
                Material mat = sharedMats[i];
                if (!IsBodySlot(mat, smr)) continue;

                if (!enabled || skinMask == null)
                    continue;

                Material tintMat = ResolveTintMaterial(smr, i, mat, bodySourceMaterial, skinShader);
                if (tintMat == null || !EnemyBodySkinTint.IsSkinTintShader(tintMat.shader))
                    continue;

                var block = new MaterialPropertyBlock();
                smr.GetPropertyBlock(block, i);
                block.SetTexture(EnemyBodySkinTint.SkinMaskId, skinMask);
                block.SetColor(EnemyBodySkinTint.SourceSkinColorId, EnemyBodySkinTint.SourceSkinColor);
                block.SetColor(EnemyBodySkinTint.SkinTintColorId, skinTintColor);
                smr.SetPropertyBlock(block, i);
            }
        }
    }

    private static bool IsBodySlot(Material mat, SkinnedMeshRenderer smr)
    {
        if (EnemyBodySkinTint.IsBodySkinMaterial(mat)) return true;
        return EnemyBodySkinTint.IsBodyMeshRenderer(smr);
    }

    private static Material ResolveTintMaterial(
        SkinnedMeshRenderer smr,
        int index,
        Material slotMaterial,
        Material bodySourceMaterial,
        Shader skinShader)
    {
        if (smr == null || skinShader == null) return null;

#if UNITY_EDITOR
        if (!Application.isPlaying)
            return AssignPersistentSkinTintMaterial(smr, index, slotMaterial, bodySourceMaterial, skinShader);
#endif

        Material source = slotMaterial != null ? slotMaterial : bodySourceMaterial;
        if (source == null) return null;

        if (EnemyBodySkinTint.IsSkinTintShader(source.shader))
            return source;

        Material instance = new Material(source);
        ConvertMaterialToSkinTint(instance, skinShader);

        var runtimeMats = smr.materials;
        if (runtimeMats != null && index >= 0 && index < runtimeMats.Length)
        {
            runtimeMats[index] = instance;
            smr.materials = runtimeMats;
        }

        return instance;
    }

#if UNITY_EDITOR
    private static Material AssignPersistentSkinTintMaterial(
        SkinnedMeshRenderer smr,
        int index,
        Material slotMaterial,
        Material bodySourceMaterial,
        Shader skinShader)
    {
        Material source = slotMaterial != null ? slotMaterial : bodySourceMaterial;
        if (source == null) return null;

        if (EnemyBodySkinTint.IsSkinTintShader(source.shader))
            return AssignMaterialToSlot(smr, index, source);

        Material tintAsset = GetOrCreateSkinTintMaterialAsset(source, skinShader);
        if (tintAsset == null) return null;

        return AssignMaterialToSlot(smr, index, tintAsset);
    }

    private static Material AssignMaterialToSlot(SkinnedMeshRenderer smr, int index, Material mat)
    {
        if (smr == null || mat == null) return null;

        Material[] sharedMats = smr.sharedMaterials;
        if (sharedMats == null || index < 0 || index >= sharedMats.Length)
            return mat;

        if (sharedMats[index] == mat)
            return mat;

        sharedMats[index] = mat;
        smr.sharedMaterials = sharedMats;
        PrefabUtility.RecordPrefabInstancePropertyModifications(smr);
        EditorUtility.SetDirty(smr);
        return mat;
    }

    private static Material GetOrCreateSkinTintMaterialAsset(Material source, Shader skinShader)
    {
        if (source == null || skinShader == null) return null;

        string safeName = source.name.Replace(" (Instance)", "").Trim();
        string assetPath = $"{GeneratedMatFolder}/{safeName}_SkinTint.mat";

        var existing = AssetDatabase.LoadAssetAtPath<Material>(assetPath);
        if (existing != null) return existing;

        EnsureFolder(GeneratedMatFolder);
        var mat = new Material(source);
        ConvertMaterialToSkinTint(mat, skinShader);
        AssetDatabase.CreateAsset(mat, assetPath);
        AssetDatabase.SaveAssets();
        return mat;
    }

    private static void EnsureFolder(string assetFolder)
    {
        if (AssetDatabase.IsValidFolder(assetFolder)) return;
        string parent = Path.GetDirectoryName(assetFolder)?.Replace("\\", "/");
        string name = Path.GetFileName(assetFolder);
        if (!string.IsNullOrEmpty(parent) && !AssetDatabase.IsValidFolder(parent))
            EnsureFolder(parent);
        if (!string.IsNullOrEmpty(parent) && !string.IsNullOrEmpty(name))
            AssetDatabase.CreateFolder(parent, name);
    }
#endif

    private static void ConvertMaterialToSkinTint(Material mat, Shader skinShader)
    {
        if (mat == null || skinShader == null) return;

        EnemyBodySkinTint.TryGetAlbedoTexture(mat, out Texture2D albedoTex);
        Texture baseMap = albedoTex;
        Color baseColor = mat.HasProperty("_BaseColor") ? mat.GetColor("_BaseColor") : Color.white;
        float smooth = mat.HasProperty("_Smoothness") ? mat.GetFloat("_Smoothness") : 0.5f;
        float metallic = mat.HasProperty("_Metallic") ? mat.GetFloat("_Metallic") : 0f;
        Texture bumpMap = mat.HasProperty("_BumpMap") ? mat.GetTexture("_BumpMap") : null;
        float bumpScale = mat.HasProperty("_BumpScale") ? mat.GetFloat("_BumpScale") : 1f;

        mat.shader = skinShader;
        if (baseMap != null) mat.SetTexture("_BaseMap", baseMap);
        mat.SetColor("_BaseColor", baseColor);
        mat.SetFloat("_Smoothness", smooth);
        mat.SetFloat("_Metallic", metallic);

        if (bumpMap != null)
        {
            mat.SetTexture("_BumpMap", bumpMap);
            mat.SetFloat("_BumpScale", bumpScale);
            mat.EnableKeyword("_NORMALMAP");
        }
        else
        {
            mat.DisableKeyword("_NORMALMAP");
        }
    }
}
