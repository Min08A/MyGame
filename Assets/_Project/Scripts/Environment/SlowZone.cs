using UnityEngine;

namespace LabEscape
{
    /// <summary>Лужа клея: пока мышь внутри, она двигается медленнее.</summary>
    [RequireComponent(typeof(Collider))]
    public class SlowZone : MonoBehaviour
    {
        [SerializeField, Range(0.1f, 1f)] private float speedMultiplier = 0.4f;
        [SerializeField] private string message = "Клей! Лапки прилипают...";

        private void Awake()
        {
            GetComponent<Collider>().isTrigger = true;
        }

        private void OnTriggerEnter(Collider other)
        {
            PlayerStats p = other.GetComponentInParent<PlayerStats>();
            if (p == null) return;
            p.SetSlow(speedMultiplier);
            if (GameManager.Instance != null) GameManager.Instance.ShowMessage(message, 1.5f);
        }

        private void OnTriggerExit(Collider other)
        {
            PlayerStats p = other.GetComponentInParent<PlayerStats>();
            if (p != null) p.ClearSlow();
        }
    }
}
