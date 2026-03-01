using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;

public class MainMenu : MonoBehaviour
{
    [Tooltip("Name must exactly match the scene name in File > Build Settings")]
    public string sceneName = "Cave Demo";

    [Tooltip("Delay in seconds before the scene loads")]
    public float loadDelay = 2f;

    /// <summary>
    /// Call this from your UI Button's OnClick event.
    /// </summary>
    public void PlayGame()
    {
        StartCoroutine(LoadWithDelay());
    }

    private IEnumerator LoadWithDelay()
    {
        Debug.Log("Loading...");
        yield return new WaitForSeconds(loadDelay);
        SceneManager.LoadScene(sceneName);
    }
}