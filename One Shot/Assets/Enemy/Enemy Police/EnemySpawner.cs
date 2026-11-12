
using System.Collections;
using Unity.Netcode;
using UnityEngine;

public class EnemySpawner : MonoBehaviour
{
    [Header("Enemy Prefab")]
    [SerializeField] private NetworkObject enemyPrefab;

    [Header("Spawn Position")]
    [SerializeField] private Vector3 spawnPosition =
        new Vector3(21.35f, 0.32f, 1.94929f);

    private IEnumerator Start()
    {
        // Wait until the multiplayer session starts.
        yield return new WaitUntil(() =>
            NetworkManager.Singleton != null &&
            NetworkManager.Singleton.IsListening
        );

        // Only the server spawns the enemy.
        if (!NetworkManager.Singleton.IsServer)
            yield break;

        if (enemyPrefab == null)
        {
            Debug.LogError("EnemySpawner: Enemy Prefab is not assigned!");
            yield break;
        }

        NetworkObject enemy = Instantiate(
            enemyPrefab,
            spawnPosition,
            Quaternion.identity
        );

        enemy.Spawn();

        Debug.Log("Enemy spawned at: " + spawnPosition);
    }
}
