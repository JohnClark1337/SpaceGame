namespace SpaceGame.Models;

public class ResourceDef
{
    public string Id { get; set; } = "";
    public string Name { get; set; } = "";
    public string Symbol { get; set; } = "";
    public string Category { get; set; } = "common";
    public string Description { get; set; } = "";
    public int BasePrice { get; set; }
    public int Volume { get; set; } = 1;
}

public class ResourcesData
{
    public List<ResourceDef> Resources { get; set; } = new();
}

public class EquipmentDef
{
    public string Id { get; set; } = "";
    public string Name { get; set; } = "";
    public string Description { get; set; } = "";
    public string Slot { get; set; } = ""; // weapon, shield, engine, utility, armor
    public string EffectType { get; set; } = "";
    public float EffectValue { get; set; }
    public int Cost { get; set; }
    public string? Location { get; set; }
    public int MinQuests { get; set; } = 0;
}

public class EquipmentData
{
    public List<EquipmentDef> Equipment { get; set; } = new();
}

public class ConsumableDef
{
    public string Id { get; set; } = "";
    public string Name { get; set; } = "";
    public string Description { get; set; } = "";
    public int Cost { get; set; }
    public string EffectType { get; set; } = "";
    public float EffectValue { get; set; }
    public int MinQuests { get; set; } = 0;
}

public class InventoryEntry
{
    public string Id { get; set; } = "";
    public int Quantity { get; set; } = 1;
}
