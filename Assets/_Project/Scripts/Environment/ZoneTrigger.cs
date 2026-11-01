using UnityEngine;

namespace LabEscape
{
    /// <summary>
    /// Невидимый триггер: при входе мыши показывает подсказку, меняет название зоны в HUD
    /// и может включить спавнеры (например, котов в главной лаборатории).
    /// </summary>
    [RequireComponent(typeof(Collider))]
    public class ZoneTrigger : MonoBehaviour
    {
        [Tooltip("Название зоны для HUD (пусто — не менять)")]
        [SerializeField] private string zoneName;
        [SerializeField, TextArea(2, 4)] private string hint;
        [SerializeField] private float hintDuration = 5f;
        [SerializeField] private bool onlyOnce = true;
        [SerializeField] private Spawner[] spawnersToActivate;

        private bool triggered;

        private void Awake()
        {
            GetComponent<Collider>().isTrigger = true;
        }

        private void OnTriggerEnter(Collider other)
        {
            if (onlyOnce && triggered) return;
            if (other.GetComponentInParent<PlayerStats>() == null) return;
            GameManager gm = GameManager.Instance;
            if (gm == null) return;

            triggered = true;
            if (!string.IsNullOrEmpty(zoneName)) gm.CurrentZone = zoneName;
            if (!string.IsNullOrEmpty(hint)) gm.ShowMessage(hint, hintDuration);

            if (spawnersToActivate != null)
                foreach (Spawner s in spawnersToActivate)
                    if (s != null) s.Activate();
        }
    }
}
