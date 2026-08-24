using UnityEngine;

/// <summary>
/// 플레이 중 상점을 띄울 때 쓰는 프리팹 참조입니다. Resources에서 불러옵니다.
/// </summary>
[CreateAssetMenu(fileName = "InGameShopRefs", menuName = "Game/Shop/InGame Shop Refs")]
public class InGameShopRefs : ScriptableObject
{
    public GameObject popupPrefab;
}
