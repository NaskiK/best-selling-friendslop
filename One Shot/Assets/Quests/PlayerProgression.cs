using Unity.Netcode;
using UnityEngine;
using UnityEngine.Events;

public class PlayerProgression : NetworkBehaviour
{
    [Header("Level Settings")]
    [SerializeField] private int maxLevel = 50;

    public NetworkVariable<int> CurrentLevel = new NetworkVariable<int>(1, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);
    public NetworkVariable<int> CurrentXP = new NetworkVariable<int>(0, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);
    public NetworkVariable<int> RequiredXPToLevelUp = new NetworkVariable<int>(100, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

    public UnityEvent<int, int> OnXPChanged;
    public UnityEvent<int> OnLevelUp;

    public override void OnNetworkSpawn()
    {
        CurrentXP.OnValueChanged += OnXPValueChanged;
        CurrentLevel.OnValueChanged += OnLevelingUp;
    }

    public override void OnNetworkDespawn()
    {
        CurrentXP.OnValueChanged -= OnXPValueChanged;
        CurrentLevel.OnValueChanged -= OnLevelingUp;
    }

    public void AddXP(int amount)
    {
        if (!IsServer) return;

        CurrentXP.Value += amount;
        Debug.Log($"[Server] Player gained {amount} XP. Total: {CurrentXP.Value}/{RequiredXPToLevelUp.Value}");

        while (CurrentXP.Value >= RequiredXPToLevelUp.Value && CurrentLevel.Value < maxLevel)
        {
            CurrentXP.Value -= RequiredXPToLevelUp.Value;
            CurrentLevel.Value++;
            RequiredXPToLevelUp.Value = Mathf.RoundToInt(RequiredXPToLevelUp.Value * 1.5f);
        }
    }

    private void OnXPValueChanged(int oldXP, int newXP)
    {
        if (IsOwner)
            OnXPChanged?.Invoke(newXP, RequiredXPToLevelUp.Value);
    }

    private void OnLevelingUp(int oldLevel, int newLevel)
    {
        if (IsOwner)
        {
            Debug.Log($"[Client] LEVEL UP! You reached level {newLevel}!");
            OnLevelUp?.Invoke(newLevel);
        }
    }
}