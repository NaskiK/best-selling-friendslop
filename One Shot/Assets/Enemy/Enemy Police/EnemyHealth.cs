using UnityEngine;
using UnityEngine.AI;

public class EnemyHealth : MonoBehaviour
{
    [Header("Health Settings")]
    [SerializeField] private int maxHealth = 50;

    public int CurrentHealth { get; private set; }
    public bool IsDead { get; private set; }

    void Awake()
    {
        CurrentHealth = maxHealth;
    }

    /// <summary>
    /// Call this from weapon or attack scripts to deal damage to this enemy.
    /// </summary>
    public void TakeDamage(int damageAmount)
    {
        if (IsDead) return;

        CurrentHealth -= damageAmount;
        CurrentHealth = Mathf.Clamp(CurrentHealth, 0, maxHealth);

        Debug.Log($"[{gameObject.name}] Took {damageAmount} damage. Health: {CurrentHealth}/{maxHealth}");

        if (CurrentHealth <= 0)
        {
            Die();
        }
    }

    private void Die()
    {
        if (IsDead) return;
        IsDead = true;

        Debug.Log($"[{gameObject.name}] Enemy died!");

        // Disable AI script and NavMeshAgent so it stops moving/chasing
        EnemyAI ai = GetComponent<EnemyAI>();
        if (ai != null) ai.enabled = false;

        NavMeshAgent agent = GetComponent<NavMeshAgent>();
        if (agent != null) agent.enabled = false;

        // Disable main collider so the player doesn't bump into an invisible wall
        Collider col = GetComponent<Collider>();
        if (col != null) col.enabled = false;

        // Trigger 'Die' parameter in Animator if you have a death animation set up
        Animator animator = GetComponentInChildren<Animator>();
        if (animator != null)
        {
            animator.SetTrigger("Die");
        }

        // Destroy the enemy GameObject after 3 seconds
        Destroy(gameObject, 3.0f);
    }
}