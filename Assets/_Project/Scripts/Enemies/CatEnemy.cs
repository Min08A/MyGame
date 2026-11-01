using System.Collections.Generic;
using UnityEngine;

namespace LabEscape
{
    /// <summary>
    /// Кот-охранник. Простой конечный автомат:
    /// Patrol (ходит по точкам) → Chase (видит мышь и гонится) → атака на близкой дистанции,
    /// Stunned (оглушён крошкой сыра, ящиком или прыжком на голову), Dead (уничтожается через Destroy).
    /// Босс (isBoss) после смерти роняет ключ-карту.
    /// </summary>
    [RequireComponent(typeof(CharacterController))]
    public class CatEnemy : MonoBehaviour
    {
        public enum CatState { Patrol, Chase, Stunned, Dead }

        [Header("Характеристики")]
        [SerializeField] private string displayName = "Кот";
        [SerializeField, Min(1)] private int maxHealth = 2;
        [SerializeField] private float patrolSpeed = 1.8f;
        [SerializeField] private float chaseSpeed = 4.2f;
        [SerializeField] private float turnSpeed = 420f;
        [SerializeField] private float gravity = -25f;

        [Header("Обнаружение")]
        [SerializeField] private float detectionRadius = 8f;
        [Tooltip("Если мышь дальше — кот бросает погоню")]
        [SerializeField] private float loseRadius = 14f;
        [SerializeField] private bool isBoss;

        [Header("Атака")]
        [SerializeField] private float attackRange = 1.2f;
        [SerializeField] private float attackDamage = 15f;
        [SerializeField] private float attackCooldown = 1.2f;

        [Header("Оглушение")]
        [SerializeField] private float stunDuration = 2.5f;

        [Header("Патруль")]
        [SerializeField] private List<Transform> waypoints = new List<Transform>();
        [SerializeField] private float waitAtWaypoint = 1f;

        [Header("Награда")]
        [Tooltip("Что выпадает после победы над котом (сыр, ключ-карта)")]
        [SerializeField] private GameObject dropOnDeath;
        [SerializeField, Range(0f, 1f)] private float dropChance = 0.5f;

        [Header("Визуал")]
        [SerializeField] private Transform model;

        public CatState State { get; private set; } = CatState.Patrol;
        public int Health { get; private set; }
        public bool IsBoss => isBoss;
        public bool CanBeStomped => State != CatState.Dead;
        public float TopY => cc.bounds.max.y;

        /// <summary>Событие смерти — на него подписываются спавнеры и т. п.</summary>
        public event System.Action<CatEnemy> Died;

        private CharacterController cc;
        private Transform player;
        private PlayerStats playerStats;
        private int waypointIndex;
        private float waitTimer;
        private float attackTimer;
        private float stunTimer;
        private float verticalVelocity;
        private Quaternion modelBaseRotation;

        private void Awake()
        {
            cc = GetComponent<CharacterController>();
            Health = maxHealth;
            if (model != null) modelBaseRotation = model.localRotation;
        }

        private void Start()
        {
            GameObject p = GameObject.FindWithTag("Player");
            if (p != null)
            {
                player = p.transform;
                playerStats = p.GetComponent<PlayerStats>();
            }
        }

        /// <summary>Задать маршрут патруля (используется спавнером).</summary>
        public void SetWaypoints(List<Transform> points)
        {
            waypoints = new List<Transform>(points);
            waypointIndex = waypoints.Count > 0 ? Random.Range(0, waypoints.Count) : 0;
        }

        private void Update()
        {
            if (!GameManager.IsPlaying || State == CatState.Dead) return;
            attackTimer -= Time.deltaTime;

            switch (State)
            {
                case CatState.Patrol: UpdatePatrol(); break;
                case CatState.Chase: UpdateChase(); break;
                case CatState.Stunned: UpdateStunned(); break;
            }

            // Возвращаем модель в нормальное положение после оглушения
            if (model != null && State != CatState.Stunned)
                model.localRotation = Quaternion.Slerp(model.localRotation, modelBaseRotation, 1f - Mathf.Exp(-8f * Time.deltaTime));
        }

        private void UpdatePatrol()
        {
            if (CanSeePlayer(detectionRadius))
            {
                State = CatState.Chase;
                return;
            }

            if (waypoints.Count == 0)
            {
                Move(Vector3.zero, 0f);
                return;
            }

            Transform target = waypoints[waypointIndex];
            if (target == null)
            {
                waypointIndex = (waypointIndex + 1) % waypoints.Count;
                return;
            }

            Vector3 toTarget = Flat(target.position - transform.position);
            if (toTarget.magnitude < 0.6f)
            {
                Move(Vector3.zero, 0f);
                waitTimer += Time.deltaTime;
                if (waitTimer >= waitAtWaypoint)
                {
                    waitTimer = 0f;
                    waypointIndex = (waypointIndex + 1) % waypoints.Count;
                }
            }
            else
            {
                Move(toTarget.normalized, patrolSpeed);
            }
        }

