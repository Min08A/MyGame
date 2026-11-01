using System.Collections.Generic;
using UnityEngine;

namespace LabEscape
{
    /// <summary>
    /// Универсальная система спавна (враги или бонусы). Всё настраивается в Inspector:
    /// список префабов, точки появления, интервал, максимум одновременно живых и общий лимит.
    /// Объекты создаются через Instantiate, мёртвые/подобранные удаляются из списка.
    /// </summary>
    public class Spawner : MonoBehaviour
    {
        [Header("Что спавнить (выбирается случайно)")]
        [SerializeField] private GameObject[] prefabs;

        [Header("Где")]
        [SerializeField] private Transform[] spawnPoints;

        [Header("Как часто")]
        [SerializeField, Min(0.1f)] private float spawnInterval = 8f;
        [SerializeField, Min(0f)] private float firstSpawnDelay = 1f;
        [Tooltip("Сколько объектов может существовать одновременно")]
        [SerializeField, Min(1)] private int maxAlive = 3;
        [Tooltip("Сколько всего можно создать. 0 — без ограничения")]
        [SerializeField, Min(0)] private int totalLimit = 0;

        [Header("Активация")]
        [Tooltip("Если выключено — спавнер запускает ZoneTrigger, когда мышь входит в зону")]
        [SerializeField] private bool activeOnStart;

        [Header("Только для врагов")]
        [Tooltip("Маршрут патруля, который получат созданные коты")]
        [SerializeField] private List<Transform> enemyPatrolPoints = new List<Transform>();

        private readonly List<GameObject> alive = new List<GameObject>();
        private bool isActive;
        private float timer;
        private int spawnedTotal;

        public int AliveCount
        {
            get
            {
                alive.RemoveAll(go => go == null);
                return alive.Count;
            }
        }

        private void Start()
        {
            if (activeOnStart) Activate();
        }

        public void Activate()
        {
            if (isActive) return;
            isActive = true;
            timer = firstSpawnDelay;
        }

        public void Deactivate() => isActive = false;

        private void Update()
        {
            if (!isActive || !GameManager.IsPlaying) return;
            if (totalLimit > 0 && spawnedTotal >= totalLimit) return;
            if (AliveCount >= maxAlive) return;

            timer -= Time.deltaTime;
            if (timer > 0f) return;

            timer = spawnInterval;
            Spawn();
        }

        public GameObject Spawn()
        {
            if (prefabs == null || prefabs.Length == 0 || spawnPoints == null || spawnPoints.Length == 0)
                return null;

            GameObject prefab = prefabs[Random.Range(0, prefabs.Length)];
            Transform point = PickFreePoint();
            if (prefab == null || point == null) return null;

            GameObject go = Instantiate(prefab, point.position, point.rotation, transform);
            spawnedTotal++;
            go.name = $"{prefab.name}_{spawnedTotal}";

            CatEnemy cat = go.GetComponent<CatEnemy>();
            if (cat != null && enemyPatrolPoints.Count > 0) cat.SetWaypoints(enemyPatrolPoints);

            alive.Add(go);
            return go;
        }

        /// <summary>Случайная точка, рядом с которой ещё ничего не заспавнено.</summary>
        private Transform PickFreePoint()
        {
            for (int attempt = 0; attempt < 6; attempt++)
            {
                Transform p = spawnPoints[Random.Range(0, spawnPoints.Length)];
                if (p == null) continue;
                bool occupied = false;
                foreach (GameObject go in alive)
                {
                    if (go != null && (go.transform.position - p.position).sqrMagnitude < 2.25f)
                    {
                        occupied = true;
                        break;
                    }
                }
                if (!occupied) return p;
            }
            return spawnPoints[Random.Range(0, spawnPoints.Length)];
        }

        private void OnDrawGizmos()
        {
            if (spawnPoints == null) return;
            Gizmos.color = new Color(1f, 0.4f, 0.9f, 0.8f);
            foreach (Transform p in spawnPoints)
                if (p != null) Gizmos.DrawWireSphere(p.position, 0.4f);
        }
    }
}
