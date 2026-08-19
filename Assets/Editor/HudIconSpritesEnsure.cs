using System.IO;
using UnityEditor;
using UnityEngine;

/// <summary>
/// 코인/젬 아이콘 참조 에셋을 Resources에 만들어 둡니다.
/// 플레이 빌드에서도 아이콘을 불러올 수 있게 하기 위함입니다.
/// </summary>
[InitializeOnLoad]
public static class HudIconSpritesEnsure
{
    private const string Folder = "Assets/Resources/UI";
    private const string AssetPath = Folder + "/HudIconSprites.asset";

    static HudIconSpritesEnsure()
    {
        EditorApplication.delayCall += EnsureAsset;
    }

    private static void EnsureAsset()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
            return;

        var existing = AssetDatabase.LoadAssetAtPath<HudIconSprites>(AssetPath);
        if (existing != null)
        {
            bool dirty = false;
            if (existing.coin == null)
            {
                existing.coin = AssetDatabase.LoadAssetAtPath<Sprite>(HudResourceIcons.CoinEditorPath);
                dirty = existing.coin != null;
            }
            if (existing.gem == null)
            {
                existing.gem = AssetDatabase.LoadAssetAtPath<Sprite>(HudResourceIcons.GemEditorPath);
                dirty = dirty || existing.gem != null;
            }
            if (dirty)
            {
                EditorUtility.SetDirty(existing);
                AssetDatabase.SaveAssets();
            }
            return;
        }

        if (!AssetDatabase.IsValidFolder("Assets/Resources"))
            AssetDatabase.CreateFolder("Assets", "Resources");
        if (!AssetDatabase.IsValidFolder(Folder))
            AssetDatabase.CreateFolder("Assets/Resources", "UI");

        if (File.Exists(AssetPath))
            return;

        var asset = ScriptableObject.CreateInstance<HudIconSprites>();
        asset.coin = AssetDatabase.LoadAssetAtPath<Sprite>(HudResourceIcons.CoinEditorPath);
        asset.gem = AssetDatabase.LoadAssetAtPath<Sprite>(HudResourceIcons.GemEditorPath);
        AssetDatabase.CreateAsset(asset, AssetPath);
        AssetDatabase.SaveAssets();
    }
}
