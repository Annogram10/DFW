using UnityEngine;

public class ZombieAI : MonoBehaviour
{
    // ── Animator State IDs ────────────────────────────────────────────────────
    private const int STATE_IDLE   = 0;
    private const int STATE_WALK   = 1;
    private const int STATE_ATTACK = 2;

    private static readonly int ZombieStateParam = Animator.StringToHash("ZombieState");

    private const string ANIM_IDLE   = "Z_Idle";
    private const string ANIM_WALK   = "Z_Walk1_InPlace";
    private const string ANIM_ATTACK = "Z_Attack";

    [Header("Detection")]
    public float detectionRange = 15f;
    public float attackRange = 1.5f;

    [Header("Movement")]
    public float moveSpeed = 2f;
    public float rotationSpeed = 5f;
    public float transitionDuration = 0f;

    [Header("Attack")]
    [Tooltip("How long after entering attack range before the kill triggers")]
    public float attackDelay = 0.5f;

    [Header("Gravity")]
    public float gravity = -20f;

    private Animator _animator;
    private CharacterController _controller;
    private GameObject _player;
    private int _lastState = -1;
    private float _verticalVelocity = 0f;

    private bool _attackPending = false;
    private float _attackTimer = 0f;

    void Start()
    {
        _animator   = GetComponent<Animator>();
        _controller = GetComponent<CharacterController>();
        _player     = GameObject.FindWithTag("Player");

        if (_player == null)
            Debug.LogWarning("ZombieAI: No GameObject tagged 'Player' found.");
        if (_animator == null)
            Debug.LogWarning("ZombieAI: No Animator found.");
        if (_controller == null)
            Debug.LogWarning("ZombieAI: No CharacterController found.");
    }

    void Update()
    {
        if (_player == null || _animator == null) return;

        float distance = Vector3.Distance(transform.position, _player.transform.position);

        // Gravity
        if (_controller != null && !_controller.isGrounded)
            _verticalVelocity += gravity * Time.deltaTime;
        else
            _verticalVelocity = -1f;

        if (distance <= attackRange)
        {
            SetState(STATE_ATTACK, ANIM_ATTACK);
            FacePlayer();

            // Start attack timer
            if (!_attackPending)
            {
                _attackPending = true;
                _attackTimer = attackDelay;
            }

            if (_attackPending)
            {
                _attackTimer -= Time.deltaTime;
                if (_attackTimer <= 0f)
                {
                    KillPlayer();
                    _attackTimer = attackDelay; // reset so it keeps firing if player stays in range
                }
            }
        }
        else
        {
            _attackPending = false;

            if (distance <= detectionRange)
            {
                SetState(STATE_WALK, ANIM_WALK);
                FacePlayer();
                MoveTowardPlayer();
            }
            else
            {
                SetState(STATE_IDLE, ANIM_IDLE);
            }
        }

        if (_controller != null)
            _controller.Move(new Vector3(0, _verticalVelocity, 0) * Time.deltaTime);
    }

    private void KillPlayer()
    {
        // TODO: replace this with actual player death logic later
        Debug.Log("PLAYER KILLED BY ZOMBIE");
    }

    private void MoveTowardPlayer()
    {
        if (_controller == null) return;
        Vector3 direction = (_player.transform.position - transform.position).normalized;
        direction.y = 0f;
        _controller.Move(direction * moveSpeed * Time.deltaTime);
    }

    private void SetState(int stateID, string clipName)
    {
        if (_lastState == stateID) return;
        _lastState = stateID;
        _animator.SetInteger(ZombieStateParam, stateID);
        _animator.CrossFadeInFixedTime(clipName, transitionDuration);
    }

    private void FacePlayer()
    {
        Vector3 direction = (_player.transform.position - transform.position).normalized;
        direction.y = 0f;
        if (direction == Vector3.zero) return;
        Quaternion targetRotation = Quaternion.LookRotation(direction);
        transform.rotation = Quaternion.Slerp(transform.rotation, targetRotation, Time.deltaTime * rotationSpeed);
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(transform.position, detectionRange);
        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(transform.position, attackRange);
    }
}