using System.IO;
using UnityEditor;
using UnityEngine;

/// <summary>
/// 업그레이드 UI용 둥근 칸·게이지 조각을 PNG로 만듭니다.
/// </summary>
public static class LobbyUpgradeSpriteMaker
{
    private const string Folder = "Assets/Resources/UI/LobbyUpgrade";

    public static void GenerateUiPieces()
    {
        Directory.CreateDirectory(Folder);
        WritePng("UiRound", MakeRoundedRect(64, 64, 16f, Color.white), new Vector4(16f, 16f, 16f, 16f));
        WritePng("UiPip", MakeRoundedRect(24, 16, 4f, Color.white), new Vector4(4f, 4f, 4f, 4f));
        AssetDatabase.Refresh();
    }

    private static void WritePng(string fileName, Texture2D texture, Vector4 border)
    {
        string path = Folder + "/" + fileName + ".png";
        File.WriteAllBytes(path, texture.EncodeToPNG());
        Object.DestroyImmediate(texture);
        AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);

        var importer = AssetImporter.GetAtPath(path) as TextureImporter;
        if (importer == null)
            return;

        importer.textureType = TextureImporterType.Sprite;
        importer.spriteImportMode = SpriteImportMode.Single;
        importer.spritePixelsPerUnit = 100f;
        importer.spriteBorder = border;
        importer.alphaIsTransparency = true;
        importer.mipmapEnabled = false;
        importer.filterMode = FilterMode.Bilinear;
        importer.wrapMode = TextureWrapMode.Clamp;
        importer.npotScale = TextureImporterNPOTScale.None;
        importer.SaveAndReimport();
    }

    private static Texture2D MakeRoundedRect(int width, int height, float radius, Color color)
    {
        var tex = new Texture2D(width, height, TextureFormat.RGBA32, false);
        var pixels = new Color[width * height];
        Vector2 size = new Vector2(width, height);
        Vector2 center = size * 0.5f;
        Vector2 half = size * 0.5f - new Vector2(radius, radius);

        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                Vector2 p = new Vector2(x + 0.5f, y + 0.5f) - center;
                Vector2 q = new Vector2(Mathf.Abs(p.x), Mathf.Abs(p.y)) - half;
                float d = Mathf.Max(q.x, q.y);
                if (q.x > 0f && q.y > 0f)
                    d = q.magnitude;
                d -= radius;
                float alpha = Mathf.Clamp01(0.5f - d);
                pixels[y * width + x] = new Color(color.r, color.g, color.b, color.a * alpha);
            }
        }

        tex.SetPixels(pixels);
        tex.Apply(false, false);
        return tex;
    }
}
