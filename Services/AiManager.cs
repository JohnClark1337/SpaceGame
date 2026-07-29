using SpaceGame.Models;
using SpaceGame.Systems;

namespace SpaceGame.Services;

public class AiManager
{
    private readonly Galaxy _galaxy;
    private readonly RouteManager _routeManager;
    private readonly Player _player;
    private readonly AttackManager _attackManager;
    private readonly LlmService? _llmService;
    private readonly NewsService _newsService;
    private readonly List<GalacticBroadcast> _pendingBroadcasts;
    private readonly Action<string> _setStatus;
    private readonly Action<string, float> _setStatusTimed;

    private bool _useLlm;
    private bool _llmChecked;

    public float AiTickTimer { get; set; } = 8f;
    public float AiCaptureTimer { get; set; } = 30f;
    public float FederationAiTimer { get; set; } = 30f;
    public float AiDefenseTimer { get; set; } = 60f;
    public float AiDefenseQuestTimer { get; set; } = 120f;
    public int InitialIndependentCount { get; set; }
    public bool UseLlm => _useLlm;

    public AiManager(Galaxy galaxy, RouteManager routeManager, Player player,
        AttackManager attackManager, LlmService? llmService, NewsService newsService,
        List<GalacticBroadcast> pendingBroadcasts,
        Action<string> setStatus, Action<string, float> setStatusTimed)
    {
        _galaxy = galaxy;
        _routeManager = routeManager;
        _player = player;
        _attackManager = attackManager;
        _llmService = llmService;
        _newsService = newsService;
        _pendingBroadcasts = pendingBroadcasts;
        _setStatus = setStatus;
        _setStatusTimed = setStatusTimed;
    }

    public async Task CheckLlmAsync()
    {
        if (_llmService == null) return;
        _useLlm = await _llmService.DetectAsync();
        if (_useLlm)
        {
            _setStatus("LLM commanders online (Empire + Federation)");
        }
    }

    public void Tick(float dt, string? playerCurrentSystemId, float totalGameTime)
    {
        if (playerCurrentSystemId == null) return;

        if (!_llmChecked)
        {
            _llmChecked = true;
            _ = CheckLlmAsync();
        }

        if (_useLlm)
        {
            AiTickTimer -= dt;
            if (AiTickTimer <= 0f)
            {
                AiTickTimer = 15f;
                _ = LlmAiTickAsync(totalGameTime);
            }
        }
        else
        {
            AiTickTimer -= dt;
            if (AiTickTimer <= 0f)
            {
                AiTickTimer = 4f;
                _routeManager.AiTick(playerCurrentSystemId);
            }

            AiCaptureTimer -= dt;
            if (AiCaptureTimer <= 0f)
            {
                AiCaptureTimer = 30f;
                AiCaptureTick();
            }

            FederationAiTimer -= dt;
            if (FederationAiTimer <= 0f)
            {
                FederationAiTimer = 30f;
                FederationAiTick();
            }

            AiDefenseTimer -= dt;
            if (AiDefenseTimer <= 0f)
            {
                AiDefenseTimer = 60f;
                AiDefenseTick();
            }

            AiDefenseQuestTimer -= dt;
            if (AiDefenseQuestTimer <= 0f)
            {
                AiDefenseQuestTimer = 120f;
                GenerateDefenseQuest();
            }
        }
    }

    private void AiCaptureTick()
    {
        var targets = _galaxy.Systems
            .Where(s => s.Hostility < 3 && s.Station != null && s.Id != _player.CurrentSystemId)
            .ToList();
        if (targets.Count == 0) return;

        var target = targets[Random.Shared.Next(targets.Count)];

        bool adjacentToEmpire = false;
        foreach (var conn in target.Connections)
        {
            var neighbor = _galaxy.FindSystemById(conn);
            if (neighbor != null && neighbor.Faction == "Trigor Empire")
            {
                adjacentToEmpire = true;
                break;
            }
        }
        if (!adjacentToEmpire) return;

        _attackManager.EmpireStartAttack(target.Id);
    }

