namespace SpaceGame.Models;

public class PlanetData
{
    public string Name { get; set; } = "";
    public float OrbitRadius { get; set; } = 200f;
    public float Radius { get; set; } = 20f;
    public string Color { get; set; } = "888888";
    public float OrbitSpeed { get; set; } = 0.3f;
}

public class StationData
{
    public string Name { get; set; } = "";
    public float OrbitRadius { get; set; } = 150f;
    public float OrbitSpeed { get; set; } = 0.1f;
    public float Angle { get; set; }
    public float Radius { get; set; } = 40f;
    public int DefenseLevel { get; set; }
}

public class EconomyData
{
    public Dictionary<string, float> Production { get; set; } = new();
    public Dictionary<string, float> Demand { get; set; } = new();
}

public class StarSystemData
{
    public string Id { get; set; } = "";
    public string Name { get; set; } = "";
    public float X { get; set; }
    public float Y { get; set; }
    public float Radius { get; set; } = 35f;
    public string Color { get; set; } = "FFFFFF";
    public string Description { get; set; } = "";
    public List<string> Connections { get; set; } = new();
    public List<string> Services { get; set; } = new();
    public int Hostility { get; set; }
    public string? Faction { get; set; }
    public float StarRadius { get; set; } = 80f;
    public List<PlanetData> Planets { get; set; } = new();
    public StationData? Station { get; set; }
    public EconomyData? Economy { get; set; }
}

public class SystemsData
{
    public List<StarSystemData> Systems { get; set; } = new();
}
