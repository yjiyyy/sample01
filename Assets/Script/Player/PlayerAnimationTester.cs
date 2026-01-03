#if UNITY_EDITOR
using UnityEngine;

public class PlayerAnimationTester : MonoBehaviour
{
    [Header("테스트용 무기 SO들 (1~9번 키 매핑)")]
    [SerializeField] private WeaponDataSO[] testWeapons;

    private PlayerWeaponController weaponController;
    private PlayerHealth health;

    void Awake()
    {
        weaponController = GetComponent<PlayerWeaponController>();
        health = GetComponent<PlayerHealth>();
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

        // 체력 0 → 사망 테스트 (K키)
        if (InputManager.Instance != null && InputManager.Instance.GetKeyDown(KeyCode.K) && health != null)
        {
            health.SetHealth(0);
            Debug.Log("☠️ 체력을 0으로 설정 → 사망");
        }
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

        // 1) 프리팹으로 장착 (기존 흐름 그대로 사용)
        weaponController.EquipWeapon(so.weaponPrefab);

        // 2) 혹시 프리팹 안의 WeaponBehavior.data가 다른 SO를 가리키는 경우를 대비해서 강제로 덮어쓰기
        //    (듀얼 테스트에서도 CurrentWeaponData가 이 SO로 잡히는 게 중요)
        var equip = weaponController.GetComponent<PlayerEquipmentController>();
        if (equip != null && equip.WeaponBehavior != null)
        {
            equip.WeaponBehavior.data = so;
        }

        Debug.Log($"[PlayerAnimationTester] Equip SO: {so.weaponName} ({so.name})");
    }
}
#endif