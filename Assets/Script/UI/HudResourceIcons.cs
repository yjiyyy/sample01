using UnityEngine;
using UnityEngine.UI;
using TMPro;
#if UNITY_EDITOR
using UnityEditor;
#endif

/// <summary>
/// 상단 HUD에 쓰는 코인/젬 아이콘 그림입니다. Resources에서 불러옵니다.
/// </summary>
public class HudIconSprites : ScriptableObject
{
    public Sprite coin;
    public Sprite gem;
}

/// <summary>
/// 돈/젬 숫자 옆에 아이콘을 붙입니다.
/// 이미 있으면 위치·크기를 건드리지 않아서, 에디터에서 조정한 값이 유지됩니다.
/// </summary>
public static class HudResourceIcons
{
    public const string CoinEditorPath = "Assets/Arts/Icon/UI/Icon_Coin.png";
    public const string GemEditorPath = "Assets/Arts/Icon/UI/Icon_Gem.png";
    public const string ResourcesAssetPath = "UI/HudIconSprites";

    public const string CoinChildName = "Icon_Coin";
    public const string GemChildName = "Icon_Gem";

    private static Sprite _coin;
    private static Sprite _gem;
    private static bool _loggedMissing;

    public static Sprite Coin
    {
        get
        {
            EnsureLoaded();
            return _coin;
        }
    }

    public static Sprite Gem
    {
        get
        {
            EnsureLoaded();
            return _gem;
        }
    }

    public static void EnsureLoaded()
    {
        if (_coin != null && _gem != null)
            return;

        var lib = Resources.Load<HudIconSprites>(ResourcesAssetPath);
        if (lib != null)
        {
            if (_coin == null)
                _coin = lib.coin;
            if (_gem == null)
                _gem = lib.gem;
        }

#if UNITY_EDITOR
        if (_coin == null)
            _coin = AssetDatabase.LoadAssetAtPath<Sprite>(CoinEditorPath);
        if (_gem == null)
            _gem = AssetDatabase.LoadAssetAtPath<Sprite>(GemEditorPath);
#endif

        if ((_coin == null || _gem == null) && !_loggedMissing)
        {
            _loggedMissing = true;
            Debug.LogWarning("[HudResourceIcons] Icon_Coin / Icon_Gem 스프라이트를 찾지 못했습니다.");
        }
    }

    /// <summary>
    /// 아이콘이 없으면 만들고, 있으면 그대로 둡니다. 위치는 새로 만들 때만 잡습니다.
    /// </summary>
    public static Image GetOrCreateIcon(Graphic amountText, Sprite sprite, string childName, float defaultIconSize)
    {
        if (amountText == null)
            return null;

        var parent = amountText.rectTransform;
        var existing = parent.Find(childName);
        if (existing != null)
        {
            var existingImage = existing.GetComponent<Image>();
            if (existingImage != null && existingImage.sprite == null && sprite != null)
                existingImage.sprite = sprite;
            return existingImage;
        }

        if (sprite == null)
            return null;

        var go = new GameObject(childName, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        go.layer = amountText.gameObject.layer;
        go.transform.SetParent(parent, false);

#if UNITY_EDITOR
        Undo.RegisterCreatedObjectUndo(go, "Create " + childName);
        if (!Application.isPlaying)
            EditorUtility.SetDirty(amountText.gameObject);
#endif

        var image = go.GetComponent<Image>();
        image.sprite = sprite;
        image.preserveAspect = true;
        image.raycastTarget = false;
        image.color = Color.white;

        ApplyDefaultPlacement(amountText, go.GetComponent<RectTransform>(), defaultIconSize);
        return image;
    }

    private static void ApplyDefaultPlacement(Graphic amountText, RectTransform iconRect, float iconSize)
    {
        if (amountText == null || iconRect == null)
            return;

        const float gap = 6f;
        bool rightAligned = IsRightAligned(amountText);

        if (rightAligned)
        {
            float textW = GetPreferredWidth(amountText);
            iconRect.anchorMin = new Vector2(1f, 0.5f);
            iconRect.anchorMax = new Vector2(1f, 0.5f);
            iconRect.pivot = new Vector2(1f, 0.5f);
            iconRect.sizeDelta = new Vector2(iconSize, iconSize);
            iconRect.anchoredPosition = new Vector2(-(textW + gap), 0f);
            return;
        }

        iconRect.anchorMin = new Vector2(0f, 0.5f);
        iconRect.anchorMax = new Vector2(0f, 0.5f);
        iconRect.pivot = new Vector2(0f, 0.5f);
        iconRect.sizeDelta = new Vector2(iconSize, iconSize);
        iconRect.anchoredPosition = Vector2.zero;
        ApplyLeftPaddingOnce(amountText, iconSize + gap);
    }

    private static bool IsRightAligned(Graphic amountText)
    {
        if (amountText is Text legacy)
        {
            return legacy.alignment == TextAnchor.UpperRight
                || legacy.alignment == TextAnchor.MiddleRight
                || legacy.alignment == TextAnchor.LowerRight;
        }

        if (amountText is TextMeshProUGUI tmp)
        {
            return tmp.alignment == TextAlignmentOptions.Right
                || tmp.alignment == TextAlignmentOptions.TopRight
                || tmp.alignment == TextAlignmentOptions.MidlineRight
                || tmp.alignment == TextAlignmentOptions.BottomRight
                || tmp.alignment == TextAlignmentOptions.CaplineRight
                || tmp.alignment == TextAlignmentOptions.BaselineRight;
        }

        return false;
    }

    private static float GetPreferredWidth(Graphic amountText)
    {
        if (amountText is Text legacy)
            return legacy.preferredWidth;
        if (amountText is TextMeshProUGUI tmp)
            return tmp.preferredWidth;
        return 0f;
    }

    private static void ApplyLeftPaddingOnce(Graphic amountText, float left)
    {
        if (amountText is TextMeshProUGUI tmp)
        {
            var m = tmp.margin;
            m.x = left;
            tmp.margin = m;
        }
    }
}
