using UnityEngine;

namespace UnityChan
{
    public class SpringCollider : MonoBehaviour
    {
        [Header("충돌 범위 반지름")]
        public float radius = 0.5f;

        private void OnDrawGizmosSelected()
        {
            Gizmos.color = Color.green;
            Gizmos.DrawWireSphere(transform.position, radius);
        }
    }
}
