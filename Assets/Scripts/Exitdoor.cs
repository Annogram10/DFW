using UnityEngine;
using UnityEngine.SceneManagement;

public class ExitDoor : MonoBehaviour
{
    [Header("Settings")]
    public string nextSceneName = "GameOver";
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
}