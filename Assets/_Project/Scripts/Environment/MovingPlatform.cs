using UnityEngine;

namespace LabEscape
{
    /// <summary>
    /// Платформа, которая плавно ездит туда-обратно. Мышь, стоящая на ней, едет вместе с платформой
    /// (PlayerController читает Delta). Выполняется раньше остальных скриптов.
    /// </summary>
    [DefaultExecutionOrder(-100)]
    public class MovingPlatform : MonoBehaviour
    {
        [Tooltip("Куда сдвигается платформа относительно стартовой позиции")]
        [SerializeField] private Vector3 moveOffset = new Vector3(0f, 0f, 4f);
        [SerializeField, Min(0.1f)] private float speed = 1.5f;
        [SerializeField] private float waitTime = 0.8f;

        /// <summary>На сколько платформа сдвинулась за этот кадр.</summary>
        public Vector3 Delta { get; private set; }

        private Vector3 startPosition;
        private float progress;   // 0 — старт, 1 — конец
        private int direction = 1;
        private float waitTimer;

        private void Awake()
        {
            startPosition = transform.position;
        }

        private void Update()
        {
            Delta = Vector3.zero;
            if (!GameManager.IsPlaying) return;

            if (waitTimer > 0f)
            {
                waitTimer -= Time.deltaTime;
                return;
            }

            float length = Mathf.Max(0.01f, moveOffset.magnitude);
            progress += direction * speed / length * Time.deltaTime;
            if (progress >= 1f || progress <= 0f)
            {
                progress = Mathf.Clamp01(progress);
                direction = -direction;
                waitTimer = waitTime;
            }

            Vector3 old = transform.position;
            transform.position = startPosition + moveOffset * Mathf.SmoothStep(0f, 1f, progress);
            Delta = transform.position - old;
        }

        private void OnDrawGizmosSelected()
        {
            Vector3 from = Application.isPlaying ? startPosition : transform.position;
            Gizmos.color = Color.green;
            Gizmos.DrawLine(from, from + moveOffset);
            Gizmos.DrawWireCube(from + moveOffset, transform.lossyScale);
        }
    }
}
