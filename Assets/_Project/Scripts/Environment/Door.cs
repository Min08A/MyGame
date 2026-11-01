using UnityEngine;

namespace LabEscape
{
    /// <summary>
    /// Дверь/ворота, которые плавно уезжают вверх.
    /// Открываются нажимной плитой (PressurePlate) или ключ-картой (requiresKeycard).
    /// </summary>
    public class Door : MonoBehaviour
    {
        [SerializeField] private Vector3 openOffset = new Vector3(0f, 3f, 0f);
        [SerializeField] private float speed = 2.5f;

        [Header("Ключ-карта")]
        [SerializeField] private bool requiresKeycard;
        [SerializeField] private float keycardOpenDistance = 4f;

        public bool IsOpen { get; private set; }

        private Vector3 closedPosition;
        private float nextHintTime;

        private void Awake()
        {
            closedPosition = transform.localPosition;
        }

        private void Update()
        {
            if (requiresKeycard && !IsOpen) CheckKeycard();

            Vector3 target = closedPosition + (IsOpen ? openOffset : Vector3.zero);
            transform.localPosition = Vector3.MoveTowards(transform.localPosition, target, speed * Time.deltaTime);
        }

        private void CheckKeycard()
        {
            GameManager gm = GameManager.Instance;
            if (gm == null || gm.Player == null || !GameManager.IsPlaying) return;

            Vector3 d = gm.Player.transform.position - transform.position;
            d.y = 0f;
            if (d.magnitude > keycardOpenDistance) return;

            if (gm.HasKeycard)
            {
                Open();
                gm.ShowMessage("Ключ-карта подошла! Путь к свободе открыт!", 3f);
            }
            else if (Time.time >= nextHintTime)
            {
                gm.ShowMessage("Заперто. Нужна ключ-карта охраны — её носит Барсик!", 2.5f);
                nextHintTime = Time.time + 4f;
            }
        }

        public void Open() => IsOpen = true;
        public void Close() => IsOpen = false;
    }
}
