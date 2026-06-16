using System.Text.Json;
using SpaceGame.Models;

namespace SpaceGame.Systems;

public class Galaxy
{
    public List<StarSystemData> Systems { get; private set; } = new();
    public List<QuestData> AllQuests { get; private set; } = new();
    public List<UpgradeData> AllUpgrades { get; private set; } = new();
    public List<QuestData> ActiveQuests { get; private set; } = new();
    public List<QuestData> AvailableQuests { get; private set; } = new();

    public StarSystemData? CurrentSystem { get; set; }
    public StarSystemData? TargetSystem { get; set; }

    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        PropertyNameCaseInsensitive = true
    };

    public void LoadData()
    {
        string basePath = AppDomain.CurrentDomain.BaseDirectory;

        string LoadJson(string file)
        {
            string path = Path.Combine(basePath, "Data", file);
            if (!File.Exists(path))
                path = Path.Combine(basePath, file);
            if (!File.Exists(path))
                path = Path.Combine(Directory.GetCurrentDirectory(), "Data", file);
            if (!File.Exists(path))
                path = Path.Combine(Directory.GetCurrentDirectory(), file);
            return File.Exists(path) ? File.ReadAllText(path) : "";
        }

        string systemsJson = LoadJson("systems.json");
        if (!string.IsNullOrEmpty(systemsJson))
            Systems = JsonSerializer.Deserialize<SystemsData>(systemsJson, JsonOpts)?.Systems ?? new();

        string questsJson = LoadJson("quests.json");
        if (!string.IsNullOrEmpty(questsJson))
            AllQuests = JsonSerializer.Deserialize<QuestsData>(questsJson, JsonOpts)?.Quests ?? new();

        string upgradesJson = LoadJson("upgrades.json");
        if (!string.IsNullOrEmpty(upgradesJson))
            AllUpgrades = JsonSerializer.Deserialize<UpgradesData>(upgradesJson, JsonOpts)?.Upgrades ?? new();

        AvailableQuests = new List<QuestData>(AllQuests);
    }

    public StarSystemData? FindSystemById(string id) =>
        Systems.FirstOrDefault(s => s.Id == id);

    public StarSystemData? FindSystemAtPosition(Vector2 pos, float threshold = 60f)
    {
        foreach (var sys in Systems)
        {
            float dx = pos.X - sys.X;
            float dy = pos.Y - sys.Y;
            if (dx * dx + dy * dy < threshold * threshold)
                return sys;
        }
        return null;
    }

    public void RefreshAvailableQuests(Player player)
    {
        AvailableQuests = AllQuests
            .Where(q => !player.CompletedQuests.Contains(q.Id))
            .Where(q => !ActiveQuests.Any(aq => aq.Id == q.Id))
            .ToList();
    }

    public void AcceptQuest(string questId)
    {
        var quest = AvailableQuests.FirstOrDefault(q => q.Id == questId);
        if (quest != null)
        {
            ActiveQuests.Add(quest);
            AvailableQuests.Remove(quest);
        }
    }

    public void CheckQuestProgress(Player player)
    {
        var completed = new List<QuestData>();
        foreach (var quest in ActiveQuests)
        {
            if (quest.ObjectiveType == "travel" && quest.TargetSystem == player.CurrentSystemId)
            {
                completed.Add(quest);
            }
        }

        foreach (var quest in completed)
        {
            player.Credits += quest.RewardCredits;
            if (quest.RewardUpgrade != null && !player.OwnedUpgrades.Contains(quest.RewardUpgrade))
                player.OwnedUpgrades.Add(quest.RewardUpgrade);
            player.CompletedQuests.Add(quest.Id);
            ActiveQuests.Remove(quest);
        }
    }

    public List<UpgradeData> GetAvailableUpgradesForSystem(string systemId, Player player) =>
        AllUpgrades
            .Where(u => u.Location == systemId)
            .Where(u => !player.OwnedUpgrades.Contains(u.Id))
            .ToList();
}
