using UnityEngine;
using UnityEngine.AI;

[RequireComponent(typeof(NavMeshAgent))]
[RequireComponent(typeof(Rigidbody))]
public class Character002_NavMesh : MonoBehaviour
{
    [Header("Identity")]
    public string characterId = "002";

    [Header("Agent Movement")]
    public float moveSpeed = 5f;
    public float acceleration = 100f;
    public float angularSpeed = 720f;
    public float stoppingDistance = 0.01f;
    public bool autoBraking = true;

    [Header("Visual Sync")]
    public bool visualSyncEnabled = true;
    public bool visualSmoothingEnabled = true;
    public float visualSmoothingSpeed = 10f;

    [Header("Control")]
    [Tooltip("입력이 거의 없으면 멈춥니다.")]
    public bool stopWhenNoInput = true;
    public float inputDeadzone = 0.05f;

    private NavMeshAgent agent;
    private Rigidbody rb;
    private Camera mainCam;

    private Vector3 lastInput = Vector3.zero;

    void Awake()
    {
        // 간단한 식별용 이름 설정 (요청대로 캐릭터 002로 등록)
        gameObject.name = $"Character {characterId}";
    }

    void Start()
    {
        agent = GetComponent<NavMeshAgent>();
        rb = GetComponent<Rigidbody>();
        mainCam = Camera.main;

        // NavMeshAgent 기본 셋업, PlayerMovement과 같은 방식으로 동작하게 함
        agent.updateRotation = false;
        agent.updateUpAxis = true;
        agent.updatePosition = false;

        agent.speed = moveSpeed;
        agent.acceleration = acceleration;
        agent.angularSpeed = angularSpeed;
        agent.stoppingDistance = stoppingDistance;
        agent.autoBraking = autoBraking;
        agent.obstacleAvoidanceType = ObstacleAvoidanceType.LowQualityObstacleAvoidance;

        // Rigidbody는 에이전트가 위치를 제어하므로 kinematic, 중력 비활성화 권장
        rb.isKinematic = true;
        rb.useGravity = false;
        rb.constraints = RigidbodyConstraints.FreezeRotation | RigidbodyConstraints.FreezePositionY;

        // NavMesh 위에 없으면 SamplePosition으로 Y 보정 (있는 경우만)
        if (NavMesh.SamplePosition(transform.position, out NavMeshHit hit, 2f, NavMesh.AllAreas))
        {
            Vector3 fixedPos = transform.position;
            fixedPos.y = hit.position.y + 0.05f;
            transform.position = fixedPos;
            rb.position = fixedPos;
            agent.nextPosition = fixedPos;
        }
    }

    void Update()
    {
        Vector2 rawMove;
        // InputManager가 있으면 우선 사용
        if (InputManager.Instance != null)
        {
            rawMove = InputManager.Instance.GetMoveInput();
        }
        else
        {
            rawMove = new Vector2(Input.GetAxisRaw("Horizontal"), Input.GetAxisRaw("Vertical"));
        }

        lastInput = new Vector3(rawMove.x, 0f, rawMove.y);

        bool hasInput = lastInput.magnitude > inputDeadzone;

        if (!hasInput && stopWhenNoInput)
        {
            if (agent != null && agent.isOnNavMesh)
            {
                agent.isStopped = true;
                if (agent.hasPath) agent.ResetPath();
                agent.velocity = Vector3.zero;
            }
            return;
        }

        if (hasInput)
        {
            if (agent != null && agent.isOnNavMesh)
            {
                agent.isStopped = false;

                Vector3 moveDir = CameraRelative(lastInput);
                if (moveDir.sqrMagnitude > 0.0001f) moveDir.Normalize();
                else moveDir = Vector3.zero;

                Vector3 displacement = moveDir * agent.speed * Time.deltaTime;

                // PlayerMovement과 동일하게 agent.Move 사용 (navmesh 내부 보정 포함)
                agent.Move(displacement);

                // 수동 회전: 이동방향으로 부드럽게 회전
                if (moveDir.sqrMagnitude > 0.0001f)
                {
                    Quaternion targetRot = Quaternion.LookRotation(moveDir, Vector3.up);
                    transform.rotation = Quaternion.Slerp(transform.rotation, targetRot, Mathf.Clamp01((agent.angularSpeed / 360f) * Time.deltaTime * 10f));
                }
            }
            else
            {
                // NavMesh가 없으면 단순 Transform 이동
                Vector3 moveDir = CameraRelative(lastInput);
                if (moveDir.sqrMagnitude > 0.0001f) moveDir.Normalize();
                transform.position += moveDir * moveSpeed * Time.deltaTime;

                if (moveDir.sqrMagnitude > 0.0001f)
                {
                    Quaternion targetRot = Quaternion.LookRotation(moveDir, Vector3.up);
                    transform.rotation = Quaternion.Slerp(transform.rotation, targetRot, Mathf.Clamp01((angularSpeed / 360f) * Time.deltaTime * 10f));
                }
            }
        }
    }

    void LateUpdate()
    {
        // PlayerMovement과 동일한 시각적 동기화 처리
        if (visualSyncEnabled && agent != null && agent.isOnNavMesh && agent.enabled)
        {
            Vector3 targetPos = agent.nextPosition;

            if (visualSmoothingEnabled)
            {
                float t = Mathf.Clamp01(visualSmoothingSpeed * Time.deltaTime);
                transform.position = Vector3.Lerp(transform.position, targetPos, t);
            }
            else
            {
                transform.position = targetPos;
            }
        }
    }

    // 카메라 기준으로 입력 방향을 월드로 변환 (PlayerMovement와 동일)
    private Vector3 CameraRelative(Vector3 input)
    {
        if (mainCam == null)
        {
            mainCam = Camera.main;
            if (mainCam == null) return input;
        }

        Vector3 camForward = mainCam.transform.forward;
        Vector3 camRight = mainCam.transform.right;
        camForward.y = 0f; camRight.y = 0f;
        camForward.Normalize(); camRight.Normalize();

        Vector3 world = camForward * input.z + camRight * input.x;
        if (world.sqrMagnitude > 0.0001f) return world.normalized;
        return Vector3.zero;
    }
}