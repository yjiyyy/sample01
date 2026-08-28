using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// 캐릭터 선택 화면. CharacterDataSO 배열과 UI를 연결하고, CharacterSpawnPoint에 3D 모델을 전시합니다.
/// UI는 Background(뒤) → 3D 캐릭터(중간) → Foreground UI(앞) 순으로 보입니다.
/// </summary>
public class CharacterSelectionController : MonoBehaviour
{
    public const string SelectAnimStateName = "Ani_Select";

    [Header("캐릭터 목록")]
    [SerializeField] private CharacterDataSO[] characters = new CharacterDataSO[5];

    [Header("씬 전환")]
    [SceneName]
    [SerializeField] private string nextScene = SceneNames.Lobby;

    [SceneName]
    [SerializeField] private string returnScene = SceneNames.Lobby;

    [Header("UI")]
    [SerializeField] private CharacterSelectionUI ui;

    [Header("모델 전시")]
    [Tooltip("가운데에 캐릭터 프리팹을 스폰할 위치.")]
    [SerializeField] private Transform characterSpawnPoint;

    [Tooltip("스폰 직후 재생할 Animator 스테이트 이름.")]
    [SerializeField] private string selectAnimStateName = SelectAnimStateName;

    private GameObject _spawnedCharacter;

    /// <summary>에디터 씬 미리보기·런타임 스폰 공용 위치. 없으면 null (자동 생성하지 않음).</summary>
    public Transform GetCharacterSpawnPoint()
    {
        if (characterSpawnPoint != null)
            return characterSpawnPoint;

        var spawnGo = GameObject.Find("CharacterSpawnPoint");
        return spawnGo != null ? spawnGo.transform : null;
    }

    private void Start()
    {
        EnsureGameState();
        ResolveSpawnPoint();
        CharacterSelectionCanvasLayering.Apply(Camera.main);

        if (ui == null)
            ui = FindFirstObjectByType<CharacterSelectionUI>();

        if (ui == null)
        {
            Debug.LogError("[CharacterSelectionController] CharacterSelectionUI를 찾지 못했습니다.");
            return;
        }

        ui.ReturnClicked += OnReturn;
        ui.ConfirmClicked += OnConfirm;
        ui.SelectionChanged += OnSelectionChanged;
        ui.BindCharacters(characters);

        // BindCharacters가 SelectionChanged를 notify=false로 첫 선택하므로 한 번 맞춰 줍니다.
        if (ui.SelectedCharacter != null)
            ShowCharacter(ui.SelectedCharacter);
    }

    private void OnDestroy()
    {
        if (ui == null)
            return;

        ui.ReturnClicked -= OnReturn;
        ui.ConfirmClicked -= OnConfirm;
        ui.SelectionChanged -= OnSelectionChanged;
    }

    private void EnsureGameState()
    {
        if (GameState.Instance == null)
        {
            var go = new GameObject("GameState");
            go.AddComponent<GameState>();
        }
    }

    private void ResolveSpawnPoint()
    {
        if (characterSpawnPoint != null)
            return;

        var spawnGo = GameObject.Find("CharacterSpawnPoint");
        if (spawnGo != null)
        {
            characterSpawnPoint = spawnGo.transform;
            return;
        }

        spawnGo = new GameObject("CharacterSpawnPoint");
        var cam = Camera.main;
        if (cam != null)
        {
            Vector3 forward = cam.transform.forward;
            forward.y = 0f;
            if (forward.sqrMagnitude < 0.001f)
                forward = Vector3.forward;
            forward.Normalize();
            spawnGo.transform.position = cam.transform.position + forward * 3.2f + Vector3.down * 0.8f;
            spawnGo.transform.rotation = Quaternion.LookRotation(-forward, Vector3.up);
        }
        else
        {
            spawnGo.transform.position = new Vector3(0f, 0f, 0f);
            spawnGo.transform.rotation = Quaternion.Euler(0f, 180f, 0f);
        }

        characterSpawnPoint = spawnGo.transform;
    }

    private void OnSelectionChanged(int index)
    {
        ShowCharacter(ui != null ? ui.SelectedCharacter : null);
    }

