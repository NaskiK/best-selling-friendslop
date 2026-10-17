
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

        if (activeRagdoll == null)
            Debug.LogError(
                "[Slap] ActiveRagdoll missing from Player root.", this);

        if (animator == null)
            Debug.LogError("[Slap] Animator not found.", this);
        else
            Debug.Log("[Slap] Animator found: " + animator.name, this);
    }

    public override void OnNetworkSpawn()
    {
        Debug.Log(
            $"[Slap] Spawned: {name}, Owner={IsOwner}, " +
            $"ClientId={OwnerClientId}, IsServer={IsServer}, " +
            $"NetworkObjectId={NetworkObjectId}",
            this);
    }

    private void Update()
    {
        if (!IsSpawned || !IsOwner || Keyboard.current == null)
            return;

        if (!Keyboard.current[slapKey].wasPressedThisFrame)
            return;

        Debug.Log("[Slap] F pressed by local owner.", this);

        if (Time.time < nextSlapTime)
            return;

        nextSlapTime = Time.time + slapCooldown;

        PlaySlapAnimation();

        ActiveRagdoll target = FindTarget();

        if (target == null)
        {
            Debug.LogWarning("[Slap] No target found.", this);
            return;
        }

        NetworkObject targetObject =
            target.GetComponent<NetworkObject>();

        if (targetObject == null)
        {
            Debug.LogWarning(
                "[Slap] Target has no NetworkObject on its root.", target);
            return;
        }

        Debug.Log(
            $"[Slap] Target selected: {targetObject.name}, " +
            $"ID={targetObject.NetworkObjectId}, " +
            $"Owner={targetObject.OwnerClientId}",
            this);

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
            Debug.LogWarning(
                "[Slap] NetworkManager, ActiveRagdoll, or Hips missing.",
                this);
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

        Debug.Log(
            $"[Slap] Searching {spawnedObjects.Count} spawned objects. " +
            $"Origin={origin}, Forward={forward}",
            this);

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

            float facing = distance > 0.001f
                ? Vector3.Dot(forward, offset.normalized)
                : 1f;

            Debug.Log(
                $"[Slap] Candidate {candidate.name}: " +
                $"distance={distance:F2}, facing={facing:F2}",
                candidate);

            if (distance > slapRange || distance < 0.001f)
                continue;

            if (facing < 0.15f)
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
        Debug.Log(
            $"[Slap] ServerRpc received for target ID={targetNetworkObjectId}.",
            this);

        if (Time.time < serverNextSlapTime)
        {
            Debug.Log("[Slap] Server rejected slap: cooldown.", this);
            return;
        }

        NetworkManager manager = NetworkManager.Singleton;

        if (manager == null ||
            !manager.SpawnManager.SpawnedObjects.TryGetValue(
                targetNetworkObjectId, out NetworkObject targetObject))
        {
            Debug.LogWarning(
                "[Slap] Server could not find target NetworkObject.", this);
            return;
        }

        ActiveRagdoll target =
            targetObject.GetComponent<ActiveRagdoll>();

        if (target == null || target.Hips == null ||
            activeRagdoll == null || activeRagdoll.Hips == null)
        {
            Debug.LogWarning(
                "[Slap] Server could not find target or attacker Hips.",
                this);
            return;
        }

        Vector3 offset = target.Hips.position - activeRagdoll.Hips.position;
        offset.y = 0f;

        if (offset.magnitude > slapRange + 0.5f ||
            offset.sqrMagnitude < 0.001f)
        {
            Debug.LogWarning(
                $"[Slap] Server rejected distance: {offset.magnitude:F2}.",
                this);
            return;
        }

        Vector3 forward = transform.forward;
        forward.y = 0f;
        forward.Normalize();

        if (Vector3.Dot(forward, offset.normalized) < 0.15f)
        {
            Debug.LogWarning(
                "[Slap] Server rejected target: not in front.", this);
            return;
        }

        serverNextSlapTime = Time.time + slapCooldown;

        PlaySlapAnimationClientRpc();

        Vector3 left = -transform.right;
        left.y = 0f;
        left.Normalize();

        Vector3 direction =
            (forward * forwardForce + left * leftForce).normalized;

        float horizontalForce = Mathf.Sqrt(
            forwardForce * forwardForce +
            leftForce * leftForce);

        Debug.Log(
            $"[Slap] Server accepted slap. Target={targetObject.name}, " +
            $"TargetOwner={targetObject.OwnerClientId}, " +
            $"Direction={direction}, HorizontalForce={horizontalForce}, " +
            $"VerticalForce={upwardForce}",
            this);

        StartCoroutine(ApplySlapAfterDelay(
            target, direction, horizontalForce, upwardForce));
    }

    private IEnumerator ApplySlapAfterDelay(
        ActiveRagdoll target,
        Vector3 direction,
        float horizontalForce,
        float verticalForce)
    {
        yield return new WaitForSeconds(impactDelay);

        Debug.Log(
            $"[Slap] Impact delay finished. " +
            $"Target valid={target != null}, " +
            $"Target spawned={(target != null && target.IsSpawned)}, " +
            $"Hips valid={(target != null && target.Hips != null)}",
            this);

        if (target == null || !target.IsSpawned || target.Hips == null)
            yield break;

        target.ApplySlapKnockbackClientRpc(
            direction, horizontalForce, verticalForce);
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
            transform.position + Vector3.up, slapRange);
    }
}
