using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace StarterAssets
{
    public class ZombieSpawner : MonoBehaviour
    {
        [Header("References")]
        public GameObject zombiePrefab;
        public Transform caveRoot;

        [Header("Spawn Settings")]
        public int maxZombiesAlive = 5;
        public float spawnDelay = 3f;
        [Tooltip("How often to check and respawn zombies (seconds)")]
        public float respawnCheckInterval = 5f;

        [Header("Spawn Distance From Player")]
        public float minSpawnDistanceFromPlayer = 10f;
        public float maxSpawnDistanceFromPlayer = 40f;

        [Header("Raycast")]
        public float rayStartPadding = 10f;
        public float maxRayDistance = 200f;
        public LayerMask groundLayers = ~0;

        [Header("Placement Rules")]
        public float minSpacingBetweenZombies = 4f;
        public float maxSlopeAngle = 35f;
        public float spawnYOffset = 0.5f;

        [Header("Attempts")]
        public int maxAttemptsMultiplier = 40;

        private List<GameObject> _aliveZombies = new List<GameObject>();
        private GameObject _player;
        private Bounds _caveBounds;
        private bool _boundsReady = false;

        private void Start()
        {
            _player = GameObject.FindWithTag("Player");
            if (_player == null)
                Debug.LogError("ZombieSpawner: No GameObject tagged 'Player' found!");

            StartCoroutine(InitAndSpawn());
        }

        private IEnumerator InitAndSpawn()
        {
            // Wait for cave to generate
            yield return new WaitForSeconds(spawnDelay);

            // Compute cave bounds once
            if (caveRoot != null)
            {
                var renderers = caveRoot.GetComponentsInChildren<Renderer>();
                if (renderers.Length > 0)
                {
                    _caveBounds = renderers[0].bounds;
                    for (int i = 1; i < renderers.Length; i++)
                        _caveBounds.Encapsulate(renderers[i].bounds);
                    _boundsReady = true;
                }
            }

            if (!_boundsReady)
            {
                Debug.LogError("ZombieSpawner: Could not compute cave bounds. Assign caveRoot.");
                yield break;
            }

            // Initial spawn
            SpawnZombies();

            // Keep checking and respawning forever
            while (true)
            {
                yield return new WaitForSeconds(respawnCheckInterval);
                CleanDeadZombies();
                if (_aliveZombies.Count < maxZombiesAlive)
                    SpawnZombies();
            }
        }

        private void CleanDeadZombies()
        {
            _aliveZombies.RemoveAll(z => z == null);
        }

        private void SpawnZombies()
        {
            if (zombiePrefab == null)
            {
                Debug.LogError("ZombieSpawner: No zombie prefab assigned!");
                return;
            }

            int toSpawn = maxZombiesAlive - _aliveZombies.Count;
            if (toSpawn <= 0) return;

            float minY = _caveBounds.min.y;
            float maxY = _caveBounds.max.y;

            int spawned = 0;
            int attempts = 0;
            int maxAttempts = Mathf.Max(toSpawn * maxAttemptsMultiplier, 200);

            List<Vector3> spawnedPositions = new List<Vector3>();

            // Seed with existing zombie positions so new ones space away from them
            foreach (var z in _aliveZombies)
                if (z != null) spawnedPositions.Add(z.transform.position);

            while (spawned < toSpawn && attempts < maxAttempts)
            {
                attempts++;

                float x = Random.Range(_caveBounds.min.x, _caveBounds.max.x);
                float z = Random.Range(_caveBounds.min.z, _caveBounds.max.z);

                Vector3 rayOrigin = new Vector3(x, maxY + rayStartPadding, z);

                if (!Physics.Raycast(rayOrigin, Vector3.down, out RaycastHit hit, maxRayDistance, groundLayers))
                    continue;

                float slope = Vector3.Angle(hit.normal, Vector3.up);
                if (slope > maxSlopeAngle) continue;

                if (hit.point.y < minY - 2f) continue;

                Vector3 spawnPos = hit.point + Vector3.up * spawnYOffset;

                // Must be within distance range from player
                if (_player != null)
                {
                    float distToPlayer = Vector3.Distance(spawnPos, _player.transform.position);
                    if (distToPlayer < minSpawnDistanceFromPlayer || distToPlayer > maxSpawnDistanceFromPlayer)
                        continue;
                }

                // Spacing check
                bool tooClose = false;
                foreach (var p in spawnedPositions)
                {
                    if (Vector3.Distance(spawnPos, p) < minSpacingBetweenZombies)
                    {
                        tooClose = true;
                        break;
                    }
                }
                if (tooClose) continue;

                GameObject zombie = Instantiate(zombiePrefab, spawnPos, Quaternion.identity);
                _aliveZombies.Add(zombie);
                spawnedPositions.Add(spawnPos);
                spawned++;
            }

            Debug.Log($"ZombieSpawner: Spawned {spawned}/{toSpawn} zombies. Total alive: {_aliveZombies.Count}");
        }

        private void OnDrawGizmosSelected()
        {
            if (_player != null)
            {
                Gizmos.color = new Color(1f, 0f, 0f, 0.15f);
                Gizmos.DrawWireSphere(_player.transform.position, maxSpawnDistanceFromPlayer);
                Gizmos.color = new Color(1f, 0.5f, 0f, 0.15f);
                Gizmos.DrawWireSphere(_player.transform.position, minSpawnDistanceFromPlayer);
            }
        }
    }
}