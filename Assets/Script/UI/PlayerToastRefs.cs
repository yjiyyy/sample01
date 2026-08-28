using UnityEngine;

/// <summary>
/// 플레이 중 토스트를 띄울 때 쓰는 프리팹 참조. Resources에서 불러옵니다.
/// </summary>
[CreateAssetMenu(fileName = "PlayerToastRefs", menuName = "Game/UI/Player Toast Refs")]
public class PlayerToastRefs : ScriptableObject
{
    public GameObject toastPrefab;
}
