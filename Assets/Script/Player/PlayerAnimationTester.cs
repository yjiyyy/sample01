#if UNITY_EDITOR
using UnityEngine;

public class PlayerAnimationTester : MonoBehaviour
{
    [Header("테스트용 무기 SO들 (1~9번 키 매핑)")]
    [SerializeField] private WeaponDataSO[] testWeapons;

    private PlayerWeaponController weaponController;

    void Awake()
    {
        weaponController = GetComponent<PlayerWeaponController>();
    }

    void Update()
    {
        if (weaponController == null) return;

        // 무기 장착 테스트 (1~9번 키)
        for (int i = 0; i < testWeapons.Length; i++)
        {
            if (InputManager.Instance != null && InputManager.Instance.GetKeyDown(KeyCode.Alpha1 + i))
            {
                EquipBySO(testWeapons[i]);
            }
        }

        // 공격 테스트 (0번 키)
        if (InputManager.Instance != null && InputManager.Instance.GetKeyDown(KeyCode.Alpha0))
            weaponController.PlayAttack();
    }

    private void EquipBySO(WeaponDataSO so)
    {
        if (so == null)
        {
            Debug.LogWarning("[PlayerAnimationTester] WeaponDataSO가 비어 있습니다.");
            return;
        }

        if (so.weaponPrefab == null)
        {
            Debug.LogWarning($"[PlayerAnimationTester] '{so.name}' SO에 weaponPrefab이 비어 있습니다.");
            return;
        }

        if (weaponController == null)
        {
            Debug.LogWarning("[PlayerAnimationTester] PlayerWeaponController를 찾을 수 없습니다.");
            return;
        }

        // PlayerWeaponController.EquipWeapon(WeaponDataSO) → EquipByData (AOC/idle 동기화)
        weaponController.EquipWeapon(so);

        Debug.Log($"[PlayerAnimationTester] Equip SO: {so.weaponName} ({so.name})");
    }
}
#endif