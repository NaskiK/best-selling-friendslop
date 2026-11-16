using System;

[Serializable]
public class ActiveQuest
{
    public QuestData questData;
    public int currentAmount;
    public bool isCompleted;

    public ActiveQuest(QuestData data)
    {
        questData = data;
        currentAmount = 0;
        isCompleted = false;
    }

    public void AddProgress(int amount)
    {
        if (isCompleted) return;

        currentAmount += amount;
        if (currentAmount >= questData.requiredAmount)
        {
            currentAmount = questData.requiredAmount;
            isCompleted = true;
        }
    }
}