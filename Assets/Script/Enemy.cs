using System.Collections;
using System.Linq;
using UnityEngine;
using UnityEngine.AI;
using System.Collections.Generic;

public class Enemy : MonoBehaviour
{
    private NavMeshAgent agent;
    private Transform player;
    private EnemyAnimationController anim;
    private EnemyAttackController attackController;
    private bool isAttacking;

    [Header("넉백 시 머리 팍 튕기기")]
    [SerializeField] private MultiBoneJerkController jerkController;

    [SerializeField] private Animator animator;

    [Header("이동 속도")]
    public float moveSpeed = 3.5f;

    [Header("넉백 관련")]
    private Coroutine knockbackRoutine;
    private Coroutine stunRoutine;

    [Header("사망 랙돌 체급(무게)")]
    [Tooltip("0.5 = 가벼움, 1 = 보통, 2 = 탱크")]
    public float weight = 1f;

    [Header("디버그 모드")]
    [SerializeField] private bool debugMode = true;

    private enum EnemyState { Chase, Attack, Knockback, Stunned, Dead }
    private EnemyState currentState;

    private readonly Dictionary<BodySliceType, string[]> sliceBones = new()
    {
        { BodySliceType.Head,       new[] { "Bip001 Head" } },
        { BodySliceType.LeftArm,    new[] { "Bip001 L UpperArm" } },
        { BodySliceType.RightArm,   new[] { "Bip001 R UpperArm" } },
        { BodySliceType.LeftLeg,    new[] { "Bip001 L Thigh" } },
        { BodySliceType.RightLeg,   new[] { "Bip001 R Thigh" } },
        { BodySliceType.All,        new[] {
            "Bip001 Head",
            "Bip001 L UpperArm", "Bip001 R UpperArm",
            "Bip001 L Thigh", "Bip001 R Thigh"
        } }
    };

    private float debugTimer = 0f;
    private float attackTimer;

    void Start()
    {
        agent = GetComponent<NavMeshAgent>();
        agent.speed = moveSpeed;
        agent.updateRotation = false;

        anim = GetComponent<EnemyAnimationController>();
        attackController = GetComponent<EnemyAttackController>();
        player = GameObject.FindWithTag("Player")?.transform;

        if (animator == null)
            animator = GetComponent<Animator>();

        SetState(EnemyState.Chase);
    }

    void Update()
    {
        if (currentState == EnemyState.Dead || player == null) return;
        /*
        if (debugMode)
        {
            debugTimer += Time.deltaTime;
            if (debugTimer >= 0.1f)
            {
                AnimatorStateInfo stateInfo = animator.GetCurrentAnimatorStateInfo(0);

                string animName = "Unknown";
                if (stateInfo.IsName("Attack")) animName = "Attack";
                else if (stateInfo.IsName("Run")) animName = "Move";
                else if (stateInfo.IsName("Stun")) animName = "Stun";
                else if (stateInfo.IsName("Dead")) animName = "Dead";
                else if (stateInfo.IsName("Knockback01")) animName = "Knockback01";
                else if (stateInfo.IsName("Knockback02")) animName = "Knockback02";
                else if (stateInfo.IsName("Knockback03")) animName = "Knockback03";

                string stateLog =
                    $"{gameObject.name} ▶ " +
                    $"State={currentState} | " +
                    $"Anim={animName} | " +
                    $"Normalized={stateInfo.normalizedTime:F2} | " +
                    $"Animator.speed={animator.speed:F2}";

                Debug.Log(stateLog);
                debugTimer = 0f;
            }
        }*/

        switch (currentState)
        {
            case EnemyState.Chase:
                HandleChase();
                break;
            case EnemyState.Attack:
                HandleAttack();
                break;
            case EnemyState.Stunned:
                break;
        }
    }

