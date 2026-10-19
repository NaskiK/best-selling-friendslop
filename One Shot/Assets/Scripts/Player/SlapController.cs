
using System.Collections;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.InputSystem;

[RequireComponent(typeof(NetworkObject))]
public class SlapController : NetworkBehaviour
{
    [Header("Slap")]
    [SerializeField] private Key slapKey = Key.F;
    [SerializeField] private float slapRange = 2f;
    [SerializeField] private float impactDelay = 0.20f;
    [SerializeField] private float slapCooldown = 0.8f;

    [Header("Slap Targeting")]
    [SerializeField, Range(-1f, 1f)]
    private float minimumFacingDot = -0.2f;

    [Header("Knockback")]
    [SerializeField] private float forwardForce = 12f;
    [SerializeField] private float leftForce = 4f;
    [SerializeField] private float upwardForce = 4f;

    private static readonly int SlapHash =
        Animator.StringToHash("Slap");

    private ActiveRagdoll activeRagdoll;
    private NetworkObject playerNetworkObject;
    private Animator animator;

    private float nextSlapTime;
    private float serverNextSlapTime;

    private void Awake()
    {
        playerNetworkObject = GetComponent<NetworkObject>();
        activeRagdoll = GetComponent<ActiveRagdoll>();

        Transform animatedRig = transform.Find("AnimatedRig");

        if (animatedRig != null)
            animator = animatedRig.GetComponentInChildren<Animator>(true);

        if (animator == null)
            animator = GetComponentInChildren<Animator>(true);
    }

    private void Update()
{
    if (!IsSpawned || !IsOwner || Keyboard.current == null)
        return;

    if (!Keyboard.current[slapKey].wasPressedThisFrame)
        return;

    if (Time.time < nextSlapTime)
        return;

    // Always play the animation when F is pressed.
    PlaySlapAnimation();

    ActiveRagdoll target = FindTarget();

    // No target means no cooldown and no slap request.
    if (target == null)
        return;

    NetworkObject targetObject =
        target.GetComponent<NetworkObject>();

    if (targetObject == null)
        return;

    // Start cooldown only when a valid target is found.
    nextSlapTime = Time.time + slapCooldown;

    RequestSlapServerRpc(targetObject.NetworkObjectId);
}

    private void PlaySlapAnimation()
    {
        if (animator == null)
            return;

        animator.ResetTrigger(SlapHash);
        animator.SetTrigger(SlapHash);
    }

    private ActiveRagdoll FindTarget()
    {
        if (NetworkManager.Singleton == null ||
            activeRagdoll == null ||
            activeRagdoll.Hips == null)
        {
            return null;
        }

        Vector3 origin = activeRagdoll.Hips.position;

        Vector3 forward = transform.forward;
        forward.y = 0f;

        if (forward.sqrMagnitude < 0.001f)
            forward = activeRagdoll.Hips.transform.forward;

        forward.y = 0f;
        forward.Normalize();

        ActiveRagdoll closestTarget = null;
        float closestDistance = float.MaxValue;

        var spawnedObjects =
            NetworkManager.Singleton.SpawnManager.SpawnedObjects;

        foreach (NetworkObject candidate in spawnedObjects.Values)
        {
            if (candidate == null ||
                candidate == playerNetworkObject ||
                !candidate.IsSpawned)
            {
                continue;
            }

            ActiveRagdoll ragdoll =
                candidate.GetComponent<ActiveRagdoll>();

            if (ragdoll == null || ragdoll.Hips == null)
                continue;

            Vector3 offset = ragdoll.Hips.position - origin;
            offset.y = 0f;

            float distance = offset.magnitude;

            if (distance > slapRange || distance < 0.001f)
                continue;

            float facing = Vector3.Dot(
                forward,
                offset.normalized
            );

            if (facing < minimumFacingDot)
                continue;

            if (distance < closestDistance)
            {
                closestDistance = distance;
                closestTarget = ragdoll;
            }
        }

        return closestTarget;
    }

    [ServerRpc]
    private void RequestSlapServerRpc(ulong targetNetworkObjectId)
    {
        if (Time.time < serverNextSlapTime)
            return;

        NetworkManager manager = NetworkManager.Singleton;

        if (manager == null ||
            !manager.SpawnManager.SpawnedObjects.TryGetValue(
                targetNetworkObjectId, out NetworkObject targetObject))
        {
            return;
        }

        ActiveRagdoll target =
            targetObject.GetComponent<ActiveRagdoll>();

        if (target == null ||
            target.Hips == null ||
            activeRagdoll == null ||
            activeRagdoll.Hips == null)
        {
            return;
        }

        Vector3 offset =
            target.Hips.position - activeRagdoll.Hips.position;

        offset.y = 0f;

        if (offset.magnitude > slapRange + 0.5f ||
            offset.sqrMagnitude < 0.001f)
        {
            return;
        }

        Vector3 forward = transform.forward;
        forward.y = 0f;

        if (forward.sqrMagnitude < 0.001f)
            return;

        forward.Normalize();

        if (Vector3.Dot(forward, offset.normalized) < minimumFacingDot)
            return;

        serverNextSlapTime = Time.time + slapCooldown;

        PlaySlapAnimationClientRpc();

        Vector3 left = -transform.right;
        left.y = 0f;
        left.Normalize();

        Vector3 direction =
            (forward * forwardForce + left * leftForce).normalized;

        float horizontalForce = Mathf.Sqrt(
            forwardForce * forwardForce +
            leftForce * leftForce
        );

        StartCoroutine(ApplySlapAfterDelay(
            target,
            direction,
            horizontalForce,
            upwardForce
        ));
    }

    private IEnumerator ApplySlapAfterDelay(
        ActiveRagdoll target,
        Vector3 direction,
        float horizontalForce,
        float verticalForce)
    {
        yield return new WaitForSeconds(impactDelay);

        // Do not recheck distance or facing.
        // The accepted slap still lands if the victim moves away.
        if (target == null ||
            !target.IsSpawned ||
            target.Hips == null)
        {
            yield break;
        }

        target.ApplySlapKnockbackClientRpc(
            direction,
            horizontalForce,
            verticalForce
        );
    }

    [ClientRpc]
    private void PlaySlapAnimationClientRpc()
    {
        if (!IsOwner)
            PlaySlapAnimation();
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.red;

        Gizmos.DrawWireSphere(
            transform.position + Vector3.up,
            slapRange
        );
    }
}
