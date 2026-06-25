namespace SpaceGameEditor.Models;

public class QuestData
{
    public string Id { get; set; } = "";
    public string Name { get; set; } = "";
    public string Description { get; set; } = "";
    public string ObjectiveType { get; set; } = "travel";
    public string? TargetSystem { get; set; }
    public string? TargetItem { get; set; }
    public int TargetCount { get; set; } = 1;
    public int RewardCredits { get; set; }
    public string? RewardUpgrade { get; set; }
    public string? RewardEquipment { get; set; }
    public string GiverSystem { get; set; } = "";
    public string? RewardDefenseSystem { get; set; }
    public Dictionary<string, int> RequiredResources { get; set; } = new();
}

public class QuestsData
{
    public List<QuestData> Quests { get; set; } = new();
}
