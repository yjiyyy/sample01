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

    private int mobileEvadeDownFrames;
    private int mobileEvadeUpFrames;

    [HideInInspector]
    public bool OverlayInputBlocked = false;

    /// <summary>사망·옵션·치트 창이 열려 플레이 입력을 막아야 할 때.</summary>
    public bool IsGameplayInputBlocked =>
        playerDeathBlocked || OverlayInputBlocked || GameplayTime.IsGameplayPaused;

    /// <summary>
    /// 오버레이로 입력을 막을지 설정합니다.
    /// 옵션으로 일시정지 중이면 풀리지 않습니다.
    /// </summary>
    public void SetOverlayInputBlocked(bool blocked)
    {
        OverlayInputBlocked = blocked || GameplayTime.IsGameplayPaused;
    }

    /// <summary> 플레이어 사망 시 true — 모든 플레이어 입력 차단 (죽음 이벤트만 유지) </summary>
    private static bool playerDeathBlocked = false;

    /// <summary> 사망 시 호출. 이후 GetMoveInput 등은 0/false 반환. </summary>
    public static void SetPlayerDeathBlock(bool block) { playerDeathBlocked = block; }

    /// <summary> 사망 시 호출. 모바일 입력(조이스틱 등)을 즉시 초기화하여 적용 방지. </summary>
    public void ClearPlayerInput()
    {
        mobileMove = Vector2.zero;
        mobileAttackPressed = false;
        mobileAttackDownFrames = 0;
        mobileAttackUpFrames = 0;
        mobileEvadeDownFrames = 0;
        mobileEvadeUpFrames = 0;
    }

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
        playerDeathBlocked = false;
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
        if (IsGameplayInputBlocked) return Vector2.zero;
        if (IsMobileRuntimeActive && mobileMove.sqrMagnitude > 0.0001f)
            return mobileMove; // 이미 ClampMagnitude로 크기 1.0이 보장됨

        float x = 0f, y = 0f;
        if (GetKey(KeyCode.A)) x -= 1f;
        if (GetKey(KeyCode.D)) x += 1f;
        if (GetKey(KeyCode.S)) y -= 1f;
        if (GetKey(KeyCode.W)) y += 1f;

        Vector2 wasd = new Vector2(x, y);
        if (wasd.sqrMagnitude > 0.0001f)
        {
            // [수정] 대각선 입력 시 벡터 크기가 1.0을 초과하므로 정규화(normalized)하여 반환
            return wasd.normalized;
        }

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

        // 게임패드 스틱 입력은 이미 단위 원 내의 값을 반환하므로 추가 정규화 불필요
        return gamepad;
    }

    /* ───── 무기 슬롯 ───── */
    public int GetWeaponSwapInput()
    {
        if (IsGameplayInputBlocked) return -1;

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
        if (IsGameplayInputBlocked) return false;
        bool kb = GetKeyDown(KeyCode.Alpha0);
        bool mobile = IsMobileRuntimeActive && mobileAttackDownFrames > 0;
        return kb || mobile;
    }

    public bool GetAttack()
    {
        if (IsGameplayInputBlocked) return false;
        bool kb = GetKey(KeyCode.Alpha0);
        bool mobile = IsMobileRuntimeActive && mobileAttackPressed;
        return kb || mobile;
    }

    public bool GetAttackUp()
    {
        if (IsGameplayInputBlocked) return false;
        bool kb = GetKeyUp(KeyCode.Alpha0);
        bool mobile = IsMobileRuntimeActive && mobileAttackUpFrames > 0;
        return kb || mobile;
    }

    public bool GetEvadeInput()
    {
        if (IsGameplayInputBlocked) return false;
        bool kb = GetKeyDown(KeyCode.Space);
        bool mobile = IsMobileRuntimeActive && mobileEvadeDownFrames > 0;
        return kb || mobile;
    }

    public bool GetDamageTestInput() => !IsGameplayInputBlocked && GetKeyDown(KeyCode.Minus);
    public bool GetHealTestInput() => !IsGameplayInputBlocked && GetKeyDown(KeyCode.Equals);

    // Mobile setters (UI에서 호출)
    public void SetMobileMove(Vector2 v)
    {
        mobileMove = Vector2.ClampMagnitude(v, 1f);
    }
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
        if (!TryGetKeyControl(kb, kc, out var key)) return false;
        return key != null && key.isPressed;
#else
        return UnityEngine.Input.GetKey(kc);
#endif
    }

    public bool GetKeyDown(KeyCode kc)
    {
#if ENABLE_INPUT_SYSTEM
        var kb = Keyboard.current;
        if (kb == null) return false;
        if (!TryGetKeyControl(kb, kc, out var key)) return false;
        return key != null && key.wasPressedThisFrame;
#else
        return UnityEngine.Input.GetKeyDown(kc);
#endif
    }

    public bool GetKeyUp(KeyCode kc)
    {
#if ENABLE_INPUT_SYSTEM
        var kb = Keyboard.current;
        if (kb == null) return false;
        if (!TryGetKeyControl(kb, kc, out var key)) return false;
        return key != null && key.wasReleasedThisFrame;
#else
        return UnityEngine.Input.GetKeyUp(kc);
#endif
    }

