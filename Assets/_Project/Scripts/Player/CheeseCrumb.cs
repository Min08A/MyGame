using UnityEngine;

namespace LabEscape
{
    /// <summary>
    /// Снаряд — крошка сыра. Физический объект (Rigidbody): летит по дуге,
    /// при попадании в кота наносит урон и уничтожается (Destroy).
    /// Если ни в кого не попал — исчезает через lifetime секунд.
    /// </summary>
    [RequireComponent(typeof(Rigidbody))]
    public class CheeseCrumb : MonoBehaviour
    {
        [SerializeField] private int damage = 1;
        [SerializeField] private float lifetime = 4f;

        private Rigidbody rb;
        private bool hasHit;

        private void Awake()
        {
            rb = GetComponent<Rigidbody>();
        }

        private void Start()
        {
            Destroy(gameObject, lifetime);
        }

        public void Launch(Vector3 velocity, Collider[] ignoreColliders)
        {
            Collider own = GetComponent<Collider>();
            if (own != null && ignoreColliders != null)
            {
                foreach (Collider c in ignoreColliders)
                    if (c != null) Physics.IgnoreCollision(own, c);
            }
            rb.linearVelocity = velocity;
        }

        private void OnCollisionEnter(Collision collision)
        {
            if (hasHit) return;

            CatEnemy cat = collision.collider.GetComponentInParent<CatEnemy>();
            if (cat != null)
            {
                hasHit = true;
                cat.TakeDamage(damage);
                Destroy(gameObject);
            }
        }
    }
}
