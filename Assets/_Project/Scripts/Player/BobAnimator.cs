using UnityEngine;

namespace LabEscape
{
    /// <summary>
    /// Простая процедурная анимация без Animator: модель подпрыгивает при беге,
    /// хвост виляет. Работает и для мыши, и для котов.
    /// </summary>
    public class BobAnimator : MonoBehaviour
    {
        [SerializeField] private Transform model;
        [SerializeField] private Transform tail;
        [SerializeField] private float bobHeight = 0.06f;
        [SerializeField] private float bobFrequency = 14f;
        [Tooltip("При какой скорости покачивание максимальное")]
        [SerializeField] private float speedForFullBob = 4f;
        [SerializeField] private float tailWagAngle = 25f;
        [SerializeField] private float tailWagSpeed = 6f;

        private Vector3 lastPosition;
        private Vector3 modelBasePosition;
        private Quaternion tailBaseRotation;
        private float phase;

        private void Start()
        {
            lastPosition = transform.position;
            if (model != null) modelBasePosition = model.localPosition;
            if (tail != null) tailBaseRotation = tail.localRotation;
        }

        private void Update()
        {
            float dt = Time.deltaTime;
            if (dt <= 0f) return;

            Vector3 delta = transform.position - lastPosition;
            delta.y = 0f;
            lastPosition = transform.position;
            float k = Mathf.Clamp01(delta.magnitude / dt / speedForFullBob);

            if (model != null)
            {
                phase += dt * bobFrequency * Mathf.Lerp(0.3f, 1f, k);
                model.localPosition = modelBasePosition + Vector3.up * (Mathf.Abs(Mathf.Sin(phase)) * bobHeight * k);
            }

            if (tail != null)
            {
                float angle = Mathf.Sin(Time.time * tailWagSpeed * (1f + k)) * tailWagAngle;
                tail.localRotation = tailBaseRotation * Quaternion.Euler(0f, angle, 0f);
            }
        }
    }
}
