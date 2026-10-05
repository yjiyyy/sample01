using UnityEngine;

[CreateAssetMenu(menuName = "Game/Lobby/Shop Category", fileName = "ShopCategory")]
public class LobbyShopCategorySO : ScriptableObject
{
    public LobbyShopCategory category = new LobbyShopCategory();
}
