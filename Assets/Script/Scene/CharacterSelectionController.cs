using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using System.Collections.Generic;

/// <summary>
/// 캐릭터 선택 화면. 왼쪽에 초상화 그리드, 오른쪽에 3D 모델 표시.
/// Inspector에서 CharacterDataSO 배열에 드래그 앤 드롭으로 캐릭터를 등록하세요.
/// </summary>
public class CharacterSelectionController : MonoBehaviour
{
    [Header("캐릭터 목록")]
    [Tooltip("등록할 캐릭터들. CharacterDataSO를 드래그 앤 드롭으로 지정하세요. (2개 권장)")]
    [SerializeField] private CharacterDataSO[] characters = new CharacterDataSO[2];

    [Header("씬 전환")]
    [SceneName]
    [SerializeField] private string nextScene = "03_Lobby";

    [Header("참조 (씬에 배치된 오브젝트)")]
    [Tooltip("캐릭터 3D 모델이 스폰될 위치. 카메라 오른쪽에 더미로 배치하세요.")]
    [SerializeField] private Transform spawnPoint;
    [Tooltip("초상화 그리드가 들어갈 부모. 비워두면 자동 생성합니다.")]
    [SerializeField] private RectTransform portraitGridParent;
    [Tooltip("확인 버튼. 비워두면 'Confirm' 이름으로 찾습니다.")]
    [SerializeField] private Button confirmButton;

    private GameObject _spawnedModel;
    private CharacterDataSO _selectedCharacter;
    private int _selectedIndex;

    private void Start()
    {
        EnsureGameState();
        SetupPortraitGrid();
        SetupConfirmButton();
        if (characters != null && characters.Length > 0 && characters[0] != null)
            SelectCharacter(0);
    }

    private void EnsureGameState()
    {
        if (GameState.Instance == null)
        {
            var go = new GameObject("GameState");
            go.AddComponent<GameState>();
        }
    }

    [Header("초상화 레이아웃")]
    [Tooltip("초상화 슬롯 크기 (px). UI에서 지정하며, 이미지는 슬롯 안에 꽉 차게 표시됩니다.")]
    [SerializeField] private Vector2 portraitSlotSize = new Vector2(120, 120);
    [Tooltip("초상화 영역 배경색. 레이아웃 경계를 보여줍니다.")]
    [SerializeField] private Color portraitAreaBgColor = new Color(0.2f, 0.2f, 0.25f, 0.6f);

    private void SetupPortraitGrid()
    {
        if (portraitGridParent == null)
        {
            // 배경 패널 (레이아웃 경계 표시)
            var bgGO = new GameObject("PortraitAreaBg");
            bgGO.transform.SetParent(transform, false);
            var bgRect = bgGO.AddComponent<RectTransform>();
            bgRect.anchorMin = new Vector2(0, 0.2f);
            bgRect.anchorMax = new Vector2(0.35f, 0.8f);
            bgRect.offsetMin = Vector2.zero;
            bgRect.offsetMax = Vector2.zero;
            var bgImg = bgGO.AddComponent<Image>();
            bgImg.color = portraitAreaBgColor;
            bgImg.raycastTarget = false;

            // 그리드 부모
            var gridGO = new GameObject("PortraitGrid");
            gridGO.transform.SetParent(bgGO.transform, false);
            var rect = gridGO.AddComponent<RectTransform>();
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = new Vector2(12, 12);
            rect.offsetMax = new Vector2(-12, -12);

            var hlg = gridGO.AddComponent<HorizontalLayoutGroup>();
            hlg.spacing = 16;
            hlg.childAlignment = TextAnchor.MiddleCenter;
            hlg.childControlWidth = true;
            hlg.childControlHeight = true;
            hlg.childForceExpandWidth = false;
            hlg.childForceExpandHeight = false;
            hlg.padding = new RectOffset(0, 0, 0, 0);

            portraitGridParent = rect;
        }
        else
        {
            // 씬에 배치된 placeholder 교체: 기존 자식 제거 (Destroy는 프레임 끝에 반영되므로 끝에서부터 순회)
            for (int i = portraitGridParent.childCount - 1; i >= 0; i--)
                Destroy(portraitGridParent.GetChild(i).gameObject);
        }

        if (characters == null) return;

        for (int i = 0; i < characters.Length; i++)
        {
            var data = characters[i];
            if (data == null) continue;

            var btn = CreatePortraitButton(data.portrait, i, portraitSlotSize);
            btn.transform.SetParent(portraitGridParent, false);
        }
    }

