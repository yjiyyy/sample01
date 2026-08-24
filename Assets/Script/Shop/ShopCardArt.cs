using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

/// <summary>
/// Shop_Card003 프레임이면 Icon_Shop_Card003 아이콘을 찾아 줍니다.
/// </summary>
public static class ShopCardArt
{
    private const string ShopIconFolder = "Assets/Arts/Icon/UI/Shop";

    public static Sprite GetCategoryIcon(Sprite cardFrame)
    {
        if (cardFrame == null)
            return null;

        string frameName = cardFrame.name;
        if (string.IsNullOrEmpty(frameName))
            return null;

        string iconName = frameName.StartsWith("Icon_")
            ? frameName
            : "Icon_" + frameName;

#if UNITY_EDITOR
        Sprite editorSprite = LoadInEditor(iconName);
        if (editorSprite != null)
            return editorSprite;
#endif

        return Resources.Load<Sprite>("UI/Shop/" + iconName);
    }

#if UNITY_EDITOR
    private static Sprite LoadInEditor(string iconName)
    {
        string[] paths =
        {
            ShopIconFolder + "/" + iconName + ".png",
            ShopIconFolder + "/" + iconName + ".Png",
            ShopIconFolder + "/" + iconName + ".PNG"
        };

        for (int i = 0; i < paths.Length; i++)
        {
            var sprite = AssetDatabase.LoadAssetAtPath<Sprite>(paths[i]);
            if (sprite != null)
                return sprite;
        }

        string[] guids = AssetDatabase.FindAssets(iconName + " t:Sprite", new[] { ShopIconFolder });
        for (int i = 0; i < guids.Length; i++)
        {
            string path = AssetDatabase.GUIDToAssetPath(guids[i]);
            var sprite = AssetDatabase.LoadAssetAtPath<Sprite>(path);
            if (sprite != null && sprite.name == iconName)
                return sprite;
        }

        return null;
    }
#endif
}
