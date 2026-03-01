using UnityEngine;
using UnityEngine.SceneManagement;

public class ExitDoor : MonoBehaviour
{
    [Header("Settings")]
    [Tooltip("Name of the scene to load when player walks through")]
    public string nextSceneName = "GameOver";

    [Tooltip("Delay before loading the next scene")]
    public float transitionDelay = 0.5f;


    private bool _triggered = false;

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
        Gizmos.color = new Color(0f, 1f, 0.5f, 0.3f);
        Gizmos.DrawCube(transform.position, transform.localScale);
    }
}