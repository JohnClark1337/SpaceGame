namespace SpaceGameEditor.Models;

public class ResourceDef
{
    public string Id { get; set; } = "";
    public string Name { get; set; } = "";
    public string Symbol { get; set; } = "";
    public string Category { get; set; } = "";
    public string Description { get; set; } = "";
    public int BasePrice { get; set; }
    public int Volume { get; set; } = 1;
}

public class ResourcesData
{
    public List<ResourceDef> Resources { get; set; } = new();
}
