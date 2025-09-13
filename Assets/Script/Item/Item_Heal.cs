using UnityEngine;

public class Item_Heal : MonoBehaviour
{
    [Header("È¸º¹·®")]
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