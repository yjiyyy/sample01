using UnityEngine;

/// <summary>
/// Unity6(6000.0.42f1) 환경에서 Rigidbody가 '떨어지지 않는' 문제를 진단하기 위한 디버그 도구.
/// Char002 같은 객체에 부착해서 중력 적용 상태, 속도 변화, Sleep 여부 등을 주기적으로 로그로 출력합니다.
/// </summary>
[DisallowMultipleComponent]
public class RigidbodyGravityDebugger : MonoBehaviour
{
    [Header("로그 옵션")]
    [Tooltip("GameManager.isDebugMode가 false여도 강제로 로그 출력할지 여부")]
    public bool ignoreGlobalDebugFlag = false;

    [Tooltip("FixedUpdate마다 로그를 찍지 않고, 지정한 프레임 간격마다 한 번 찍습니다.")]
    [Min(1)] public int logEveryNFixedFrames = 10;

    [Tooltip("최대 몇 번까지 자세한 로그를 찍을지 (0이면 무제한)")]
    public int maxDetailedLogs = 0;

    [Tooltip("시작 시점에 환경(중력, timeScale 등) 1회 요약 로그 출력")]
    public bool logEnvironmentOnStart = true;

    [Header("자동 감지 / 수정")]
    [Tooltip("중력이 거의 0이면 기본 중력(-9.81)으로 복구 시도")]
    public bool autoFixZeroGravity = true;

    [Tooltip("Rigidbody가 Sleep 상태이면 WakeUp() 호출")]
    public bool wakeSleepingBody = true;

    [Header("테스트 기능")]
    [Tooltip("플레이 시 최초 1회 아래로 떨어지는지 확인하기 위해 약간의 위/아래 힘을 줍니다.")]
    public bool applyTestImpulse = false;
    [Tooltip("임펄스 크기 (위쪽 양수). 0이면 위로 힘을 주지 않고 살짝 아래로만 속도 초기화")]
    public float upwardImpulse = 2f;

    [Tooltip("바닥과의 거리 추정을 위해 아래로 레이캐스트 길이")]
    public float groundRayLength = 10f;

    [Tooltip("Raycast로 Ground 레이어만 검사하고 싶다면 설정 (0이면 모든 레이어)")]
    public LayerMask groundLayerMask = 0;

    private Rigidbody rb;
    private int fixedFrameCounter;
    private int detailedLogCount;
    private Vector3 initialPosition;
    private bool impulseApplied;

    void Awake()
    {
        rb = GetComponent<Rigidbody>();
        if (rb == null)
        {
            Debug.LogError($"[RigidbodyGravityDebugger] '{name}'에 Rigidbody가 없습니다.");
            enabled = false;
            return;
        }
        initialPosition = transform.position;
    }

    void Start()
    {
        if (logEnvironmentOnStart && ShouldLog())
        {
            Debug.Log($"[RigidbodyGravityDebugger] Start 환경 정보: " +
                      $"gravity={Physics.gravity}, timeScale={Time.timeScale}, fixedDelta={Time.fixedDeltaTime}, " +
                      $"isKinematic={rb.isKinematic}, useGravity={rb.useGravity}, constraints={rb.constraints}, " +
                      $"initialY={initialPosition.y}");
        }

        // 자동 중력 복구
        if (autoFixZeroGravity && Physics.gravity.sqrMagnitude < 0.001f)
        {
            Physics.gravity = new Vector3(0, -9.81f, 0);
            if (ShouldLog())
                Debug.Log("[RigidbodyGravityDebugger] 중력이 0 또는 매우 작아서 기본 중력으로 복구했습니다: (0,-9.81,0)");
        }

        // 즉시 Sleep 상태면 깨우기
        if (wakeSleepingBody && rb.IsSleeping())
        {
            rb.WakeUp();
            if (ShouldLog())
                Debug.Log("[RigidbodyGravityDebugger] Rigidbody가 Sleep 상태여서 WakeUp() 호출.");
        }
    }

    void FixedUpdate()
    {
        fixedFrameCounter++;

        // 최초 임펄스 테스트
        if (applyTestImpulse && !impulseApplied)
        {
            impulseApplied = true;
            // 아래로 관성 테스트 위해 현재 속도 초기화
            rb.linearVelocity = Vector3.zero;

            if (upwardImpulse > 0f)
            {
                rb.AddForce(Vector3.up * upwardImpulse, ForceMode.Impulse);
                if (ShouldLog())
                    Debug.Log($"[RigidbodyGravityDebugger] Upward 임펄스 적용: {upwardImpulse}");
            }
            else
            {
                // 아래로 떨어질 준비를 위해 아주 미세한 음의 속도 부여 (옵션)
                rb.AddForce(Vector3.down * 0.1f, ForceMode.Impulse);
                if (ShouldLog())
                    Debug.Log("[RigidbodyGravityDebugger] 미세한 하향 임펄스 적용 (속도 확인).");
            }
        }

        bool doLog = (fixedFrameCounter % logEveryNFixedFrames == 0);
        if (!doLog) return;

        if (maxDetailedLogs > 0 && detailedLogCount >= maxDetailedLogs)
            return;

        detailedLogCount++;

        if (ShouldLog())
        {
            string state = $"y={transform.position.y:F3}, velY={rb.linearVelocity.y:F3}, isKinematic={rb.isKinematic}, useGravity={rb.useGravity}, sleeping={rb.IsSleeping()}";

            // 바닥 Raycast
            Ray ray = new Ray(transform.position, Vector3.down);
            if (Physics.Raycast(ray, out RaycastHit hit, groundRayLength, groundLayerMask.value == 0 ? Physics.DefaultRaycastLayers : groundLayerMask))
            {
                state += $", groundDist={hit.distance:F3}, groundObj={hit.collider.name}";
            }
            else
            {
                state += ", groundDist=none";
            }

            Debug.Log($"[RigidbodyGravityDebugger] {state}");
        }

        // 수상한 상황 감지
        if (rb.useGravity && !rb.isKinematic && Mathf.Abs(rb.linearVelocity.y) < 0.0001f)
        {
            // Y 속도가 거의 0인데 공중에 떠 있다면 (지면과 거리가 큰데 속도 0)
            if (!Physics.Raycast(transform.position, Vector3.down, groundRayLength))
            {
                if (ShouldLog())
                    Debug.LogWarning("[RigidbodyGravityDebugger] 속도Y≈0 & 지면 미검출 → 중력이 적용되지 않은 것처럼 보입니다. gravity / timeScale / 외부 스크립트 확인 필요.");
            }
        }
    }

    private bool ShouldLog()
    {
        if (ignoreGlobalDebugFlag) return true;
        // GameManager가 있으면 isDebugMode에 따라
        if (GameManager.Instance != null)
        {
            return GameManager.Instance.isDebugMode;
        }
        // GameManager 없으면 기본적으로 출력
        return true;
    }

    void OnDrawGizmosSelected()
    {
        // 디버그 Ray 시각화
        Gizmos.color = Color.yellow;
        Vector3 start = Application.isPlaying ? transform.position : transform.position;
        Gizmos.DrawLine(start, start + Vector3.down * groundRayLength);
        Gizmos.DrawWireSphere(start + Vector3.down * groundRayLength, 0.05f);
    }
}