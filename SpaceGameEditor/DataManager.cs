using System.Text.Json;
using SpaceGameEditor.Models;

namespace SpaceGameEditor;

public class DataManager
{
    public string DataPath { get; }

    public List<StarSystemData> Systems { get; private set; } = new();
    public List<QuestData> Quests { get; private set; } = new();
    public List<EquipmentDef> Equipment { get; private set; } = new();
    public List<ResourceDef> Resources { get; private set; } = new();
    public List<UpgradeData> Upgrades { get; private set; } = new();
    public List<SpawnDef> Spawns { get; private set; } = new();

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNameCaseInsensitive = true
    };

    public DataManager(string dataPath)
    {
        DataPath = dataPath;
    }

    public bool LoadAll()
    {
        try
        {
            Systems = LoadFile<SystemsData>("systems.json")?.Systems ?? new();
            Quests = LoadFile<QuestsData>("quests.json")?.Quests ?? new();
            Equipment = LoadFile<EquipmentData>("equipment.json")?.Equipment ?? new();
            Resources = LoadFile<ResourcesData>("resources.json")?.Resources ?? new();
            Upgrades = LoadFile<UpgradesData>("upgrades.json")?.Upgrades ?? new();
            Spawns = LoadFile<SpawnsData>("spawns.json")?.Spawns ?? new();
            return true;
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Error loading data: {ex.Message}", "Load Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            return false;
        }
    }

    public bool SaveAll()
    {
        try
        {
            SaveFile("systems.json", new SystemsData { Systems = Systems });
            SaveFile("quests.json", new QuestsData { Quests = Quests });
            SaveFile("equipment.json", new EquipmentData { Equipment = Equipment });
            SaveFile("resources.json", new ResourcesData { Resources = Resources });
            SaveFile("upgrades.json", new UpgradesData { Upgrades = Upgrades });
            SaveFile("spawns.json", new SpawnsData { Spawns = Spawns });
            return true;
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Error saving data: {ex.Message}", "Save Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            return false;
        }
    }

    private T? LoadFile<T>(string filename) where T : class
    {
        string path = Path.Combine(DataPath, filename);
        if (!File.Exists(path)) return null;
        string json = File.ReadAllText(path);
        return JsonSerializer.Deserialize<T>(json, JsonOptions);
    }

    private void SaveFile<T>(string filename, T data)
    {
        string path = Path.Combine(DataPath, filename);
        string json = JsonSerializer.Serialize(data, JsonOptions);
        File.WriteAllText(path, json);
    }
}
