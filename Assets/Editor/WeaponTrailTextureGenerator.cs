// WeaponTrailTextureGenerator.cs
// 메뉴에서 실행하여 트레일용 그라데이션 텍스처 생성.
// 방망이(무기) 쪽이 진하게, 꼬리로 갈수록 페이드. TrailEnd(방망이 끝)가 TrailStart(손잡이)보다 진하게.

using UnityEngine;
using UnityEditor;
using System.IO;

public static class WeaponTrailTextureGenerator
{
    const string RelativePath = "Arts/FX/Tex_WeaponTrail_Gradient.png";

    [MenuItem("Tools/Generate Weapon Trail Texture")]
    public static void Generate()
    {
        int w = 256;
        int h = 64;
        var tex = new Texture2D(w, h, TextureFormat.RGBA32, false);
        tex.wrapMode = TextureWrapMode.Clamp;
        tex.filterMode = FilterMode.Bilinear;

        for (int y = 0; y < h; y++)
        {
            float v = (float)y / (h - 1); // 0~1, 폭 방향 (0=TrailStart 손잡이, 1=TrailEnd 방망이 끝)
            // 폭 방향: TrailEnd(방망이 끝) 쪽이 더 진하게
            float vFactor = Mathf.Lerp(0.6f, 1f, v);

            for (int x = 0; x < w; x++)
            {
                float u = (float)x / (w - 1); // 0~1, 길이 (0=꼬리, 1=방망이/무기)
                // 길이: 방망이(U=1) 진하게, 꼬리(U=0)로 갈수록 페이드
                float alpha = Mathf.Pow(u, 1.2f) * vFactor;
                tex.SetPixel(x, y, new Color(1f, 1f, 1f, Mathf.Clamp01(alpha)));
            }
        }
        tex.Apply();

        var fullPath = Path.Combine(Application.dataPath, RelativePath);
        var dir = Path.GetDirectoryName(fullPath);
        if (!Directory.Exists(dir)) Directory.CreateDirectory(dir);

        var bytes = tex.EncodeToPNG();
        File.WriteAllBytes(fullPath, bytes);
        Object.DestroyImmediate(tex);

        AssetDatabase.Refresh();

        // 생성된 텍스처를 트레일 재질에 자동 할당
        var texAsset = AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/" + RelativePath);
        var mat = AssetDatabase.LoadAssetAtPath<Material>("Assets/Arts/FX/Mat_WeaponTrail_SoftRefraction.mat");
        if (texAsset != null && mat != null && mat.shader.name == "Custom/WeaponTrail_SoftAlpha")
        {
            mat.SetTexture("_MainTex", texAsset);
            EditorUtility.SetDirty(mat);
            AssetDatabase.SaveAssets();
        }

        Debug.Log($"[WeaponTrail] 텍스처 생성 완료: Assets/{RelativePath}");
    }
}
