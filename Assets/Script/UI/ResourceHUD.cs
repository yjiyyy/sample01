using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 상단 등에 Money / Jam 텍스트 표시. <see cref="PlayerResources"/> 이벤트로 갱신.
/// </summary>
/// <remarks>
/// <see cref="ExecuteAlways"/> — 플레이하지 않아도 에디터/게임 뷰에서 문구·위치 확인 가능 (초기 "Money: 0" / "Jam: 0").
/// 씬에 UI 자동 생성: <b>GameObject → UI → Resource HUD (Money/Jam)</b> 또는
/// <b>Tools → UI → Create Resource HUD (Money/Jam)</b>
/// </remarks>
[ExecuteAlways]
[DisallowMultipleComponent]
public class ResourceHUD : MonoBehaviour
{
    [Header("UI (Legacy Text)")]
    [SerializeField] private Text moneyText;
    [SerializeField] private Text jamText;

    [Tooltip("비우면 씬에서 PlayerResources 검색")]
    [SerializeField] private PlayerResources resources;

    private void OnEnable()
    {
        RefreshDisplay();
    }

    private void OnDisable()
    {
        if (Application.isPlaying && resources != null)
            resources.OnResourcesChanged -= OnResourcesChanged;
    }

    private void Start()
    {
        if (!Application.isPlaying)
            return;

        if (resources == null)
            resources = PlayerResources.Instance != null
                ? PlayerResources.Instance
                : Object.FindFirstObjectByType<PlayerResources>();

        if (resources != null)
        {
            resources.OnResourcesChanged -= OnResourcesChanged;
            resources.OnResourcesChanged += OnResourcesChanged;
            Refresh(resources.Money, resources.Jam);
        }
        else
            RefreshDisplay();
    }

    private void OnValidate()
    {
        // 인스펙터에서 Text 할당 직후 등 에디터에서도 즉시 표시
        RefreshDisplay();
    }

    private void OnResourcesChanged(int money, int jam)
    {
        Refresh(money, jam);
    }

    /// <summary>
    /// 에디터(미플레이)에서는 0으로 표시해 레이아웃 확인. 플레이 중에는 현재 보유량.
    /// </summary>
    private void RefreshDisplay()
    {
        int m = 0;
        int j = 0;

        if (Application.isPlaying)
        {
            var res = resources != null
                ? resources
                : (PlayerResources.Instance != null
                    ? PlayerResources.Instance
                    : Object.FindFirstObjectByType<PlayerResources>());
            if (res != null)
            {
                m = res.Money;
                j = res.Jam;
            }
        }

        Refresh(m, j);
    }

    private void Refresh(int money, int jam)
    {
        if (moneyText != null)
            moneyText.text = $"Money: {money}";
        if (jamText != null)
            jamText.text = $"Jam: {jam}";
    }
}
