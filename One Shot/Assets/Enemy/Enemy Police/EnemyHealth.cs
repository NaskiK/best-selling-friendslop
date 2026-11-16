
using Unity.Netcode;
using UnityEngine;
using UnityEngine.AI;

[RequireComponent(typeof(NetworkObject))]
public class EnemyHealth : NetworkBehaviour
{
    [Header("Health Settings")]
    [SerializeField] private int maxHealth = 50;
    [SerializeField] private EnemyData enemyData;

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

    private EnemyAI enemyAI;
    private NavMeshAgent agent;

    public int CurrentHealth => currentHealth.Value;
    public bool IsDead => isDead.Value;

    private void Awake()
    {
        enemyAI = GetComponent<EnemyAI>();
        agent = GetComponent<NavMeshAgent>();
    }

    public override void OnNetworkSpawn()
    {
        currentHealth.OnValueChanged += OnHealthChanged;
        isDead.OnValueChanged += OnDeadChanged;

        if (IsServer)
        {
            currentHealth.Value = maxHealth;
            isDead.Value = false;
        }

        if (IsDead)
            HandleDeath();
    }

    public override void OnNetworkDespawn()
    {
        currentHealth.OnValueChanged -= OnHealthChanged;
        isDead.OnValueChanged -= OnDeadChanged;
    }

    public void TakeDamage(int damageAmount, GameObject killer = null)
    {
        if (!IsServer || IsDead || damageAmount <= 0)
            return;

        currentHealth.Value = Mathf.Max(
            0,
            currentHealth.Value - damageAmount
        );

        Debug.Log(
            $"[{gameObject.name}] Took {damageAmount} damage. " +
            $"Health: {currentHealth.Value}/{maxHealth}"
        );

        if (currentHealth.Value <= 0)
            Die(killer);
    }

    private void Die(GameObject killer = null)
    {
        if (!IsServer || IsDead)
            return;

        isDead.Value = true;

        if (enemyData != null)
        {
            if (killer != null)
            {
                PlayerProgression progression = killer.GetComponentInParent<PlayerProgression>();
                if (progression != null)
                    progression.AddXP(enemyData.xpReward);

                PlayerQuestLog questLog = killer.GetComponentInParent<PlayerQuestLog>();
                if (questLog != null)
                    questLog.TryProgressQuest(enemyData.enemyID, 1);
            }

            if (ServerGlobalQuestManager.Instance != null)
                ServerGlobalQuestManager.Instance.NotifyGlobalEnemyKilled(enemyData.enemyID);
        }

        Debug.Log($"[{gameObject.name}] Enemy died!");

        HandleDeath();

        // Immediately despawn the enemy for all players.
        if (NetworkObject.IsSpawned)
            NetworkObject.Despawn(true);
    }

    private void HandleDeath()
    {
        if (enemyAI != null)
            enemyAI.enabled = false;

        if (agent != null && agent.enabled)
            agent.enabled = false;

        foreach (Collider col in GetComponentsInChildren<Collider>())
            col.enabled = false;
    }

    private void OnHealthChanged(int previous, int current)
    {
        Debug.Log(
            $"[{gameObject.name}] Health: {current}/{maxHealth}"
        );
    }

    private void OnDeadChanged(bool previous, bool current)
    {
        if (current)
            HandleDeath();
    }
}