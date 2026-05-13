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
            else if (slot is Upgrade_05_03_AngelCurse curse &&
                     curse.companionPrefab != null &&
                     curse.projectilePrefab != null)
            {
                GameObject existing = sub.GetCompanionInstance(i);
                if (existing != null)
                {
                    var driver = existing.GetComponent<AngelCurseCompanionDriver>();
                    if (driver != null && driver.UsesSameConfig(curse))
                        continue;
                }

                sub.SetCompanionPrefab(i, curse.companionPrefab);
                GameObject inst = sub.GetCompanionInstance(i);
                if (inst == null)
                    continue;

                var d = inst.GetComponent<AngelCurseCompanionDriver>();
                if (d == null)
                    d = inst.AddComponent<AngelCurseCompanionDriver>();
                d.Initialize(curse, playerRoot);
            }
            else if (slot is Upgrade_05_04_AngelLightning lightning &&
                     lightning.companionPrefab != null)
            {
                GameObject existing = sub.GetCompanionInstance(i);
                if (existing != null)
                {
                    var driver = existing.GetComponent<AngelLightningCompanionDriver>();
                    if (driver != null && driver.UsesSameConfig(lightning))
                        continue;
                }

                sub.SetCompanionPrefab(i, lightning.companionPrefab);
                GameObject inst = sub.GetCompanionInstance(i);
                if (inst == null)
                    continue;

                var d = inst.GetComponent<AngelLightningCompanionDriver>();
                if (d == null)
                    d = inst.AddComponent<AngelLightningCompanionDriver>();
                d.Initialize(lightning, playerRoot);
            }
            else if (slot is Upgrade_05_05_AngelLottery lottery &&
                     lottery.companionPrefab != null)
            {
                GameObject existing = sub.GetCompanionInstance(i);
                if (existing != null)
                {
                    var driver = existing.GetComponent<AngelLotteryCompanionDriver>();
                    if (driver != null && driver.UsesSameConfig(lottery))
                        continue;
                }

                sub.SetCompanionPrefab(i, lottery.companionPrefab);
                GameObject inst = sub.GetCompanionInstance(i);
                if (inst == null)
                    continue;

                var d = inst.GetComponent<AngelLotteryCompanionDriver>();
                if (d == null)
                    d = inst.AddComponent<AngelLotteryCompanionDriver>();
                d.Initialize(lottery, playerRoot);
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
