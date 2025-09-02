using UnityEngine;

public class Item_Heal : MonoBehaviour
{
    [Header("ȸ����")]
    public int healAmount = 20;

    private void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag("Player"))
        {
            Health playerHealth = other.GetComponent<Health>();
            if (playerHealth != null)
            {
                playerHealth.Heal(healAmount);
                Debug.Log($"�� ������ ��� �� HP {healAmount} ȸ��!");
            }

            Destroy(gameObject);
        }
    }
}