    private Button CreatePortraitButton(Sprite portrait, int index, Vector2 size)
    {
        var go = new GameObject($"Portrait_{index}");
        var rect = go.AddComponent<RectTransform>();
        rect.sizeDelta = size;

        var le = go.AddComponent<LayoutElement>();
        le.preferredWidth = size.x;
        le.preferredHeight = size.y;
        le.minWidth = size.x;
        le.minHeight = size.y;
        le.flexibleWidth = 0;
        le.flexibleHeight = 0;

        // 반투명 배경: 슬롯 크기를 알 수 있게 함
        var bgImg = go.AddComponent<Image>();
        bgImg.color = portraitAreaBgColor;
        bgImg.raycastTarget = true;

        var btn = go.AddComponent<Button>();
        btn.targetGraphic = bgImg;
        var idx = index;
        btn.onClick.AddListener(() => SelectCharacter(idx));

        // 초상화가 슬롯 안에 꽉 차게 표시 (자식 Image)
        var portraitGO = new GameObject("PortraitImage");
        portraitGO.transform.SetParent(go.transform, false);
        var portraitRect = portraitGO.AddComponent<RectTransform>();
        portraitRect.anchorMin = Vector2.zero;
        portraitRect.anchorMax = Vector2.one;
        portraitRect.offsetMin = Vector2.zero;
        portraitRect.offsetMax = Vector2.zero;

        var portraitImg = portraitGO.AddComponent<Image>();
        portraitImg.sprite = portrait;
        portraitImg.preserveAspect = false;
        portraitImg.raycastTarget = false;
        portraitImg.color = portrait != null ? Color.white : new Color(0.3f, 0.3f, 0.3f, 0.5f);

        return btn;
    }

    private void SetupConfirmButton()
    {
        if (confirmButton == null)
        {
            var found = GetComponentsInChildren<Button>();
            foreach (var b in found)
            {
                if (b.name == "Confirm" || b.GetComponentInChildren<Text>()?.text == "Confirm")
                {
                    confirmButton = b;
                    break;
                }
            }
        }
        if (confirmButton != null)
            confirmButton.onClick.AddListener(OnConfirmSelection);
    }

    public void SelectCharacter(int index)
    {
        if (characters == null || index < 0 || index >= characters.Length) return;
        var data = characters[index];
        if (data == null) return;

        _selectedIndex = index;
        _selectedCharacter = data;
        SpawnModel(data.modelPrefab);
    }

    private void SpawnModel(GameObject prefab)
    {
        if (_spawnedModel != null)
            Destroy(_spawnedModel);

        if (spawnPoint == null || prefab == null) return;

        _spawnedModel = Instantiate(prefab, spawnPoint.position, spawnPoint.rotation);
        _spawnedModel.transform.SetParent(spawnPoint);

        // 캐릭터 선택 씬에서는 입력·이동 차단 (전시용)
        DisableCharacterInput(_spawnedModel);
    }

    /// <summary>
    /// 스폰된 캐릭터의 이동·입력 관련 컴포넌트 비활성화 (캐릭터 선택 씬 전용)
    /// </summary>
    private void DisableCharacterInput(GameObject model)
    {
        if (model == null) return;

        var pm = model.GetComponentInChildren<PlayerMovement>();
        if (pm != null) pm.enabled = false;

        var rb = model.GetComponentInChildren<Rigidbody>();
        if (rb != null) rb.isKinematic = true;
    }

    public void OnConfirmSelection()
    {
        if (GameState.Instance != null && _selectedCharacter != null)
            GameState.Instance.SelectedCharacter = _selectedCharacter;

        if (!string.IsNullOrEmpty(nextScene))
            SceneManager.LoadScene(nextScene);
        else
            Debug.LogWarning("[CharacterSelectionController] nextScene이 지정되지 않았습니다.");
    }
}
