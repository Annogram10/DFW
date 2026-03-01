using UnityEngine;
using UnityEngine.SceneManagement;

public class ExitDoor : MonoBehaviour
{
    [Header("Settings")]
    public string nextSceneName = "GameOver";
    public float transitionDelay = 0.5f;

    [Header("Placement")]
    public Transform caveRoot;
    public float spawnDelay = 3f;
    public LayerMask wallLayers = ~0;
    public float raycastDistance = 200f;

    private bool _triggered = false;

    private void Start()
    {
        Invoke(nameof(PlaceDoor), spawnDelay);
    }

    private void PlaceDoor()
    {
        if (caveRoot == null)
        {
            Debug.LogError("ExitDoor: Assign caveRoot!");
            return;
        }

        // Get cave bounds
        var renderers = caveRoot.GetComponentsInChildren<Renderer>();
        if (renderers.Length == 0) return;

        Bounds bounds = renderers[0].bounds;
        foreach (var r in renderers) bounds.Encapsulate(r.bounds);

        // Try to find a wall + floor combo
        int attempts = 0;
        while (attempts < 200)
        {
            attempts++;

            // Pick a random point inside cave bounds
            float x = Random.Range(bounds.min.x + 5f, bounds.max.x - 5f);
            float z = Random.Range(bounds.min.z + 5f, bounds.max.z - 5f);
            float y = Random.Range(bounds.min.y + 1f, bounds.max.y - 1f);

            Vector3 origin = new Vector3(x, y, z);

            // Pick a random horizontal direction to find a wall
            Vector3[] directions = { Vector3.forward, Vector3.back, Vector3.left, Vector3.right };
            Vector3 wallDir = directions[Random.Range(0, directions.Length)];

            // Check for wall hit
            if (!Physics.Raycast(origin, wallDir, out RaycastHit wallHit, raycastDistance, wallLayers))
                continue;

            // Check wall is roughly vertical
            float wallAngle = Vector3.Angle(wallHit.normal, Vector3.up);
            if (wallAngle < 60f || wallAngle > 120f) continue;

            // From the wall hit point, raycast down to find the floor
            Vector3 floorOrigin = wallHit.point + wallHit.normal * 0.5f + Vector3.up * 3f;
            if (!Physics.Raycast(floorOrigin, Vector3.down, out RaycastHit floorHit, 10f, wallLayers))
                continue;

            // Check floor is roughly horizontal
            float floorAngle = Vector3.Angle(floorHit.normal, Vector3.up);
            if (floorAngle > 30f) continue;

            // Place the door flush against the wall, sitting on the floor
            Vector3 doorPos = floorHit.point + wallHit.normal * 0.1f;
            transform.position = doorPos;

            // Rotate door to face away from wall
            transform.rotation = Quaternion.LookRotation(wallHit.normal);

            Debug.Log($"ExitDoor placed at {doorPos} after {attempts} attempts.");
            return;
        }

        Debug.LogWarning("ExitDoor: Could not find a valid wall+floor position after 200 attempts.");
    }

    private void OnTriggerEnter(Collider other)
    {
        if (_triggered) return;
        if (!other.CompareTag("Player")) return;

        _triggered = true;
        Debug.Log("Player reached the exit!");
        Invoke(nameof(LoadNextScene), transitionDelay);
    }

    private void LoadNextScene()
    {
        if (!string.IsNullOrEmpty(nextSceneName))
            SceneManager.LoadScene(nextSceneName);
        else
            Debug.LogWarning("ExitDoor: No scene name set!");
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = new Color(0f, 1f, 0.5f, 0.4f);
        Gizmos.DrawCube(transform.position, transform.localScale);
    }
}   