using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json;
using SpaceGame.Models;

namespace SpaceGame.Services;

public class LlmDecision
{
    public List<List<string>>? BlockRoutes { get; set; }
    public string? AttackSystem { get; set; }
    public string? FederationAttackSystem { get; set; }
    public List<List<string>>? FederationUnblockRoutes { get; set; }
    public string? EmpireNewsHeadline { get; set; }
    public string? EmpireNewsBody { get; set; }
    public string? FederationNewsHeadline { get; set; }
    public string? FederationNewsBody { get; set; }
}

public class LlmService : IDisposable
{
    private readonly HttpClient _http;
    private bool _available;

    public bool IsAvailable => _available;

    public LlmService()
    {
        _http = new HttpClient
        {
            BaseAddress = new Uri("http://localhost:11434"),
            Timeout = TimeSpan.FromSeconds(20)
        };
    }

    public async Task<bool> DetectAsync()
    {
        try
        {
            var resp = await _http.GetAsync("/api/tags");
            _available = resp.IsSuccessStatusCode;
        }
        catch
        {
            _available = false;
        }
        return _available;
    }

    public async Task<LlmDecision?> RequestDecisionAsync(List<StarSystemData> systems, string? playerSystemId,
        IReadOnlySet<string> blockedRoutes, int maxBlocked, List<QuestData> activeQuests)
    {
        if (!_available) return null;

        string prompt = BuildPrompt(systems, playerSystemId, blockedRoutes, maxBlocked, activeQuests);

        var body = new
        {
            model = "llama3.2:1b",
            prompt,
            stream = false,
            format = "json"
        };

        try
        {
            var resp = await _http.PostAsJsonAsync("/api/generate", body);
            if (!resp.IsSuccessStatusCode) return null;

            using var doc = await JsonDocument.ParseAsync(await resp.Content.ReadAsStreamAsync());
            var text = doc.RootElement.GetProperty("response").GetString();
            if (string.IsNullOrEmpty(text)) return null;

            return JsonSerializer.Deserialize<LlmDecision>(text, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            });
        }
        catch
        {
            return null;
        }
    }

    private static string BuildPrompt(List<StarSystemData> systems, string? playerSystemId,
        IReadOnlySet<string> blockedRoutes, int maxBlocked, List<QuestData> activeQuests)
    {
        var lines = new List<string>
        {
            "You coordinate TWO faction commanders in a space game. Decide each faction's strategic moves.",
            "",
            "GALAXY STATE:"
        };

        foreach (var sys in systems)
        {
            lines.Add($"- {sys.Id} ({sys.Name}) | Faction: {sys.Faction ?? "none"} | Hostility: {sys.Hostility} | Connections: {string.Join(", ", sys.Connections)}");
        }

        lines.Add("");
        lines.Add($"Player location: {playerSystemId ?? "unknown"}");
        lines.Add($"Current blockades: {string.Join(", ", blockedRoutes)}");
        lines.Add($"Max blockades allowed: {maxBlocked}");

        if (activeQuests.Count > 0)
        {
            lines.Add("Active quests: " + string.Join(", ", activeQuests.Select(q => q.Id)));
        }

        lines.Add("");
        lines.Add("EMPIRE COMMANDER RULES:");
        lines.Add("- You can ONLY block routes between two EMPIRE systems (both have Hostility >= 3)");
        lines.Add("- You can ONLY attack systems that have a direct connection to an Empire system (Hostility >= 3)");
        lines.Add("");
        lines.Add("FEDERATION COMMANDER RULES:");
        lines.Add("- Capture Empire systems (Hostility >= 3) adjacent to Federation OR Independent systems");
        lines.Add("- Unblock Empire blockades on routes near Federation or Independent territory");
        lines.Add("- Federation can attack any Empire system adjacent to non-Empire systems");
        lines.Add("");
        lines.Add("GLOBAL RULES:");
        lines.Add("- Do not block more routes than max blockades allowed");
        lines.Add("- Keep paths open for active quests");
        lines.Add("");
        lines.Add("NEWS RULES:");
        lines.Add("- Optionally include a propaganda news headline/body for each faction's actions");
        lines.Add("- Empire news source: \"Imperial Herald\"");
        lines.Add("- Federation news source: \"Federation News Network\"");
        lines.Add("- Keep headlines short and dramatic. Keep bodies 1-2 sentences.");
        lines.Add("");
        lines.Add("Respond ONLY with JSON, no other text:");
        lines.Add("{ \"blockRoutes\": [[\"systemA\",\"systemB\"]], \"attackSystem\": \"systemId or null\", \"federationAttackSystem\": \"systemId or null\", \"federationUnblockRoutes\": [[\"systemA\",\"systemB\"]], \"empireNewsHeadline\": \"...\", \"empireNewsBody\": \"...\", \"federationNewsHeadline\": \"...\", \"federationNewsBody\": \"...\" }");

        return string.Join("\n", lines);
    }

    public void Dispose() => _http.Dispose();
}
