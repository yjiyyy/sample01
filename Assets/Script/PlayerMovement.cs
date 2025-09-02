using UnityEngine;
using UnityEngine.AI;
using System.Collections;

[RequireComponent(typeof(NavMeshAgent))]
[RequireComponent(typeof(Rigidbody))]
public class PlayerMovement : MonoBehaviour
{
    [Header("이동 속도 조절")]
    public float moveSpeed = 5f;
    public float acceleration = 100f;
    public float angularSpeed = 720f;
    public float stoppingDistance = 0.01f;
    public bool autoBraking = true;

    [Header("컨트롤 옵션")]
    public bool stopWhenNoInput = true;

    private NavMeshAgent agent;
    private Rigidbody rb;
    private Camera mainCam;
    private float baseSpeed;

    private bool isKnockbacked = false;
    private Vector3 knockbackDirection;
    private float knockbackSpeed;
    private float knockbackDuration;
    private float knockbackTimer;

    private bool isPushing = false;
    private float slowMultiplier = 0.4f;

    private Vector3 lastInput = Vector3.zero;
    private Vector3 lastPosition;

    void Start()
    {
        agent = GetComponent<NavMeshAgent>();
        rb = GetComponent<Rigidbody>();
        mainCam = Camera.main;

        agent.updateRotation = false;
        agent.updateUpAxis = true;

        agent.speed = moveSpeed;
        agent.acceleration = acceleration;
        agent.angularSpeed = angularSpeed;
        agent.stoppingDistance = stoppingDistance;
        agent.autoBraking = autoBraking;

        GameManager.Instance.playerTransform = this.transform;
        baseSpeed = agent.speed;
        lastPosition = transform.position;

        // ✅ NavMesh에 맞춘 Y좌표 초기 보정
        if (NavMesh.SamplePosition(transform.position, out NavMeshHit hit, 2f, NavMesh.AllAreas))
        {
            Vector3 fixedPos = transform.position;
            fixedPos.y = hit.position.y + 0.05f; // 살짝 띄워 안정성 확보
            transform.position = fixedPos;
            rb.position = fixedPos;
        }
    }

    void Update()
    {
        // ✅ 넉백 처리 먼저 실행
        if (isKnockbacked)
        {
            knockbackTimer += Time.deltaTime;

            // 남은 시간 비율 (0 → 시작, 1 → 끝)
            float t = knockbackTimer / knockbackDuration;
            t = Mathf.Clamp01(t);

            // 선형 감속 (처음엔 full speed, 끝나갈수록 0)
            float currentSpeed = knockbackSpeed * (1f - t);

            Vector3 displacement = knockbackDirection * currentSpeed * Time.deltaTime;
            transform.position += displacement;

            Debug.Log($"[Knockback] speed={currentSpeed:F2}, disp={displacement}");

            if (knockbackTimer >= knockbackDuration)
            {
                isKnockbacked = false;
                Debug.Log("[Knockback] 끝");
            }

            return; // 넉백 중이면 입력/에이전트 무시
        }

        // ✅ 무기 컨트롤러 상태 확인
        var weaponCtrl = GetComponent<PlayerWeaponController>();
        if (weaponCtrl != null)
        {
            if (weaponCtrl.CurrentState == PlayerState.Attack ||
                weaponCtrl.CurrentState == PlayerState.Knockback ||
                weaponCtrl.CurrentState == PlayerState.Stun)
            {
                if (CanUseAgent())
                {
                    agent.ResetPath();
                    agent.isStopped = true;
                    agent.velocity = Vector3.zero;
                }
                return; // 이동 처리 완전히 차단
            }
        }

        // ✅ 일반 이동 처리
        Vector2 moveInput = InputManager.Instance.GetMoveInput();
        lastInput = new Vector3(moveInput.x, 0, moveInput.y);

        if (lastInput.magnitude > 0.1f && !agent.enabled)
        {
            agent.enabled = true;
        }

        if (lastInput.magnitude > 0.1f)
        {
            if (CanUseAgent())
            {
                agent.isStopped = false;
                Vector3 moveDir = CameraRelative(lastInput);
                Vector3 destination = transform.position + moveDir;

                agent.SetDestination(destination);

                Quaternion rot = Quaternion.LookRotation(moveDir);
                transform.rotation = Quaternion.Slerp(transform.rotation, rot, Time.deltaTime * 20f);
            }
        }
        else if (stopWhenNoInput)
        {
            if (CanUseAgent())
            {
                agent.ResetPath();
                agent.SetDestination(transform.position);
                agent.velocity = Vector3.zero;
            }
        }

        agent.speed = isPushing ? baseSpeed * slowMultiplier : baseSpeed;

        // ✅ 테스트 입력 (데미지 / 회복)
        if (InputManager.Instance.GetDamageTestInput())
        {
            if (TryGetComponent(out Health health))
            {
                health.ApplyDamage(10f);
                Debug.Log("[테스트] 플레이어에게 10 데미지 적용");
            }
        }

        if (InputManager.Instance.GetHealTestInput())
        {
            if (TryGetComponent(out Health health))
            {
                health.Heal(20f);
                Debug.Log("[테스트] 플레이어 체력 20 회복");
            }
        }
    }


