using System.Collections.Generic;
using UnityEngine;

namespace LabEscape
{
    /// <summary>
    /// Опасная зона-триггер.
    /// Continuous — урон каждую секунду, пока мышь внутри (кислота).
    /// Hit — разовый удар с отбрасыванием (лазер). Лазеры могут мигать (blinking).
    /// </summary>
    [RequireComponent(typeof(Collider))]
    public class DamageZone : MonoBehaviour
    {
        public enum DamageMode { Continuous, Hit }

        [SerializeField] private DamageMode mode = DamageMode.Continuous;
        [Tooltip("Continuous — урон в секунду, Hit — урон за удар")]
        [SerializeField] private float damage = 25f;
        [SerializeField] private string deathMessage = "Мышь не пережила эксперимент...";

        [Header("Мигание (лазеры)")]
        [SerializeField] private bool blinking;
        [SerializeField] private float onTime = 1.5f;
        [SerializeField] private float offTime = 1.2f;
        [SerializeField] private float phaseOffset;
        [SerializeField] private Renderer[] visuals;

        public bool IsActive { get; private set; } = true;

        private readonly HashSet<PlayerStats> inside = new HashSet<PlayerStats>();

        private void Awake()
        {
            GetComponent<Collider>().isTrigger = true;
        }

        private void Update()
        {
            if (blinking)
            {
                float t = Mathf.Repeat(Time.time + phaseOffset, onTime + offTime);
                bool active = t < onTime;
                if (active != IsActive)
                {
                    IsActive = active;
                    if (visuals != null)
                        foreach (Renderer r in visuals) if (r != null) r.enabled = active;
                }
            }

            if (!IsActive || !GameManager.IsPlaying) return;

            inside.RemoveWhere(p => p == null);
            foreach (PlayerStats p in inside)
            {
                if (mode == DamageMode.Continuous)
                    p.TakeDamageOverTime(damage * Time.deltaTime, deathMessage);
                else
                    p.TakeDamage(damage, transform.position, deathMessage);
            }
        }

        private void OnTriggerEnter(Collider other)
        {
            PlayerStats p = other.GetComponentInParent<PlayerStats>();
            if (p != null) inside.Add(p);
        }

        private void OnTriggerExit(Collider other)
        {
            PlayerStats p = other.GetComponentInParent<PlayerStats>();
            if (p != null) inside.Remove(p);
        }
    }
}
