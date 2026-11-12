
using Unity.Netcode;
using UnityEngine;

[RequireComponent(typeof(Collider))]
public class BatHitbox : MonoBehaviour
{
    private BatWeaponController weapon;
    private Collider hitbox;

    private void Awake()
    {
        hitbox = GetComponent<Collider>();

        weapon = GetComponentInParent<BatWeaponController>();

        hitbox.isTrigger = true;
    }

    private void OnTriggerEnter(Collider other)
    {
        if (weapon == null)
            return;

        if (!weapon.IsSpawned || !weapon.IsOwner)
            return;

        EnemyHealth enemy =
            other.GetComponentInParent<EnemyHealth>();

        if (enemy == null)
            return;

        if (!enemy.NetworkObject.IsSpawned)
            return;

        weapon.RequestEnemyDamageServerRpc(
            new NetworkObjectReference(enemy.NetworkObject)
        );
    }
}