    private void SetState(EnemyState newState)
    {
        if (debugMode && currentState != newState)
            Debug.Log($"[EnemyState] {gameObject.name} {currentState} → {newState}");

        currentState = newState;

        switch (newState)
        {
            case EnemyState.Stunned:
                animator.Play("Stun", 0, 0f);
                if (agent != null && agent.isOnNavMesh) agent.isStopped = true;
                break;

            case EnemyState.Knockback:
                int rand = Random.Range(1, 4);
                animator.Play($"Knockback0{rand}", 0, 0f);
                if (agent != null && agent.isOnNavMesh) agent.isStopped = true;
                break;

            case EnemyState.Chase:
                animator.Play("Run", 0, 0f);
                if (agent != null && agent.isOnNavMesh) agent.isStopped = false;
                break;

            case EnemyState.Attack:
                animator.Play("Attack", 0, 0f);
                animator.Update(0f);              // ✅ Attack 즉시 반영
                attackController.NotifyAttack(0); // 현재 공격 데이터 캐싱
                if (agent != null && agent.isOnNavMesh) agent.isStopped = true;
                attackTimer = 0f;                 // Attack 진입 시 쿨타임 초기화
                break;

            case EnemyState.Dead:
                animator.Play("Die", 0, 0f);
                if (agent != null && agent.isOnNavMesh) agent.isStopped = true;
                break;
        }
    }

    void HandleChase()
    {
        if (player == null || agent == null || !agent.isOnNavMesh) return;

        Vector3 dir = player.position - transform.position;
        float distance = dir.magnitude;

        float attackRange = attackController != null && attackController.AttackCount > 0
            ? attackController.GetAttackRange(0)
            : 2f;

        if (distance < attackRange)
        {
            SetState(EnemyState.Attack);
            return;
        }

        agent.isStopped = false;
        agent.SetDestination(player.position);
        anim.UpdateMovement(agent.velocity.magnitude);

        dir.y = 0f;
        if (dir != Vector3.zero)
            transform.rotation = Quaternion.LookRotation(dir);
    }

    void HandleAttack()
    {
        if (player == null) return;

        // 🔹 플레이어 바라보기
        Vector3 dir = player.position - transform.position;
        dir.y = 0f;
        if (dir != Vector3.zero)
            transform.rotation = Quaternion.LookRotation(dir);

        // 🔹 공격 상태 잠금 중이면 → 쿨타임 끝날 때까지 유지
        attackTimer += Time.deltaTime;
        if (isAttacking)
        {
            if (attackTimer >= attackController.GetAttackCooldown(0))
            {
                // 공격 종료 → 다시 사거리 체크
                isAttacking = false;
            }
            return; // 쿨타임 전에는 무조건 Attack 상태 유지
        }

        // 🔹 공격 쿨타임이 끝난 이후 → 사거리 판정
        float attackRange = attackController != null && attackController.AttackCount > 0
            ? attackController.GetAttackRange(0)
            : 2f;

        if (Vector3.Distance(transform.position, player.position) >= attackRange)
        {
            SetState(EnemyState.Chase);
            return;
        }

        // 🔹 새로운 공격 시작
        animator.Play("Attack", 0, 0f);
        animator.Update(0f);
        attackController.NotifyAttack(0);
        isAttacking = true;
        attackTimer = 0f;
    }


    /* ───────── 데미지 처리 ───────── */
    public void OnDamage(Vector3 hitDir, WeaponDataSO weapon, float impactScale = 1f)
    {
        if (currentState == EnemyState.Dead) return;

        if (stunRoutine != null)
        {
            StopCoroutine(stunRoutine);
            stunRoutine = null;
        }

        if (knockbackRoutine != null) StopCoroutine(knockbackRoutine);

        knockbackRoutine = StartCoroutine(KnockbackThenStunRoutine(hitDir, weapon, impactScale));

        if (weapon.jerkIntensity > 0f && jerkController != null)
            jerkController.TriggerJerk(weapon.jerkIntensity, weapon.jerkDuration);
    }

    public void ApplyKnockback(Vector3 dir, WeaponDataSO weapon)
    {
        OnDamage(dir, weapon, 1f);
    }

    private IEnumerator KnockbackThenStunRoutine(Vector3 direction, WeaponDataSO weapon, float scale = 1f)
    {
        yield return StartCoroutine(KnockbackRoutine(direction, weapon, scale));

        if (weapon.stunDuration > 0f)
        {
            stunRoutine = StartCoroutine(StunRoutine(weapon.stunDuration));
        }
        else
        {
            SetState(EnemyState.Chase);
        }
    }

