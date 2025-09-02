using UnityEngine;

public class Item_Heal : MonoBehaviour
{
    [Header("회복량")]
    public int healAmount = 20;

    private void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag("Player"))
        {
            Health playerHealth = other.GetComponent<Health>();
            if (playerHealth != null)
            {
                playerHealth.Heal(healAmount);
                Debug.Log($"힐 아이템 사용 → HP {healAmount} 회복!");
            }

            Destroy(gameObject);
        }
    }
}