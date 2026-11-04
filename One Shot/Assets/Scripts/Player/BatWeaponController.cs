
using System.Collections;
using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.InputSystem;

[RequireComponent(typeof(NetworkObject))]
public class BatWeaponController : NetworkBehaviour
{
    [Header("Bat")]
    [SerializeField] private GameObject batVisual;
    [SerializeField] private Key equipKey = Key.Q;

    [Header("Attack")]
    [SerializeField] private float swingCooldown = 1f;
    [SerializeField] private float swingMovementLockDuration = 0.9f;
    [SerializeField] private float impactDelay = 0.2f;
    [SerializeField] private int damage = 25;

    [Header("Server Hit Detection")]
    [SerializeField] private Vector3 attackBoxCenter =
        new Vector3(0f, 1f, 1f);

    [SerializeField] private Vector3 attackBoxSize =
        new Vector3(1.6f, 1.6f, 2f);

    [SerializeField] private LayerMask hitLayers = ~0;

    private static readonly int BatSwingHash =
        Animator.StringToHash("BatSwing");

    private NetworkVariable<bool> isEquipped =
        new NetworkVariable<bool>(
            false,
            NetworkVariableReadPermission.Everyone,
            NetworkVariableWritePermission.Server
        );

    private Animator animator;
    private ActiveRagdoll activeRagdoll;

    private float nextLocalSwingTime;
    private float nextServerSwingTime;
    private bool equipRequestPending;

    public bool IsEquipped => isEquipped.Value;

    private void Awake()
    {
        activeRagdoll = GetComponent<ActiveRagdoll>();

        Transform animatedRig = transform.Find("AnimatedRig");

        if (animatedRig != null)
            animator = animatedRig.GetComponentInChildren<Animator>(true);

        if (animator == null)
            animator = GetComponentInChildren<Animator>(true);

        if (batVisual != null)
            batVisual.SetActive(false);
    }

    public override void OnNetworkSpawn()
    {
        isEquipped.OnValueChanged += OnEquippedChanged;
        UpdateBatVisibility(isEquipped.Value);
    }

    public override void OnNetworkDespawn()
    {
        isEquipped.OnValueChanged -= OnEquippedChanged;
    }

    private void Update()
    {
        if (!IsSpawned || !IsOwner)
            return;

        if (Keyboard.current != null &&
            Keyboard.current[equipKey].wasPressedThisFrame &&
            !equipRequestPending)
        {
            equipRequestPending = true;
            RequestEquipServerRpc(!IsEquipped);
        }

        if (Mouse.current != null &&
            Mouse.current.leftButton.wasPressedThisFrame &&
            IsEquipped &&
            Time.time >= nextLocalSwingTime)
        {
            nextLocalSwingTime = Time.time + swingCooldown;
            RequestBatSwingServerRpc();
        }
    }

    [ServerRpc]
    private void RequestEquipServerRpc(bool equip)
    {
        isEquipped.Value = equip;
    }

    [ServerRpc]
    private void RequestBatSwingServerRpc()
    {
        if (!isEquipped.Value)
            return;

        if (Time.time < nextServerSwingTime)
            return;

        nextServerSwingTime = Time.time + swingCooldown;

        PlayBatSwingClientRpc();

        StartCoroutine(ApplyBatDamage());
    }

    [ClientRpc]
    private void PlayBatSwingClientRpc()
    {
        if (animator != null)
        {
            animator.ResetTrigger(BatSwingHash);
            animator.SetTrigger(BatSwingHash);
        }

        if (activeRagdoll != null)
        {
            activeRagdoll.BeginAttackMovementLock(
                swingMovementLockDuration
            );
        }
    }

    private IEnumerator ApplyBatDamage()
    {
        yield return new WaitForSeconds(impactDelay);

        Vector3 center = transform.TransformPoint(attackBoxCenter);

        Vector3 halfExtents = attackBoxSize * 0.5f;

        Collider[] hits = Physics.OverlapBox(
            center,
            halfExtents,
            transform.rotation,
            hitLayers,
            QueryTriggerInteraction.Collide
        );

        HashSet<EnemyHealth> hitEnemies = new HashSet<EnemyHealth>();

        foreach (Collider hit in hits)
        {
            EnemyHealth enemy =
                hit.GetComponentInParent<EnemyHealth>();

            if (enemy == null || enemy.IsDead)
                continue;

            if (!hitEnemies.Add(enemy))
                continue;

            enemy.TakeDamage(damage);
        }
    }

    private void OnEquippedChanged(bool previous, bool current)
    {
        equipRequestPending = false;
        UpdateBatVisibility(current);
    }

    private void UpdateBatVisibility(bool visible)
    {
        if (batVisual != null)
            batVisual.SetActive(visible);
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.red;

        Matrix4x4 oldMatrix = Gizmos.matrix;

        Gizmos.matrix = transform.localToWorldMatrix;

        Gizmos.DrawWireCube(
            attackBoxCenter,
            attackBoxSize
        );

        Gizmos.matrix = oldMatrix;
    }
}
