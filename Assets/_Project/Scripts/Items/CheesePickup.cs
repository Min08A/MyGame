using UnityEngine;

namespace LabEscape
{
    /// <summary>Сыр — главная еда мыши. Восстанавливает сытость и увеличивает счёт.</summary>
    public class CheesePickup : Pickup
    {
        [SerializeField] private float foodAmount = 30f;
        [SerializeField] private int scoreValue = 1;

        protected override bool TryApply(PlayerStats player)
        {
            player.AddFood(foodAmount);
            GameManager.Instance.AddCheese(scoreValue);
            return true;
        }

        protected override string PickupMessage => $"Сыр! Сытость +{foodAmount:0}";
    }
}
