using SpaceGame.Systems;

namespace SpaceGame.Models;

public class SaveGameData
{
    public Vector2 PlayerPosition { get; set; }
    public Vector2 PlayerVelocity { get; set; }
    public float PlayerAngle { get; set; }
    public int Credits { get; set; }
    public int Health { get; set; }
    public int MaxHealth { get; set; }
    public float Fuel { get; set; }
    public float MaxFuel { get; set; }
    public int CargoCapacity { get; set; }
    public List<string> OwnedUpgrades { get; set; } = new();
    public List<string> CompletedQuests { get; set; } = new();
    public string? CurrentSystemId { get; set; }
    public List<InventoryEntry> Resources { get; set; } = new();
    public List<InventoryEntry> QuestItems { get; set; } = new();
    public List<InventoryEntry> Consumables { get; set; } = new();
    public List<InventoryEntry> UnequippedEquipment { get; set; } = new();
    public Dictionary<string, string> Equipment { get; set; } = new();
    public float BaseMaxSpeed { get; set; } = 300f;
    public float BaseThrust { get; set; } = 500f;
    public float BaseRotationSpeed { get; set; } = 5f;

    public List<SystemSaveData> Systems { get; set; } = new();

    public List<QuestData> ActiveQuests { get; set; } = new();
    public List<QuestData> AvailableQuests { get; set; } = new();
    public List<QuestData> AllQuests { get; set; } = new();

    public string? CurrentSystemIdRef { get; set; }
    public string? TargetSystemId { get; set; }

    public List<string> BlockedRoutes { get; set; } = new();
    public int AiDifficulty { get; set; }

    public Dictionary<string, SystemMarketState> Markets { get; set; } = new();

    public List<NewsArticle> NewsArticles { get; set; } = new();

    public Dictionary<string, List<AttackStateSave>> ActiveAttacks { get; set; } = new();

    public Vector2 GalaxyPlayerPos { get; set; }
    public Vector2 GalaxyPlayerVel { get; set; }

    public float AiTickTimer { get; set; }
    public float AiCaptureTimer { get; set; }
    public float FederationAiTimer { get; set; }
    public float AiDefenseTimer { get; set; }
    public int InitialIndependentCount { get; set; }
    public bool IsInSystemView { get; set; }
}

public class SystemSaveData
{
    public string Id { get; set; } = "";
    public int Hostility { get; set; }
    public string? Faction { get; set; }
    public int DefenseLevel { get; set; }
    public float StationAngle { get; set; }
}

public class AttackStateSave
{
    public float Timer { get; set; }
    public string Attacker { get; set; } = "";
}