        private void UpdateChase()
        {
            if (!CanSeePlayer(loseRadius))
            {
                State = CatState.Patrol;
                return;
            }

            Vector3 toPlayer = Flat(player.position - transform.position);
            float dist = toPlayer.magnitude;
            float reach = attackRange + cc.radius * transform.lossyScale.x;

            if (dist > reach * 0.8f) Move(toPlayer.normalized, chaseSpeed);
            else
            {
                Face(toPlayer);
                Move(Vector3.zero, 0f);
            }

            bool playerOnTop = player.position.y > TopY - 0.2f;
            bool sameLevel = Mathf.Abs(player.position.y - transform.position.y) < 1.5f * transform.lossyScale.y;
            if (dist <= reach && sameLevel && !playerOnTop)
            {
                if (playerStats.IsShielded)
                {
                    Stun();
                    GameManager.Instance.ShowMessage("Кокосовый щит! Кот отлетел!", 1.5f);
                }
                else if (attackTimer <= 0f)
                {
                    attackTimer = attackCooldown;
                    playerStats.TakeDamage(attackDamage, transform.position, $"{displayName} поймал мышь!");
                }
            }
        }

        private void UpdateStunned()
        {
            stunTimer -= Time.deltaTime;
            Move(Vector3.zero, 0f);

            // Кот «заваливается на бок»
            if (model != null)
            {
                Quaternion lying = modelBaseRotation * Quaternion.Euler(0f, 0f, 75f);
                model.localRotation = Quaternion.Slerp(model.localRotation, lying, 1f - Mathf.Exp(-10f * Time.deltaTime));
            }

            if (stunTimer <= 0f) State = CatState.Chase;
        }

        private bool CanSeePlayer(float radius)
        {
            if (player == null || playerStats == null || !playerStats.IsAlive) return false;
            Vector3 d = player.position - transform.position;
            if (Mathf.Abs(d.y) > 4f) return false;
            return Flat(d).magnitude <= radius;
        }

        private void Move(Vector3 direction, float speed)
        {
            float dt = Time.deltaTime;
            if (cc.isGrounded && verticalVelocity < 0f) verticalVelocity = -2f;
            verticalVelocity += gravity * dt;

            if (direction.sqrMagnitude > 0.001f) Face(direction);
            cc.Move((direction * speed + Vector3.up * verticalVelocity) * dt);
        }

        private void Face(Vector3 direction)
        {
            if (direction.sqrMagnitude < 0.001f) return;
            Quaternion target = Quaternion.LookRotation(direction);
            transform.rotation = Quaternion.RotateTowards(transform.rotation, target, turnSpeed * Time.deltaTime);
        }

        private static Vector3 Flat(Vector3 v)
        {
            v.y = 0f;
            return v;
        }

        public void Stun()
        {
            if (State == CatState.Dead) return;
            State = CatState.Stunned;
            stunTimer = stunDuration;
        }

        /// <summary>Урон от крошки сыра, падающего ящика и т. д.</summary>
        public void TakeDamage(int amount)
        {
            if (State == CatState.Dead) return;

            Health -= amount;
            if (Health <= 0)
            {
                Die();
                return;
            }

            Stun();
            if (isBoss)
                GameManager.Instance.ShowMessage($"{displayName}: осталось {Health} из {maxHealth} жизней", 1.5f);
        }

        /// <summary>Мышь прыгнула на голову.</summary>
        public void Stomp() => TakeDamage(1);

        private void Die()
        {
            State = CatState.Dead;
            if (GameManager.Instance != null) GameManager.Instance.RegisterCatDefeated(isBoss, displayName);

            if (dropOnDeath != null && (isBoss || Random.value <= dropChance))
                Instantiate(dropOnDeath, transform.position + Vector3.up * 0.6f, Quaternion.identity);

            Died?.Invoke(this);
            Destroy(gameObject);
        }

        private void OnDrawGizmosSelected()
        {
            Gizmos.color = new Color(1f, 0.8f, 0f, 0.6f);
            Gizmos.DrawWireSphere(transform.position, detectionRadius);
            Gizmos.color = new Color(1f, 0f, 0f, 0.6f);
            Gizmos.DrawWireSphere(transform.position, attackRange);
            Gizmos.color = Color.cyan;
            for (int i = 0; i < waypoints.Count; i++)
            {
                if (waypoints[i] == null) continue;
                Transform next = waypoints[(i + 1) % waypoints.Count];
                if (next != null) Gizmos.DrawLine(waypoints[i].position, next.position);
            }
        }
    }
}
