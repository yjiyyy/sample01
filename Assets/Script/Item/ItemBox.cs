using System.Collections;
using UnityEngine;

/// <summary>
/// 아이템 박스: Idle 유지 → Open 조건 충족 시 Open 애니 → 끝 프레임 고정 → 디스폰.
/// Open 시 등록된 아이템을 몬스터 드랍과 동일한 방식으로 방출합니다.
/// </summary>
[DisallowMultipleComponent]
public class ItemBox : MonoBehaviour
{
    public enum OpenMode
    {
        [Tooltip("플레이어가 지정 거리 안으로 들어오면 열림 (거리는 스크립트에서 설정)")]
        Proximity = 0,

        [Tooltip("플레이어 무기 HitBox가 프리팹 Collider에 맞으면 열림")]
        PlayerHit = 1,
    }

    [Header("Open 조건")]
    [SerializeField] private OpenMode openMode = OpenMode.Proximity;

    [Header("Proximity (Open Mode = Proximity일 때만)")]
    [Tooltip("플레이어와의 수평(XZ) 거리. 이 값 이내로 들어오면 열립니다.")]
    [SerializeField] private float proximityRadius = 2.5f;

    [Tooltip("거리 검사 주기(초). 매 프레임보다 가볍습니다.")]
    [SerializeField] private float proximityCheckInterval = 0.2f;

    [Header("Player Hit (Open Mode = PlayerHit일 때만)")]
    [Tooltip("타격 판정에 쓸 Collider. 비어 있으면 자식 Collider 전체에 반응합니다.")]
    [SerializeField] private Collider hitCollider;

    [Header("Animation")]
    [Tooltip("비어 있으면 자식(FBX 모델)에서 Animator를 찾습니다.")]
    [SerializeField] private Animator animator;

    [SerializeField] private string openTriggerName = "Open";
    [SerializeField] private string openStateName = "Open";

    [Tooltip("Open 애니 끝난 뒤 끝 프레임을 유지하는 시간(초).")]
    [SerializeField] private float holdDurationAfterOpen = 5f;

    [Header("FX")]
    [Tooltip("평소 재생할 루프 FX(자식 ParticleSystem). Open 시 Stop.")]
    [SerializeField] private ParticleSystem[] idleFxSystems;

    [Tooltip("Open 순간 1회 스폰할 FX 프리팹.")]
    [SerializeField] private GameObject openFxPrefab;

    [SerializeField] private Transform openFxSpawnPoint;

    [Header("Item Drop")]
    [Tooltip("Open 시작 후 아이템을 방출하기까지 대기 시간(초).")]
    [SerializeField] private float dropDelayAfterOpen = 0.1f;

    [Tooltip("방출할 아이템 개수 최소값. Min~Max 사이에서 랜덤으로 정해집니다.")]
    [SerializeField] private int totalDropCountMin = 1;
    [Tooltip("방출할 아이템 개수 최대값.")]
    [SerializeField] private int totalDropCountMax = 1;
    [SerializeField] private ItemDropEntry[] dropEntries = new ItemDropEntry[0];
    [SerializeField] private LayerMask dropGroundLayerMask = 0;

    private int openTriggerHash;
    private int openStateHash;
    private bool opened;
    private Coroutine openRoutine;
    private Coroutine proximityRoutine;
    private Transform playerTransform;
    private float proximityRadiusSqr;

    private void Awake()
    {
        ResolveAnimator();
        openTriggerHash = Animator.StringToHash(openTriggerName);
        openStateHash = Animator.StringToHash(openStateName);
        proximityRadiusSqr = proximityRadius * proximityRadius;
    }

    private void ResolveAnimator()
    {
        if (animator != null)
            return;

        animator = GetComponent<Animator>();
        if (animator == null)
            animator = GetComponentInChildren<Animator>(true);
    }

