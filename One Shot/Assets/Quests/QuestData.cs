using UnityEngine;

public enum QuestType { Kill, TalkToNPC, Collect }

[CreateAssetMenu(fileName = "NewQuest", menuName = "RPG Data/Quest Data")]
public class QuestData : ScriptableObject
{
    [Header("Quest Information")]
    public string questID;
    public string questTitle;
    [TextArea(3, 5)]
    public string description;

    [Header("Quest Scope")]
    public bool isSharedQuest = false;

    [Header("Objective")]
    public QuestType type = QuestType.Kill;
    public string targetID;
    public int requiredAmount = 5;

    [Header("Rewards")]
    public int rewardXP = 100;
    public QuestData nextQuestInChain;
}