using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace StarterAssets
{
    public class GemSpawner : MonoBehaviour
    {
        [Header("References")]
        public GameObject gemPrefab;

        [Tooltip("Drag your cave parent here (same as BatterySpawner)")]
        public Transform caveRoot;

        [Header("Spawn Count")]
        public int spawnCount = 20;

        [Header("Spread Across Map")]
        public float boundsPadding = 2f;

        [Header("Raycast")]
        public float rayStartPadding = 10f;
        public float maxRayDistance = 200f;
        public LayerMask groundLayers = ~0;

        [Header("Placement Rules")]
        public float minSpacingBetweenGems = 3f;
        public float maxSlopeAngle = 35f;
        public float spawnYOffset = 1.5f;

        [Header("Timing")]
        public float spawnDelay = 5f;

        [Header("Attempts")]
        public int maxAttemptsMultiplier = 40;

        private void Start()
        {
            StartCoroutine(SpawnAfterDelay());
        }

        private IEnumerator SpawnAfterDelay()
        {
            yield return new WaitForSeconds(spawnDelay);
            SpawnGems();
        }

        [ContextMenu("Spawn Gems")]
        public void SpawnGems()
        {
            if (gemPrefab == null)
            {
                Debug.LogError("GemSpawner: No gem prefab assigned!");
                return;
            }

            if (caveRoot == null)
            {
                Debug.LogError("GemSpawner: Assign caveRoot (your Caves/Chunks parent).");
                return;
            }

            // Compute cave bounds from all renderers
            var renderers = caveRoot.GetComponentsInChildren<Renderer>();
            if (renderers.Length == 0)
            {
                Debug.LogError("GemSpawner: No renderers found under caveRoot.");
                return;
            }

            Bounds caveBounds = renderers[0].bounds;
            for (int i = 1; i < renderers.Length; i++)
                caveBounds.Encapsulate(renderers[i].bounds);

            float minY = caveBounds.min.y;
            float maxY = caveBounds.max.y;

            // Debug: test a single ray from cave center to check if colliders are working
            Vector3 testOrigin = new Vector3(caveBounds.center.x, maxY + rayStartPadding, caveBounds.center.z);
            Debug.DrawRay(testOrigin, Vector3.down * maxRayDistance, Color.red, 10f);
            if (Physics.Raycast(testOrigin, Vector3.down, out RaycastHit testHit, maxRayDistance))
                Debug.Log($"GemSpawner test ray HIT: {testHit.collider.name} layer={testHit.collider.gameObject.layer} at {testHit.point}");
            else
                Debug.LogWarning("GemSpawner test ray HIT NOTHING - try increasing spawnDelay or set groundLayers to Everything");

            float padX = Mathf.Min(boundsPadding, caveBounds.size.x * 0.45f);
            float padZ = Mathf.Min(boundsPadding, caveBounds.size.z * 0.45f);

            int spawned = 0;
            int attempts = 0;
            int maxAttempts = Mathf.Max(spawnCount * maxAttemptsMultiplier, 200);

            List<Vector3> spawnedPositions = new List<Vector3>(spawnCount);

            while (spawned < spawnCount && attempts < maxAttempts)
            {
                attempts++;

                float x = Random.Range(caveBounds.min.x + padX, caveBounds.max.x - padX);
                float z = Random.Range(caveBounds.min.z + padZ, caveBounds.max.z - padZ);

                Vector3 rayOrigin = new Vector3(x, maxY + rayStartPadding, z);

                if (!Physics.Raycast(rayOrigin, Vector3.down, out RaycastHit hit, maxRayDistance))
                    continue;

                float slope = Vector3.Angle(hit.normal, Vector3.up);
                if (slope > maxSlopeAngle) continue;

                if (hit.point.y < minY - 2f) continue;

                Vector3 spawnPos = hit.point + Vector3.up * spawnYOffset;

                bool tooClose = false;
                for (int i = 0; i < spawnedPositions.Count; i++)
                {
                    if (Vector3.Distance(spawnPos, spawnedPositions[i]) < minSpacingBetweenGems)
                    {
                        tooClose = true;
                        break;
                    }
                }
                if (tooClose) continue;

                Instantiate(gemPrefab, spawnPos, Quaternion.identity, caveRoot);
                spawnedPositions.Add(spawnPos);
                spawned++;
            }

            Debug.Log(
                $"GemSpawner: Spawned {spawned}/{spawnCount} gems in {attempts} attempts. " +
                $"CaveY[{minY:F1},{maxY:F1}] Bounds[{caveBounds.size.x:F1}x{caveBounds.size.z:F1}]"
            );

            if (spawned < spawnCount)
                Debug.LogWarning($"GemSpawner: Only spawned {spawned}/{spawnCount}. Try increasing spawnDelay, maxAttemptsMultiplier, or reducing minSpacingBetweenGems.");
        }

        private void OnDrawGizmosSelected()
        {
            if (caveRoot == null) return;

            var renderers = caveRoot.GetComponentsInChildren<Renderer>();
            if (renderers.Length == 0) return;

            Bounds caveBounds = renderers[0].bounds;
            for (int i = 1; i < renderers.Length; i++)
                caveBounds.Encapsulate(renderers[i].bounds);

            Gizmos.color = new Color(0.5f, 0f, 1f, 0.1f);
            Gizmos.DrawCube(caveBounds.center, caveBounds.size);
        }
    }
}