    private void FederationAiTick()
    {
        int currentIndependent = _galaxy.Systems.Count(s => s.Hostility < 3 && s.Faction != "Atlas Federation");
        float capturedRatio = InitialIndependentCount > 0
            ? 1f - (float)currentIndependent / InitialIndependentCount
            : 0f;
        bool canOffense = capturedRatio >= 0.5f;

        foreach (var route in _routeManager.BlockedRoutes.ToList())
        {
            var parts = route.Split('-');
            if (parts.Length == 2)
            {
                var a = _galaxy.FindSystemById(parts[0]);
                var b = _galaxy.FindSystemById(parts[1]);
                if ((a != null && a.Hostility < 3) || (b != null && b.Hostility < 3))
                {
                    _routeManager.UnblockRoute(parts[0], parts[1]);
                    break;
                }
            }
        }

        if (!canOffense) return;

        var targets = _galaxy.Systems
            .Where(s => s.Hostility >= 3 && s.Station != null && s.Id != _player.CurrentSystemId)
            .ToList();
        if (targets.Count == 0) return;

        var target = targets[Random.Shared.Next(targets.Count)];

        bool adjacentToFriendly = false;
        foreach (var conn in target.Connections)
        {
            var neighbor = _galaxy.FindSystemById(conn);
            if (neighbor != null && neighbor.Hostility < 3)
            {
                adjacentToFriendly = true;
                break;
            }
        }
        if (!adjacentToFriendly) return;

        _attackManager.FederationStartAttack(target.Id);
    }

    private void AiDefenseTick()
    {
        var fedSystems = _galaxy.Systems
            .Where(s => s.Station != null && s.Station.DefenseLevel < 3)
            .Where(s => s.Faction == "Atlas Federation")
            .ToList();
        if (fedSystems.Count > 0)
        {
            var target = fedSystems[Random.Shared.Next(fedSystems.Count)];
            target.Station.DefenseLevel++;
            return;
        }

        var empSystems = _galaxy.Systems
            .Where(s => s.Station != null && s.Station.DefenseLevel < 5)
            .Where(s => s.Faction == "Trigor Empire")
            .ToList();
        if (empSystems.Count == 0) return;

        var weighted = new List<(StarSystemData sys, int weight)>();
        foreach (var sys in empSystems)
        {
            int dist = GetHopDistance("trigor", sys.Id);
            int weight = Math.Max(1, 5 - dist);
            weighted.Add((sys, weight));
        }

        int totalWeight = weighted.Sum(w => w.weight);
        int roll = Random.Shared.Next(totalWeight);
        int cumulative = 0;
        foreach (var (sys, weight) in weighted)
        {
            cumulative += weight;
            if (roll < cumulative)
            {
                sys.Station.DefenseLevel++;
                return;
            }
        }
    }

    private void GenerateDefenseQuest()
    {
        var candidates = _galaxy.Systems
            .Where(s => s.Station != null &&
                s.Station.DefenseLevel >= 3 && s.Station.DefenseLevel < 5)
            .Where(s => s.Faction == "Atlas Federation")
            .ToList();
        if (candidates.Count == 0) return;

        var target = candidates[Random.Shared.Next(candidates.Count)];
        int nextLevel = target.Station.DefenseLevel + 1;

        var cost = GetDefenseCost(nextLevel);
        if (cost.resources.Count == 0) return;

        string questId = $"defense_{target.Id}_{nextLevel}_{Random.Shared.Next(9999)}";
        string faction = target.Faction ?? "Unknown";

        var givers = _galaxy.Systems
            .Where(s => s.Id != target.Id && s.Hostility < 3 && s.Station != null)
            .ToList();
        if (givers.Count == 0) return;
        var giver = givers[Random.Shared.Next(givers.Count)];

        var quest = new QuestData
        {
            Id = questId,
            Name = $"Defense Upgrade: {target.Name} (Lv {target.Station.DefenseLevel}->{nextLevel})",
            Description = $"{faction} needs resources delivered to {target.Name} Station to upgrade defenses.",
            ObjectiveType = "deliver",
            TargetSystem = target.Id,
            RewardCredits = 500 * nextLevel,
            GiverSystem = giver.Id,
            RewardDefenseSystem = target.Id,
            RequiredResources = cost.resources.ToDictionary(r => r.id, r => r.qty)
        };

        _galaxy.AllQuests.Add(quest);
        _galaxy.RefreshAvailableQuests(_player);
    }

    private static (int credits, List<(string id, int qty)> resources) GetDefenseCost(int level)
    {
        return level switch
        {
            1 => (500, new() { ("fe", 10), ("c", 15) }),
            2 => (1500, new() { ("ti", 8), ("cu", 10) }),
            3 => (3000, new() { ("si", 12), ("al", 15) }),
            4 => (6000, new() { ("li", 6), ("nd", 4) }),
            5 => (10000, new() { ("pt", 3), ("au", 4) }),
            _ => (0, new())
        };
    }

