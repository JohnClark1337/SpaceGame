using System.Text.Json;
using SpaceGame.Models;

namespace SpaceGame.Systems;

public class Galaxy
{
    public List<StarSystemData> Systems { get; private set; } = new();
    public List<QuestData> AllQuests { get; private set; } = new();
    public List<UpgradeData> AllUpgrades { get; private set; } = new();
    public List<ResourceDef> AllResources { get; private set; } = new();
    public List<EquipmentDef> AllEquipment { get; private set; } = new();
    public List<ConsumableDef> AllConsumables { get; private set; } = new();
    public List<QuestData> ActiveQuests { get; private set; } = new();
    public List<QuestData> AvailableQuests { get; private set; } = new();
    public Economy Economy { get; private set; } = null!;

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

        string resourcesJson = LoadJson("resources.json");
        if (!string.IsNullOrEmpty(resourcesJson))
            AllResources = JsonSerializer.Deserialize<ResourcesData>(resourcesJson, JsonOpts)?.Resources ?? new();

        string equipmentJson = LoadJson("equipment.json");
        if (!string.IsNullOrEmpty(equipmentJson))
            AllEquipment = JsonSerializer.Deserialize<EquipmentData>(equipmentJson, JsonOpts)?.Equipment ?? new();

        AllConsumables = new List<ConsumableDef>
        {
            new ConsumableDef
            {
                Id = "energy_canister",
                Name = "Energy Canister",
                Description = "Refuels the ship by 50%",
                Cost = 50,
                EffectType = "fuel_refill",
                EffectValue = 0.2f
            }
        };

        AvailableQuests = new List<QuestData>(AllQuests);
        Economy = new Economy(this);
        Economy.Initialize();
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

    public bool IsQuestObjectiveMet(QuestData quest, Player player)
    {
        if (quest.ObjectiveType == "travel" && quest.TargetSystem == player.CurrentSystemId)
            return true;
        if (quest.ObjectiveType == "collect" && quest.TargetItem != null &&
            quest.TargetSystem == player.CurrentSystemId &&
            player.QuestItems.Any(qi => qi.Id == quest.TargetItem))
            return true;
        return false;
    }

    public void CompleteQuest(QuestData quest, Player player)
    {
        player.Credits += quest.RewardCredits;
        if (quest.RewardUpgrade != null && !player.OwnedUpgrades.Contains(quest.RewardUpgrade))
            player.OwnedUpgrades.Add(quest.RewardUpgrade);
        if (quest.RewardEquipment != null)
        {
            var existing = player.UnequippedEquipment.FirstOrDefault(e => e.Id == quest.RewardEquipment);
            if (existing != null)
                existing.Quantity++;
            else
                player.UnequippedEquipment.Add(new InventoryEntry { Id = quest.RewardEquipment, Quantity = 1 });
        }
        if (quest.TargetItem != null)
            player.QuestItems.RemoveAll(qi => qi.Id == quest.TargetItem);
        player.CompletedQuests.Add(quest.Id);
        ActiveQuests.Remove(quest);
    }

    public void CheckQuestProgress(Player player)
    {
        var toComplete = new List<QuestData>();
        foreach (var quest in ActiveQuests)
        {
            if (quest.ObjectiveType == "travel") continue;
            if (IsQuestObjectiveMet(quest, player))
                toComplete.Add(quest);
        }
        foreach (var quest in toComplete)
            CompleteQuest(quest, player);
    }

    public List<UpgradeData> GetAvailableUpgradesForSystem(string systemId, Player player) =>
        AllUpgrades
            .Where(u => u.Location == systemId)
            .Where(u => !player.OwnedUpgrades.Contains(u.Id))
            .ToList();

    public ResourceDef? FindResource(string id) =>
        AllResources.FirstOrDefault(r => r.Id == id);

    public EquipmentDef? FindEquipment(string id) =>
        AllEquipment.FirstOrDefault(e => e.Id == id);

    public ConsumableDef? FindConsumable(string id) =>
        AllConsumables.FirstOrDefault(c => c.Id == id);

    public List<EquipmentDef> GetAvailableEquipmentForSystem(string systemId, Player player) =>
        AllEquipment
            .Where(e => e.Location == systemId)
            .Where(e => !player.Equipment.ContainsValue(e.Id))
            .Where(e => !player.UnequippedEquipment.Any(u => u.Id == e.Id))
            .ToList();
}
