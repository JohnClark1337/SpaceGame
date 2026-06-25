namespace SpaceGameEditor.Models;

public class EquipmentDef
{
    public string Id { get; set; } = "";
    public string Name { get; set; } = "";
    public string Description { get; set; } = "";
    public string Slot { get; set; } = "";
    public string EffectType { get; set; } = "";
    public float EffectValue { get; set; }
    public int Cost { get; set; }
    public string Location { get; set; } = "";
    public int MinQuests { get; set; }
}

public class EquipmentData
{
    public List<EquipmentDef> Equipment { get; set; } = new();
}
