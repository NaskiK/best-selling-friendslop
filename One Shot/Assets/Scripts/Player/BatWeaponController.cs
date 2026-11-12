
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
    [SerializeField] private int damage = 25;

    [Header("Hit Timing")]
    [SerializeField] private float impactDelay = 0.2f;
    [SerializeField] private float hitWindowDuration = 0.25f;

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
    private bool canDealDamage;

    private Coroutine damageWindowCoroutine;

    private readonly HashSet<EnemyHealth> hitEnemies = new();

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

        if (damageWindowCoroutine != null)
            StopCoroutine(damageWindowCoroutine);

        canDealDamage = false;
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

        StartServerSwing();

        PlayBatSwingClientRpc();
    }

    private void StartServerSwing()
    {
        if (damageWindowCoroutine != null)
            StopCoroutine(damageWindowCoroutine);

        canDealDamage = false;
        hitEnemies.Clear();

        damageWindowCoroutine = StartCoroutine(EnableDamageWindow());
    }

    private IEnumerator EnableDamageWindow()
    {
        // Wait until the bat reaches the impact part of the swing.
        yield return new WaitForSeconds(impactDelay);

        canDealDamage = true;

        // Only allow damage during this short window.
        yield return new WaitForSeconds(hitWindowDuration);

        canDealDamage = false;
        damageWindowCoroutine = null;
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

    // Called by the bat's collider through BatHitbox.cs.
    [ServerRpc]
    public void RequestEnemyDamageServerRpc(
        NetworkObjectReference enemyReference)
    {
        if (!isEquipped.Value || !canDealDamage)
            return;

        if (!enemyReference.TryGet(out NetworkObject enemyObject))
            return;

        EnemyHealth enemy = enemyObject.GetComponent<EnemyHealth>();

        if (enemy == null)
            return;

        TryDamageEnemy(enemy);
    }

    private void TryDamageEnemy(EnemyHealth enemy)
    {
        if (!IsServer || !isEquipped.Value || !canDealDamage)
            return;

        if (enemy == null || enemy.IsDead)
            return;

        // One enemy can only be damaged once per swing.
        if (!hitEnemies.Add(enemy))
            return;

        enemy.TakeDamage(damage);
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
}
