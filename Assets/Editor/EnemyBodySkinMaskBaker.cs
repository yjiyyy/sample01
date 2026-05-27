#if UNITY_EDITOR
using System.IO;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Body 알베도에서 #F0D1B2 근처 픽셀을 찾아 피부 마스크 PNG를 자동 생성합니다.
/// </summary>
public static class EnemyBodySkinMaskBaker
{
    private const string GeneratedFolder = "Assets/Arts/Characters/FBX/Body/Mat/Generated";
    private const string BodyMatFolder = "Assets/Arts/Characters/FBX/Body/Mat";

    private static readonly (string nameToken, string materialFileName)[] KnownBodyMaterials =
    {
        ("PC_M_Normal", "Mat_PC_M_Normal.mat"),
        ("PC_M_Fat", "Mat_PC_M_Fat.mat"),
        ("PC_M_Muscle", "Mat_PC_M_Muscle.mat"),
        ("PC_F_Normal", "Mat_PC_F_Normal.mat"),
        ("PC_F_Fat", "Mat_PC_F_Fat.mat"),
    };

    public static bool TryBakeForEnemyBodyPartSlots(EnemyBodyPartSlots slots, float colorThreshold, out string message)
    {
        message = null;
        if (slots == null)
        {
            message = "EnemyBodyPartSlots가 없습니다.";
            return false;
        }

        if (!TryFindBodyAlbedoForSlots(slots, out Texture2D albedo, out string albedoPath))
        {
            message =
                "Body 알베도 텍스처를 찾지 못했습니다.\n" +
                "1) bodySkinMaterial에 Mat_PC_* 지정\n" +
                "2) M_Body 렌더러 Materials에 Mat_PC_* 할당\n" +
                "3) 프리팹 이름이 PC_F_Normal_Skin 형식인지 확인";
            return false;
        }

        if (!TryBakeMaskFromAlbedo(albedo, albedoPath, colorThreshold, out Texture2D mask, out string maskPath))
        {
            message = "마스크 Bake에 실패했습니다.";
            return false;
        }

        Undo.RecordObject(slots, "Bake Skin Mask");
        slots.skinMaskTexture = mask;
        EditorUtility.SetDirty(slots);

        message = $"마스크 생성: {maskPath}";
        return true;
    }

    /// <summary>bodySkinMaterial → M_Body 렌더러 → 프리팹 이름(Mat_PC_*) 순으로 알베도 탐색.</summary>
    public static bool TryFindBodyAlbedoForSlots(EnemyBodyPartSlots slots, out Texture2D albedo, out string albedoAssetPath)
    {
        albedo = null;
        albedoAssetPath = null;
        if (slots == null) return false;

        if (TryGetAlbedoFromMaterial(slots.bodySkinMaterial, out albedo, out albedoAssetPath))
            return true;

        if (TryFindBodyAlbedoFromRenderers(slots.gameObject, out albedo, out albedoAssetPath))
            return true;

        Material inferred = TryResolveMaterialFromPrefabName(slots.gameObject.name);
        return TryGetAlbedoFromMaterial(inferred, out albedo, out albedoAssetPath);
    }

    public static bool TryFindBodyAlbedoFromRenderers(GameObject root, out Texture2D albedo, out string albedoAssetPath)
    {
        albedo = null;
        albedoAssetPath = null;
        if (root == null) return false;

        foreach (var smr in root.GetComponentsInChildren<SkinnedMeshRenderer>(true))
        {
            if (smr == null) continue;
            var mats = smr.sharedMaterials;
            if (mats == null) continue;

            for (int i = 0; i < mats.Length; i++)
            {
                Material mat = mats[i];
                if (!IsBodyMaterialSlot(mat, smr)) continue;

                if (TryGetAlbedoFromMaterial(mat, out albedo, out albedoAssetPath))
                    return true;
            }
        }

        return false;
    }

    private static bool IsBodyMaterialSlot(Material mat, SkinnedMeshRenderer smr)
    {
        if (EnemyBodySkinTint.IsBodySkinMaterial(mat)) return true;
        return mat != null && EnemyBodySkinTint.IsBodyMeshRenderer(smr);
    }

    private static bool TryGetAlbedoFromMaterial(Material mat, out Texture2D albedo, out string albedoAssetPath)
    {
        albedo = null;
        albedoAssetPath = null;
        if (mat == null) return false;

        if (!EnemyBodySkinTint.TryGetAlbedoTexture(mat, out albedo) || albedo == null)
            return false;

        albedoAssetPath = AssetDatabase.GetAssetPath(albedo);
        return !string.IsNullOrEmpty(albedoAssetPath);
    }

    private static Material TryResolveMaterialFromPrefabName(string prefabName)
    {
        if (string.IsNullOrEmpty(prefabName)) return null;

        foreach (var entry in KnownBodyMaterials)
        {
            if (!prefabName.Contains(entry.nameToken)) continue;
            string path = $"{BodyMatFolder}/{entry.materialFileName}";
            var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (mat != null) return mat;
        }

        return null;
    }

