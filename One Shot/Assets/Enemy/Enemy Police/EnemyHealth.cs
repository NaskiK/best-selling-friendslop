
using System.Collections;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.AI;

[RequireComponent(typeof(NetworkObject))]
public class EnemyHealth : NetworkBehaviour
{
    [Header("Health Settings")]
    [SerializeField] private int maxHealth = 50;

    private NetworkVariable<int> currentHealth =
        new NetworkVariable<int>(
            50,
            NetworkVariableReadPermission.Everyone,
            NetworkVariableWritePermission.Server
        );

    private NetworkVariable<bool> isDead =
        new NetworkVariable<bool>(
            false,
            NetworkVariableReadPermission.Everyone,
            NetworkVariableWritePermission.Server
        );

    public int CurrentHealth => currentHealth.Value;
    public bool IsDead => isDead.Value;

    private EnemyAI enemyAI;
    private NavMeshAgent agent;
    private Animator animator;

    private void Awake()
    {
        enemyAI = GetComponent<EnemyAI>();
        agent = GetComponent<NavMeshAgent>();
        animator = GetComponentInChildren<Animator>();
    }

    public override void OnNetworkSpawn()
    {
        if (IsServer)
            currentHealth.Value = maxHealth;
    }

    public void TakeDamage(int damageAmount)
    {
        if (!IsServer || IsDead)
            return;

        currentHealth.Value = Mathf.Max(
            0,
            currentHealth.Value - damageAmount
        );

        Debug.Log(
            $"[{name}] Took {damageAmount} damage. " +
            $"Health: {currentHealth.Value}/{maxHealth}"
        );

        if (currentHealth.Value <= 0)
            Die();
    }

    private void Die()
    {
        if (IsDead)
            return;

        isDead.Value = true;

        Debug.Log($"[{name}] Enemy died!");

        if (enemyAI != null)
            enemyAI.enabled = false;

        if (agent != null)
            agent.enabled = false;

        DieClientRpc();

        StartCoroutine(DespawnAfterDeath());
    }

    [ClientRpc]
    private void DieClientRpc()
    {
        if (animator != null)
            animator.SetTrigger("Die");

        foreach (Collider col in GetComponentsInChildren<Collider>())
            col.enabled = false;
    }

    private IEnumerator DespawnAfterDeath()
    {
        yield return new WaitForSeconds(3f);

        if (NetworkObject != null && NetworkObject.IsSpawned)
            NetworkObject.Despawn(true);
    }
}
