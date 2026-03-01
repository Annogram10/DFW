using UnityEngine;
using UnityEngine.AI;

public class ZombieAI : MonoBehaviour
{
    private NavMeshAgent agent;
    private GameObject player;

    void Start()
    {
        agent = GetComponent<NavMeshAgent>();

        player = GameObject.FindWithTag("Player");

        if (player == null)
        {
            Debug.LogWarning("ZombieAI: Could not find a GameObject tagged 'Player'. " +
                             "Make sure PlayerCapsule is tagged as 'Player'.");
        }
    }

    void Update()
    {
        if (player == null) return;

        agent.SetDestination(player.transform.position);
    }
}