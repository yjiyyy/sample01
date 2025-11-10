using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Controls;
#endif

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

    // mobileEvadePressed는 사용하지 않으므로 프레임 펄스만 유지
    private int mobileEvadeDownFrames;
    private int mobileEvadeUpFrames;

    // 새 플래그: 오버레이가 열려있을 때 모바일 입력을 차단하려면 true로 설정
    [HideInInspector]
    public bool OverlayInputBlocked = false;

    private bool IsMobileRuntimeActive =>
#if UNITY_EDITOR
        forceMobileInEditor;
#else
        Application.isMobilePlatform;
#endif

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
            Destroy(this);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);
    }

    private void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    private void LateUpdate()
    {
        if (mobileAttackDownFrames > 0) mobileAttackDownFrames--;
        if (mobileAttackUpFrames > 0) mobileAttackUpFrames--;
        if (mobileEvadeDownFrames > 0) mobileEvadeDownFrames--;
        if (mobileEvadeUpFrames > 0) mobileEvadeUpFrames--;
    }

    /* ───── 이동 입력 ───── */
    public Vector2 GetMoveInput()
    {
        // 모바일 런타임이고, 오버레이가 차단중이면 모바일 입력 무시
        if (IsMobileRuntimeActive && !OverlayInputBlocked && mobileMove.sqrMagnitude > 0.0001f)
            return mobileMove;

        float x = 0f, y = 0f;
        if (GetKey(KeyCode.A)) x -= 1f;
        if (GetKey(KeyCode.D)) x += 1f;
        if (GetKey(KeyCode.S)) y -= 1f;
        if (GetKey(KeyCode.W)) y += 1f;

        Vector2 wasd = new Vector2(x, y);
        if (wasd.sqrMagnitude > 0.0001f)
            return wasd;

        bool arrowHeld =
            GetKey(KeyCode.UpArrow) ||
            GetKey(KeyCode.DownArrow) ||
            GetKey(KeyCode.LeftArrow) ||
            GetKey(KeyCode.RightArrow);

        if (arrowHeld)
            return Vector2.zero;

        float h = GetAxisRaw("Horizontal");
        float v = GetAxisRaw("Vertical");
        Vector2 gamepad = new Vector2(h, v);

        if (gamepad.magnitude < gamepadDeadzone)
            return Vector2.zero;

        return gamepad;
    }

    /* ───── 무기 슬롯 ───── */
    public int GetWeaponSwapInput()
    {
        // 오버레이가 모바일 입력을 막고 있으면 슬롯 입력 무시
        if (IsMobileRuntimeActive && OverlayInputBlocked)
            return -1;

        if (GetKeyDown(KeyCode.Alpha1)) return 1;
        if (GetKeyDown(KeyCode.Alpha2)) return 2;
        if (GetKeyDown(KeyCode.Alpha3)) return 3;
        if (GetKeyDown(KeyCode.Alpha4)) return 4;
        if (GetKeyDown(KeyCode.Alpha5)) return 5;
        if (GetKeyDown(KeyCode.Alpha6)) return 6;
        if (GetKeyDown(KeyCode.Alpha7)) return 7;
        if (GetKeyDown(KeyCode.Alpha8)) return 8;
        if (GetKeyDown(KeyCode.Alpha9)) return 9;
        return -1;
    }

    /* ───── 공격(즉발/홀드/업) ───── */
    public bool GetAttackInput() => GetAttackDown();

    public bool GetAttackDown()
    {
        bool kb = GetKeyDown(KeyCode.Alpha0);
        bool mobile = IsMobileRuntimeActive && !OverlayInputBlocked && mobileAttackDownFrames > 0;
        return kb || mobile;
    }

    public bool GetAttack()
    {
        bool kb = GetKey(KeyCode.Alpha0);
        bool mobile = IsMobileRuntimeActive && !OverlayInputBlocked && mobileAttackPressed;
        return kb || mobile;
    }

    public bool GetAttackUp()
    {
        bool kb = GetKeyUp(KeyCode.Alpha0);
        bool mobile = IsMobileRuntimeActive && !OverlayInputBlocked && mobileAttackUpFrames > 0;
        return kb || mobile;
    }

    /* ───── 회피 ───── */
    public bool GetEvadeInput()
    {
        bool kb = GetKeyDown(KeyCode.Space);
        bool mobile = IsMobileRuntimeActive && !OverlayInputBlocked && mobileEvadeDownFrames > 0;
        return kb || mobile;
    }

    /* ───── 테스트 키 ───── */
    public bool GetDamageTestInput() => GetKeyDown(KeyCode.Minus);
    public bool GetHealTestInput() => GetKeyDown(KeyCode.Equals);

    // Mobile setters (UI에서 호출)
    public void SetMobileMove(Vector2 v) => mobileMove = Vector2.ClampMagnitude(v, 1f);
    public void MobileAttackDown() { mobileAttackPressed = true; mobileAttackDownFrames = 2; }
    public void MobileAttackUp() { mobileAttackPressed = false; mobileAttackUpFrames = 2; }
    public void MobileEvadeDown() { mobileEvadeDownFrames = 2; }
    public void MobileEvadeUp() { mobileEvadeUpFrames = 2; }

    // ───────── InputSystem-compatible wrappers ─────────
    public bool GetKey(KeyCode kc)
    {
#if ENABLE_INPUT_SYSTEM
        var kb = Keyboard.current;
        if (kb == null) return false;

        // safe access (some KeyControl properties can be null on some platforms)
        switch (kc)
        {
            case KeyCode.A: return kb.aKey != null && kb.aKey.isPressed;
            case KeyCode.D: return kb.dKey != null && kb.dKey.isPressed;
            case KeyCode.S: return kb.sKey != null && kb.sKey.isPressed;
            case KeyCode.W: return kb.wKey != null && kb.wKey.isPressed;
            case KeyCode.UpArrow: return kb.upArrowKey != null && kb.upArrowKey.isPressed;
            case KeyCode.DownArrow: return kb.downArrowKey != null && kb.downArrowKey.isPressed;
            case KeyCode.LeftArrow: return kb.leftArrowKey != null && kb.leftArrowKey.isPressed;
            case KeyCode.RightArrow: return kb.rightArrowKey != null && kb.rightArrowKey.isPressed;
            case KeyCode.Space: return kb.spaceKey != null && kb.spaceKey.isPressed;
            case KeyCode.BackQuote: return kb.backquoteKey != null && kb.backquoteKey.isPressed;
            case KeyCode.PageDown: return kb.pageDownKey != null && kb.pageDownKey.isPressed;
            case KeyCode.PageUp: return kb.pageUpKey != null && kb.pageUpKey.isPressed;
            case KeyCode.Return:
            case KeyCode.KeypadEnter: return kb.enterKey != null && kb.enterKey.isPressed;
            case KeyCode.Escape: return kb.escapeKey != null && kb.escapeKey.isPressed;
            case KeyCode.K: return kb.kKey != null && kb.kKey.isPressed;
            case KeyCode.Minus: return kb.minusKey != null && kb.minusKey.isPressed;
            case KeyCode.Equals: return kb.equalsKey != null && kb.equalsKey.isPressed;
            default:
                if (kc >= KeyCode.Alpha0 && kc <= KeyCode.Alpha9)
                {
                    int n = kc - KeyCode.Alpha0;
                    var digits = new KeyControl[] {
                        kb.digit0Key, kb.digit1Key, kb.digit2Key, kb.digit3Key, kb.digit4Key,
                        kb.digit5Key, kb.digit6Key, kb.digit7Key, kb.digit8Key, kb.digit9Key
                    };
                    var kctrl = digits[n];
                    return kctrl != null && kctrl.isPressed;
                }
                break;
        }
        return false;
#else
        return UnityEngine.Input.GetKey(kc);
#endif
    }

    public bool GetKeyDown(KeyCode kc)
    {
#if ENABLE_INPUT_SYSTEM
        var kb = Keyboard.current;
        if (kb == null) return false;

        switch (kc)
        {
            case KeyCode.A: return kb.aKey != null && kb.aKey.wasPressedThisFrame;
            case KeyCode.D: return kb.dKey != null && kb.dKey.wasPressedThisFrame;
            case KeyCode.S: return kb.sKey != null && kb.sKey.wasPressedThisFrame;
            case KeyCode.W: return kb.wKey != null && kb.wKey.wasPressedThisFrame;
            case KeyCode.UpArrow: return kb.upArrowKey != null && kb.upArrowKey.wasPressedThisFrame;
            case KeyCode.DownArrow: return kb.downArrowKey != null && kb.downArrowKey.wasPressedThisFrame;
            case KeyCode.LeftArrow: return kb.leftArrowKey != null && kb.leftArrowKey.wasPressedThisFrame;
            case KeyCode.RightArrow: return kb.rightArrowKey != null && kb.rightArrowKey.wasPressedThisFrame;
            case KeyCode.Space: return kb.spaceKey != null && kb.spaceKey.wasPressedThisFrame;
            case KeyCode.BackQuote: return kb.backquoteKey != null && kb.backquoteKey.wasPressedThisFrame;
            case KeyCode.PageDown: return kb.pageDownKey != null && kb.pageDownKey.wasPressedThisFrame;
            case KeyCode.PageUp: return kb.pageUpKey != null && kb.pageUpKey.wasPressedThisFrame;
            case KeyCode.Return:
            case KeyCode.KeypadEnter: return kb.enterKey != null && kb.enterKey.wasPressedThisFrame;
            case KeyCode.Escape: return kb.escapeKey != null && kb.escapeKey.wasPressedThisFrame;
            case KeyCode.K: return kb.kKey != null && kb.kKey.wasPressedThisFrame;
            case KeyCode.Minus: return kb.minusKey != null && kb.minusKey.wasPressedThisFrame;
            case KeyCode.Equals: return kb.equalsKey != null && kb.equalsKey.wasPressedThisFrame;
            default:
                if (kc >= KeyCode.Alpha0 && kc <= KeyCode.Alpha9)
                {
                    int n = kc - KeyCode.Alpha0;
                    var digits = new KeyControl[] {
                        kb.digit0Key, kb.digit1Key, kb.digit2Key, kb.digit3Key, kb.digit4Key,
                        kb.digit5Key, kb.digit6Key, kb.digit7Key, kb.digit8Key, kb.digit9Key
                    };
                    var kctrl = digits[n];
                    return kctrl != null && kctrl.wasPressedThisFrame;
                }
                break;
        }
        return false;
#else
        return UnityEngine.Input.GetKeyDown(kc);
#endif
    }

    public bool GetKeyUp(KeyCode kc)
    {
#if ENABLE_INPUT_SYSTEM
        var kb = Keyboard.current;
        if (kb == null) return false;

        switch (kc)
        {
            case KeyCode.A: return kb.aKey != null && kb.aKey.wasReleasedThisFrame;
            case KeyCode.D: return kb.dKey != null && kb.dKey.wasReleasedThisFrame;
            case KeyCode.S: return kb.sKey != null && kb.sKey.wasReleasedThisFrame;
            case KeyCode.W: return kb.wKey != null && kb.wKey.wasReleasedThisFrame;
            case KeyCode.UpArrow: return kb.upArrowKey != null && kb.upArrowKey.wasReleasedThisFrame;
            case KeyCode.DownArrow: return kb.downArrowKey != null && kb.downArrowKey.wasReleasedThisFrame;
            case KeyCode.LeftArrow: return kb.leftArrowKey != null && kb.leftArrowKey.wasReleasedThisFrame;
            case KeyCode.RightArrow: return kb.rightArrowKey != null && kb.rightArrowKey.wasReleasedThisFrame;
            case KeyCode.Space: return kb.spaceKey != null && kb.spaceKey.wasReleasedThisFrame;
            case KeyCode.BackQuote: return kb.backquoteKey != null && kb.backquoteKey.wasReleasedThisFrame;
            case KeyCode.PageDown: return kb.pageDownKey != null && kb.pageDownKey.wasReleasedThisFrame;
            case KeyCode.PageUp: return kb.pageUpKey != null && kb.pageUpKey.wasReleasedThisFrame;
            case KeyCode.Return:
            case KeyCode.KeypadEnter: return kb.enterKey != null && kb.enterKey.wasReleasedThisFrame;
            case KeyCode.Escape: return kb.escapeKey != null && kb.escapeKey.wasReleasedThisFrame;
            case KeyCode.K: return kb.kKey != null && kb.kKey.wasReleasedThisFrame;
            case KeyCode.Minus: return kb.minusKey != null && kb.minusKey.wasReleasedThisFrame;
            case KeyCode.Equals: return kb.equalsKey != null && kb.equalsKey.wasReleasedThisFrame;
            default:
                if (kc >= KeyCode.Alpha0 && kc <= KeyCode.Alpha9)
                {
                    int n = kc - KeyCode.Alpha0;
                    var digits = new KeyControl[] {
                        kb.digit0Key, kb.digit1Key, kb.digit2Key, kb.digit3Key, kb.digit4Key,
                        kb.digit5Key, kb.digit6Key, kb.digit7Key, kb.digit8Key, kb.digit9Key
                    };
                    var kctrl = digits[n];
                    return kctrl != null && kctrl.wasReleasedThisFrame;
                }
                break;
        }
        return false;
#else
        return UnityEngine.Input.GetKeyUp(kc);
#endif
    }

    // Axis emulation: Horizontal / Vertical only (used in project)
    public float GetAxisRaw(string axisName)
    {
#if ENABLE_INPUT_SYSTEM
        var gp = Gamepad.current;
        if (gp != null)
        {
            if (axisName == "Horizontal") return gp.leftStick.x.ReadValue();
            if (axisName == "Vertical") return gp.leftStick.y.ReadValue();
        }

        // Keyboard fallback using GetKey wrappers (which use Keyboard.current safely)
        float x = 0f, y = 0f;
        if (GetKey(KeyCode.A)) x -= 1f;
        if (GetKey(KeyCode.D)) x += 1f;
        if (GetKey(KeyCode.S)) y -= 1f;
        if (GetKey(KeyCode.W)) y += 1f;

        if (axisName == "Horizontal") return x;
        if (axisName == "Vertical") return y;
        return 0f;
#else
        return UnityEngine.Input.GetAxisRaw(axisName);
#endif
    }
}