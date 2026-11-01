namespace LabEscape
{
    /// <summary>Ключ-карта охраны — открывает вентиляционную дверь на выходе.</summary>
    public class KeycardPickup : Pickup
    {
        protected override bool TryApply(PlayerStats player)
        {
            GameManager.Instance.GiveKeycard();
            return true;
        }

        protected override string PickupMessage => "Ключ-карта охраны получена! Беги к выходу!";
    }
}
