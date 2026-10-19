using UnityEngine;
using UnityEngine.AI;

public class EnemyAI : MonoBehaviour
{
    [Header("Detection & Attack Ranges")]
    [Tooltip("Distance at which the enemy notices and starts chasing the player.")]
    public float chaseRange = 10f;

    [Tooltip("Distance at which the enemy stops to attack.")]
    public float attackRange = 2f;

    [Tooltip("Time in seconds between consecutive attacks.")]
    public float attackCooldown = 1.5f;

    [Header("Component References")]
    public Transform player;
    public NavMeshAgent agent;
    public Animator animator;

    private float lastAttackTime;

    void Start()
    {
        // Automatically fetch missing components on this GameObject or children
        if (agent == null) agent = GetComponent<NavMeshAgent>();
        if (animator == null) animator = GetComponentInChildren<Animator>();

        // Find player by tag if not explicitly assigned in Inspector
        if (player == null)
        {
            GameObject playerObj = GameObject.FindGameObjectWithTag("Player");
            if (playerObj != null)
            {
                player = playerObj.transform;
            }
            else
            {
                Debug.LogWarning("EnemyAI: No GameObject with tag 'Player' found in scene!");
            }
        }
    }

    void Update()
    {
        if (player == null) return;

        // 1. Send current NavMesh movement speed to Animator for Idle <-> Walk blending
        float currentSpeed = agent.velocity.magnitude;
        animator.SetFloat("Speed", currentSpeed);

        // 2. Measure distance to player
        float distanceToPlayer = Vector3.Distance(transform.position, player.position);

        // 3. Determine state based on distance
        if (distanceToPlayer <= attackRange)
        {
            // Inside Attack Range: Stop moving & Attack on cooldown
            agent.isStopped = true;

            if (Time.time >= lastAttackTime + attackCooldown)
            {
                AttackPlayer();
                lastAttackTime = Time.time;
            }
        }
        else if (distanceToPlayer <= chaseRange)
        {
            // Inside Chase Range: Move towards player
            agent.isStopped = false;
            agent.SetDestination(player.position);
        }
        else
        {
            // Outside Range: Stand still in place
            agent.isStopped = true;
        }
    }

    void AttackPlayer()
    {
        // Smoothly rotate to face the player before swinging
        Vector3 lookDirection = (player.position - transform.position).normalized;
        lookDirection.y = 0; // Lock rotation to Y axis so the enemy doesn't tilt up/down

        if (lookDirection != Vector3.zero)
        {
            transform.rotation = Quaternion.LookRotation(lookDirection);
        }

        // Trigger the attack animation state
        animator.SetTrigger("Attack");
    }

    void OnDrawGizmosSelected()
    {
        // Visual debug spheres in Scene view:
        // Yellow = Chase Detection Range | Red = Attack Range
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(transform.position, chaseRange);

        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(transform.position, attackRange);
    }
}