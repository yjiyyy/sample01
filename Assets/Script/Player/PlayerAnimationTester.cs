#if UNITY_EDITOR
using UnityEngine;

public class PlayerAnimationTester : MonoBehaviour
{
    [Header("테스트용 무기 SO들 (1~9번 키 매핑)")]
    [SerializeField] private WeaponDataSO[] testWeapons;

    private PlayerWeaponController weaponController;
    private PlayerEquipmentController equip;
    private PlayerHealth health;

    void Awake()
    {
        weaponController = GetComponent<PlayerWeaponController>();
        equip = GetComponent<PlayerEquipmentController>();
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

        if (equip == null)
        {
            Debug.LogWarning("[PlayerAnimationTester] PlayerEquipmentController를 찾을 수 없습니다.");
            return;
        }

        // ✅ 핵심: SO 기준으로 장착 (CurrentWeaponData/AOC/UI가 같이 갱신됨)
        equip.EquipByData(so, transform.root, debugLogs: true);

        Debug.Log($"[PlayerAnimationTester] Equip SO: {so.weaponName} ({so.name})");
    }
}
#endif