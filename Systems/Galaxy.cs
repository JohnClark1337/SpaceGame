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
    public NewsService NewsService { get; private set; } = null!;

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
                Description = "Refuels the ship by 20%",
                Cost = 50,
                EffectType = "fuel_refill",
                EffectValue = 0.2f
            },
            new ConsumableDef
            {
                Id = "fuel_cell",
                Name = "Fuel Cell",
                Description = "Adds 20 fuel instantly",
                Cost = 0,
                EffectType = "fuel_add",
                EffectValue = 20f
            }
        };

        AvailableQuests = new List<QuestData>(AllQuests);
        Economy = new Economy(this);
        Economy.Initialize();
        NewsService = new NewsService();
        NewsService.SetGalaxy(this);
        NewsService.GenerateDefaultArticles();
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
        if (quest.ObjectiveType == "deliver" && quest.TargetSystem == player.CurrentSystemId)
        {
            foreach (var kv in quest.RequiredResources)
            {
                var inv = player.Resources.FirstOrDefault(r => r.Id == kv.Key);
                if (inv == null || inv.Quantity < kv.Value)
                    return false;
            }
            return true;
        }
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

        // Consume resources for deliver quests
        if (quest.ObjectiveType == "deliver" && quest.RequiredResources.Count > 0)
        {
            foreach (var kv in quest.RequiredResources)
            {
                var inv = player.Resources.FirstOrDefault(r => r.Id == kv.Key);
                if (inv != null)
                    inv.Quantity -= kv.Value;
            }
        }

        // Apply defense upgrade reward
        if (quest.RewardDefenseSystem != null)
        {
            var sys = Systems.FirstOrDefault(s => s.Id == quest.RewardDefenseSystem);
            if (sys?.Station != null && sys.Station.DefenseLevel < 5)
                sys.Station.DefenseLevel++;
        }

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
