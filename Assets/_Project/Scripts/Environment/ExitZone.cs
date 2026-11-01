using UnityEngine;

namespace LabEscape
{
    /// <summary>Конечная точка: если у мыши есть ключ-карта — победа.</summary>
    [RequireComponent(typeof(Collider))]
    public class ExitZone : MonoBehaviour
    {
        private void Awake()
        {
            GetComponent<Collider>().isTrigger = true;
        }

        private void OnTriggerEnter(Collider other)
        {
            if (other.GetComponentInParent<PlayerStats>() == null) return;
            GameManager gm = GameManager.Instance;
            if (gm == null) return;

            if (gm.HasKeycard) gm.Win();
            else gm.ShowMessage("Сначала нужна ключ-карта охраны!", 2f);
        }
    }
}