    private void ShowCharacter(CharacterDataSO data)
    {
        if (_spawnedCharacter != null)
        {
            Destroy(_spawnedCharacter);
            _spawnedCharacter = null;
        }

        if (data == null)
        {
            SetCharacterIllustrationVisible(true);
            return;
        }

        if (data.GetPreviewPrefab() == null)
        {
            SetCharacterIllustrationVisible(true);
            return;
        }

        if (characterSpawnPoint == null)
            return;

        // 가운데는 3D 프리뷰 프리팹, CharacterIllustration(2D)은 겹치지 않게 숨깁니다.
        SetCharacterIllustrationVisible(false);

        var previewPrefab = data.GetPreviewPrefab();
        _spawnedCharacter = Instantiate(
            previewPrefab,
            characterSpawnPoint.position,
            characterSpawnPoint.rotation,
            characterSpawnPoint);
        _spawnedCharacter.name = string.IsNullOrWhiteSpace(data.displayName)
            ? previewPrefab.name
            : $"Select_{data.displayName}";

        DisableGameplaySystemsForPreview(_spawnedCharacter);
        EnsureCharacterAnimatorOverrideForPreview(_spawnedCharacter);
        EnsureBodyPartsAttached(_spawnedCharacter);
        PrepareRenderersForPreview(_spawnedCharacter);
        PlaySelectAnimationForPreview(_spawnedCharacter, selectAnimStateName);
    }

    private static void SetCharacterIllustrationVisible(bool visible)
    {
        var illustration = CharacterSelectionCanvasLayering.FindCharacterIllustration();
        if (illustration != null)
            illustration.gameObject.SetActive(visible);
    }

    private static void EnsureBodyPartsAttached(GameObject model)
    {
        if (model == null)
            return;

        var slots = model.GetComponentsInChildren<PlayerBodyPartSlots>(true);
        for (int i = 0; i < slots.Length; i++)
        {
            if (slots[i] != null)
                slots[i].TryAttachParts();
        }
    }

    internal static void DisableGameplaySystemsForPreview(GameObject model) => DisableGameplaySystems(model);

    internal static void EnsureCharacterAnimatorOverrideForPreview(GameObject model) =>
        EnsureCharacterAnimatorOverride(model);

    internal static void PrepareRenderersForPreview(GameObject model) => PrepareRenderers(model);

    internal static void PlaySelectAnimationForPreview(GameObject model, string stateName) =>
        PlaySelectAnimation(model, stateName);

    private static void EnsureCharacterAnimatorOverride(GameObject model)
    {
        if (model == null)
            return;

        var facade = model.GetComponentInChildren<PlayerFacade>(true);
        if (facade == null || facade.config == null || facade.config.overrideController == null)
            return;

        var anim = model.GetComponentInChildren<Animator>(true);
        if (anim == null)
            return;

        if (anim.runtimeAnimatorController != facade.config.overrideController)
            anim.runtimeAnimatorController = facade.config.overrideController;
    }

    private static void PrepareRenderers(GameObject model)
    {
        var skinned = model.GetComponentsInChildren<SkinnedMeshRenderer>(true);
        for (int i = 0; i < skinned.Length; i++)
            skinned[i].updateWhenOffscreen = true;
    }

    private static void PlaySelectAnimation(GameObject model, string stateName)
    {
        if (model == null)
            return;

        if (string.IsNullOrEmpty(stateName))
            stateName = SelectAnimStateName;
        var animators = model.GetComponentsInChildren<Animator>(true);
        for (int i = 0; i < animators.Length; i++)
        {
            var anim = animators[i];
            if (anim == null || !anim.isActiveAndEnabled)
                continue;
            if (anim.runtimeAnimatorController == null)
                continue;

            for (int layer = 1; layer < anim.layerCount; layer++)
                anim.SetLayerWeight(layer, 0f);

            anim.Play(stateName, 0, 0f);
            anim.Update(0f);
        }
    }

    private static void DisableGameplaySystems(GameObject model)
    {
        if (model == null)
            return;

        var pm = model.GetComponentInChildren<PlayerMovement>(true);
        if (pm != null)
            pm.enabled = false;

        var pwc = model.GetComponentInChildren<PlayerWeaponController>(true);
        if (pwc != null)
            pwc.enabled = false;

        var pec = model.GetComponentInChildren<PlayerEquipmentController>(true);
        if (pec != null)
            pec.enabled = false;

        var charge = model.GetComponentInChildren<PlayerChargeController>(true);
        if (charge != null)
            charge.enabled = false;

        var animCtrl = model.GetComponentInChildren<PlayerAnimationController>(true);
        if (animCtrl != null)
            animCtrl.enabled = false;

        var rb = model.GetComponentInChildren<Rigidbody>(true);
        if (rb != null)
            rb.isKinematic = true;
    }

    public void OnReturn()
    {
        LoadScene(returnScene);
    }

    public void OnConfirm()
    {
        var selected = ui != null ? ui.SelectedCharacter : null;
        if (GameState.Instance != null && selected != null)
            GameState.Instance.SelectedCharacter = selected;

        LoadScene(nextScene);
    }

    private static void LoadScene(string sceneName)
    {
        if (string.IsNullOrEmpty(sceneName))
        {
            Debug.LogWarning("[CharacterSelectionController] 씬 이름이 비어 있습니다.");
            return;
        }

        SceneManager.LoadScene(sceneName);
    }
}
