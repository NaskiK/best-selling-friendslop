
using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;

[RequireComponent(typeof(Collider))]
public class BatHitbox : MonoBehaviour
{
    [Header("Hit Detection")]
    [SerializeField] private float requestInterval = 0.08f;

    private BatWeaponController weapon;
    private Collider hitbox;

    // Prevents sending requests every physics frame.
    // Each enemy has its own request timer.
    private readonly Dictionary<ulong, float> nextRequestTimes = new();

    private void Awake()
    {
        hitbox = GetComponent<Collider>();

        weapon = GetComponentInParent<BatWeaponController>();

        hitbox.isTrigger = true;
    }

    private void OnTriggerStay(Collider other)
    {
        if (weapon == null)
            return;

        if (!weapon.IsSpawned || !weapon.IsOwner)
            return;

        if (!weapon.IsEquipped)
            return;

        EnemyHealth enemy =
            other.GetComponentInParent<EnemyHealth>();

        if (enemy == null || enemy.IsDead)
            return;

        if (!enemy.NetworkObject.IsSpawned)
            return;

        ulong enemyId = enemy.NetworkObject.NetworkObjectId;

        // Check whether this enemy can receive another request.
        if (nextRequestTimes.TryGetValue(enemyId, out float nextTime))
        {
            if (Time.time < nextTime)
                return;
        }

        nextRequestTimes[enemyId] =
            Time.time + requestInterval;

        // The server checks whether the attack window is active.
        weapon.RequestEnemyDamageServerRpc(
            new NetworkObjectReference(enemy.NetworkObject)
        );
    }

    private void OnTriggerExit(Collider other)
    {
        EnemyHealth enemy =
            other.GetComponentInParent<EnemyHealth>();

        if (enemy != null && enemy.NetworkObject != null)
        {
            nextRequestTimes.Remove(
                enemy.NetworkObject.NetworkObjectId
            );
        }
    }
}
