using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public enum PlayerState
{
    Idle,
    Move,
    Attack,
    Knockback,
    Stun,
    Dead
}

public class PlayerWeaponController : MonoBehaviour
{
    [Header("무기 부착 위치")]
    [SerializeField] private Transform weaponSocket;

    [Header("애니메이션 컨트롤러")]
    [SerializeField] private PlayerAnimationController animationController;

    [Header("플레이어 감지기 (EnemyDetector)")]
    public EnemyDetector enemyDetector;

    [Header("기본 무기 (Weapon_None 프리팹)")]
    [SerializeField] private GameObject defaultWeaponPrefab;

    [Header("디버그 모드")]
    [SerializeField] private bool debugMode = true;

    private GameObject currentWeapon;
    private WeaponBehavior weaponBehavior;
    private WeaponDataSO currentWeaponData;

    private float lastAttackTime = -999f;
    private PlayerMovement movement;
    private PlayerState state = PlayerState.Idle;
    private PlayerState previousState = PlayerState.Idle;

    // 🆕 현재 실행 중인 코루틴들 추적
    private Coroutine currentAttackCoroutine;
    private Coroutine currentKnockbackCoroutine;

    private void Awake()
    {
        movement = GetComponent<PlayerMovement>();
    }

    private void Start()
    {
        EquipWeapon(null);
        ChangeState(PlayerState.Idle);
    }

    private void Update()
    {
        if (state == PlayerState.Dead) return;

        switch (state)
        {
            case PlayerState.Idle:
                HandleIdle();
                break;
            case PlayerState.Move:
                HandleMove();
                break;
            case PlayerState.Attack:
                break;
            case PlayerState.Knockback:
            case PlayerState.Stun:
                break;
        }
    }

    private void HandleIdle()
    {
        if (state == PlayerState.Attack || state == PlayerState.Knockback || state == PlayerState.Stun)
            return;

        if (movement.GetVelocityMagnitude() > 0.1f)
        {
            ChangeState(PlayerState.Move);
            return;
        }

        if (InputManager.Instance.GetAttackInput())
            PlayAttack();
    }

    private void HandleMove()
    {
        if (state == PlayerState.Attack || state == PlayerState.Knockback || state == PlayerState.Stun)
            return;

        if (movement.GetVelocityMagnitude() <= 0.1f)
        {
            ChangeState(PlayerState.Idle);
            return;
        }

        if (InputManager.Instance.GetAttackInput())
            PlayAttack();
    }

    /// <summary>
    /// 🆕 상태 변경 시 애니메이션도 강제로 맞춤
    /// </summary>
    private void ChangeState(PlayerState newState)
    {
        if (state == newState) return;

        previousState = state;
        state = newState;

        // 애니메이션 강제 전환
        if (animationController != null)
        {
            animationController.ForceAnimationByState(newState);
        }

        if (debugMode)
        {
            Debug.Log($"[PlayerWeaponController] 상태 변경: {previousState} → {newState}");
        }
    }

    public void EquipWeapon(GameObject weaponPrefab)
    {
        if (currentWeapon != null)
            Destroy(currentWeapon);

        GameObject prefabToSpawn = weaponPrefab != null ? weaponPrefab : defaultWeaponPrefab;
        if (prefabToSpawn == null)
        {
            Debug.LogError("❌ 기본 무기 프리팹이 연결되지 않았습니다.");
            return;
        }

        currentWeapon = Instantiate(prefabToSpawn, weaponSocket);
        currentWeapon.transform.localPosition = Vector3.zero;
        currentWeapon.transform.localRotation = Quaternion.identity;

        weaponBehavior = currentWeapon.GetComponent<WeaponBehavior>();
        currentWeaponData = weaponBehavior != null ? weaponBehavior.data : null;

        if (animationController != null && currentWeaponData != null && currentWeaponData.overrideController != null)
        {
            animationController.GetAnimator().runtimeAnimatorController = currentWeaponData.overrideController;
        }

        Debug.Log($"무기 장착됨 → {currentWeaponData?.weaponName ?? "null"}");
    }

    public void PlayAttack()
    {
        if (currentWeaponData == null) return;
        if (state == PlayerState.Attack || state == PlayerState.Knockback || state == PlayerState.Stun) return;

        float delta = Time.time - lastAttackTime;
        if (delta < currentWeaponData.cooldown) return;

        lastAttackTime = Time.time;

        // 기존 공격 코루틴이 있으면 중단
        if (currentAttackCoroutine != null)
        {
            StopCoroutine(currentAttackCoroutine);
            currentAttackCoroutine = null;
        }

        currentAttackCoroutine = StartCoroutine(AttackRoutine());
    }

