using UnityEngine;

namespace LabEscape
{
    /// <summary>
    /// Управление мышью через CharacterController:
    /// ходьба (WASD/стрелки) относительно камеры, бег (Shift), прыжок (Space),
    /// плавный разгон, поворот модели по направлению движения, толкание физических предметов,
    /// езда на движущихся платформах и «прыжок на голову» кота.
    /// Все расчёты умножаются на Time.deltaTime, поэтому скорость не зависит от FPS.
    /// </summary>
    [RequireComponent(typeof(CharacterController))]
    public class PlayerController : MonoBehaviour
    {
        [Header("Ссылки")]
        [SerializeField] private Transform cameraTransform;
        [Tooltip("Визуальная модель мыши — поворачивается в сторону движения")]
        [SerializeField] private Transform model;

        [Header("Скорость")]
        [SerializeField, Min(0f)] private float walkSpeed = 4f;
        [SerializeField, Min(0f)] private float runSpeed = 7.5f;
        [Tooltip("Чем больше — тем резче разгон")]
        [SerializeField] private float acceleration = 10f;
        [Tooltip("Чем больше — тем быстрее остановка")]
        [SerializeField] private float deceleration = 14f;
        [Tooltip("Доля управления в воздухе (0..1)")]
        [SerializeField, Range(0f, 1f)] private float airControl = 0.6f;
        [SerializeField] private float rotationSpeed = 720f;

        [Header("Прыжок")]
        [SerializeField] private float jumpHeight = 1.4f;
        [Tooltip("Высота прыжка под синим шприцем")]
        [SerializeField] private float boostedJumpHeight = 2.1f;
        [SerializeField] private float gravity = -25f;
        [Tooltip("Можно прыгнуть чуть позже, чем сошёл с края")]
        [SerializeField] private float coyoteTime = 0.12f;
        [Tooltip("Нажатие прыжка чуть раньше приземления тоже засчитывается")]
        [SerializeField] private float jumpBufferTime = 0.15f;
        [SerializeField] private float maxFallSpeed = -30f;

        [Header("Взаимодействие")]
        [Tooltip("С какой скоростью мышь толкает ящики (делится на массу)")]
        [SerializeField] private float pushStrength = 6f;
        [SerializeField] private float stompBounceHeight = 1.6f;
        [SerializeField] private float stompRadius = 0.35f;

        public bool IsGrounded { get; private set; }
        public bool IsRunning { get; private set; }
        public Vector3 HorizontalVelocity => horizontalVelocity;

        private CharacterController cc;
        private PlayerStats stats;
        private Vector3 horizontalVelocity;
        private Vector3 externalVelocity;
        private float verticalVelocity;
        private float lastGroundedTime = -10f;
        private float lastJumpPressedTime = -10f;
        private float nextStompTime;
        private int airJumpsUsed;
        private MovingPlatform groundPlatform;

        private void Awake()
        {
            cc = GetComponent<CharacterController>();
            stats = GetComponent<PlayerStats>();
            if (cameraTransform == null && Camera.main != null) cameraTransform = Camera.main.transform;
        }

