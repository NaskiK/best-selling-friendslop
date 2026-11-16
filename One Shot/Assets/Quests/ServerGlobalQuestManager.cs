using Unity.Netcode;
using UnityEngine;

public class ServerGlobalQuestManager : NetworkBehaviour
{
    public static ServerGlobalQuestManager Instance;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
    }

    /// <summary>
    /// Called exclusively on the Server when an enemy dies. 
    /// Advances shared quests for all connected players and triggers completion rewards.
    /// </summary>
    public void NotifyGlobalEnemyKilled(string enemyID)
    {
        if (!IsServer) return;

        // Gather every active player's quest log in the current session
        PlayerQuestLog[] allPlayerLogs = FindObjectsByType<PlayerQuestLog>(FindObjectsSortMode.None);

        foreach (PlayerQuestLog playerLog in allPlayerLogs)
        {
            if (playerLog == null) continue;

            // Iterate backwards (or safely) in case CompleteQuest appends a chained quest to activeQuests
            for (int i = playerLog.activeQuests.Count - 1; i >= 0; i--)
            {
                ActiveQuest quest = playerLog.activeQuests[i];

                if (quest.isCompleted) continue;

                // Evaluate only shared kill quests matching the defeated enemy ID
                if (quest.questData.isSharedQuest &&
                    quest.questData.type == QuestType.Kill &&
                    quest.questData.targetID == enemyID)
                {
                    quest.AddProgress(1);

                    // 1. Sync updated progress count down to this specific client's UI
                    playerLog.SyncQuestProgressClientRpc(quest.questData.questID, quest.currentAmount);

                    Debug.Log($"[Shared Quest] {playerLog.name} updated '{quest.questData.questTitle}': {quest.currentAmount}/{quest.questData.requiredAmount}");

                    // 2. If the shared target count is reached, trigger completion & grant XP to this player
                    if (quest.isCompleted)
                    {
                        playerLog.CompleteQuest(quest);
                    }
                }
            }
        }
    }
}