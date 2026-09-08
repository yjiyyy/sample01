using UnityEngine;

public class Item_Heal : MonoBehaviour
{
    [Header("회복량")]
    public int healAmount = 20;

    private void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag("Player"))
        {
            PlayerHealth hp = other.GetComponent<PlayerHealth>();
            if (hp != null)
            {
                hp.Heal(healAmount);
                Destroy(gameObject);
            }
        }
    }
}