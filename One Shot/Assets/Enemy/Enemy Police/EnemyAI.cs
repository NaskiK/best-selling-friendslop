using UnityEngine;
using UnityEngine.AI;

public class EnemyAI : MonoBehaviour
{
    [Header("Detection & Attack Ranges")]
    public float chaseRange = 15f;
    public float attackRange = 2f;
    public float attackCooldown = 1.5f;

    [Header("Multiplayer Targeting")]
    [Tooltip("How often (in seconds) the enemy searches for the nearest player.")]
    public float targetSearchInterval = 0.2f;

    [Header("Component References")]
    public NavMeshAgent agent;
    public Animator animator;

    private Transform currentTarget;
    private float lastAttackTime;
    private float searchTimer;

    void Start()
    {
        if (agent == null) agent = GetComponent<NavMeshAgent>();
        if (animator == null) animator = GetComponentInChildren<Animator>();
    }

    void Update()
    {
        // 1. Periodically search for the nearest valid player inside chase range
        searchTimer += Time.deltaTime;
        if (searchTimer >= targetSearchInterval)
        {
            FindClosestPlayer();
            searchTimer = 0f;
        }

        // 2. Drive Walk/Idle animation speed
        float currentSpeed = agent.velocity.magnitude;
        if (animator != null)
        {
            animator.SetFloat("Speed", currentSpeed);
        }

        // 3. If no player is in range, remain idle
        if (currentTarget == null)
        {
            agent.isStopped = true;
            return;
        }

        float distanceToTarget = Vector3.Distance(transform.position, currentTarget.position);

        // 4. Perform combat movement and attack logic
        if (distanceToTarget <= attackRange)
        {
            agent.isStopped = true;

            if (Time.time >= lastAttackTime + attackCooldown)
            {
                TriggerAttack();
                lastAttackTime = Time.time;
            }
        }
        else
        {
            // If we have a currentTarget, it is guaranteed to be within chaseRange
            agent.isStopped = false;
            agent.SetDestination(currentTarget.position);
        }
    }

    void FindClosestPlayer()
    {
        GameObject[] players = GameObject.FindGameObjectsWithTag("Player");

        if (players.Length == 0)
        {
            currentTarget = null;
            return;
        }

        float closestDistance = Mathf.Infinity;
        Transform bestTarget = null;

        foreach (GameObject playerObj in players)
        {
            float distance = Vector3.Distance(transform.position, playerObj.transform.position);

            // ONLY consider players that are inside chaseRange
            if (distance <= chaseRange && distance < closestDistance)
            {
                closestDistance = distance;
                bestTarget = playerObj.transform;
            }
        }

        // Assign the best valid target (will be null if everyone is out of range)
        currentTarget = bestTarget;
    }

    void TriggerAttack()
    {
        if (currentTarget == null) return;

        Vector3 lookDirection = (currentTarget.position - transform.position).normalized;
        lookDirection.y = 0;
        if (lookDirection != Vector3.zero)
        {
            transform.rotation = Quaternion.LookRotation(lookDirection);
        }

        if (animator != null)
        {
            animator.SetTrigger("Attack");
        }
    }

    void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(transform.position, chaseRange);

        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(transform.position, attackRange);
    }
}