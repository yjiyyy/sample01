using UnityEngine;

/// <summary>
/// 보스 입장 트리거 존. 플레이어가 들어오면 BossEncounter_Taxi에 알립니다.
/// </summary>
[DisallowMultipleComponent]
public class BossEncounterTriggerZone : MonoBehaviour
{
    [SerializeField] private BossEncounter_Taxi owner;
    [SerializeField] private bool oneShot = true;

    private bool triggered;
    private bool playerInside;

    private void Awake()
    {
        Collider col = GetComponent<Collider>();
        if (col != null)
            col.isTrigger = true;
    }

    public void SetOwner(BossEncounter_Taxi encounterOwner)
    {
        owner = encounterOwner;
    }

    private void OnTriggerEnter(Collider other)
    {
        if (triggered && oneShot)
            return;

        if (other == null || !other.CompareTag("Player"))
            return;

        if (owner == null)
            return;

        playerInside = true;
        TryNotifyOwner();
    }

    private void OnTriggerStay(Collider other)
    {
        if (other != null && other.CompareTag("Player"))
        {
            playerInside = true;
            TryNotifyOwner();
        }
    }

    private void OnTriggerExit(Collider other)
    {
        if (other != null && other.CompareTag("Player"))
            playerInside = false;
    }

    private void TryNotifyOwner()
    {
        if (!playerInside || owner == null || (triggered && oneShot))
            return;

        if (owner.HandleIntroTriggerEntered())
            triggered = true;
    }
}
