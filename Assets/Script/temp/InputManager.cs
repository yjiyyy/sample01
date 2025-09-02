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
        }
    }

    /* ���������� �̵� �Է� ���������� */
    public Vector2 GetMoveInput()
    {
        float h = Input.GetAxisRaw("Horizontal");
        float v = Input.GetAxisRaw("Vertical");
        return new Vector2(h, v);
    }

    /* ���������� ���� ��ü �Է� ���������� */
    public int GetWeaponSwapInput()
    {
        // 1~9�� Ű �� ���� ���� ��ȣ ��ȯ
        if (Input.GetKeyDown(KeyCode.Alpha1)) return 1;
        if (Input.GetKeyDown(KeyCode.Alpha2)) return 2;
        if (Input.GetKeyDown(KeyCode.Alpha3)) return 3;
        if (Input.GetKeyDown(KeyCode.Alpha4)) return 4;
        if (Input.GetKeyDown(KeyCode.Alpha5)) return 5;
        if (Input.GetKeyDown(KeyCode.Alpha6)) return 6;
        if (Input.GetKeyDown(KeyCode.Alpha7)) return 7;
        if (Input.GetKeyDown(KeyCode.Alpha8)) return 8;
        if (Input.GetKeyDown(KeyCode.Alpha9)) return 9;

        return -1; // �Է� ����
    }

    /* ���������� ���� �Է� ���������� */
    public bool GetAttackInput()
    {
        // ���� 0�� Ű�θ� ����
        return Input.GetKeyDown(KeyCode.Alpha0);
    }

    /* ���������� �׽�Ʈ �Է� ���������� */
    public bool GetDamageTestInput() => Input.GetKeyDown(KeyCode.Minus);
    public bool GetHealTestInput() => Input.GetKeyDown(KeyCode.Equals); // =/+ Ű
}
