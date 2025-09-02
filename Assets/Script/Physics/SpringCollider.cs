using UnityEngine;

namespace UnityChan
{
    public class SpringCollider : MonoBehaviour
    {
        [Header("�浹 ���� ������")]
        public float radius = 0.5f;

        private void OnDrawGizmosSelected()
        {
            Gizmos.color = Color.green;
            Gizmos.DrawWireSphere(transform.position, radius);
        }
    }
}
