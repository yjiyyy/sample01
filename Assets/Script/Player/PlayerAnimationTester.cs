#if UNITY_EDITOR
using UnityEngine;

public class PlayerAnimationTester : MonoBehaviour
{
    [Header("테스트용 무기 프리팹들 (1~9번 키 매핑)")]
    [SerializeField] private GameObject[] testWeaponPrefabs;

    private PlayerWeaponController weaponController;
    private Health health;

    void Awake()
    {
        weaponController = GetComponent<PlayerWeaponController>();
        health = GetComponent<Health>();
    }

    void Update()
    {
        if (weaponController == null) return;

        // 무기 장착 테스트 (1~9번 키)
        for (int i = 0; i < testWeaponPrefabs.Length; i++)
        {
            if (Input.GetKeyDown(KeyCode.Alpha1 + i))
            {
                if (testWeaponPrefabs[i] != null)
                    weaponController.EquipWeapon(testWeaponPrefabs[i]);
            }
        }

        // 공격 테스트 (0번 키)
        if (Input.GetKeyDown(KeyCode.Alpha0))
            weaponController.PlayAttack();

        // 체력 0 → 사망 테스트 (K키)
        if (Input.GetKeyDown(KeyCode.K) && health != null)
        {
            health.SetHealth(0);
            Debug.Log("☠️ 체력을 0으로 설정 → 사망");
        }
    }
}
#endif
