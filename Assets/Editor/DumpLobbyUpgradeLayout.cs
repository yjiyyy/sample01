using System.IO;
using System.Text;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 씬에 있는 업그레이드 패널의 현재 크기·위치·글자 크기를 파일로 적습니다.
/// 씬을 고치지 않고 읽기만 합니다.
/// 메뉴: Tools → Dump Lobby Upgrade Layout
/// </summary>
public static class DumpLobbyUpgradeLayout
{
    private const string ScenePath = "Assets/Scenes/03_Lobby.unity";
    private const string OutputPath = "Logs/LobbyUpgrade/layout-dump.txt";

    [MenuItem("Tools/Dump Lobby Upgrade Layout")]
    public static void Run()
    {
        EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);

        var sb = new StringBuilder();
        DumpRoot(sb, "UpgradePanel");
        DumpRoot(sb, "ShopPage");

        Directory.CreateDirectory(Path.GetDirectoryName(OutputPath) ?? "Logs");
        File.WriteAllText(OutputPath, sb.ToString(), new UTF8Encoding(false));
        Debug.Log("[DumpLobbyUpgradeLayout] 저장: " + Path.GetFullPath(OutputPath));
    }

    private static void DumpRoot(StringBuilder sb, string rootName)
    {
        var canvasGo = GameObject.Find("LobbyCanvas");
        if (canvasGo == null)
        {
            sb.AppendLine("LobbyCanvas 없음");
            return;
        }

        var root = canvasGo.transform.Find(rootName);
        sb.AppendLine("################ " + rootName + " ################");
        if (root == null)
        {
            sb.AppendLine("(없음)");
            sb.AppendLine();
            return;
        }

        DumpNode(sb, root, 0);
        sb.AppendLine();
    }

    private static void DumpNode(StringBuilder sb, Transform node, int depth)
    {
        string pad = new string(' ', depth * 2);
        var rect = node as RectTransform;

        sb.Append(pad).Append("- ").Append(node.name);
        if (!node.gameObject.activeSelf)
            sb.Append("  [비활성]");
        sb.AppendLine();

        if (rect != null)
        {
            sb.Append(pad).Append("  rect anchorMin=").Append(V(rect.anchorMin))
              .Append(" anchorMax=").Append(V(rect.anchorMax))
              .Append(" pivot=").Append(V(rect.pivot))
              .AppendLine();
            sb.Append(pad).Append("  rect anchoredPos=").Append(V(rect.anchoredPosition))
              .Append(" sizeDelta=").Append(V(rect.sizeDelta))
              .Append(" offsetMin=").Append(V(rect.offsetMin))
              .Append(" offsetMax=").Append(V(rect.offsetMax))
              .AppendLine();
            sb.Append(pad).Append("  rect worldSize=").Append(V(rect.rect.size))
              .Append(" scale=").Append(V3(rect.localScale))
              .AppendLine();
        }

        var le = node.GetComponent<LayoutElement>();
        if (le != null)
        {
            sb.Append(pad).Append("  LayoutElement min=").Append(le.minWidth).Append("x").Append(le.minHeight)
              .Append(" pref=").Append(le.preferredWidth).Append("x").Append(le.preferredHeight)
              .Append(" flex=").Append(le.flexibleWidth).Append("x").Append(le.flexibleHeight)
              .Append(" ignore=").Append(le.ignoreLayout)
              .AppendLine();
        }

        var vertical = node.GetComponent<VerticalLayoutGroup>();
        if (vertical != null)
            DumpHv(sb, pad, "VerticalLayoutGroup", vertical);

        var horizontal = node.GetComponent<HorizontalLayoutGroup>();
        if (horizontal != null)
            DumpHv(sb, pad, "HorizontalLayoutGroup", horizontal);

        var grid = node.GetComponent<GridLayoutGroup>();
        if (grid != null)
        {
            sb.Append(pad).Append("  GridLayoutGroup cell=").Append(V(grid.cellSize))
              .Append(" spacing=").Append(V(grid.spacing))
              .Append(" padding=").Append(Pad(grid.padding))
              .Append(" constraint=").Append(grid.constraint).Append("/").Append(grid.constraintCount)
              .Append(" align=").Append(grid.childAlignment)
              .AppendLine();
        }

        var fitter = node.GetComponent<ContentSizeFitter>();
        if (fitter != null)
        {
            sb.Append(pad).Append("  ContentSizeFitter h=").Append(fitter.horizontalFit)
              .Append(" v=").Append(fitter.verticalFit).AppendLine();
        }

        var image = node.GetComponent<Image>();
        if (image != null)
        {
            sb.Append(pad).Append("  Image sprite=").Append(image.sprite != null ? image.sprite.name : "(없음)")
              .Append(" type=").Append(image.type)
              .Append(" color=").Append(C(image.color))
              .Append(" preserveAspect=").Append(image.preserveAspect)
              .Append(" raycast=").Append(image.raycastTarget)
              .AppendLine();
        }

        var tmp = node.GetComponent<TextMeshProUGUI>();
        if (tmp != null)
        {
            sb.Append(pad).Append("  TMP fontSize=").Append(tmp.fontSize)
              .Append(" autoSize=").Append(tmp.enableAutoSizing)
              .Append(" range=").Append(tmp.fontSizeMin).Append("~").Append(tmp.fontSizeMax)
              .Append(" align=").Append(tmp.alignment)
              .Append(" wrap=").Append(tmp.textWrappingMode)
              .Append(" overflow=").Append(tmp.overflowMode)
              .Append(" color=").Append(C(tmp.color))
              .Append(" font=").Append(tmp.font != null ? tmp.font.name : "(없음)")
              .AppendLine();
            sb.Append(pad).Append("  TMP margin=").Append(V4(tmp.margin))
              .Append(" text=\"").Append(tmp.text.Replace("\n", "\\n")).Append("\"")
              .AppendLine();
        }

        var loc = node.GetComponent<LocalizedText>();
        if (loc != null)
            sb.Append(pad).AppendLine("  LocalizedText 있음");

        var button = node.GetComponent<Button>();
        if (button != null)
        {
            sb.Append(pad).Append("  Button transition=").Append(button.transition)
              .Append(" target=").Append(button.targetGraphic != null ? button.targetGraphic.name : "(없음)")
              .AppendLine();
        }

        var row = node.GetComponent<LobbyUpgradeRowUI>();
        if (row != null)
            sb.Append(pad).Append("  LobbyUpgradeRowUI statId=").Append(row.StatId).AppendLine();

        var scroll = node.GetComponent<ScrollRect>();
        if (scroll != null)
        {
            sb.Append(pad).Append("  ScrollRect vertical=").Append(scroll.vertical)
              .Append(" horizontal=").Append(scroll.horizontal)
              .Append(" movement=").Append(scroll.movementType)
              .AppendLine();
        }

        for (int i = 0; i < node.childCount; i++)
            DumpNode(sb, node.GetChild(i), depth + 1);
    }

    private static void DumpHv(StringBuilder sb, string pad, string label, HorizontalOrVerticalLayoutGroup group)
    {
        sb.Append(pad).Append("  ").Append(label)
          .Append(" spacing=").Append(group.spacing)
          .Append(" padding=").Append(Pad(group.padding))
          .Append(" align=").Append(group.childAlignment)
          .Append(" controlWH=").Append(group.childControlWidth).Append("/").Append(group.childControlHeight)
          .Append(" expandWH=").Append(group.childForceExpandWidth).Append("/").Append(group.childForceExpandHeight)
          .AppendLine();
    }

    private static string V(Vector2 v) => "(" + v.x + ", " + v.y + ")";
    private static string V3(Vector3 v) => "(" + v.x + ", " + v.y + ", " + v.z + ")";
    private static string V4(Vector4 v) => "(" + v.x + ", " + v.y + ", " + v.z + ", " + v.w + ")";
    private static string Pad(RectOffset p) => "L" + p.left + " R" + p.right + " T" + p.top + " B" + p.bottom;

    private static string C(Color c)
    {
        return "(" + c.r.ToString("0.###") + ", " + c.g.ToString("0.###") + ", " + c.b.ToString("0.###")
            + ", " + c.a.ToString("0.###") + ")";
    }
}
