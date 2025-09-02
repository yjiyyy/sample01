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

    private GameObject currentWeapon;
    private WeaponBehavior weaponBehavior;
    private WeaponDataSO currentWeaponData;

    private float lastAttackTime = -999f;
    private PlayerMovement movement;
    private PlayerState state = PlayerState.Idle;

    private void Awake()
    {
        movement = GetComponent<PlayerMovement>();
    }

    private void Start()
    {
        EquipWeapon(null);
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
        if (state == PlayerState.Attack) return;

        if (movement.GetVelocityMagnitude() > 0.1f)
        {
            state = PlayerState.Move;
            return;
        }

        if (InputManager.Instance.GetAttackInput())
            PlayAttack();
    }

    private void HandleMove()
    {
        if (state == PlayerState.Attack) return;

        if (movement.GetVelocityMagnitude() <= 0.1f)
        {
            state = PlayerState.Idle;
            return;
        }

        if (InputManager.Instance.GetAttackInput())
            PlayAttack();
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
        StartCoroutine(AttackRoutine());
    }

    private IEnumerator AttackRoutine()
    {
        state = PlayerState.Attack;
        animationController?.PlayAttack(currentWeaponData);

        if (weaponBehavior != null)
            weaponBehavior.AttackHit();

        yield return new WaitForSeconds(currentWeaponData.cooldown);

        state = PlayerState.Idle;
        animationController?.EndAttack();
    }

    /* ───────── Knockback + Stun ───────── */
    public void ApplyKnockback(Vector3 dir, float power, float duration, float stun)
    {
        StartCoroutine(KnockbackRoutine(dir, power, duration, stun));
    }

    private IEnumerator KnockbackRoutine(Vector3 dir, float power, float duration, float stun)
    {
        float elapsed = 0f;

        while (elapsed < duration)
        {
            float t = elapsed / duration;
            float currentSpeed = Mathf.Lerp(power, 0f, t);
            transform.position += dir * currentSpeed * Time.deltaTime;

            elapsed += Time.deltaTime;
            yield return null;
        }

        if (stun > 0f)
        {
            // TODO: stun 시간 동안 입력 막기 / 애니메이션 재생
            yield return new WaitForSeconds(stun);
        }
    }

    private IEnumerator KnockbackThenStunRoutine(Vector3 hitDir, WeaponDataSO weapon, float impactScale)
    {
        float resistance = 1f;
        if (TryGetComponent(out Health health))
            resistance = Mathf.Max(health.GetWeight(), 0.01f);

        if (weapon.knockbackDuration > 0f && weapon.knockbackPower > 0f)
        {
            state = PlayerState.Knockback;
            animationController?.PlayKnockback();

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
            state = PlayerState.Stun;
            animationController?.PlayStun();
            yield return new WaitForSeconds(weapon.stunDuration);
        }

        state = PlayerState.Idle;
        animationController?.GetAnimator().Play("Idle/Run", 0, 0f);
    }

    /* ───────── EnemyDetector 프록시 ───────── */
    public List<Transform> DetectEnemies()
    {
        if (enemyDetector == null)
            return new List<Transform>();

        // EnemyDetector 기본 시야거리 활용
        return enemyDetector.GetEnemiesInRange(enemyDetector.viewDistance);
    }

    public WeaponDataSO GetCurrentWeaponData() => currentWeaponData;
    public PlayerState CurrentState => state;
}