    private int GetHopDistance(string from, string to)
    {
        if (from == to) return 0;
        var visited = new HashSet<string> { from };
        var queue = new Queue<(string id, int dist)>();
        queue.Enqueue((from, 0));
        while (queue.Count > 0)
        {
            var (cur, dist) = queue.Dequeue();
            var sys = _galaxy.FindSystemById(cur);
            if (sys == null) continue;
            foreach (var conn in sys.Connections)
            {
                if (conn == to) return dist + 1;
                if (visited.Add(conn))
                    queue.Enqueue((conn, dist + 1));
            }
        }
        return 99;
    }

    private async Task LlmAiTickAsync(float totalGameTime)
    {
        if (_llmService == null || _player.CurrentSystemId == null) return;

        var decision = await _llmService.RequestDecisionAsync(
            _galaxy.Systems,
            _player.CurrentSystemId,
            _routeManager.BlockedRoutes,
            _routeManager.MaxBlocked,
            _galaxy.ActiveQuests,
            _galaxy.Commanders);

        if (decision == null)
        {
            _routeManager.AiTick(_player.CurrentSystemId);
            AiCaptureTick();
            FederationAiTick();
            return;
        }

        if (!string.IsNullOrEmpty(decision.EmpireNewsHeadline) && !string.IsNullOrEmpty(decision.EmpireNewsBody))
        {
            _newsService?.PostFactionNews(
                decision.EmpireNewsHeadline,
                decision.EmpireNewsBody,
                "Imperial Herald", "Trigor Empire");
        }

        if (!string.IsNullOrEmpty(decision.FederationNewsHeadline) && !string.IsNullOrEmpty(decision.FederationNewsBody))
        {
            _newsService?.PostFactionNews(
                decision.FederationNewsHeadline,
                decision.FederationNewsBody,
                "Atlas Federation News Network", "Atlas Federation");
        }

        if (!string.IsNullOrEmpty(decision.EmpireBroadcast))
        {
            _pendingBroadcasts.Add(new GalacticBroadcast
            {
                Faction = "Trigor Empire",
                CommanderName = "Emperor Cyrus III",
                CommanderTitle = "Emperor of the Trigor Empire",
                Message = decision.EmpireBroadcast,
                Timestamp = totalGameTime
            });
            _setStatus("Galactic Broadcast Received! Press B to view");
        }
        if (!string.IsNullOrEmpty(decision.FederationBroadcast))
        {
            _pendingBroadcasts.Add(new GalacticBroadcast
            {
                Faction = "Atlas Federation",
                CommanderName = "Prime Minister Ezara Loban",
                CommanderTitle = "Prime Minister of the Atlas Federation",
                Message = decision.FederationBroadcast,
                Timestamp = totalGameTime
            });
            _setStatus("Galactic Broadcast Received! Press B to view");
        }

        if (decision.BlockRoutes != null)
        {
            foreach (var route in decision.BlockRoutes)
            {
                if (route.Count >= 2)
                    _routeManager.BlockRoute(route[0], route[1]);
            }
        }

        if (!string.IsNullOrEmpty(decision.AttackSystem))
        {
            var target = _galaxy.FindSystemById(decision.AttackSystem);
            if (target != null && target.Station != null && target.Hostility < 3)
            {
                bool adjacent = false;
                foreach (var conn in target.Connections)
                {
                    var neighbor = _galaxy.FindSystemById(conn);
                    if (neighbor != null && neighbor.Faction == "Trigor Empire")
                    {
                        adjacent = true;
                        break;
                    }
                }
                if (adjacent)
                    _attackManager.EmpireStartAttack(target.Id);
            }
        }

        if (decision.FederationUnblockRoutes != null)
        {
            foreach (var route in decision.FederationUnblockRoutes)
            {
                if (route.Count >= 2)
                    _routeManager.UnblockRoute(route[0], route[1]);
            }
        }

        int currentIndependent = _galaxy.Systems.Count(s => s.Hostility < 3 && s.Faction != "Atlas Federation");
        float capturedRatio = InitialIndependentCount > 0
            ? 1f - (float)currentIndependent / InitialIndependentCount
            : 0f;

        if (capturedRatio >= 0.5f && !string.IsNullOrEmpty(decision.FederationAttackSystem))
        {
            var target = _galaxy.FindSystemById(decision.FederationAttackSystem);
            if (target != null && target.Station != null && target.Hostility >= 3)
            {
                bool adjacent = false;
                foreach (var conn in target.Connections)
                {
                    var neighbor = _galaxy.FindSystemById(conn);
                    if (neighbor != null && neighbor.Hostility < 3)
                    {
                        adjacent = true;
                        break;
                    }
                }
                if (adjacent)
                    _attackManager.FederationStartAttack(target.Id);
            }
        }
    }
}
