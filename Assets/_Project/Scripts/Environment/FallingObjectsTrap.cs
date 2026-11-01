using UnityEngine;

namespace LabEscape
{
    /// <summary>
    /// Ловушка-триггер: когда мышь входит в зону, «замороженные» (kinematic) ящики
    /// включают физику и падают сверху.
    /// </summary>
    [RequireComponent(typeof(Collider))]
    public class FallingObjectsTrap : MonoBehaviour
    {
        [SerializeField] private Rigidbody[] objects;
        [SerializeField] private string warning = "Сверху падают ящики! Беги!";

        private bool fired;

        private void Awake()
        {
            GetComponent<Collider>().isTrigger = true;
            if (objects == null) return;
            foreach (Rigidbody rb in objects)
                if (rb != null) rb.isKinematic = true;
        }

        private void OnTriggerEnter(Collider other)
        {
            if (fired || other.GetComponentInParent<PlayerStats>() == null) return;
            fired = true;

            foreach (Rigidbody rb in objects)
            {
                if (rb == null) continue;
                rb.isKinematic = false;
                rb.WakeUp();
            }

            if (GameManager.Instance != null) GameManager.Instance.ShowMessage(warning, 2f);
        }
    }
}
