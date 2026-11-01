using UnityEngine;

namespace LabEscape
{
    /// <summary>
    /// Шприц-буст. Тип задаётся в Inspector (у каждого Prefab Variant свой):
    /// Speed — скорость, Jump — высокий двойной прыжок, Coconut — лечение и щит.
    /// </summary>
    public class SyringePickup : Pickup
    {
        [SerializeField] private BoostType boostType = BoostType.Speed;
        [SerializeField] private float duration = 8f;

        protected override bool TryApply(PlayerStats player)
        {
            player.ApplyBoost(boostType, duration);
            return true;
        }

        protected override string PickupMessage
        {
            get
            {
                switch (boostType)
                {
                    case BoostType.Speed: return $"Зелёный шприц: скорость x1.6 на {duration:0} с!";
                    case BoostType.Jump: return $"Синий шприц: двойной прыжок на {duration:0} с!";
                    case BoostType.Coconut: return $"Кокосовая сыворотка: лечение и щит на {duration:0} с!";
                    default: return "Шприц!";
                }
            }
        }
    }
}
