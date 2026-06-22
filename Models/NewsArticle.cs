namespace SpaceGame.Models;

public class NewsArticle
{
    public string Headline { get; set; } = "";
    public string Body { get; set; } = "";
    public string Source { get; set; } = "";
    public string? Faction { get; set; }
    public float Timestamp { get; set; }
    public bool IsBreaking { get; set; }
}