    private void Start()
    {
        PlayIdleFx();
        ValidateSetup();

        if (openMode == OpenMode.Proximity)
            proximityRoutine = StartCoroutine(ProximityCheckRoutine());
    }

    private void OnValidate()
    {
        totalDropCountMin = Mathf.Max(0, totalDropCountMin);
        totalDropCountMax = Mathf.Max(totalDropCountMin, totalDropCountMax);
        holdDurationAfterOpen = Mathf.Max(0f, holdDurationAfterOpen);
        dropDelayAfterOpen = Mathf.Max(0f, dropDelayAfterOpen);
        proximityRadius = Mathf.Max(0.1f, proximityRadius);
        proximityCheckInterval = Mathf.Max(0.05f, proximityCheckInterval);
        proximityRadiusSqr = proximityRadius * proximityRadius;
    }

    private void OnTriggerEnter(Collider other)
    {
        if (opened || other == null || openMode != OpenMode.PlayerHit)
            return;

        if (!IsPlayerWeaponCollider(other))
            return;

        if (hitCollider != null && !IsHitColliderInvolved(other))
            return;

        TryOpen();
    }

    /// <summary>외부에서 강제로 열 때 사용(디버그·연출 등).</summary>
    public void TryOpen()
    {
        if (opened)
            return;

        opened = true;
        StopProximityCheck();

        if (openRoutine != null)
            StopCoroutine(openRoutine);
        openRoutine = StartCoroutine(OpenSequenceRoutine());
    }

    private IEnumerator ProximityCheckRoutine()
    {
        var wait = new WaitForSeconds(proximityCheckInterval);

        while (!opened)
        {
            CachePlayerIfNeeded();

            if (playerTransform != null
                && GetFlatDistanceSqr(transform.position, playerTransform.position) <= proximityRadiusSqr)
            {
                TryOpen();
                yield break;
            }

            yield return wait;
        }
    }

    private IEnumerator OpenSequenceRoutine()
    {
        StopIdleFx();
        SpawnOpenFx();
        DisableColliders();

        if (animator != null)
            PlayOpenAnimationOnce();

        if (dropDelayAfterOpen > 0f)
            yield return new WaitForSeconds(dropDelayAfterOpen);

        DropItems();

        if (animator != null)
            yield return WaitForOpenAnimationFinish();

        FreezeOpenPose();

        if (holdDurationAfterOpen > 0f)
            yield return new WaitForSeconds(holdDurationAfterOpen);

        Destroy(gameObject);
    }

    private void PlayOpenAnimationOnce()
    {
        animator.ResetTrigger(openTriggerHash);
        animator.Play(openStateHash, 0, 0f);
    }

    private void FreezeOpenPose()
    {
        if (animator == null)
            return;

        animator.Play(openStateHash, 0, 1f);
        animator.speed = 0f;
    }

    private IEnumerator WaitForOpenAnimationFinish()
    {
        const float enterTimeout = 2f;
        float enterElapsed = 0f;

        while (enterElapsed < enterTimeout)
        {
            var state = animator.GetCurrentAnimatorStateInfo(0);
            if (state.shortNameHash == openStateHash)
                break;

            enterElapsed += Time.deltaTime;
            yield return null;
        }

        while (true)
        {
            var state = animator.GetCurrentAnimatorStateInfo(0);
            if (state.shortNameHash != openStateHash)
            {
                yield return null;
                continue;
            }

            if (state.normalizedTime >= 1f)
                yield break;

            yield return null;
        }
    }

    private void StopProximityCheck()
    {
        if (proximityRoutine == null)
            return;

        StopCoroutine(proximityRoutine);
        proximityRoutine = null;
    }

    private void DropItems()
    {
        ItemDropSpawner.DropItemsForItemBox(
            transform.position,
            totalDropCountMin,
            totalDropCountMax,
            dropEntries,
            dropGroundLayerMask);
    }

