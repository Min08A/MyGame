using UnityEngine;

namespace LabEscape
{
    /// <summary>
    /// Базовый класс всех подбираемых предметов. Вращается и покачивается,
    /// при касании мыши применяет эффект (TryApply) и уничтожается (Destroy).
    /// Наследники: CheesePickup, HealthPickup, SyringePickup, KeycardPickup.
    /// </summary>
    [RequireComponent(typeof(Collider))]
    public abstract class Pickup : MonoBehaviour
    {
        [Header("Анимация")]
        [SerializeField] protected Transform visual;
        [SerializeField] private float spinSpeed = 90f;
        [SerializeField] private float bobHeight = 0.12f;
        [SerializeField] private float bobSpeed = 2.5f;

        private Vector3 visualStart;
        private float phase;

        protected virtual void Awake()
        {
            GetComponent<Collider>().isTrigger = true;
            if (visual != null) visualStart = visual.localPosition;
            phase = Random.value * 10f;
        }

        private void Update()
        {
            if (visual == null) return;
            visual.Rotate(0f, spinSpeed * Time.deltaTime, 0f, Space.World);
            visual.localPosition = visualStart + Vector3.up * (Mathf.Sin((Time.time + phase) * bobSpeed) * bobHeight);
        }

        private void OnTriggerEnter(Collider other)
        {
            if (!GameManager.IsPlaying) return;
            PlayerStats player = other.GetComponentInParent<PlayerStats>();
            if (player == null || !player.IsAlive) return;

            if (!TryApply(player)) return;

            GameManager.Instance.ShowMessage(PickupMessage, 2f);
            Destroy(gameObject);
        }

        /// <summary>Применить эффект. false — предмет не нужен сейчас и остаётся лежать.</summary>
        protected abstract bool TryApply(PlayerStats player);

        protected abstract string PickupMessage { get; }
    }
}
