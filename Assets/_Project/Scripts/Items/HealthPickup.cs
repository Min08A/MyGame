using UnityEngine;

namespace LabEscape
{
    /// <summary>Бинт (аптечка): лечит мышь. При полном здоровье не подбирается.</summary>
    public class HealthPickup : Pickup
    {
        [SerializeField] private float healAmount = 35f;

        private float nextHintTime;

        protected override bool TryApply(PlayerStats player)
        {
            if (player.Heal(healAmount)) return true;

            if (Time.time >= nextHintTime)
            {
                GameManager.Instance.ShowMessage("Здоровье полное — бинт пригодится позже", 1.5f);
                nextHintTime = Time.time + 3f;
            }
            return false;
        }

        protected override string PickupMessage => $"Бинт! Здоровье +{healAmount:0}";
    }
}