    private IEnumerator KnockbackRoutine(Vector3 direction, WeaponDataSO weapon, float scale = 1f)
    {
        SetState(EnemyState.Knockback);

        if (debugMode)
            Debug.Log($"[Knockback] {gameObject.name} 시작 - Dir:{direction}, Power:{weapon.knockbackPower}, Dur:{weapon.knockbackDuration}");

        float duration = weapon.knockbackDuration;
        float timer = 0f;

        Vector3 dir = direction;
        dir.y = 0f;
        if (dir == Vector3.zero) dir = Vector3.back;
        dir = dir.normalized;

        while (timer < duration)
        {
            if (currentState == EnemyState.Dead) yield break;

            float t = timer / duration;
            float currentSpeed = Mathf.Lerp(weapon.knockbackPower * scale, 0f, t);
            transform.position += dir * currentSpeed * Time.deltaTime;

            timer += Time.deltaTime;
            yield return null;
        }

        if (debugMode)
            Debug.Log($"[Knockback] {gameObject.name} 종료");
    }

    private IEnumerator StunRoutine(float duration)
    {
        SetState(EnemyState.Stunned);

        if (debugMode)
            Debug.Log($"[Stun] {gameObject.name} 시작 ({duration:F2}s)");

        yield return new WaitForSeconds(duration);

        float attackRange = attackController != null && attackController.AttackCount > 0
            ? attackController.GetAttackRange(0)
            : 2f;

        if (player != null && Vector3.Distance(transform.position, player.position) < attackRange)
        {
            SetState(EnemyState.Attack);
        }
        else
        {
            SetState(EnemyState.Chase);
        }

        if (debugMode)
            Debug.Log($"[Stun] {gameObject.name} 종료");

        stunRoutine = null;
    }

    /* ───────── 사망 처리 ───────── */
    public void Die(Vector3 hitDir, WeaponDataSO weapon) => Die(hitDir, weapon, 1f);

    public void Die(Vector3 hitDir, WeaponDataSO weapon, float impactScale)
    {
        SetState(EnemyState.Dead);

        if (debugMode)
            Debug.Log($"[Death] {gameObject.name} - Weapon:{weapon?.name}, Type:{weapon?.deathType}, Scale:{impactScale}");

        if (knockbackRoutine != null) StopCoroutine(knockbackRoutine);
        if (jerkController != null) jerkController.StopJerk();

        if (agent) agent.enabled = false;
        if (TryGetComponent(out Collider rootCol)) rootCol.enabled = false;
        if (TryGetComponent(out Rigidbody rootRb)) rootRb.isKinematic = true;
        if (TryGetComponent(out Animator rootAnim)) rootAnim.enabled = false;

        switch (weapon?.deathType ?? EnemyDeathType.Default)
        {
            case EnemyDeathType.Ragdoll:
                PlayRagdollDeath(hitDir, weapon, impactScale);
                break;
            case EnemyDeathType.Slice:
                var type = ChooseRandomSlicePart(weapon);
                SliceBody(type, hitDir, weapon, impactScale);
                break;
            case EnemyDeathType.Default:
            default:
                if (animator) animator.SetTrigger("Die");
                break;
        }
    }

    private void ScheduleDestroyGibs(Transform root, float delay)
    {
        foreach (var rb in root.GetComponentsInChildren<Rigidbody>())
        {
            if (rb.transform == transform) continue;
            Destroy(rb.gameObject, delay);
        }
    }

    private void PlayRagdollDeath(Vector3 hitDir, WeaponDataSO weapon, float impactScale = 1f)
    {
        float horizBase = weapon ? weapon.ragdollImpulse * impactScale : 0f;
        float upwardBase = weapon ? weapon.upwardImpulse * impactScale : 0f;
        float torqueBase = weapon ? weapon.torqueImpulse * impactScale : horizBase;

        PlayRagdollDeathDirect(hitDir, horizBase, upwardBase, torqueBase);
    }

