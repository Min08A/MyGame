using System.Text;
using UnityEngine;

namespace LabEscape
{
    /// <summary>Виды шприцов-бустов.</summary>
    public enum BoostType
    {
        Speed,   // зелёный: ускорение
        Jump,    // синий: высокий и двойной прыжок
        Coconut  // кокосовая сыворотка: лечение + щит (коты отскакивают)
    }

    /// <summary>
    /// Статы мыши: здоровье, сытость (сыр), временные бусты и эффекты замедления.
    /// Сытость постепенно падает (при беге быстрее). Если еда кончилась — мышь теряет здоровье.
    /// </summary>
    public class PlayerStats : MonoBehaviour
    {
        [Header("Здоровье")]
        [SerializeField, Min(1f)] private float maxHealth = 100f;
        [Tooltip("Секунды неуязвимости после удара")]
        [SerializeField] private float invulnerabilityTime = 1f;

        [Header("Сытость (сыр)")]
        [SerializeField, Min(1f)] private float maxFood = 100f;
        [SerializeField] private float startFood = 80f;
        [Tooltip("Сколько сытости тратится в секунду при ходьбе")]
        [SerializeField] private float foodDrainPerSecond = 0.8f;
        [Tooltip("Во сколько раз быстрее тратится еда при беге")]
        [SerializeField] private float runDrainMultiplier = 2.5f;
        [Tooltip("Урон в секунду, когда сытость = 0")]
        [SerializeField] private float starvationDamagePerSecond = 5f;

        [Header("Бусты")]
        [SerializeField] private float speedBoostMultiplier = 1.6f;
        [SerializeField] private float coconutHeal = 40f;

        [Header("Визуал")]
        [Tooltip("Рендереры, которые мигают во время неуязвимости")]
        [SerializeField] private Renderer[] blinkRenderers;

        public float Health { get; private set; }
        public float MaxHealth => maxHealth;
        public float Food { get; private set; }
        public float MaxFood => maxFood;

        public float SpeedBoostTime { get; private set; }
        public float JumpBoostTime { get; private set; }
        public float ShieldTime { get; private set; }

        public bool IsAlive => Health > 0f;
        public bool HasDoubleJump => JumpBoostTime > 0f;
        public bool IsShielded => ShieldTime > 0f;
        public bool IsSlowed => slowMultiplier < 1f;

        /// <summary>Итоговый множитель скорости с учётом буста и замедления (клей).</summary>
        public float SpeedMultiplier => (SpeedBoostTime > 0f ? speedBoostMultiplier : 1f) * slowMultiplier;

        private float slowMultiplier = 1f;
        private float invulnerableTimer;
        private bool renderersVisible = true;
        private PlayerController controller;

        private void Awake()
        {
            controller = GetComponent<PlayerController>();
            Health = maxHealth;
            Food = Mathf.Clamp(startFood, 0f, maxFood);
        }

        private void Update()
        {
            if (!GameManager.IsPlaying || !IsAlive) return;
            float dt = Time.deltaTime;

            // Таймеры бустов
            SpeedBoostTime = Mathf.Max(0f, SpeedBoostTime - dt);
            JumpBoostTime = Mathf.Max(0f, JumpBoostTime - dt);
            ShieldTime = Mathf.Max(0f, ShieldTime - dt);

            // Мигание после урона
            invulnerableTimer -= dt;
            bool visible = invulnerableTimer <= 0f || Mathf.Repeat(Time.time * 12f, 1f) > 0.5f;
            SetRenderersVisible(visible);

            // Голод: при беге еда уходит быстрее
            float drain = foodDrainPerSecond;
            if (controller != null && controller.IsRunning) drain *= runDrainMultiplier;
            Food = Mathf.Max(0f, Food - drain * dt);

            if (Food <= 0f)
                ApplyDamage(starvationDamagePerSecond * dt, "Мышь ослабла от голода... Не забывай есть сыр!");
        }

        /// <summary>Удар (кот, лазер, ящик): даёт неуязвимость и отбрасывает мышь.</summary>
        public void TakeDamage(float amount, Vector3 sourcePosition, string cause)
        {
            if (!IsAlive || IsShielded || invulnerableTimer > 0f) return;

            invulnerableTimer = invulnerabilityTime;
            Vector3 dir = transform.position - sourcePosition;
            dir.y = 0f;
            if (dir.sqrMagnitude < 0.001f) dir = -transform.forward;
            if (controller != null) controller.Knockback(dir.normalized, 6f);

            ApplyDamage(amount, cause);
        }

        /// <summary>Постепенный урон (кислота) — без отбрасывания и неуязвимости.</summary>
        public void TakeDamageOverTime(float amount, string cause)
        {
            if (!IsAlive || IsShielded) return;
            ApplyDamage(amount, cause);
        }

        private void ApplyDamage(float amount, string cause)
        {
            Health = Mathf.Max(0f, Health - amount);
            if (Health <= 0f && GameManager.Instance != null)
                GameManager.Instance.Lose(cause);
        }

        /// <returns>true, если здоровье действительно восстановилось</returns>
        public bool Heal(float amount)
        {
            if (Health >= maxHealth) return false;
            Health = Mathf.Min(maxHealth, Health + amount);
            return true;
        }

        public void AddFood(float amount)
        {
            Food = Mathf.Min(maxFood, Food + amount);
        }

        /// <summary>Потратить еду (бросок крошки сыра). Возвращает false, если еды не хватает.</summary>
        public bool SpendFood(float amount)
        {
            if (Food < amount) return false;
            Food -= amount;
            return true;
        }

        public void ApplyBoost(BoostType type, float duration)
        {
            switch (type)
            {
                case BoostType.Speed:
                    SpeedBoostTime = Mathf.Max(SpeedBoostTime, duration);
                    break;
                case BoostType.Jump:
                    JumpBoostTime = Mathf.Max(JumpBoostTime, duration);
                    break;
                case BoostType.Coconut:
                    Heal(coconutHeal);
                    ShieldTime = Mathf.Max(ShieldTime, duration);
                    break;
            }
        }

        public void SetSlow(float multiplier) => slowMultiplier = Mathf.Clamp(multiplier, 0.1f, 1f);
        public void ClearSlow() => slowMultiplier = 1f;

        /// <summary>Текст для HUD со списком активных эффектов.</summary>
        public string ActiveEffectsDescription
        {
            get
            {
                var sb = new StringBuilder();
                if (SpeedBoostTime > 0f) sb.AppendLine($"<color=#6CFF6C>Скорость: {SpeedBoostTime:0} с</color>");
                if (JumpBoostTime > 0f) sb.AppendLine($"<color=#6CC8FF>Двойной прыжок: {JumpBoostTime:0} с</color>");
                if (ShieldTime > 0f) sb.AppendLine($"<color=#FFF2C8>Кокосовый щит: {ShieldTime:0} с</color>");
                if (IsSlowed) sb.AppendLine("<color=#FFD24A>Лапки липнут к клею!</color>");
                if (Food <= 0f) sb.AppendLine("<color=#FF6060>ГОЛОД! Найди сыр!</color>");
                return sb.ToString();
            }
        }

        private void SetRenderersVisible(bool visible)
        {
            if (visible == renderersVisible || blinkRenderers == null) return;
            renderersVisible = visible;
            foreach (var r in blinkRenderers)
                if (r != null) r.enabled = visible;
        }
    }
}