    private void PlayIdleFx()
    {
        if (idleFxSystems == null)
            return;

        for (int i = 0; i < idleFxSystems.Length; i++)
        {
            var ps = idleFxSystems[i];
            if (ps == null)
                continue;
            ps.Play(true);
        }
    }

    private void StopIdleFx()
    {
        if (idleFxSystems == null)
            return;

        for (int i = 0; i < idleFxSystems.Length; i++)
        {
            var ps = idleFxSystems[i];
            if (ps == null)
                continue;
            ps.Stop(true, ParticleSystemStopBehavior.StopEmitting);
        }
    }

    private void SpawnOpenFx()
    {
        if (openFxPrefab == null)
            return;

        if (openFxPrefab == gameObject || openFxPrefab.GetComponentInChildren<ItemBox>(true) != null)
        {
            Debug.LogWarning(
                $"[ItemBox] '{name}' Open Fx Prefab에 ItemBox가 들어가 있습니다. 박스가 반복 생성·열릴 수 있어 FX를 스킵합니다.",
                this);
            return;
        }

        Transform spawn = openFxSpawnPoint != null ? openFxSpawnPoint : transform;
        AttackFXEntry.SpawnWorldOneShot(openFxPrefab, spawn.position, spawn.rotation);
    }

    private void DisableColliders()
    {
        var colliders = GetComponentsInChildren<Collider>(true);
        for (int i = 0; i < colliders.Length; i++)
            colliders[i].enabled = false;
    }

    private bool IsHitColliderInvolved(Collider other)
    {
        if (hitCollider == null || other == null)
            return true;

        Vector3 closest = hitCollider.ClosestPoint(other.transform.position);
        return (closest - other.transform.position).sqrMagnitude < 0.25f
            || hitCollider.bounds.Intersects(other.bounds);
    }

    private void CachePlayerIfNeeded()
    {
        if (playerTransform != null)
            return;

        if (PlayerResources.Instance != null)
        {
            playerTransform = PlayerResources.Instance.transform;
            return;
        }

        var go = GameObject.FindGameObjectWithTag("Player");
        if (go != null)
            playerTransform = go.transform;
    }

    private static float GetFlatDistanceSqr(Vector3 a, Vector3 b)
    {
        float dx = a.x - b.x;
        float dz = a.z - b.z;
        return dx * dx + dz * dz;
    }

    private static bool IsPlayerWeaponCollider(Collider other)
    {
        return other.GetComponentInParent<HitBox_PC>() != null
            || other.GetComponentInParent<HitBox_PC_Projectile>() != null
            || other.GetComponentInParent<HitBox_PC_Sector>() != null
            || other.GetComponentInParent<HitBox_PC_Projectile_Sector>() != null;
    }

    private void ValidateSetup()
    {
        ResolveAnimator();
        if (animator == null)
        {
            Debug.LogWarning(
                $"[ItemBox] '{name}'에 Animator가 없습니다. 자식 FBX(ItemBox)에 Animator를 붙이거나 Inspector에 할당하세요.",
                this);
        }

        if (openMode == OpenMode.PlayerHit)
        {
            if (hitCollider == null)
                hitCollider = GetComponentInChildren<Collider>(true);

            if (hitCollider == null)
            {
                Debug.LogWarning(
                    $"[ItemBox] '{name}' PlayerHit 모드는 타격용 Collider가 필요합니다.",
                    this);
            }

            if (GetComponentInChildren<Rigidbody>() == null)
            {
                Debug.LogWarning(
                    $"[ItemBox] '{name}' PlayerHit 모드는 Kinematic Rigidbody가 필요합니다. 무기 HitBox 감지가 불안정할 수 있습니다.",
                    this);
            }
        }
    }

#if UNITY_EDITOR
    private void OnDrawGizmosSelected()
    {
        if (openMode != OpenMode.Proximity)
            return;

        Gizmos.color = new Color(0.2f, 0.85f, 1f, 0.9f);
        Gizmos.DrawWireSphere(transform.position, proximityRadius);
    }
#endif
}
