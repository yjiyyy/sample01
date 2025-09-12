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

    private bool isKnockbacked = false;
    private Vector3 knockbackDirection;
    private float knockbackSpeed;
    private float knockbackDuration;
    private float knockbackTimer;

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

        agent.obstacleAvoidanceType = ObstacleAvoidanceType.LowQualityObstacleAvoidance;

        // Rigidbody 설정 (Unity 표준 API)
        rb.isKinematic = false;
        rb.useGravity = false;
        rb.mass = 1f;
        rb.linearDamping = 5f;          // was: linearDamping
        rb.angularDamping = 5f;   // was: angularDamping
        rb.constraints = RigidbodyConstraints.FreezeRotation | RigidbodyConstraints.FreezePositionY;

        GameManager.Instance.playerTransform = this.transform;
        lastPosition = transform.position;

        if (NavMesh.SamplePosition(transform.position, out NavMeshHit hit, 2f, NavMesh.AllAreas))
        {
            Vector3 fixedPos = transform.position;
            fixedPos.y = hit.position.y + 0.05f;
            transform.position = fixedPos;
            rb.position = fixedPos;
        }
    }

    void Update()
    {
        if (isKnockbacked)
        {
            knockbackTimer += Time.deltaTime;
            float t = knockbackTimer / knockbackDuration;
            t = Mathf.Clamp01(t);
            float currentSpeed = knockbackSpeed * (1f - t);
            Vector3 displacement = knockbackDirection * currentSpeed * Time.deltaTime;
            transform.position += displacement;

            if (knockbackTimer >= knockbackDuration)
            {
                isKnockbacked = false;
                Debug.Log("[PlayerMovement] 넉백 종료");
            }
            return;
        }

        var weaponCtrl = GetComponent<PlayerWeaponController>();
        if (weaponCtrl != null)
        {
            if (weaponCtrl.CurrentState == PlayerState.Attack ||
                weaponCtrl.CurrentState == PlayerState.Knockback ||
                weaponCtrl.CurrentState == PlayerState.Stun ||
                weaponCtrl.CurrentState == PlayerState.Dead ||
                weaponCtrl.CurrentState == PlayerState.Evade)
            {
                if (CanUseAgent())
                {
                    agent.ResetPath();
                    agent.isStopped = true;
                    agent.velocity = Vector3.zero;
                }
                return;
            }
        }

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

        if (InputManager.Instance.GetDamageTestInput())
        {
            if (TryGetComponent(out Health health))
            {
                health.ApplyDamage(10f);
                Debug.Log("[테스트] 플레이어에게 10 데미지 적용 (-키)");
            }
        }

        if (InputManager.Instance.GetHealTestInput())
        {
            if (TryGetComponent(out Health health))
            {
                health.Heal(20f);
                Debug.Log("[테스트] 플레이어 체력 20 회복 (=키)");
            }
        }
    }

    void LateUpdate()
    {
        bool shouldStop = !isKnockbacked && lastInput.magnitude < 0.01f;

        if (shouldStop)
        {
            if (agent.enabled && agent.isOnNavMesh)
            {
                agent.enabled = false;
                Debug.Log("🛑 NavMeshAgent 꺼짐 + 회전 고정");
            }

            transform.rotation = Quaternion.Euler(0f, transform.rotation.eulerAngles.y, 0f);
            rb.linearVelocity = Vector3.zero;        // was: linearVelocity
            rb.angularVelocity = Vector3.zero;
        }

        lastPosition = transform.position;
    }

    public void ApplyKnockback(Vector3 direction, float force, float duration, Transform attacker = null)
    {
        isKnockbacked = true;
        knockbackDirection = direction.normalized;

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

        if (attacker != null)
        {
            Vector3 lookDir = (attacker.position - transform.position).normalized;
            lookDir.y = 0;
            if (lookDir != Vector3.zero)
                transform.rotation = Quaternion.LookRotation(lookDir);
        }
    }

    public bool IsCurrentlyKnockbacked() => isKnockbacked;
    public float GetVelocityMagnitude() => agent != null ? agent.velocity.magnitude : 0f;
    private bool CanUseAgent() => agent.enabled && agent.isOnNavMesh;

    Vector3 CameraRelative(Vector3 input)
    {
        Vector3 camForward = mainCam.transform.forward;
        Vector3 camRight = mainCam.transform.right;
        camForward.y = 0; camRight.y = 0;
        camForward.Normalize(); camRight.Normalize();
        return (camForward * input.z + camRight * input.x).normalized;
    }
}