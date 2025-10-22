using UnityEngine;

[DisallowMultipleComponent]
public class InputManager : MonoBehaviour
{
    public static InputManager Instance { get; private set; }

    [Header("Move Input Settings")]
    [Tooltip("게임패드 스틱 입력 데드존(이 값보다 작으면 0으로 처리)")]
    [Range(0f, 0.5f)]
    public float gamepadDeadzone = 0.15f;

    [Header("Mobile Settings")]
    [Tooltip("에디터에서도 모바일 UI/입력을 강제로 활성화")]
    public bool forceMobileInEditor = false;

    // ───────── Mobile state (조이스틱/버튼에서 주입) ─────────
    private Vector2 mobileMove; // -1..1
    private bool mobileAttackPressed;
    private int mobileAttackDownFrames;
    private int mobileAttackUpFrames;

    private bool mobileEvadePressed;
    private int mobileEvadeDownFrames;
    private int mobileEvadeUpFrames;

    private bool IsMobileRuntimeActive =>
#if UNITY_EDITOR
        forceMobileInEditor;
#else
        Application.isMobilePlatform;
#endif

    // 도메인 리로드 비활성 시에도 정적 필드 초기화
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics()
    {
        Instance = null;
    }

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
#if UNITY_EDITOR
            Debug.LogWarning("[InputManager] Duplicate detected. Destroying this component (not GameObject).", this);
#endif
            // 호스트 오브젝트 파괴 대신, 컴포넌트만 제거
            Destroy(this);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject); // 씬 전환 시에도 유지
    }

    private void OnDestroy()
    {
        if (Instance == this)
            Instance = null;
    }

    private void LateUpdate()
    {
        // 모바일 엣지 신호는 1~2프레임만 유지
        if (mobileAttackDownFrames > 0) mobileAttackDownFrames--;
        if (mobileAttackUpFrames > 0) mobileAttackUpFrames--;
        if (mobileEvadeDownFrames > 0) mobileEvadeDownFrames--;
        if (mobileEvadeUpFrames > 0) mobileEvadeUpFrames--;
    }

    /* ───── 이동 입력 (전역으로 방향키 제외) ───── */
    public Vector2 GetMoveInput()
    {
        // 0) 모바일 조이스틱이 활성값이면 최우선 사용
        if (IsMobileRuntimeActive && mobileMove.sqrMagnitude > 0.0001f)
            return mobileMove;

        // 1) 키보드: WASD만 직접 계산
        float x = 0f, y = 0f;
        if (Input.GetKey(KeyCode.A)) x -= 1f;
        if (Input.GetKey(KeyCode.D)) x += 1f;
        if (Input.GetKey(KeyCode.S)) y -= 1f;
        if (Input.GetKey(KeyCode.W)) y += 1f;

        Vector2 wasd = new Vector2(x, y);
        if (wasd.sqrMagnitude > 0.0001f)
            return wasd;

        // 2) 방향키가 눌린 경우 → 이동에 영향 0 (전역 차단)
        bool arrowHeld =
            Input.GetKey(KeyCode.UpArrow) ||
            Input.GetKey(KeyCode.DownArrow) ||
            Input.GetKey(KeyCode.LeftArrow) ||
            Input.GetKey(KeyCode.RightArrow);

        if (arrowHeld)
            return Vector2.zero;

        // 3) 게임패드/축 입력(방향키 영향 제거를 위해 방향키가 눌린 프레임엔 무시)
        float h = Input.GetAxisRaw("Horizontal");
        float v = Input.GetAxisRaw("Vertical");
        Vector2 gamepad = new Vector2(h, v);

        if (gamepad.magnitude < gamepadDeadzone)
            return Vector2.zero;

        return gamepad;
    }

    /* ───── 무기 교체 입력 ───── */
    public int GetWeaponSwapInput()
    {
        // 1~9번 키 → 무기 슬롯 번호 반환
        if (Input.GetKeyDown(KeyCode.Alpha1)) return 1;
        if (Input.GetKeyDown(KeyCode.Alpha2)) return 2;
        if (Input.GetKeyDown(KeyCode.Alpha3)) return 3;
        if (Input.GetKeyDown(KeyCode.Alpha4)) return 4;
        if (Input.GetKeyDown(KeyCode.Alpha5)) return 5;
        if (Input.GetKeyDown(KeyCode.Alpha6)) return 6;
        if (Input.GetKeyDown(KeyCode.Alpha7)) return 7;
        if (Input.GetKeyDown(KeyCode.Alpha8)) return 8;
        if (Input.GetKeyDown(KeyCode.Alpha9)) return 9;

        return -1; // 입력 없음
    }

    /* ───── 공격 입력(즉발) ───── */
    public bool GetAttackInput()
    {
        // 기존 설계상 GetAttackInput은 즉발(Down)로 사용
        return GetAttackDown();
    }

    /* ───── 홀드/업(차지 모니터링) ───── */
    public bool GetAttackDown()
    {
        bool kb = Input.GetKeyDown(KeyCode.Alpha0);
        bool mobile = IsMobileRuntimeActive && mobileAttackDownFrames > 0;
        return kb || mobile;
    }

    public bool GetAttack()
    {
        bool kb = Input.GetKey(KeyCode.Alpha0);
        bool mobile = IsMobileRuntimeActive && mobileAttackPressed;
        return kb || mobile;
    }

    public bool GetAttackUp()
    {
        bool kb = Input.GetKeyUp(KeyCode.Alpha0);
        bool mobile = IsMobileRuntimeActive && mobileAttackUpFrames > 0;
        return kb || mobile;
    }

    /* ───── 회피 입력 ───── */
    public bool GetEvadeInput()
    {
        bool kb = Input.GetKeyDown(KeyCode.Space);
        bool mobile = IsMobileRuntimeActive && mobileEvadeDownFrames > 0;
        return kb || mobile;
    }

    /* ───── 테스트 입력 ───── */
    public bool GetDamageTestInput() => Input.GetKeyDown(KeyCode.Minus);      // - 키
    public bool GetHealTestInput() => Input.GetKeyDown(KeyCode.Equals);       // = 키 (+ 키)

    // ───────── Mobile setters (UI에서 호출) ─────────
    public void SetMobileMove(Vector2 v)
    {
        // Clamp -1..1
        v = Vector2.ClampMagnitude(v, 1f);
        mobileMove = v;
    }

    public void MobileAttackDown()
    {
        mobileAttackPressed = true;
        mobileAttackDownFrames = 2; // 1~2프레임 유지
    }

    public void MobileAttackUp()
    {
        mobileAttackPressed = false;
        mobileAttackUpFrames = 2;
    }

    public void MobileEvadeDown()
    {
        mobileEvadePressed = true;
        mobileEvadeDownFrames = 2;
    }

    public void MobileEvadeUp()
    {
        mobileEvadePressed = false;
        mobileEvadeUpFrames = 2;
    }
}