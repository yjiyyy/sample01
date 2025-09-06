using UnityEngine;

public class InputManager : MonoBehaviour
{
    public static InputManager Instance { get; private set; }

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

    /* ───── 이동 입력 ───── */
    public Vector2 GetMoveInput()
    {
        float h = Input.GetAxisRaw("Horizontal");
        float v = Input.GetAxisRaw("Vertical");
        return new Vector2(h, v);
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

    /* ───── 공격 입력 ───── */
    public bool GetAttackInput()
    {
        // 오직 0번 키로만 공격
        return Input.GetKeyDown(KeyCode.Alpha0);
    }

    /* ───── ✅ 회피 입력 (새로 추가) ───── */
    public bool GetEvadeInput()
    {
        return Input.GetKeyDown(KeyCode.Space);
    }

    /* ───── 테스트 입력 ───── */
    public bool GetDamageTestInput() => Input.GetKeyDown(KeyCode.Minus);      // - 키
    public bool GetHealTestInput() => Input.GetKeyDown(KeyCode.Equals);       // = 키 (+ 키)
}