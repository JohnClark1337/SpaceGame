namespace SpaceGame.Models;

public class GalacticBroadcast
{
    public string Faction { get; set; } = "";
    public string CommanderName { get; set; } = "";
    public string CommanderTitle { get; set; } = "";
    public string Message { get; set; } = "";
    public float Timestamp { get; set; }
}
