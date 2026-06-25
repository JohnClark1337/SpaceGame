namespace SpaceGameEditor.Models;

public class UpgradeData
{
    public string Id { get; set; } = "";
    public string Name { get; set; } = "";
    public string Description { get; set; } = "";
    public string EffectType { get; set; } = "";
    public float EffectValue { get; set; }
    public int Cost { get; set; }
    public string Location { get; set; } = "";
}

public class UpgradesData
{
    public List<UpgradeData> Upgrades { get; set; } = new();
}
