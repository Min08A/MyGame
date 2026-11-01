using UnityEngine;

namespace LabEscape
{
    /// <summary>
    /// Физический ящик (Rigidbody). Мышь может его толкать.
    /// Если ящик падает или врезается на большой скорости — оглушает котов и ранит мышь.
    /// </summary>
    [RequireComponent(typeof(Rigidbody))]
    public class PhysicsCrate : MonoBehaviour
    {
        [Tooltip("Скорость удара, начиная с которой ящик опасен")]
        [SerializeField] private float dangerousImpactSpeed = 4f;
        [SerializeField] private float damageToPlayer = 15f;

        private Rigidbody rb;

        private void Awake()
        {
            rb = GetComponent<Rigidbody>();
        }

        private void OnCollisionEnter(Collision collision)
        {
            if (collision.relativeVelocity.magnitude < dangerousImpactSpeed) return;

            CatEnemy cat = collision.collider.GetComponentInParent<CatEnemy>();
            if (cat != null)
            {
                cat.TakeDamage(1);
                return;
            }

            PlayerStats player = collision.collider.GetComponentInParent<PlayerStats>();
            if (player != null && rb.linearVelocity.y < -2f)
                player.TakeDamage(damageToPlayer, transform.position, "Мышь придавило ящиком!");
        }
    }
}
