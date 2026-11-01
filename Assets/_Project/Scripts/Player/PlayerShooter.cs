using UnityEngine;

namespace LabEscape
{
    /// <summary>
    /// Бросок крошки сыра (ЛКМ или F). Крошка создаётся через Instantiate,
    /// летит туда, куда смотрит камера, и оглушает/ранит котов.
    /// Каждый бросок тратит сытость — сыр одновременно еда и «боеприпасы».
    /// </summary>
    public class PlayerShooter : MonoBehaviour
    {
        [SerializeField] private CheeseCrumb crumbPrefab;
        [SerializeField] private Transform firePoint;
        [Tooltip("Камера, по направлению которой целимся")]
        [SerializeField] private Transform aimCamera;
        [SerializeField] private float launchSpeed = 18f;
        [SerializeField] private float upwardBoost = 1.5f;
        [Tooltip("Сколько сытости стоит один бросок")]
        [SerializeField] private float foodCost = 6f;
        [SerializeField] private float cooldown = 0.35f;

        private PlayerStats stats;
        private PlayerController controller;
        private Collider[] ownColliders;
        private float nextShotTime;

        private void Awake()
        {
            stats = GetComponent<PlayerStats>();
            controller = GetComponent<PlayerController>();
            ownColliders = GetComponentsInChildren<Collider>();
            if (aimCamera == null && Camera.main != null) aimCamera = Camera.main.transform;
        }

        private void Update()
        {
            if (!GameManager.IsPlaying || crumbPrefab == null) return;
            if (!GameInput.FirePressed || Time.time < nextShotTime) return;

            if (!stats.SpendFood(foodCost))
            {
                GameManager.Instance.ShowMessage("Слишком голодна для броска! Найди сыр.", 1.5f);
                nextShotTime = Time.time + 0.5f;
                return;
            }

            nextShotTime = Time.time + cooldown;
            Fire();
        }

        private void Fire()
        {
            Vector3 origin = firePoint != null ? firePoint.position : transform.position + Vector3.up * 0.5f;
            Vector3 direction = GetAimPoint() - origin;
            if (direction.sqrMagnitude < 0.01f) direction = transform.forward;
            direction.Normalize();

            CheeseCrumb crumb = Instantiate(crumbPrefab, origin, Quaternion.LookRotation(direction));
            crumb.Launch(direction * launchSpeed + Vector3.up * upwardBoost, ownColliders);

            if (controller != null) controller.FaceDirection(direction);
        }

        /// <summary>Точка, на которую смотрит центр экрана (пропускаем саму мышь).</summary>
        private Vector3 GetAimPoint()
        {
            if (aimCamera == null) return transform.position + transform.forward * 20f;

            Ray ray = new Ray(aimCamera.position, aimCamera.forward);
            RaycastHit[] hits = Physics.RaycastAll(ray, 60f, ~0, QueryTriggerInteraction.Ignore);
            float best = float.MaxValue;
            Vector3 point = ray.GetPoint(40f);
            foreach (RaycastHit hit in hits)
            {
                if (hit.transform.root == transform.root) continue;
                if (hit.distance < best)
                {
                    best = hit.distance;
                    point = hit.point;
                }
            }
            return point;
        }
    }
}
