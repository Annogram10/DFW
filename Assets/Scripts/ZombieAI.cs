using UnityEngine;
using UnityEngine.AI;

public class ZombieAI : MonoBehaviour
{
    // ── Animator State IDs ────────────────────────────────────────────────────
    private const int STATE_IDLE   = 0; // Z_Idle
    private const int STATE_WALK   = 1; // Z_Walk1_InPlace
    private const int STATE_ATTACK = 2; // Z_Attack

    // ── Animator Parameter ────────────────────────────────────────────────────
    private static readonly int ZombieStateParam = Animator.StringToHash("ZombieState");

    // ── Animation Clip Names (must match exactly in your Animator Controller) ─
    private const string ANIM_IDLE   = "Z_Idle";
    private const string ANIM_WALK   = "Z_Walk1_InPlace";
    private const string ANIM_ATTACK = "Z_Attack";

    // ── Tuning ────────────────────────────────────────────────────────────────
    [Header("Detection")]
    [Tooltip("Distance at which the zombie notices the player and starts chasing")]
    public float detectionRange = 15f;

    [Tooltip("Distance at which the zombie stops moving and attacks")]
    public float attackRange = 1.5f;

    [Tooltip("How fast the zombie rotates to face the player (degrees per second)")]
    public float rotationSpeed = 5f;

    [Tooltip("Blend time for animation transitions — keep at 0 for instant switching")]
    public float transitionDuration = 0f;

    // ── Component References ──────────────────────────────────────────────────
    private NavMeshAgent agent;
    private Animator animator;
    private GameObject player;

    // ── State Tracking (prevents spamming CrossFade every frame) ─────────────
    private int lastState = -1;

    // ─────────────────────────────────────────────────────────────────────────

    void Start()
    {
        agent    = GetComponent<NavMeshAgent>();
        animator = GetComponent<Animator>();

        player = GameObject.FindWithTag("Player");

        if (player == null)
            Debug.LogWarning("ZombieAI: No GameObject tagged 'Player' found. " +
                             "Make sure PlayerCapsule is tagged 'Player'.");
        if (animator == null)
            Debug.LogWarning("ZombieAI: No Animator found on this GameObject.");
        if (agent == null)
            Debug.LogWarning("ZombieAI: No NavMeshAgent found on this GameObject.");
    }

    void Update()
    {
        if (player == null || agent == null || animator == null) return;

        float distance = Vector3.Distance(transform.position, player.transform.position);

        if (distance <= attackRange)
        {
            SetState(STATE_ATTACK, ANIM_ATTACK);
            agent.isStopped = true;
            agent.ResetPath();
            FacePlayer();
        }
        else if (distance <= detectionRange)
        {
            SetState(STATE_WALK, ANIM_WALK);
            agent.isStopped = false;
            agent.SetDestination(player.transform.position);
            FacePlayer();
        }
        else
        {
            SetState(STATE_IDLE, ANIM_IDLE);
            agent.isStopped = true;
            agent.ResetPath();
        }
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    /// <summary>
    /// Sets the ZombieState integer AND forces an immediate animation crossfade.
    /// Only fires when the state actually changes to avoid spamming the Animator.
    /// </summary>
    private void SetState(int stateID, string clipName)
    {
        if (lastState == stateID) return; // Already in this state — do nothing

        lastState = stateID;
        animator.SetInteger(ZombieStateParam, stateID);
        animator.CrossFadeInFixedTime(clipName, transitionDuration);
    }

    private void FacePlayer()
    {
        Vector3 direction = (player.transform.position - transform.position).normalized;
        direction.y = 0f;

        if (direction == Vector3.zero) return;

        Quaternion targetRotation = Quaternion.LookRotation(direction);
        transform.rotation = Quaternion.Slerp(transform.rotation, targetRotation, Time.deltaTime * rotationSpeed);
    }

    // ── Gizmos ────────────────────────────────────────────────────────────────

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(transform.position, detectionRange);

        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(transform.position, attackRange);
    }
}