    void LateUpdate()
    {
        bool shouldStop =
            !isKnockbacked &&
            !isPushing &&
            lastInput.magnitude < 0.01f;

        if (shouldStop)
        {
            if (agent.enabled && agent.isOnNavMesh)
            {
                agent.enabled = false;
                Debug.Log("🛑 NavMeshAgent 꺼짐 + 회전 고정");
            }

            transform.rotation = Quaternion.Euler(0f, transform.rotation.eulerAngles.y, 0f);
            rb.linearVelocity = Vector3.zero;
            rb.angularVelocity = Vector3.zero;
        }

        if (isPushing)
        {
            Collider[] hits = Physics.OverlapSphere(transform.position, 0.6f, LayerMask.GetMask("Enemy"));
            if (hits.Length == 0)
            {
                isPushing = false;
                Debug.Log("🧯 밀기 상태 강제 해제 (적 없음)");
            }
        }

        lastPosition = transform.position;
    }

    void OnCollisionStay(Collision collision)
    {
        if (collision.gameObject.CompareTag("Enemy"))
        {
            Vector3 moveDir = agent.desiredVelocity.normalized;
            bool pushing = false;

            foreach (ContactPoint contact in collision.contacts)
            {
                Vector3 normal = contact.normal;
                float dot = Vector3.Dot(moveDir, -normal);
                if (dot > 0.5f)
                {
                    pushing = true;
                    break;
                }
            }

            isPushing = pushing;
        }
    }

    void OnCollisionExit(Collision collision)
    {
        if (collision.gameObject.CompareTag("Enemy"))
        {
            isPushing = false;
        }
    }

    // 기존
    public void ApplyKnockback(Vector3 direction, float force, float duration)
    {
        isKnockbacked = true;
        knockbackDirection = direction.normalized;
        knockbackSpeed = force;
        knockbackDuration = duration;
        knockbackTimer = 0f;

        if (CanUseAgent())
        {
            agent.ResetPath();
            agent.isStopped = true;
        }
    }

    // 수정 후
    public void ApplyKnockback(Vector3 direction, float force, float duration, Transform attacker = null)
    {
        isKnockbacked = true;

        knockbackDirection = direction.normalized;

        // ✅ weight 반영
        float finalForce = force;
        if (TryGetComponent(out Health health))
        {
            finalForce /= Mathf.Max(0.01f, health.GetWeight());
        }
        knockbackSpeed = finalForce;

        knockbackDuration = duration;
        knockbackTimer = 0f;

        if (CanUseAgent())
        {
            agent.ResetPath();
            agent.isStopped = true;
        }

        // ✅ 바라보는 방향은 공격자 쪽
        if (attacker != null)
        {
            Vector3 lookDir = (attacker.position - transform.position).normalized;
            lookDir.y = 0;
            if (lookDir != Vector3.zero)
                transform.rotation = Quaternion.LookRotation(lookDir);
        }
    }




    public bool IsCurrentlyKnockbacked()
    {
        return isKnockbacked;
    }

    public float GetVelocityMagnitude()
    {
        return agent != null ? agent.velocity.magnitude : 0f;
    }

    private bool CanUseAgent()
    {
        return agent.enabled && agent.isOnNavMesh;
    }

    Vector3 CameraRelative(Vector3 input)
    {
        Vector3 camForward = mainCam.transform.forward;
        Vector3 camRight = mainCam.transform.right;
        camForward.y = 0; camRight.y = 0;
        camForward.Normalize(); camRight.Normalize();
        return (camForward * input.z + camRight * input.x).normalized;
    }
}
