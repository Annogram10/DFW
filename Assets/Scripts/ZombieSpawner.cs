using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

public class ZombieSpawner : MonoBehaviour
{
    [Header("References")]
    public GameObject zombiePrefab;

    [Header("Spawn Count")]
    public int spawnCount = 5;

    [Header("Spawn Radius Around Player")]
    [Tooltip("Minimum distance from the player a zombie can spawn (so they don't appear on top of you)")]
    public float minSpawnRadius = 10f;

    [Tooltip("Maximum distance from the player a zombie can spawn (keeps them close enough to matter)")]
    public float maxSpawnRadius = 30f;

    [Header("NavMesh Sampling")]
    [Tooltip("How far from the random point we search for a valid NavMesh position")]
    public float navMeshSampleRange = 5f;

    [Header("Spacing Between Zombies")]
    [Tooltip("Minimum distance required between each spawned zombie")]
    public float minSpacingBetweenZombies = 3f;

    [Header("Attempts")]
    public int maxAttemptsMultiplier = 40;

    private GameObject player;

    private void Start()
    {
        player = GameObject.FindWithTag("Player");

        if (player == null)
        {
            Debug.LogError("ZombieSpawner: Could not find a GameObject tagged 'Player'. " +
                           "Make sure PlayerCapsule is tagged as 'Player'.");
            return;
        }

        SpawnZombies();
    }

    [ContextMenu("Spawn Zombies")]
    public void SpawnZombies()
    {
        if (zombiePrefab == null)
        {
            Debug.LogError("ZombieSpawner: No zombie prefab assigned!");
            return;
        }

        if (player == null)
        {
            player = GameObject.FindWithTag("Player");
            if (player == null)
            {
                Debug.LogError("ZombieSpawner: Still can't find the Player. Aborting spawn.");
                return;
            }
        }

        Vector3 playerPos = player.transform.position;

        int spawned = 0;
        int attempts = 0;
        int maxAttempts = Mathf.Max(spawnCount * maxAttemptsMultiplier, 200);

        List<Vector3> spawnedPositions = new List<Vector3>(spawnCount);

        while (spawned < spawnCount && attempts < maxAttempts)
        {
            attempts++;

            // Pick a random direction and a random distance within the donut radius
            Vector2 randomCircle = Random.insideUnitCircle.normalized;
            float distance = Random.Range(minSpawnRadius, maxSpawnRadius);

            Vector3 candidatePos = new Vector3(
                playerPos.x + randomCircle.x * distance,
                playerPos.y,
                playerPos.y + randomCircle.y * distance  // intentional: XZ plane offset
            );

            // Correct Z — redo this properly on XZ plane
            candidatePos = new Vector3(
                playerPos.x + randomCircle.x * distance,
                playerPos.y,
                playerPos.z + randomCircle.y * distance
            );

            // Make sure the point lands on the NavMesh
            if (!NavMesh.SamplePosition(candidatePos, out NavMeshHit hit, navMeshSampleRange, NavMesh.AllAreas))
                continue;

            Vector3 spawnPos = hit.position;

            // Enforce minimum spacing between zombies
            bool tooClose = false;
            for (int i = 0; i < spawnedPositions.Count; i++)
            {
                if (Vector3.Distance(spawnPos, spawnedPositions[i]) < minSpacingBetweenZombies)
                {
                    tooClose = true;
                    break;
                }
            }
            if (tooClose) continue;

            Instantiate(zombiePrefab, spawnPos, Quaternion.identity);
            spawnedPositions.Add(spawnPos);
            spawned++;
        }

        Debug.Log($"ZombieSpawner: Spawned {spawned}/{spawnCount} zombies in {attempts} attempts " +
                  $"around player at {playerPos}.");
    }

    // Draw spawn radius gizmos in the Scene view so you can visualize the donut
    private void OnDrawGizmosSelected()
    {
        Vector3 center = player != null
            ? player.transform.position
            : transform.position;

        Gizmos.color = new Color(1f, 0f, 0f, 0.2f);
        Gizmos.DrawWireSphere(center, maxSpawnRadius);

        Gizmos.color = new Color(1f, 0.5f, 0f, 0.2f);
        Gizmos.DrawWireSphere(center, minSpawnRadius);
    }
}