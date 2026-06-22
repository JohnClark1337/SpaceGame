using SpaceGame.Models;

namespace SpaceGame.Systems;

public enum AiDifficulty { Easy, Medium, Hard }

public class RouteManager
{
    private Galaxy _galaxy;

    public AiDifficulty Difficulty { get; set; } = AiDifficulty.Easy;

    // Blocked routes stored as ordered tuple "fromId-toId" (alphabetical)
    private HashSet<string> _blockedRoutes = new();

    // Events for future integration (quests, enemy units, UI)
    public event Action<string, string>? RouteBlocked;
    public event Action<string, string>? RouteUnblocked;

    public RouteManager(Galaxy galaxy)
    {
        _galaxy = galaxy;
    }

    public void SetGalaxy(Galaxy galaxy)
    {
        _galaxy = galaxy;
        _blockedRoutes.Clear();
    }

    // --- Route key helpers ---
    private static string RouteKey(string a, string b)
    {
        return string.Compare(a, b, StringComparison.Ordinal) < 0 ? $"{a}-{b}" : $"{b}-{a}";
    }

    public bool IsBlocked(string a, string b) => _blockedRoutes.Contains(RouteKey(a, b));

    public IReadOnlySet<string> BlockedRoutes => _blockedRoutes;

    public void SetBlockedRoutes(IEnumerable<string> routes)
    {
        _blockedRoutes = new HashSet<string>(routes);
    }

    public int MaxBlocked
    {
        get
        {
            int totalRoutes = CountRoutes();
            return Difficulty switch
            {
                AiDifficulty.Easy => Math.Min(1, totalRoutes / 2),
                AiDifficulty.Medium => Math.Min(3, totalRoutes / 2),
                AiDifficulty.Hard => Math.Min(5, totalRoutes / 2),
                _ => 1
            };
        }
    }

    public int CountBlocked => _blockedRoutes.Count;

    public bool IsRouteBetweenEnemySystems(string a, string b)
    {
        var sysA = _galaxy.FindSystemById(a);
        var sysB = _galaxy.FindSystemById(b);
        return sysA != null && sysB != null && sysA.Hostility >= 3 && sysB.Hostility >= 3;
    }

    // --- Blocking ---
    public bool BlockRoute(string a, string b)
    {
        string key = RouteKey(a, b);
        if (_blockedRoutes.Contains(key)) return false;
        if (_blockedRoutes.Count >= MaxBlocked) return false;

        _blockedRoutes.Add(key);
        RouteBlocked?.Invoke(a, b);
        return true;
    }

    public void UnblockRoute(string a, string b)
    {
        string key = RouteKey(a, b);
        if (_blockedRoutes.Remove(key))
            RouteUnblocked?.Invoke(a, b);
    }

    // --- Graph ---
    private int CountRoutes()
    {
        var seen = new HashSet<string>();
        foreach (var sys in _galaxy.Systems)
        {
            foreach (var conn in sys.Connections)
                seen.Add(RouteKey(sys.Id, conn));
        }
        return seen.Count;
    }

    // BFS shortest path — returns path as list of system IDs, empty if unreachable
    public List<string> FindPath(string fromId, string toId)
    {
        if (fromId == toId) return new List<string> { fromId };

        var visited = new HashSet<string> { fromId };
        var queue = new Queue<string>();
        var parent = new Dictionary<string, string>();
        queue.Enqueue(fromId);

        while (queue.Count > 0)
        {
            var current = queue.Dequeue();
            var sys = _galaxy.FindSystemById(current);
            if (sys == null) continue;

            foreach (var next in sys.Connections)
            {
                if (visited.Contains(next)) continue;
                if (IsBlocked(current, next)) continue;

                visited.Add(next);
                parent[next] = current;
                if (next == toId)
                {
                    var path = new List<string> { toId };
                    string node = toId;
                    while (parent.ContainsKey(node))
                    {
                        node = parent[node];
                        path.Add(node);
                    }
                    path.Reverse();
                    return path;
                }
                queue.Enqueue(next);
            }
        }
        return new List<string>();
    }

    // Check if any quest objective is unreachable from the given system
    public bool HasGuaranteedPaths(string fromId)
    {
        foreach (var quest in _galaxy.ActiveQuests)
        {
            if (quest.ObjectiveType == "travel" && !string.IsNullOrEmpty(quest.TargetSystem))
            {
                var path = FindPath(fromId, quest.TargetSystem);
                if (path.Count == 0) return false;
            }
        }
        return true;
    }

    // --- AI decision ---
    // Rule-based pick for now. LLM commander plugs in here later.
    public void AiTick(string playerSystemId)
    {
        if (_blockedRoutes.Count >= MaxBlocked) return;

        // Score every unblocked route by how much it increases path length
        var candidates = new List<(string key, string a, string b, float score)>();
        var seen = new HashSet<string>();

        foreach (var sys in _galaxy.Systems)
        {
            foreach (var conn in sys.Connections)
            {
                var key = RouteKey(sys.Id, conn);
                if (seen.Contains(key)) continue;
                seen.Add(key);
                if (_blockedRoutes.Contains(key)) continue;
                if (!IsRouteBetweenEnemySystems(sys.Id, conn)) continue;

                // Temporarily block and measure impact on quest paths
                _blockedRoutes.Add(key);
                float score = ScoreBlockade(sys.Id, conn, playerSystemId);
                _blockedRoutes.Remove(key);

                candidates.Add((key, sys.Id, conn, score));
            }
        }

        if (candidates.Count == 0) return;

        // Pick based on difficulty
        var pick = Difficulty switch
        {
            AiDifficulty.Easy => candidates[Random.Shared.Next(candidates.Count)],
            AiDifficulty.Medium => candidates.OrderByDescending(c => c.score).Skip(1).FirstOrDefault(candidates[0]),
            AiDifficulty.Hard => candidates.OrderByDescending(c => c.score).First(),
            _ => candidates[Random.Shared.Next(candidates.Count)]
        };

        if (pick.key != null)
        {
            _blockedRoutes.Add(pick.key);
            RouteBlocked?.Invoke(pick.a, pick.b);

            // Guarantee: if any quest path is now blocked, undo this pick
            if (!HasGuaranteedPaths(playerSystemId))
            {
                _blockedRoutes.Remove(pick.key);
                RouteUnblocked?.Invoke(pick.a, pick.b);
            }
        }
    }

    // Score how valuable blocking this route is
    private float ScoreBlockade(string a, string b, string playerSystemId)
    {
        float score = 0;

        foreach (var quest in _galaxy.ActiveQuests)
        {
            if (quest.ObjectiveType != "travel" || string.IsNullOrEmpty(quest.TargetSystem))
                continue;

            var path = FindPath(playerSystemId, quest.TargetSystem);
            if (path.Count == 0)
            {
                score += 100f; // Blocking this route disconnects a quest — high value
            }
            else
            {
                // Count how many steps this adds vs the true shortest path
                _blockedRoutes.Remove(RouteKey(a, b));
                var truePath = FindPath(playerSystemId, quest.TargetSystem);
                _blockedRoutes.Add(RouteKey(a, b));
                float added = path.Count - truePath.Count;
                score += added * 10f;
            }
        }

        // Bonus for blocking routes near the player
        var playerSys = _galaxy.FindSystemById(playerSystemId);
        if (playerSys != null && playerSys.Connections.Contains(a) || playerSys?.Connections.Contains(b) == true)
            score += 5f;

        return score;
    }
}