    public static bool TryBakeMaskFromAlbedo(
        Texture2D albedo,
        string albedoAssetPath,
        float colorThreshold,
        out Texture2D mask,
        out string maskAssetPath)
    {
        mask = null;
        maskAssetPath = null;

        if (albedo == null) return false;

        string readablePath = albedoAssetPath;
        if (string.IsNullOrEmpty(readablePath))
            readablePath = AssetDatabase.GetAssetPath(albedo);

        if (!EnsureTextureReadable(readablePath, true))
        {
            Debug.LogError($"[EnemyBodySkinMaskBaker] 텍스처를 Read/Write 가능하게 바꿀 수 없습니다: {readablePath}");
            return false;
        }

        try
        {
            albedo = AssetDatabase.LoadAssetAtPath<Texture2D>(readablePath);
            if (albedo == null) return false;

            Color source = EnemyBodySkinTint.SourceSkinColor;
            float threshold = Mathf.Max(0.01f, colorThreshold);

            int w = albedo.width;
            int h = albedo.height;
            var pixels = albedo.GetPixels();
            var maskPixels = new Color[pixels.Length];

            for (int i = 0; i < pixels.Length; i++)
            {
                float d = ColorDistanceRgb(pixels[i], source);
                float m = d <= threshold ? 1f : 0f;
                maskPixels[i] = new Color(m, m, m, 1f);
            }

            FeatherMask(maskPixels, w, h, iterations: 1);

            EnsureFolder(GeneratedFolder);
            string baseName = Path.GetFileNameWithoutExtension(readablePath);
            maskAssetPath = $"{GeneratedFolder}/{baseName}_SkinMask.png";
            WriteMaskPng(maskAssetPath, w, h, maskPixels);

            AssetDatabase.ImportAsset(maskAssetPath, ImportAssetOptions.ForceUpdate);
            ConfigureMaskImporter(maskAssetPath);

            mask = AssetDatabase.LoadAssetAtPath<Texture2D>(maskAssetPath);
            return mask != null;
        }
        finally
        {
            if (!string.IsNullOrEmpty(readablePath))
                EnsureTextureReadable(readablePath, false);
        }
    }

    private static float ColorDistanceRgb(Color a, Color b)
    {
        float dr = a.r - b.r;
        float dg = a.g - b.g;
        float db = a.b - b.b;
        return Mathf.Sqrt(dr * dr + dg * dg + db * db);
    }

    private static void FeatherMask(Color[] pixels, int w, int h, int iterations)
    {
        if (iterations <= 0) return;

        var temp = new Color[pixels.Length];
        for (int iter = 0; iter < iterations; iter++)
        {
            for (int y = 0; y < h; y++)
            {
                for (int x = 0; x < w; x++)
                {
                    int idx = y * w + x;
                    float sum = 0f;
                    int count = 0;
                    for (int oy = -1; oy <= 1; oy++)
                    {
                        for (int ox = -1; ox <= 1; ox++)
                        {
                            int nx = x + ox;
                            int ny = y + oy;
                            if (nx < 0 || ny < 0 || nx >= w || ny >= h) continue;
                            sum += pixels[ny * w + nx].r;
                            count++;
                        }
                    }

                    float avg = count > 0 ? sum / count : pixels[idx].r;
                    temp[idx] = new Color(avg, avg, avg, 1f);
                }
            }

            System.Array.Copy(temp, pixels, pixels.Length);
        }
    }

    private static void WriteMaskPng(string assetPath, int w, int h, Color[] pixels)
    {
        var tex = new Texture2D(w, h, TextureFormat.RGBA32, false);
        tex.SetPixels(pixels);
        tex.Apply();
        byte[] png = tex.EncodeToPNG();
        Object.DestroyImmediate(tex);
        File.WriteAllBytes(assetPath, png);
    }

    private static bool EnsureTextureReadable(string assetPath, bool readable)
    {
        if (string.IsNullOrEmpty(assetPath)) return false;
        var importer = AssetImporter.GetAtPath(assetPath) as TextureImporter;
        if (importer == null) return false;
        if (importer.isReadable == readable) return true;

        importer.isReadable = readable;
        importer.SaveAndReimport();
        return true;
    }

    private static void ConfigureMaskImporter(string assetPath)
    {
        var importer = AssetImporter.GetAtPath(assetPath) as TextureImporter;
        if (importer == null) return;

        importer.textureType = TextureImporterType.Default;
        importer.sRGBTexture = false;
        importer.alphaSource = TextureImporterAlphaSource.None;
        importer.mipmapEnabled = true;
        importer.wrapMode = TextureWrapMode.Clamp;
        importer.filterMode = FilterMode.Bilinear;
        importer.isReadable = false;
        importer.SaveAndReimport();
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
}
#endif
