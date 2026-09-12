using TMPro;
using UnityEditor;
using UnityEngine;

public static class StageResultUISetup
{
    private const string PrefabPath = "Assets/Resources/UI/StageResult/StageResultOverlay.prefab";
    private const string ArtPath = "Assets/Resources/UI/StageResult/BossEagle.png";
    private const string FontPath = "Assets/Arts/Fonts/BlackHanSans-Regular SDF.asset";

    [MenuItem("Tools/Stage/결과 화면 프리팹 다시 만들기")]
    public static void BuildPrefab()
    {
        ConfigureArtImporter();
        TMP_FontAsset font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(FontPath);
        Sprite art = AssetDatabase.LoadAssetAtPath<Sprite>(ArtPath);
        if (font == null || art == null)
        {
            Debug.LogError("[StageResultUISetup] 결과 화면 폰트 또는 독수리 보스 그림을 찾지 못했습니다.");
            return;
        }

        GameObject root = new GameObject("StageResultOverlay", typeof(RectTransform));
        try
        {
            StageResultUI resultUi = root.AddComponent<StageResultUI>();
            resultUi.RebuildVisuals(font, art);
            PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
            AssetDatabase.SaveAssets();
            Debug.Log($"[StageResultUISetup] 결과 화면 프리팹 생성 완료: {PrefabPath}");
        }
        finally
        {
            Object.DestroyImmediate(root);
        }
    }

    private static void ConfigureArtImporter()
    {
        AssetDatabase.ImportAsset(ArtPath, ImportAssetOptions.ForceUpdate);
        if (AssetImporter.GetAtPath(ArtPath) is not TextureImporter importer)
            return;

        importer.textureType = TextureImporterType.Sprite;
        importer.spriteImportMode = SpriteImportMode.Single;
        importer.alphaIsTransparency = true;
        importer.mipmapEnabled = false;
        importer.wrapMode = TextureWrapMode.Clamp;
        importer.maxTextureSize = 2048;
        importer.textureCompression = TextureImporterCompression.CompressedHQ;
        importer.SaveAndReimport();
    }
}
