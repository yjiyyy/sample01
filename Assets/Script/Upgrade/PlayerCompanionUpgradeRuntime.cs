using UnityEngine;

/// <summary>
/// Upgrade 슬롯의 보조무기형 업그레이드(예: 05_01)를 감시하고 SubWeaponController 슬롯에 동기화합니다.
/// </summary>
[DisallowMultipleComponent]
public class PlayerCompanionUpgradeRuntime : MonoBehaviour
{
    private static bool loggedMissingSubWeaponController;

    [SerializeField] private Upgrade upgrade;

    private void Awake()
    {
        if (upgrade == null)
            upgrade = GetComponent<Upgrade>();
        if (upgrade == null)
            upgrade = GetComponentInParent<Upgrade>();
    }

    private void OnEnable()
    {
        if (upgrade == null)
            upgrade = GetComponent<Upgrade>() ?? GetComponentInParent<Upgrade>();

        if (upgrade != null)
        {
            upgrade.OnSlotsChanged -= SyncFromSlots;
            upgrade.OnSlotsChanged += SyncFromSlots;
        }

        SyncFromSlots();
    }

    private void OnDisable()
    {
        if (upgrade != null)
            upgrade.OnSlotsChanged -= SyncFromSlots;

        ClearAllCompanionSlots();
    }

    private SubWeaponController FindSubWeaponController()
    {
        Transform root = upgrade != null ? upgrade.transform.root : transform.root;
        if (root == null)
            return null;
        return root.GetComponentInChildren<SubWeaponController>(true);
    }

    private void SyncFromSlots()
    {
        if (upgrade == null)
        {
            ClearAllCompanionSlots();
            return;
        }

        SubWeaponController sub = FindSubWeaponController();
        if (sub == null)
        {
            if (!loggedMissingSubWeaponController)
            {
                loggedMissingSubWeaponController = true;
                Debug.LogWarning("[PlayerCompanionUpgradeRuntime] 플레이어에 SubWeaponController가 없습니다. 보조무기 업그레이드가 적용되지 않습니다.");
            }

            return;
        }

        Transform playerRoot = upgrade.transform.root;

        for (int i = 0; i < Upgrade.SlotCount; i++)
        {
            UpgradeEffectSO slot = upgrade.GetSlot(i);
            if (slot is Upgrade_05_01_AngelShooter angel &&
                angel.companionPrefab != null &&
                angel.projectilePrefab != null)
            {
                GameObject existing = sub.GetCompanionInstance(i);
                if (existing != null)
                {
                    var driver = existing.GetComponent<AngelShooterCompanionDriver>();
                    if (driver != null && driver.UsesSameConfig(angel))
                        continue;
                }

                sub.SetCompanionPrefab(i, angel.companionPrefab);
                GameObject inst = sub.GetCompanionInstance(i);
                if (inst == null)
                    continue;

                var d = inst.GetComponent<AngelShooterCompanionDriver>();
                if (d == null)
                    d = inst.AddComponent<AngelShooterCompanionDriver>();
                d.Initialize(angel, playerRoot);
            }
            else if (slot is Upgrade_05_02_AngelSlayer slayer &&
                     slayer.companionPrefab != null)
            {
                GameObject existing = sub.GetCompanionInstance(i);
                if (existing != null)
                {
                    var driver = existing.GetComponent<AngelSlayerCompanionDriver>();
                    if (driver != null && driver.UsesSameConfig(slayer))
                        continue;
                }

                sub.SetCompanionPrefab(i, slayer.companionPrefab);
                GameObject inst = sub.GetCompanionInstance(i);
                if (inst == null)
                    continue;

                var d = inst.GetComponent<AngelSlayerCompanionDriver>();
                if (d == null)
                    d = inst.AddComponent<AngelSlayerCompanionDriver>();
                d.Initialize(slayer, playerRoot);
            }
            else
            {
                sub.SetCompanionPrefab(i, null);
            }
        }
    }

    private void ClearAllCompanionSlots()
    {
        SubWeaponController sub = FindSubWeaponController();
        if (sub != null)
            sub.ClearAllCompanionSlots();
    }
}
