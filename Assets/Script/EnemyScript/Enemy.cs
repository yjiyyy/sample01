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

    // ✅ 공격 상태 관리 개선
    private bool isInAttackAnimation;  // 애니메이션 재생 중 여부
    private bool isInAttackCooldown;   // 쿨다운 중 여부
    private float lastAttackTime = -999f;

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

    // ✅ 상태 전환 코루틴들 (Boss 패턴과 유사)
    private Coroutine currentStateCoroutine;
    
    public void SetState(EnemyState newState)
    {
        if (debugMode && currentState != newState)
            Debug.Log($"[EnemyState] {gameObject.name} {currentState} → {newState}");

        // 현재 실행 중인 상태 코루틴 중단
        if (currentStateCoroutine != null)
        {
            StopCoroutine(currentStateCoroutine);
            currentStateCoroutine = null;
        }

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
                currentStateCoroutine = StartCoroutine(AttackStateCoroutine());
                break;

            case EnemyState.Dead:
                animator.Play("Die", 0, 0f);
                if (agent != null && agent.isOnNavMesh) agent.isStopped = true;
                break;
        }
    }

    // ✅ 공격 상태 코루틴 (Boss 패턴과 유사)
    private IEnumerator AttackStateCoroutine()
    {
        // ✅ 공격 시작 시에만 애니메이션 재생
        animator.Play("Attack", 0, 0f);
        animator.Update(0f);
        if (attackController != null)
        {
            attackController.NotifyAttack(0);
        }
        if (agent != null && agent.isOnNavMesh) agent.isStopped = true;

        // ✅ 공격 상태 플래그 설정
        isInAttackAnimation = true;
        isInAttackCooldown = true;
        lastAttackTime = Time.time;

        // 애니메이션 완료까지 대기
        yield return StartCoroutine(WaitForAnimationComplete("Attack"));
        
        isInAttackAnimation = false;
        Debug.Log($"[Enemy] {gameObject.name} 공격 애니메이션 완료");

        // 쿨다운 처리 코루틴 시작
        yield return StartCoroutine(WaitForAttackCooldown());
        
        isInAttackCooldown = false;
        Debug.Log($"[Enemy] {gameObject.name} 공격 쿨다운 완료");

        // 다음 상태로 전환 (Chase로 복귀)
        SetState(EnemyState.Chase);
    }

    // ✅ 애니메이션 완료까지 대기하는 헬퍼 코루틴
    private IEnumerator WaitForAnimationComplete(string animationName)
    {
        yield return null; // 한 프레임 대기

        while (true)
        {
            AnimatorStateInfo stateInfo = animator.GetCurrentAnimatorStateInfo(0);

            // 지정된 애니메이션이 거의 끝났는지 체크 (95% 완료)
            if (stateInfo.IsName(animationName) && stateInfo.normalizedTime >= 0.95f)
            {
                break;
            }

            yield return null;
        }
    }

    // ✅ 공격 쿨다운 대기 코루틴
    private IEnumerator WaitForAttackCooldown()
    {
        float cooldown = GetAttackCooldown();
        yield return new WaitForSeconds(cooldown);
    }

    void HandleChase()
    {
        if (player == null || agent == null || !agent.isOnNavMesh) return;

        Vector3 dir = player.position - transform.position;
        float distance = dir.magnitude;

        float attackRange = attackController != null && attackController.AttackCount > 0
            ? attackController.GetAttackRange(0)
            : 2f;

        // ✅ 쿨다운 체크 추가
        bool canAttack = !isInAttackCooldown && (Time.time >= lastAttackTime + GetAttackCooldown());

        if (distance < attackRange && canAttack)
        {
            SetState(EnemyState.Attack);
            return;
        }

        agent.isStopped = false;
        if (agent.enabled && agent.isOnNavMesh)
        {
            agent.SetDestination(player.position);
        }
        
        // 애니메이션 컨트롤러가 있는 경우에만 업데이트
        if (anim != null)
        {
            anim.UpdateMovement(agent.velocity.magnitude);
        }

        dir.y = 0f;
        if (dir != Vector3.zero)
            transform.rotation = Quaternion.LookRotation(dir);
    }

    void HandleAttack()
    {
        // Attack 상태는 이제 AttackStateCoroutine에서 완전히 처리됩니다
        // HandleAttack은 필요시에만 사용 (예: 특별한 공격 로직)
    }

    // ✅ 공격 쿨다운 가져오기 헬퍼 메서드
    private float GetAttackCooldown()
    {
        return attackController != null && attackController.AttackCount > 0
            ? attackController.GetAttackCooldown(0)
            : 1f;
    }

    /* ───────── 데미지 처리 ───────── */
    public void OnDamage(Vector3 hitDir, WeaponDataSO weapon, float impactScale = 1f)
    {
        if (currentState == EnemyState.Dead) return;

        // ✅ 공격 상태 초기화
        isInAttackAnimation = false;
        isInAttackCooldown = false;

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

        bool canAttack = !isInAttackCooldown && (Time.time >= lastAttackTime + GetAttackCooldown());

        if (player != null && Vector3.Distance(transform.position, player.position) < attackRange && canAttack)
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

    /* ───────── 사망 처리 (기존 코드 유지) ───────── */
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

    // ... 나머지 사망 처리 메서드들은 기존과 동일 ...
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