#if ENABLE_INPUT_SYSTEM
    private static bool TryGetKeyControl(Keyboard kb, KeyCode kc, out KeyControl key)
    {
        key = null;
        if (kb == null) return false;

        // Alpha keys
        if (kc >= KeyCode.A && kc <= KeyCode.Z)
        {
            int i = kc - KeyCode.A;
            KeyControl[] letters = {
                kb.aKey, kb.bKey, kb.cKey, kb.dKey, kb.eKey, kb.fKey, kb.gKey, kb.hKey, kb.iKey, kb.jKey, kb.kKey, kb.lKey, kb.mKey,
                kb.nKey, kb.oKey, kb.pKey, kb.qKey, kb.rKey, kb.sKey, kb.tKey, kb.uKey, kb.vKey, kb.wKey, kb.xKey, kb.yKey, kb.zKey
            };
            key = letters[i];
            return key != null;
        }

        // Top row digits
        if (kc >= KeyCode.Alpha0 && kc <= KeyCode.Alpha9)
        {
            int i = kc - KeyCode.Alpha0;
            KeyControl[] digits = {
                kb.digit0Key, kb.digit1Key, kb.digit2Key, kb.digit3Key, kb.digit4Key,
                kb.digit5Key, kb.digit6Key, kb.digit7Key, kb.digit8Key, kb.digit9Key
            };
            key = digits[i];
            return key != null;
        }

        // Numpad digits
        if (kc >= KeyCode.Keypad0 && kc <= KeyCode.Keypad9)
        {
            int i = kc - KeyCode.Keypad0;
            KeyControl[] numpadDigits = {
                kb.numpad0Key, kb.numpad1Key, kb.numpad2Key, kb.numpad3Key, kb.numpad4Key,
                kb.numpad5Key, kb.numpad6Key, kb.numpad7Key, kb.numpad8Key, kb.numpad9Key
            };
            key = numpadDigits[i];
            return key != null;
        }

        // Function keys
        if (kc >= KeyCode.F1 && kc <= KeyCode.F12)
        {
            int i = kc - KeyCode.F1;
            KeyControl[] fkeys = {
                kb.f1Key, kb.f2Key, kb.f3Key, kb.f4Key, kb.f5Key, kb.f6Key,
                kb.f7Key, kb.f8Key, kb.f9Key, kb.f10Key, kb.f11Key, kb.f12Key
            };
            key = fkeys[i];
            return key != null;
        }

        switch (kc)
        {
            case KeyCode.Space: key = kb.spaceKey; return key != null;
            case KeyCode.Tab: key = kb.tabKey; return key != null;
            case KeyCode.Escape: key = kb.escapeKey; return key != null;
            case KeyCode.Backspace: key = kb.backspaceKey; return key != null;
            case KeyCode.Return: key = kb.enterKey; return key != null;

            case KeyCode.UpArrow: key = kb.upArrowKey; return key != null;
            case KeyCode.DownArrow: key = kb.downArrowKey; return key != null;
            case KeyCode.LeftArrow: key = kb.leftArrowKey; return key != null;
            case KeyCode.RightArrow: key = kb.rightArrowKey; return key != null;
            case KeyCode.PageUp: key = kb.pageUpKey; return key != null;
            case KeyCode.PageDown: key = kb.pageDownKey; return key != null;
            case KeyCode.Home: key = kb.homeKey; return key != null;
            case KeyCode.End: key = kb.endKey; return key != null;
            case KeyCode.Insert: key = kb.insertKey; return key != null;
            case KeyCode.Delete: key = kb.deleteKey; return key != null;

            case KeyCode.BackQuote: key = kb.backquoteKey; return key != null;
            case KeyCode.Minus: key = kb.minusKey; return key != null;
            case KeyCode.Equals: key = kb.equalsKey; return key != null;
            case KeyCode.LeftBracket: key = kb.leftBracketKey; return key != null;
            case KeyCode.RightBracket: key = kb.rightBracketKey; return key != null;
            case KeyCode.Backslash: key = kb.backslashKey; return key != null;
            case KeyCode.Semicolon: key = kb.semicolonKey; return key != null;
            case KeyCode.Quote: key = kb.quoteKey; return key != null;
            case KeyCode.Comma: key = kb.commaKey; return key != null;
            case KeyCode.Period: key = kb.periodKey; return key != null;
            case KeyCode.Slash: key = kb.slashKey; return key != null;

            case KeyCode.LeftShift: key = kb.leftShiftKey; return key != null;
            case KeyCode.RightShift: key = kb.rightShiftKey; return key != null;
            case KeyCode.LeftControl: key = kb.leftCtrlKey; return key != null;
            case KeyCode.RightControl: key = kb.rightCtrlKey; return key != null;
            case KeyCode.LeftAlt: key = kb.leftAltKey; return key != null;
            case KeyCode.RightAlt: key = kb.rightAltKey; return key != null;
            case KeyCode.CapsLock: key = kb.capsLockKey; return key != null;

            case KeyCode.KeypadPeriod: key = kb.numpadPeriodKey; return key != null;
            case KeyCode.KeypadDivide: key = kb.numpadDivideKey; return key != null;
            case KeyCode.KeypadMultiply: key = kb.numpadMultiplyKey; return key != null;
            case KeyCode.KeypadMinus: key = kb.numpadMinusKey; return key != null;
            case KeyCode.KeypadPlus: key = kb.numpadPlusKey; return key != null;
            case KeyCode.KeypadEquals: key = kb.numpadEqualsKey; return key != null;
            case KeyCode.KeypadEnter: key = kb.numpadEnterKey; return key != null;

            default:
                return false;
        }
    }
#endif

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