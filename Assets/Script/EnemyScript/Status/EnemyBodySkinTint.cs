using UnityEngine;

/// <summary>
/// Body(M_Body) 피부 톤 틴트 — 기준 피부색 #F0D1B2, 마스크 텍스처로 피부 영역만 색 변경.
/// </summary>
public static class EnemyBodySkinTint
{
    public const string ShaderName = "Custom/EnemyBodySkinTint";
    public const string SkinMaskProperty = "_SkinMask";
    public const string SourceSkinColorProperty = "_SourceSkinColor";
    public const string SkinTintColorProperty = "_SkinTintColor";

    /// <summary>텍스처에 박혀 있는 기본 피부색 (#F0D1B2).</summary>
    public static readonly Color SourceSkinColor = new Color(240f / 255f, 209f / 255f, 178f / 255f, 1f);

    public static readonly int SkinMaskId = Shader.PropertyToID(SkinMaskProperty);
    public static readonly int SourceSkinColorId = Shader.PropertyToID(SourceSkinColorProperty);
    public static readonly int SkinTintColorId = Shader.PropertyToID(SkinTintColorProperty);

    private static Shader _cachedShader;

    public static Shader GetShader()
    {
        if (_cachedShader == null)
            _cachedShader = Shader.Find(ShaderName);
        return _cachedShader;
    }

    public static bool IsSkinTintShader(Shader shader)
    {
        return shader != null && shader.name == ShaderName;
    }

    /// <summary>Body 머티리얼 슬롯인지 (M_Body 이름 또는 Mat_PC_Body 계열).</summary>
    public static bool IsBodySkinMaterial(Material mat)
    {
        if (mat == null) return false;
        string n = mat.name;
        if (n.Contains("M_Body")) return true;
        if (n.StartsWith("Mat_PC_") && !n.Contains("Face") && !n.Contains("Hair")) return true;
        return false;
    }

    /// <summary>M_Body 메쉬/오브젝트인지 (이름에 M_Body 포함).</summary>
    public static bool IsBodyMeshRenderer(Renderer renderer)
    {
        if (renderer == null) return false;
        if (ContainsBodyMeshName(renderer.gameObject.name)) return true;

        if (renderer is SkinnedMeshRenderer smr && smr.sharedMesh != null)
            return ContainsBodyMeshName(smr.sharedMesh.name);

        return false;
    }

    private static bool ContainsBodyMeshName(string name)
    {
        return !string.IsNullOrEmpty(name)
            && name.IndexOf("M_Body", System.StringComparison.OrdinalIgnoreCase) >= 0;
    }

    /// <summary>Body 머티리얼에서 알베도 텍스처 추출 (_BASE_COLOR_MAP → _BaseMap → _MainTex).</summary>
    public static bool TryGetAlbedoTexture(Material mat, out Texture2D albedo)
    {
        albedo = null;
        if (mat == null) return false;

        string[] propertyNames = { "_BASE_COLOR_MAP", "_BaseMap", "_MainTex" };
        foreach (string prop in propertyNames)
        {
            if (!mat.HasProperty(prop)) continue;
            albedo = mat.GetTexture(prop) as Texture2D;
            if (albedo != null) return true;
        }

        albedo = mat.mainTexture as Texture2D;
        return albedo != null;
    }
}
