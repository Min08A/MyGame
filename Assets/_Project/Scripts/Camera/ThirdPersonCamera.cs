using UnityEngine;

namespace LabEscape
{
    /// <summary>
    /// Камера от третьего лица: перспектива, настраиваемый FOV, вращение мышью
    /// с ограничением вертикального угла, плавное следование (SmoothDamp),
    /// приближение колёсиком и защита от прохождения сквозь стены (SphereCast).
    /// </summary>
    [RequireComponent(typeof(Camera))]
    public class ThirdPersonCamera : MonoBehaviour
    {
        [Header("Цель")]
        [SerializeField] private Transform target;
        [Tooltip("Точка, вокруг которой вращается камера (над мышью)")]
        [SerializeField] private Vector3 pivotOffset = new Vector3(0f, 0.6f, 0f);

        [Header("Камера")]
        [SerializeField, Range(30f, 100f)] private float fieldOfView = 65f;
        [SerializeField] private float distance = 4f;
        [SerializeField] private float minDistance = 1.5f;
        [SerializeField] private float maxDistance = 7f;
        [SerializeField] private float zoomSpeed = 1.5f;

        [Header("Вращение мышью")]
        [SerializeField] private float mouseSensitivity = 3f;
        [SerializeField] private bool invertY;
        [SerializeField, Range(-89f, 0f)] private float minPitch = -25f;
        [SerializeField, Range(0f, 89f)] private float maxPitch = 70f;
        [SerializeField] private float startPitch = 18f;

        [Header("Плавность")]
        [Tooltip("Время сглаживания следования. 0 — жёсткое следование")]
        [SerializeField] private float followSmoothTime = 0.08f;

        [Header("Столкновения со стенами")]
        [SerializeField] private bool avoidWalls = true;
        [SerializeField] private float collisionRadius = 0.25f;
        [SerializeField] private LayerMask collisionMask = ~0;
        [SerializeField] private float wallReturnSpeed = 6f;

        private Camera cam;
        private float yaw;
        private float pitch;
        private float currentDistance;
        private Vector3 smoothedPivot;
        private Vector3 pivotVelocity;

        private void Awake()
        {
            cam = GetComponent<Camera>();
            cam.orthographic = false; // Projection = Perspective
            cam.fieldOfView = fieldOfView;
        }

        private void Start()
        {
            pitch = startPitch;
            currentDistance = distance;
            if (target != null)
            {
                yaw = target.eulerAngles.y;
                smoothedPivot = target.position + pivotOffset;
            }
        }

        public void SetTarget(Transform newTarget)
        {
            target = newTarget;
            if (target != null) smoothedPivot = target.position + pivotOffset;
        }

        private void LateUpdate()
        {
            if (target == null) return;
            float dt = Time.deltaTime;

            if (GameManager.IsPlaying)
            {
                Vector2 look = GameInput.Look;
                yaw += look.x * mouseSensitivity;
                pitch += (invertY ? 1f : -1f) * look.y * mouseSensitivity;
                pitch = Mathf.Clamp(pitch, minPitch, maxPitch); // ограничение вертикального угла
                distance = Mathf.Clamp(distance - GameInput.Zoom * zoomSpeed, minDistance, maxDistance);
            }

            Vector3 pivotTarget = target.position + pivotOffset;
            if (dt > 0f)
                smoothedPivot = Vector3.SmoothDamp(smoothedPivot, pivotTarget, ref pivotVelocity, followSmoothTime);

            Quaternion rotation = Quaternion.Euler(pitch, yaw, 0f);
            Vector3 back = rotation * Vector3.back;

            float wanted = distance;
            if (avoidWalls) wanted = Mathf.Min(wanted, GetCollisionDistance(smoothedPivot, back, distance));

            // К стене приближаемся мгновенно, от стены отъезжаем плавно
            currentDistance = wanted < currentDistance
                ? wanted
                : Mathf.Lerp(currentDistance, wanted, 1f - Mathf.Exp(-wallReturnSpeed * dt));

            transform.SetPositionAndRotation(smoothedPivot + back * currentDistance, rotation);
        }

        private float GetCollisionDistance(Vector3 origin, Vector3 direction, float maxDist)
        {
            RaycastHit[] hits = Physics.SphereCastAll(origin, collisionRadius, direction, maxDist,
                collisionMask, QueryTriggerInteraction.Ignore);

            float best = maxDist;
            foreach (RaycastHit hit in hits)
            {
                if (hit.distance <= 0f) continue;
                if (hit.transform.root == target.root) continue;                       // сама мышь
                if (hit.rigidbody != null && !hit.rigidbody.isKinematic) continue;      // ящики, колбы
                if (hit.collider.GetComponentInParent<CatEnemy>() != null) continue;   // коты
                best = Mathf.Min(best, hit.distance);
            }
            return Mathf.Max(best - 0.05f, 0.2f);
        }

        private void OnValidate()
        {
            Camera c = GetComponent<Camera>();
            if (c != null)
            {
                c.orthographic = false;
                c.fieldOfView = fieldOfView;
            }
            if (minDistance > maxDistance) minDistance = maxDistance;
        }
    }
}
