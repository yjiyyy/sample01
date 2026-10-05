using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(LobbyShopCategorySO))]
public class LobbyShopCategorySOEditor : Editor
{
    public override void OnInspectorGUI()
    {
        EditorGUILayout.HelpBox("Items에 무기 SO를 넣고 가격·재화를 지정하세요. 이름은 비워두면 무기 이름을 사용합니다. Description은 NPC 대사입니다. 아이콘과 무게는 무기 SO에서 가져옵니다.",MessageType.Info);
        DrawDefaultInspector();
        var category=(LobbyShopCategorySO)target;
        var ids=new HashSet<string>();
        foreach(var item in category.category.items)
        {
            if(item==null || !item.forSale)continue;
            if(!item.IsValid)EditorGUILayout.HelpBox("무기·프리팹·고유 ID·가격을 확인하세요. 잘못된 상품은 구매할 수 없습니다.",MessageType.Warning);
            if(item.weapon!=null && !ids.Add(item.weapon.id))EditorGUILayout.HelpBox("같은 무기 ID가 중복 등록되어 있습니다.",MessageType.Warning);
        }
        if(GUILayout.Button("로비 상점 미리보기"))SetupLobbyShopPanel.Setup();
    }
}
