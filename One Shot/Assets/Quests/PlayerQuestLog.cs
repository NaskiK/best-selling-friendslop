using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;

public class PlayerQuestLog : NetworkBehaviour
{
    [Header("Starter Quest (Optional)")]
    [SerializeField] private QuestData starterQuest;

    [Header("Active Quests")]
    public List<ActiveQuest> activeQuests = new List<ActiveQuest>();

    public override void OnNetworkSpawn()
    {
        // Auto-accept the starter quest for the owning player on spawn
        if (IsOwner && starterQuest != null)
        {
            AcceptQuest(starterQuest);
        }
    }

    /// <summary>
    /// Adds a new quest to the player's log and syncs it with the server.
    /// </summary>
    public void AcceptQuest(QuestData questData)
    {
        if (questData == null) return;

        // Prevent duplicate quests
        foreach (var q in activeQuests)
        {
            if (q.questData.questID == questData.questID) return;
        }

        ActiveQuest newQuest = new ActiveQuest(questData);
        activeQuests.Add(newQuest);
        Debug.Log($"[Quest Accepted] {questData.questTitle}");

        // If this is called on a client, tell the Server to also record this active quest
        // so the Server knows to track kills for this player.
        if (IsOwner && !IsServer)
        {
            AcceptQuestServerRpc(questData.questID);
        }
    }

    [ServerRpc]
    private void AcceptQuestServerRpc(string questID)
    {
        // Look up the QuestData asset from a Resources folder or Quest Database on the server
        QuestData data = Resources.Load<QuestData>($"Quests/{questID}");
        if (data != null)
        {
            AcceptQuest(data);
        }
    }

    /// <summary>
    /// Called by the Server when THIS specific player kills an enemy (Solo Quests).
    /// </summary>
    public void TryProgressQuest(string enemyID, int amount = 1)
    {
        if (!IsServer) return;

        foreach (var quest in activeQuests)
        {
            if (quest.isCompleted) continue;

            // Solo Quest Logic: Only progresses if NOT marked as a shared quest
            if (!quest.questData.isSharedQuest &&
                quest.questData.type == QuestType.Kill &&
                quest.questData.targetID == enemyID)
            {
                quest.AddProgress(amount);

                // Send the updated progress down to the owning client's UI
                SyncQuestProgressClientRpc(quest.questData.questID, quest.currentAmount);

                if (quest.isCompleted)
                {
                    CompleteQuest(quest);
                }
            }
        }
    }

    /// <summary>
    /// Receives progress updates from the Server (both Solo and Shared quests) so the local UI updates.
    /// </summary>
    [ClientRpc]
    public void SyncQuestProgressClientRpc(string questID, int newProgress)
    {
        // The host/server already updated its local list, so only run this on pure clients
        if (IsServer) return;

        foreach (var quest in activeQuests)
        {
            if (quest.questData.questID == questID)
            {
                quest.currentAmount = newProgress;
                if (quest.currentAmount >= quest.questData.requiredAmount)
                {
                    quest.isCompleted = true;
                }
                Debug.Log($"[Client UI Sync] {quest.questData.questTitle}: {quest.currentAmount}/{quest.questData.requiredAmount}");
                break;
            }
        }
    }

    /// <summary>
    /// Handles XP rewards and chaining to the next quest upon completion (Server-side).
    /// </summary>
    public void CompleteQuest(ActiveQuest quest)
    {
        if (!IsServer) return;

        Debug.Log($"[Server] Quest Completed: {quest.questData.questTitle}");

        // 1. Grant XP reward via PlayerProgression
        PlayerProgression progression = GetComponent<PlayerProgression>();
        if (progression != null)
        {
            progression.AddXP(quest.questData.rewardXP);
        }

        // 2. Automatically start the next quest in the chain if one is assigned
        if (quest.questData.nextQuestInChain != null)
        {
            AcceptQuest(quest.questData.nextQuestInChain);
            StartNextQuestClientRpc(quest.questData.nextQuestInChain.questID);
        }
    }

    [ClientRpc]
    private void StartNextQuestClientRpc(string nextQuestID)
    {
        if (IsServer || !IsOwner) return;

        QuestData nextData = Resources.Load<QuestData>($"Quests/{nextQuestID}");
        if (nextData != null)
        {
            AcceptQuest(nextData);
        }
    }
}