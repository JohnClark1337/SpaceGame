using SpaceGame.Models;
using SpaceGame.Systems;

namespace SpaceGame.Services;

public class AttackManager
{
    private readonly Galaxy _galaxy;
    private readonly Player _player;
    private readonly NewsService _newsService;
    private readonly Action<string> _setStatus;
    private readonly Action<string, float> _setStatusTimed;

    private readonly Dictionary<string, List<AttackState>> _activeAttacks = new();
    private const float AttackDuration = 60f;

    public IReadOnlyDictionary<string, List<AttackState>> ActiveAttacks => _activeAttacks;

    public AttackManager(Galaxy galaxy, Player player, NewsService newsService,
        Action<string> setStatus, Action<string, float> setStatusTimed)
    {
        _galaxy = galaxy;
        _player = player;
        _newsService = newsService;
        _setStatus = setStatus;
        _setStatusTimed = setStatusTimed;
    }

    public void RestoreAttacks(Dictionary<string, List<AttackStateSave>> saveData)
    {
        _activeAttacks.Clear();
        foreach (var kv in saveData)
            _activeAttacks[kv.Key] = kv.Value.Select(s => new AttackState { Timer = s.Timer, Attacker = s.Attacker }).ToList();
    }

    public Dictionary<string, List<AttackStateSave>> GetSaveData()
    {
        return _activeAttacks.ToDictionary(kv => kv.Key, kv => kv.Value.Select(s => new AttackStateSave
        {
            Timer = s.Timer,
            Attacker = s.Attacker
        }).ToList());
    }

    public bool IsUnderAttack(string systemId) =>
        _activeAttacks.ContainsKey(systemId) && _activeAttacks[systemId].Count > 0;

    public string? GetAttackAttacker(string systemId)
    {
        if (_activeAttacks.TryGetValue(systemId, out var list) && list.Count > 0)
            return list[0].Attacker;
        return null;
    }

    public List<string> GetAttackAttackers(string systemId)
    {
        if (_activeAttacks.TryGetValue(systemId, out var list))
            return list.Select(a => a.Attacker).ToList();
        return new List<string>();
    }

    public bool TryGetAttackTimer(string systemId, out float remaining)
    {
        if (_activeAttacks.TryGetValue(systemId, out var list) && list.Count > 0)
        {
            remaining = list.Min(a => a.Timer);
            return true;
        }
        remaining = 0;
        return false;
    }

    public void EmpireStartAttack(string systemId)
    {
        var sys = _galaxy.FindSystemById(systemId);
        if (sys == null || sys.Hostility >= 3) return;
        if (!_activeAttacks.ContainsKey(systemId))
            _activeAttacks[systemId] = new List<AttackState>();
        _activeAttacks[systemId].Add(new AttackState { Timer = AttackDuration, Attacker = "Trigor Empire" });
        _setStatus($"Trigor Empire is attacking {sys.Name}!");
        _newsService?.PostBreakingNews(
            $"Trigor Empire Launches Assault on {sys.Name}",
            $"Imperial forces have initiated a military strike against {sys.Name}. Civilians are urged to evacuate. The Atlas Federation has condemned the attack.",
            "Imperial Herald", "Trigor Empire");
    }

    public void FederationStartAttack(string systemId)
    {
        var sys = _galaxy.FindSystemById(systemId);
        if (sys == null || sys.Hostility < 3) return;
        if (!_activeAttacks.ContainsKey(systemId))
            _activeAttacks[systemId] = new List<AttackState>();
        _activeAttacks[systemId].Add(new AttackState { Timer = AttackDuration, Attacker = "Atlas Federation" });
        _setStatus($"Atlas Federation is attacking {sys.Name}!");
        _newsService?.PostBreakingNews(
            $"Atlas Federation Strikes Back at {sys.Name}",
            $"Atlas Federation naval forces have launched a counter-offensive against {sys.Name}. 'Freedom will prevail,' stated Fleet Command.",
            "Atlas Federation News Network", "Atlas Federation");
    }

    public void RepelAttack(string systemId)
    {
        if (!_activeAttacks.Remove(systemId)) return;
        var sys = _galaxy.FindSystemById(systemId);
        string name = sys?.Name ?? systemId;
        _setStatusTimed($"Attack on {name} repelled!", 5f);
    }

    public void Tick(float dt)
    {
        var systemsToResolve = new List<string>();
        foreach (var id in _activeAttacks.Keys.ToList())
        {
            var list = _activeAttacks[id];
            if (list.Count > 0)
            {
                var first = list[0];
                first.Timer -= dt;
                list[0] = first;
                if (first.Timer <= 0f)
                    systemsToResolve.Add(id);
            }
        }

        foreach (var id in systemsToResolve)
            ResolveAttack(id);
    }

    private void ResolveAttack(string systemId)
    {
        var sys = _galaxy.FindSystemById(systemId);
        if (sys == null) return;

        if (!_activeAttacks.TryGetValue(systemId, out var list) || list.Count == 0) return;

        if (_player.CurrentSystemId == systemId)
            return;

        string attacker = list[Random.Shared.Next(list.Count)].Attacker;

        if (attacker == "Atlas Federation")
        {
            sys.Hostility = 0;
            sys.Faction = "Atlas Federation";
            if (sys.Station != null) sys.Station.DefenseLevel = 0;
            _setStatus($"Atlas Federation has captured {sys.Name}!");
            _newsService?.PostBreakingNews(
                $"Atlas Federation Liberates {sys.Name}",
                $"The Atlas Federation has successfully captured {sys.Name}, dealing a blow to Trigor Empire aggression in the sector.",
                "Atlas Federation News Network", "Atlas Federation");
        }
        else
        {
            sys.Hostility = 5;
            sys.Faction = "Trigor Empire";
            if (sys.Station != null) sys.Station.DefenseLevel = 0;
            _setStatus($"Trigor Empire has captured {sys.Name}!");
            _newsService?.PostBreakingNews(
                $"Trigor Empire Claims {sys.Name}",
                $"Imperial forces have seized control of {sys.Name}. The Empire declares it a 'liberated territory' under Imperial protection.",
                "Imperial Herald", "Trigor Empire");

            if (sys.Connections.Count > 0)
            {
                var blockedConn = sys.Connections[Random.Shared.Next(sys.Connections.Count)];
            }
        }

        _activeAttacks.Remove(systemId);
    }

    public bool ShouldPauseTimer(string? currentSystemId)
    {
        return currentSystemId != null && _activeAttacks.ContainsKey(currentSystemId);
    }

    public void SetAttackData(Dictionary<string, List<AttackState>> attacks)
    {
        _activeAttacks.Clear();
        foreach (var kv in attacks)
            _activeAttacks[kv.Key] = kv.Value;
    }
}