        private void Update()
        {
            if (!GameManager.IsPlaying) return;
            float dt = Time.deltaTime;

            // --- Земля ---
            IsGrounded = cc.isGrounded;
            if (IsGrounded)
            {
                lastGroundedTime = Time.time;
                airJumpsUsed = 0;
                if (verticalVelocity < 0f) verticalVelocity = -2f; // прижимаем к земле
            }

            // --- Направление относительно камеры ---
            Vector2 input = GameInput.Move;
            Vector3 forward = cameraTransform != null ? cameraTransform.forward : Vector3.forward;
            forward.y = 0f;
            forward.Normalize();
            Vector3 right = new Vector3(forward.z, 0f, -forward.x);
            Vector3 wishDir = forward * input.y + right * input.x;
            if (wishDir.sqrMagnitude > 1f) wishDir.Normalize();

            bool hasInput = wishDir.sqrMagnitude > 0.01f;
            IsRunning = GameInput.Run && hasInput;

            // --- Плавный разгон/торможение (экспоненциальное сглаживание, не зависит от FPS) ---
            float speedMultiplier = stats != null ? stats.SpeedMultiplier : 1f;
            Vector3 targetVelocity = wishDir * (IsRunning ? runSpeed : walkSpeed) * speedMultiplier;
            float rate = (hasInput ? acceleration : deceleration) * (IsGrounded ? 1f : airControl);
            horizontalVelocity = Vector3.Lerp(horizontalVelocity, targetVelocity, 1f - Mathf.Exp(-rate * dt));

            // --- Прыжок (с «койот-таймом» и буфером) ---
            if (GameInput.JumpPressed) lastJumpPressedTime = Time.time;
            if (Time.time - lastJumpPressedTime <= jumpBufferTime)
            {
                if (Time.time - lastGroundedTime <= coyoteTime)
                {
                    DoJump();
                    lastGroundedTime = -10f;
                    lastJumpPressedTime = -10f;
                }
                else if (stats != null && stats.HasDoubleJump && airJumpsUsed < 1)
                {
                    // Двойной прыжок есть только под синим шприцем — бесконечно прыгать в воздухе нельзя
                    airJumpsUsed++;
                    DoJump();
                    lastJumpPressedTime = -10f;
                }
            }

            // --- Гравитация и внешние силы ---
            verticalVelocity = Mathf.Max(verticalVelocity + gravity * dt, maxFallSpeed);
            externalVelocity = Vector3.Lerp(externalVelocity, Vector3.zero, 1f - Mathf.Exp(-5f * dt));

            Vector3 platformDelta = groundPlatform != null ? groundPlatform.Delta : Vector3.zero;
            groundPlatform = null; // обновится в OnControllerColliderHit

            Vector3 motion = (horizontalVelocity + externalVelocity + Vector3.up * verticalVelocity) * dt + platformDelta;
            CollisionFlags flags = cc.Move(motion);

            // Ударились головой о потолок — прекращаем подъём
            if ((flags & CollisionFlags.Above) != 0 && verticalVelocity > 0f) verticalVelocity = 0f;

            RotateModel(dt);
            CheckStomp();
        }

        private void DoJump()
        {
            float height = stats != null && stats.HasDoubleJump ? boostedJumpHeight : jumpHeight;
            verticalVelocity = Mathf.Sqrt(2f * height * -gravity);
        }

        private void RotateModel(float dt)
        {
            if (model == null) return;
            Vector3 flat = horizontalVelocity;
            flat.y = 0f;
            if (flat.sqrMagnitude < 0.05f) return;
            Quaternion target = Quaternion.LookRotation(flat);
            model.rotation = Quaternion.RotateTowards(model.rotation, target, rotationSpeed * dt);
        }

        /// <summary>Мгновенно развернуть модель (например, в сторону броска).</summary>
        public void FaceDirection(Vector3 direction)
        {
            direction.y = 0f;
            if (model != null && direction.sqrMagnitude > 0.001f)
                model.rotation = Quaternion.LookRotation(direction);
        }

        public void Knockback(Vector3 direction, float force)
        {
            externalVelocity = direction * force;
            if (verticalVelocity < 3f) verticalVelocity = 3f;
        }

        /// <summary>Прыжок на голову кота оглушает его и подбрасывает мышь.</summary>
        private void CheckStomp()
        {
            if (verticalVelocity > 0f || Time.time < nextStompTime) return;

            Vector3 feet = transform.position + Vector3.up * 0.1f;
            Collider[] hits = Physics.OverlapSphere(feet, stompRadius, ~0, QueryTriggerInteraction.Ignore);
            foreach (Collider hit in hits)
            {
                CatEnemy cat = hit.GetComponentInParent<CatEnemy>();
                if (cat == null || !cat.CanBeStomped) continue;
                if (transform.position.y < cat.TopY - 0.35f) continue; // мы не сверху

                cat.Stomp();
                verticalVelocity = Mathf.Sqrt(2f * stompBounceHeight * -gravity);
                nextStompTime = Time.time + 0.4f;
                break;
            }
        }

        private void OnControllerColliderHit(ControllerColliderHit hit)
        {
            // Стоим на движущейся платформе — едем вместе с ней
            if (hit.normal.y > 0.5f)
            {
                MovingPlatform platform = hit.collider.GetComponentInParent<MovingPlatform>();
                if (platform != null) groundPlatform = platform;
            }

            // Толкаем физические объекты (ящики, колбы)
            Rigidbody rb = hit.collider.attachedRigidbody;
            if (rb == null || rb.isKinematic) return;
            if (hit.moveDirection.y < -0.3f) return; // не толкаем то, на чём стоим

            Vector3 push = new Vector3(hit.moveDirection.x, 0f, hit.moveDirection.z);
            Vector3 target = push * (pushStrength / Mathf.Max(1f, rb.mass));
            rb.linearVelocity = new Vector3(target.x, rb.linearVelocity.y, target.z);
        }

        private void OnDrawGizmosSelected()
        {
            Gizmos.color = Color.yellow;
            Gizmos.DrawWireSphere(transform.position + Vector3.up * 0.1f, stompRadius);
        }
    }
}