    private IEnumerator AttackRoutine()
    {
        ChangeState(PlayerState.Attack);
        animationController?.PlayAttack(currentWeaponData);

        if (weaponBehavior != null)
            weaponBehavior.AttackHit();

        yield return new WaitForSeconds(currentWeaponData.cooldown);

        ChangeState(PlayerState.Idle);
        animationController?.EndAttack();

        currentAttackCoroutine = null;
    }

    /* ───────── 🆕 강제 넉백 (모든 코루틴 중단 후 새로 시작) ───────── */

    /// <summary>
    /// 기존 모든 액션(공격, 넉백, 스턴)을 강제로 중단하고 새로운 넉백을 적용
    /// </summary>
    public void ForceApplyKnockback(Vector3 dir, float power, float duration, float stun)
    {
        // 🔹 1단계: 모든 기존 코루틴 강제 중단
        if (currentAttackCoroutine != null)
        {
            StopCoroutine(currentAttackCoroutine);
            currentAttackCoroutine = null;
            Debug.Log("[PlayerWeaponController] 공격 코루틴 강제 중단");
        }

        if (currentKnockbackCoroutine != null)
        {
            StopCoroutine(currentKnockbackCoroutine);
            currentKnockbackCoroutine = null;
            Debug.Log("[PlayerWeaponController] 기존 넉백/스턴 코루틴 강제 중단");
        }

        // 🔹 2단계: 새로운 넉백 즉시 시작
        currentKnockbackCoroutine = StartCoroutine(KnockbackRoutine(dir, power, duration, stun));
    }

    /// <summary>
    /// 기존 넉백 메서드 (호환성 유지)
    /// </summary>
    public void ApplyKnockback(Vector3 dir, float power, float duration, float stun)
    {
        ForceApplyKnockback(dir, power, duration, stun);
    }

    private IEnumerator KnockbackRoutine(Vector3 dir, float power, float duration, float stun)
    {
        // 🔹 1단계: 넉백 상태 + 강제 애니메이션
        ChangeState(PlayerState.Knockback);

        Debug.Log($"[PlayerWeaponController] 넉백 시작 - Power:{power}, Duration:{duration}");

        // 체력 컴포넌트에서 weight 가져오기
        float resistance = 1f;
        if (TryGetComponent(out Health health))
            resistance = Mathf.Max(health.GetWeight(), 0.01f);

        float elapsed = 0f;
        Vector3 knockDir = dir.normalized;
        knockDir.y = 0f;

        while (elapsed < duration)
        {
            float t = elapsed / duration;
            float currentSpeed = Mathf.Lerp(power / resistance, 0f, t);
            transform.position += knockDir * currentSpeed * Time.deltaTime;

            elapsed += Time.deltaTime;
            yield return null;
        }

        Debug.Log("[PlayerWeaponController] 넉백 완료");

        // 🔹 2단계: 스턴 처리 + 강제 애니메이션
        if (stun > 0f)
        {
            ChangeState(PlayerState.Stun);

            Debug.Log($"[PlayerWeaponController] 스턴 시작 ({stun:F2}초)");
            yield return new WaitForSeconds(stun);
            Debug.Log("[PlayerWeaponController] 스턴 완료");
        }

        // 🔹 3단계: 상태 복구 + 강제 애니메이션
        ChangeState(PlayerState.Idle);

        Debug.Log("[PlayerWeaponController] 정상 상태 복구");

        // 🔹 4단계: 코루틴 추적 해제
        currentKnockbackCoroutine = null;
    }

    private IEnumerator KnockbackThenStunRoutine(Vector3 hitDir, WeaponDataSO weapon, float impactScale)
    {
        float resistance = 1f;
        if (TryGetComponent(out Health health))
            resistance = Mathf.Max(health.GetWeight(), 0.01f);

        if (weapon.knockbackDuration > 0f && weapon.knockbackPower > 0f)
        {
            ChangeState(PlayerState.Knockback);

            float elapsed = 0f;
            Vector3 dir = hitDir.normalized;

            while (elapsed < weapon.knockbackDuration)
            {
                float t = elapsed / weapon.knockbackDuration;
                float currentPower = Mathf.Lerp((weapon.knockbackPower / resistance) * impactScale, 0f, t);
                transform.position += dir * currentPower * Time.deltaTime;

                elapsed += Time.deltaTime;
                yield return null;
            }
        }

        if (weapon.stunDuration > 0f)
        {
            ChangeState(PlayerState.Stun);
            yield return new WaitForSeconds(weapon.stunDuration);
        }

        ChangeState(PlayerState.Idle);
    }

    /* ───────── EnemyDetector 프록시 ───────── */
    public List<Transform> DetectEnemies()
    {
        if (enemyDetector == null)
            return new List<Transform>();

        return enemyDetector.GetEnemiesInRange(enemyDetector.viewDistance);
    }

    public WeaponDataSO GetCurrentWeaponData() => currentWeaponData;
    public PlayerState CurrentState => state;
}