using System;
using UnityEngine;
using UnityEngine.Serialization;

/// <summary>
/// 플레이어 Money / Gem 보유량. 픽업 자석 반경은 이후 업그레이드에서 조정 가능.
/// </summary>
[DisallowMultipleComponent]
public class PlayerResources : MonoBehaviour
{
    public static PlayerResources Instance { get; private set; }

    [Header("픽업 (거리 기반)")]
    [Tooltip("이 거리(m) 안에 들어온 드랍 아이템이 플레이어 쪽으로 끌려옵니다. 업그레이드 시 이 값을 올리면 됩니다.")]
    [SerializeField] private float pickupMagnetRadius = 3f;

    [Header("보유량 (런타임)")]
    [SerializeField] private int money;
    [FormerlySerializedAs("jam")]
    [SerializeField] private int gem;

    /// <summary>자석에 걸리기 시작하는 거리 (미터).</summary>
    public float PickupMagnetRadius => pickupMagnetRadius;

    public int Money => money;
    public int Gem => gem;

    /// <summary>Money 또는 Gem이 바뀔 때 (money, gem).</summary>
    public event Action<int, int> OnResourcesChanged;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Debug.LogWarning("[PlayerResources] 중복 인스턴스 — 오브젝트를 파괴합니다.");
            Destroy(gameObject);
            return;
        }
        Instance = this;
    }

    private void OnDestroy()
    {
        if (Instance == this)
            Instance = null;
    }

    /// <summary>업그레이드·버프 등에서 자석 거리 조절.</summary>
    public void SetPickupMagnetRadius(float radius)
    {
        pickupMagnetRadius = Mathf.Max(0.1f, radius);
    }

    public void AddMoney(int amount)
    {
        if (amount == 0) return;
        money = Mathf.Max(0, money + amount);
        OnResourcesChanged?.Invoke(money, gem);
    }

    public void AddGem(int amount)
    {
        if (amount == 0) return;
        gem = Mathf.Max(0, gem + amount);
        OnResourcesChanged?.Invoke(money, gem);
    }

#if UNITY_EDITOR
    private void OnValidate()
    {
        pickupMagnetRadius = Mathf.Max(0.1f, pickupMagnetRadius);
        money = Mathf.Max(0, money);
        gem = Mathf.Max(0, gem);
    }
#endif
}
