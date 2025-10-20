using UnityEngine;

public class InputManager : MonoBehaviour
{
    public static InputManager Instance { get; private set; }

    [Header("Move Input Settings")]
    [Tooltip("게임패드 스틱 입력 데드존(이 값보다 작으면 0으로 처리)")]
    [Range(0f, 0.5f)]
    public float gamepadDeadzone = 0.15f;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(this.gameObject);
        }
        else
        {
            Instance = this;
            DontDestroyOnLoad(gameObject); // 씬 전환 시에도 유지
        }
    }

    /* ───── 이동 입력 (전역으로 방향키 제외) ───── */
    public Vector2 GetMoveInput()
    {
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
        // 오직 0번 키로만 공격
        return Input.GetKeyDown(KeyCode.Alpha0);
    }

    /* ───── 🆕 홀드/업(차지 모니터링) ───── */
    public bool GetAttackDown() => Input.GetKeyDown(KeyCode.Alpha0);
    public bool GetAttack() => Input.GetKey(KeyCode.Alpha0);
    public bool GetAttackUp() => Input.GetKeyUp(KeyCode.Alpha0);

    /* ───── ✅ 회피 입력 ───── */
    public bool GetEvadeInput()
    {
        return Input.GetKeyDown(KeyCode.Space);
    }

    /* ───── 테스트 입력 ───── */
    public bool GetDamageTestInput() => Input.GetKeyDown(KeyCode.Minus);      // - 키
    public bool GetHealTestInput() => Input.GetKeyDown(KeyCode.Equals);       // = 키 (+ 키)
}