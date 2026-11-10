
using Unity.Netcode;
using UnityEngine;
using UnityEngine.AI;

[RequireComponent(typeof(NetworkObject))]
public class EnemyAI : NetworkBehaviour
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

    private void Awake()
    {
        if (agent == null)
            agent = GetComponent<NavMeshAgent>();

        if (animator == null)
            animator = GetComponentInChildren<Animator>();
    }

    public override void OnNetworkSpawn()
    {
        if (IsServer && agent != null && agent.enabled && agent.isOnNavMesh)
        {
            agent.isStopped = true;
        }
    }

    private void Update()
    {
        // Only the server controls enemy AI.
        if (!IsSpawned || !IsServer)
            return;

        if (agent == null || !agent.enabled || !agent.isOnNavMesh)
            return;

        // 1. Periodically search for the nearest player.
        searchTimer += Time.deltaTime;

        if (searchTimer >= Mathf.Max(0.05f, targetSearchInterval))
        {
            FindClosestPlayer();
            searchTimer = 0f;
        }

        // 2. Update movement animation.
        float currentSpeed = agent.velocity.magnitude;

        if (animator != null)
        {
            animator.SetFloat("Speed", currentSpeed);
        }

        // 3. No player in range: remain idle.
        if (currentTarget == null)
        {
            agent.isStopped = true;
            return;
        }

        float distanceToTarget = Vector3.Distance(
            transform.position,
            currentTarget.position
        );

        // 4. Attack or chase.
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
            agent.isStopped = false;
            agent.SetDestination(currentTarget.position);
        }
    }

    private void FindClosestPlayer()
    {
        GameObject[] players =
            GameObject.FindGameObjectsWithTag("Player");

        if (players.Length == 0)
        {
            currentTarget = null;
            return;
        }

        float closestDistance = Mathf.Infinity;
        Transform bestTarget = null;

        foreach (GameObject playerObj in players)
        {
            Transform targetPoint = playerObj.transform;

            ActiveRagdoll ragdoll =
                playerObj.GetComponent<ActiveRagdoll>();

            if (ragdoll != null && ragdoll.Hips != null)
            {
                targetPoint = ragdoll.Hips.transform;
            }

            float distance = Vector3.Distance(
                transform.position,
                targetPoint.position
            );

            if (distance <= chaseRange &&
                distance < closestDistance)
            {
                closestDistance = distance;
                bestTarget = targetPoint;
            }
        }

        currentTarget = bestTarget;
    }

    private void TriggerAttack()
    {
        if (currentTarget == null)
            return;

        Vector3 lookDirection =
            currentTarget.position - transform.position;

        lookDirection.y = 0f;

        if (lookDirection.sqrMagnitude > 0.001f)
        {
            transform.rotation = Quaternion.LookRotation(
                lookDirection.normalized
            );
        }

        if (animator != null)
        {
            animator.SetTrigger("Attack");
        }
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(transform.position, chaseRange);

        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(transform.position, attackRange);
    }
}
