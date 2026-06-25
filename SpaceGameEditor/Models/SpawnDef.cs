namespace SpaceGameEditor.Models;

public class QuestCondition
{
    public string QuestId { get; set; } = "";
    public string Status { get; set; } = "active"; // active, completed, inactive
}

public class ShipSpawnEntry
{
    public string Type { get; set; } = "scout";
    public int Count { get; set; } = 1;
    public string Faction { get; set; } = "";
    public string AiState { get; set; } = "Orbit";
}

public class SpawnDef
{
    public string SystemId { get; set; } = "";
    public List<ShipSpawnEntry> Ships { get; set; } = new();
    public QuestCondition? QuestCondition { get; set; }
}

public class SpawnsData
{
    public List<SpawnDef> Spawns { get; set; } = new();
}
