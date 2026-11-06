using System.Collections.Generic;
using UnityEngine;
using ZombieRunner.Player;

namespace ZombieRunner.Enemies
{
    /// <summary>Spawns configurable zombie archetypes ahead of the runner, across the three lanes.</summary>
    [DisallowMultipleComponent]
    public sealed class ZombieSpawner : MonoBehaviour
    {
        [SerializeField] private Transform player;
        [SerializeField] private GameObject normalZombiePrefab;
        [SerializeField] private GameObject fastZombiePrefab;
        [SerializeField] private GameObject bigZombiePrefab;
        [SerializeField, Min(0.25f)] private float spawnInterval = 2.8f;
        [SerializeField, Min(10f)] private float spawnDistance = 48f;
        [SerializeField, Min(1)] private int maximumAlive = 6;
        [SerializeField, Min(0.1f)] private float laneWidth = 3f;

        [Header("Testing & Predictable Spawning")]
        [SerializeField] private bool testModePredictableSpawning = false;
        [SerializeField] private ZombieArchetype forcedTestArchetype = ZombieArchetype.Normal;
        [SerializeField] private bool cycleTestArchetypes = false;

        private readonly List<ZombieController> activeZombies = new List<ZombieController>();
        private float spawnTimer;
        private int testCycleIndex;

        public event System.Action<ZombieController> ZombieKilled;
        public int ActiveZombieCount => activeZombies.Count;
        public IReadOnlyList<ZombieController> ActiveZombies => activeZombies;

        private void Start()
        {
            EnsurePlayerReference();
            EnsurePrefabReferences();
            spawnTimer = spawnInterval * 0.5f;

            // Register any pre-placed preview zombies in the scene
            ZombieController[] existing = FindObjectsByType<ZombieController>(FindObjectsInactive.Exclude);
            foreach (ZombieController z in existing)
            {
                if (z != null && !activeZombies.Contains(z))
                {
                    activeZombies.Add(z);
                    z.Died += HandleZombieDied;
                }
            }
        }

        private void EnsurePlayerReference()
        {
            if (player != null) return;

            PlayerController runner = FindAnyObjectByType<PlayerController>();
            if (runner != null)
            {
                player = runner.transform;
            }
            else
            {
                GameObject playerObj = GameObject.FindWithTag("Player");
                if (playerObj != null)
                    player = playerObj.transform;
            }
        }

        private void EnsurePrefabReferences()
        {
            if (normalZombiePrefab == null)
            {
                normalZombiePrefab = fastZombiePrefab ?? bigZombiePrefab;
#if UNITY_EDITOR
                if (normalZombiePrefab == null)
                {
                    normalZombiePrefab = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>("Assets/ZombieRunner/Prefabs/Zombies/NormalZombie.prefab")
                        ?? UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>("Assets/ZombieRunner/Prefabs/Zombies/CommonZombie.prefab");
                }
#endif
            }

            if (fastZombiePrefab == null)
            {
#if UNITY_EDITOR
                fastZombiePrefab = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>("Assets/ZombieRunner/Prefabs/Zombies/FastZombie.prefab");
#endif
                if (fastZombiePrefab == null)
                    fastZombiePrefab = normalZombiePrefab;
            }

            if (bigZombiePrefab == null)
            {
#if UNITY_EDITOR
                bigZombiePrefab = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>("Assets/ZombieRunner/Prefabs/Zombies/BigZombie.prefab")
                    ?? UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>("Assets/ZombieRunner/Prefabs/Zombies/HeavyZombie.prefab");
#endif
                if (bigZombiePrefab == null)
                    bigZombiePrefab = normalZombiePrefab;
            }
        }

        private void Update()
        {
            EnsurePlayerReference();
            EnsurePrefabReferences();

            if (player == null || normalZombiePrefab == null)
                return;

            for (int i = activeZombies.Count - 1; i >= 0; i--)
            {
                if (activeZombies[i] == null)
                {
                    activeZombies.RemoveAt(i);
                    continue;
                }
                if (player.position.z - activeZombies[i].transform.position.z > 12f)
                {
                    ZombieController missedZombie = activeZombies[i];
                    activeZombies.RemoveAt(i);
                    missedZombie.Died -= HandleZombieDied;
                    Destroy(missedZombie.gameObject);
                }
            }

            spawnTimer -= Time.deltaTime;
            if (spawnTimer > 0f)
                return;

            spawnTimer = spawnInterval;
            if (activeZombies.Count < maximumAlive)
                SpawnOne();
        }

        public void SpawnOne()
        {
            EnsurePlayerReference();
            EnsurePrefabReferences();

            if (player == null)
                return;

            ZombieArchetype archetype = ChooseArchetype();
            GameObject prefab = GetPrefab(archetype);
            if (prefab == null)
                prefab = normalZombiePrefab;
            if (prefab == null)
                return;

            int lane = Random.Range(-1, 2);
            Vector3 position = new Vector3(lane * laneWidth, 0f, player.position.z + spawnDistance);
            GameObject instance = Instantiate(prefab, position, Quaternion.Euler(0f, 180f, 0f), transform);
            if (!instance.activeSelf)
                instance.SetActive(true);

            // Ensure visual children are active
            for (int i = 0; i < instance.transform.childCount; i++)
            {
                Transform child = instance.transform.GetChild(i);
                if (child.name.StartsWith("Visual"))
                    child.gameObject.SetActive(true);
            }

            ZombieController zombie = instance.GetComponent<ZombieController>();
            if (zombie == null)
                zombie = instance.AddComponent<ZombieController>();
            zombie.Configure(archetype, player, lane, laneWidth);
            zombie.Died += HandleZombieDied;
            activeZombies.Add(zombie);
            Debug.Log($"[ZombieRunner] Spawner -> Spawned {archetype} zombie ('{prefab.name}') at lane {lane} (Pos: {position})");
        }

        private ZombieArchetype ChooseArchetype()
        {
            if (testModePredictableSpawning)
            {
                if (cycleTestArchetypes)
                {
                    ZombieArchetype[] cycle = { ZombieArchetype.Normal, ZombieArchetype.Fast, ZombieArchetype.Big };
                    ZombieArchetype chosen = cycle[testCycleIndex % cycle.Length];
                    testCycleIndex++;
                    return chosen;
                }
                return forcedTestArchetype;
            }

            float choice = Random.value;
            if (choice < 0.25f && bigZombiePrefab != null)
                return ZombieArchetype.Big;
            if (choice < 0.55f && fastZombiePrefab != null)
                return ZombieArchetype.Fast;
            return ZombieArchetype.Normal;
        }

        private GameObject GetPrefab(ZombieArchetype archetype)
        {
            switch (archetype)
            {
                case ZombieArchetype.Fast: return fastZombiePrefab;
                case ZombieArchetype.Big: return bigZombiePrefab;
                default: return normalZombiePrefab;
            }
        }

        private void HandleZombieDied(ZombieController zombie)
        {
            zombie.Died -= HandleZombieDied;
            activeZombies.Remove(zombie);
            ZombieKilled?.Invoke(zombie);
        }

        private void OnValidate()
        {
            spawnInterval = Mathf.Max(0.25f, spawnInterval);
            spawnDistance = Mathf.Max(10f, spawnDistance);
            maximumAlive = Mathf.Max(1, maximumAlive);
            laneWidth = Mathf.Max(0.1f, laneWidth);
        }
    }
}
