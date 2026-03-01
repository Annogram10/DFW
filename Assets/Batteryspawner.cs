using System.Collections.Generic;
using UnityEngine;

namespace StarterAssets
{
    public class BatterySpawner : MonoBehaviour
    {
        [Header("References")]
        public GameObject batteryPrefab;

        [Tooltip("Drag your cave parent here (ex: Caves or Chunks)")]
        public Transform caveRoot;

        [Header("Spawn Count")]
        public int spawnCount = 10;

        [Header("Spread Across Map")]
        [Tooltip("Padding from the cave bounds edges so batteries don't spawn on extreme edges/inside walls.")]
        public float boundsPadding = 2f;

        [Header("Raycast")]
        [Tooltip("How far above the cave top we start the ray")]
        public float rayStartPadding = 10f;

        [Tooltip("Max raycast distance downward")]
        public float maxRayDistance = 200f;

        [Tooltip("Layers to raycast against. Leave as Everything if you don't want to mess with layers.")]
        public LayerMask groundLayers = ~0; // Everything by default

        [Header("Placement Rules")]
        [Tooltip("Minimum distance between each battery")]
        public float minSpacingBetweenBatteries = 5f;

        [Tooltip("Only place on floor-ish surfaces")]
        public float maxSlopeAngle = 35f;

        [Tooltip("Lift spawned battery slightly above the hit point")]
        public float spawnYOffset = 0.5f;

        [Header("Attempts")]
        public int maxAttemptsMultiplier = 40;

        private void Start()
        {
            SpawnBatteries();
        }

        [ContextMenu("Spawn Batteries")]
        public void SpawnBatteries()
        {
            if (batteryPrefab == null)
            {
                Debug.LogError("BatterySpawner: No battery prefab assigned!");
                return;
            }

            if (caveRoot == null)
            {
                Debug.LogError("BatterySpawner: Assign caveRoot (your Caves/Chunks parent).");
                return;
            }

            // --- Compute cave bounds (min/max world extents) ---
            var renderers = caveRoot.GetComponentsInChildren<Renderer>();
            if (renderers.Length == 0)
            {
                Debug.LogError("BatterySpawner: No renderers found under caveRoot.");
                return;
            }

            Bounds caveBounds = renderers[0].bounds;
            for (int i = 1; i < renderers.Length; i++)
                caveBounds.Encapsulate(renderers[i].bounds);

            float minY = caveBounds.min.y;
            float maxY = caveBounds.max.y;

            // Padding safety: prevent negative ranges
            float padX = Mathf.Min(boundsPadding, caveBounds.size.x * 0.45f);
            float padZ = Mathf.Min(boundsPadding, caveBounds.size.z * 0.45f);

            int spawned = 0;
            int attempts = 0;
            int maxAttempts = Mathf.Max(spawnCount * maxAttemptsMultiplier, 200);

            List<Vector3> spawnedPositions = new List<Vector3>(spawnCount);

            while (spawned < spawnCount && attempts < maxAttempts)
            {
                attempts++;

                // ✅ Spread across the ENTIRE cave bounds (not just one quadrant / radius)
                float x = Random.Range(caveBounds.min.x + padX, caveBounds.max.x - padX);
                float z = Random.Range(caveBounds.min.z + padZ, caveBounds.max.z - padZ);

                Vector3 rayOrigin = new Vector3(x, maxY + rayStartPadding, z);

                if (!Physics.Raycast(rayOrigin, Vector3.down, out RaycastHit hit, maxRayDistance, groundLayers))
                    continue;

                // Only accept floor-ish hits
                float slope = Vector3.Angle(hit.normal, Vector3.up);
                if (slope > maxSlopeAngle) continue;

                // Extra safety: ignore weird hits far below the cave
                if (hit.point.y < minY - 2f) continue;

                Vector3 spawnPos = hit.point + Vector3.up * spawnYOffset;

                // Spacing rule
                bool tooClose = false;
                for (int i = 0; i < spawnedPositions.Count; i++)
                {
                    if (Vector3.Distance(spawnPos, spawnedPositions[i]) < minSpacingBetweenBatteries)
                    {
                        tooClose = true;
                        break;
                    }
                }
                if (tooClose) continue;

                Instantiate(batteryPrefab, spawnPos, Quaternion.identity, caveRoot);
                spawnedPositions.Add(spawnPos);
                spawned++;
            }

            Debug.Log(
                $"BatterySpawner: Spawned {spawned}/{spawnCount} batteries in {attempts} attempts. " +
                $"CaveY[{minY:F1},{maxY:F1}] Bounds[{caveBounds.size.x:F1}x{caveBounds.size.z:F1}]"
            );
        }

        private void OnDrawGizmosSelected()
        {
            if (caveRoot == null) return;

            var renderers = caveRoot.GetComponentsInChildren<Renderer>();
            if (renderers.Length == 0) return;

            Bounds caveBounds = renderers[0].bounds;
            for (int i = 1; i < renderers.Length; i++)
                caveBounds.Encapsulate(renderers[i].bounds);

            Gizmos.color = new Color(1f, 1f, 0f, 0.1f);
            Gizmos.DrawCube(caveBounds.center, caveBounds.size);
        }
    }
}