using System.Collections.Generic;
using UnityEngine;

namespace LabEscape
{
    /// <summary>
    /// Нажимная плита. Срабатывает только от тяжёлого физического объекта (ящика) —
    /// мышь слишком лёгкая. Открывает привязанные двери.
    /// </summary>
    [RequireComponent(typeof(Collider))]
    public class PressurePlate : MonoBehaviour
    {
        [Tooltip("Минимальная масса Rigidbody, чтобы плита нажалась")]
        [SerializeField] private float requiredMass = 2f;
        [SerializeField] private Door[] doors;
        [Tooltip("Остаётся нажатой навсегда после первого срабатывания")]
        [SerializeField] private bool stayActivated = true;

        [Header("Визуал")]
        [SerializeField] private Renderer indicator;
        [SerializeField] private Color idleColor = new Color(0.9f, 0.15f, 0.1f);
        [SerializeField] private Color activeColor = new Color(0.2f, 1f, 0.3f);

        private readonly HashSet<Rigidbody> bodies = new HashSet<Rigidbody>();
        private bool activated;
        private float nextHintTime;

        private void Awake()
        {
            GetComponent<Collider>().isTrigger = true;
            SetIndicator(idleColor);
        }

        private void OnTriggerEnter(Collider other)
        {
            Rigidbody rb = other.attachedRigidbody;
            if (rb != null && !rb.isKinematic && rb.mass >= requiredMass)
            {
                bodies.Add(rb);
                Refresh();
            }
            else if (!activated && other.GetComponentInParent<PlayerStats>() != null && Time.time >= nextHintTime)
            {
                nextHintTime = Time.time + 4f;
                GameManager.Instance.ShowMessage("Мышь слишком лёгкая для кнопки... Толкни сюда ящик!", 3f);
            }
        }

        private void OnTriggerExit(Collider other)
        {
            Rigidbody rb = other.attachedRigidbody;
            if (rb != null && bodies.Remove(rb)) Refresh();
        }

        private void Refresh()
        {
            bodies.RemoveWhere(b => b == null);
            bool pressed = bodies.Count > 0;

            if (pressed && !activated)
            {
                activated = true;
                foreach (Door d in doors) if (d != null) d.Open();
                SetIndicator(activeColor);
                GameManager.Instance.ShowMessage("Щёлк! Ворота открываются!", 2.5f);
            }
            else if (!pressed && activated && !stayActivated)
            {
                activated = false;
                foreach (Door d in doors) if (d != null) d.Close();
                SetIndicator(idleColor);
            }
        }

        private void SetIndicator(Color c)
        {
            if (indicator == null) return;
            Material m = indicator.material; // создаётся копия материала только для этой плиты
            if (m.HasProperty("_BaseColor")) m.SetColor("_BaseColor", c);
            if (m.HasProperty("_Color")) m.SetColor("_Color", c);
            if (m.HasProperty("_EmissionColor")) m.SetColor("_EmissionColor", c * 0.6f);
        }
    }
}
