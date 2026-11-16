using UnityEngine;

[CreateAssetMenu(fileName = "NewEnemyData", menuName = "RPG Data/Enemy Data")]
public class EnemyData : ScriptableObject
{
    [Header("Identity")]
    public string enemyID = "skeleton_basic"; // Unique string ID used by quest system
    public string enemyName = "Skeleton Warrior";

    [Header("Stats")]
    public int maxHealth = 50;
    public int attackDamage = 15;
    public float chaseRange = 15f;
    public float attackRange = 2f;
    public float attackCooldown = 1.5f;

    [Header("Rewards")]
    public int xpReward = 25;
}