    private void PlayRagdollDeathDirect(Vector3 hitDir, float horizBase, float upwardBase, float torqueBase)
    {
        float rand = Random.Range(0.9f, 1.1f);
        float horiz = horizBase * rand / Mathf.Max(weight, 0.01f);
        float up = upwardBase * rand / Mathf.Max(weight, 0.01f);
        float torque = torqueBase * rand;

        Vector3 force = hitDir.normalized * horiz;
        force.y += up;

        Rigidbody pelvisRB = GetComponentsInChildren<Rigidbody>()
                             .OrderByDescending(rb => rb.mass).FirstOrDefault();

        foreach (var rb in GetComponentsInChildren<Rigidbody>())
        {
            if (rb.transform == transform) continue;
            rb.isKinematic = false;
            rb.linearVelocity = rb.angularVelocity = Vector3.zero;
            rb.AddForce(force * Random.Range(0.95f, 1.05f), ForceMode.Impulse);

            float partTorque = (rb == pelvisRB) ? torque : torque * 0.25f;
            rb.AddTorque(Random.onUnitSphere * partTorque, ForceMode.Impulse);
        }

        foreach (var t in GetComponentsInChildren<Transform>())
        {
            if (t == transform) continue;
            if (t.TryGetComponent(out Collider col)) col.enabled = true;
            t.gameObject.layer = LayerMask.NameToLayer("Ragdoll");
        }

        ScheduleDestroyGibs(transform, 5f);
        Destroy(gameObject, 5f);
    }

    private void SliceBody(BodySliceType sliceType, Vector3 hitDir, WeaponDataSO weapon, float impactScale = 1f)
    {
        animator.enabled = false;

        float horizBase = weapon ? weapon.ragdollImpulse * impactScale : 0f;
        float upwardBase = weapon ? weapon.upwardImpulse * impactScale : 0f;
        float torqueBase = weapon ? weapon.torqueImpulse * impactScale : horizBase;

        float rand = Random.Range(0.9f, 1.1f);
        float horiz = horizBase * rand / Mathf.Max(weight, 0.01f);
        float up = upwardBase * rand / Mathf.Max(weight, 0.01f);
        float torque = torqueBase * rand;

        Vector3 force = hitDir.normalized * horiz;
        force.y += up;

        float sliceForce = weapon ? weapon.sliceForce * impactScale : 8f;

        HashSet<Transform> excludedTransforms = sliceBones.ContainsKey(sliceType)
            ? new HashSet<Transform>(sliceBones[sliceType]
                .Select(name => GetComponentsInChildren<Transform>().FirstOrDefault(t => t.name == name))
                .Where(t => t != null))
            : new HashSet<Transform>();

        foreach (var rb in GetComponentsInChildren<Rigidbody>())
        {
            if (rb.transform == transform) continue;
            if (excludedTransforms.Contains(rb.transform)) continue;

            rb.isKinematic = false;
            rb.linearVelocity = rb.angularVelocity = Vector3.zero;
            rb.AddForce(force * Random.Range(0.95f, 1.05f), ForceMode.Impulse);
            rb.AddTorque(Random.onUnitSphere * torque, ForceMode.Impulse);
        }

        foreach (var t in GetComponentsInChildren<Transform>())
        {
            if (t == transform) continue;
            if (t.TryGetComponent(out Collider col)) col.enabled = true;
            t.gameObject.layer = LayerMask.NameToLayer("Ragdoll");
        }

        foreach (Transform bone in excludedTransforms)
        {
            if (bone == null) continue;
            if (bone.TryGetComponent(out Rigidbody rb))
            {
                if (bone.TryGetComponent(out CharacterJoint joint)) Destroy(joint);
                rb.isKinematic = false;
                rb.AddForce((hitDir + Random.insideUnitSphere).normalized * sliceForce, ForceMode.Impulse);
            }
            bone.SetParent(null);
            Destroy(bone.gameObject, 5f);
        }

        Destroy(gameObject, 5f);
    }

    private BodySliceType ChooseRandomSlicePart(WeaponDataSO weapon)
    {
        if (weapon == null || weapon.possibleSliceParts == null || weapon.possibleSliceParts.Count == 0)
            return BodySliceType.None;
        return weapon.possibleSliceParts[Random.Range(0, weapon.possibleSliceParts.Count)];
    }
}
