using SpaceGame.Models;

namespace SpaceGame.Systems;

public class NewsService
{
    private readonly List<NewsArticle> _articles = new();
    private readonly Random _rng = new();
    private Galaxy _galaxy = null!;
    private float _lastGenericTick;
    private const float GenericTickInterval = 60f;

    public IReadOnlyList<NewsArticle> Articles => _articles;

    public void SetArticles(List<NewsArticle> articles)
    {
        _articles.Clear();
        _articles.AddRange(articles);
    }

    public void SetGalaxy(Galaxy galaxy)
    {
        _galaxy = galaxy;
    }

    public void GenerateDefaultArticles()
    {
        _articles.Clear();

        foreach (var sys in _galaxy.Systems)
        {
            if (_articles.Count >= 40) break;

            string source = PickSource(sys);
            string factionLabel = sys.Faction ?? "Independent";
            int roll = _rng.Next(4);

            if (roll == 0 && sys.Station != null)
            {
                _articles.Add(new NewsArticle
                {
                    Headline = $"{sys.Name} Station Reports Record Traffic",
                    Body = $"Traffic at {sys.Station.Name} has reached record levels this cycle, driven by increased trade activity across {factionLabel} space. Port authorities have announced expansion plans.",
                    Source = source,
                    Faction = sys.Faction,
                    Timestamp = 0,
                    IsBreaking = false
                });
            }
            else if (roll == 1 && !string.IsNullOrEmpty(sys.Description))
            {
                string topic = _rng.Next(2) == 0
                    ? $"New survey data reveals uncharted mineral deposits in the {sys.Name} system. Miners are flocking to stake claims."
                    : $"Astronomers at {sys.Name} have discovered unusual stellar activity, prompting a wave of scientific curiosity across the sector.";
                _articles.Add(new NewsArticle
                {
                    Headline = $"Report from {sys.Name}",
                    Body = topic,
                    Source = source,
                    Faction = sys.Faction,
                    Timestamp = 0,
                    IsBreaking = false
                });
            }
            else if (roll == 2 && sys.Economy != null && sys.Economy.Production.Count > 0)
            {
                var topProd = sys.Economy.Production.OrderByDescending(kv => kv.Value).First();
                var resDef = _galaxy.FindResource(topProd.Key);
                string resName = resDef?.Name ?? topProd.Key;
                _articles.Add(new NewsArticle
                {
                    Headline = $"{sys.Name} Leads Sector in {resName} Output",
                    Body = $"Production of {resName} in the {sys.Name} system has reached {topProd.Value:F0} units per cycle, making it a key supplier for the {factionLabel} economy.",
                    Source = source,
                    Faction = sys.Faction,
                    Timestamp = 0,
                    IsBreaking = false
                });
            }
        }

        // Faction propaganda articles
        _articles.Add(new NewsArticle
        {
            Headline = "Federation Council Affirms Commitment to Peace",
            Body = "The Terran Federation Council has issued a statement reaffirming its dedication to protecting all sovereign systems from external aggression. Naval patrols have been increased along the frontier.",
            Source = "Federation News Network",
            Faction = "Terran Federation",
            Timestamp = 0,
            IsBreaking = false
        });

        _articles.Add(new NewsArticle
        {
            Headline = "Trigor Empire Expands Influence",
            Body = "Imperial sources confirm that the Trigor Empire has secured new trade agreements with border systems. 'The galaxy will know order,' stated an Imperial spokesperson.",
            Source = "Imperial Herald",
            Faction = "Trigor Empire",
            Timestamp = 0,
            IsBreaking = false
        });

        _articles.Add(new NewsArticle
        {
            Headline = "Independent Systems Alliance Strengthens",
            Body = "Representatives from several Independent systems have ratified a mutual defense and trade pact, signaling a new era of cooperation outside the two major factions.",
            Source = "Free Worlds Press",
            Faction = "Independent",
            Timestamp = 0,
            IsBreaking = false
        });

        _articles.Sort((a, b) => -a.Timestamp.CompareTo(b.Timestamp));
    }

    public void PostBreakingNews(string headline, string body, string source, string? faction)
    {
        _articles.Insert(0, new NewsArticle
        {
            Headline = headline,
            Body = body,
            Source = source,
            Faction = faction,
            Timestamp = float.MaxValue,
            IsBreaking = true
        });

        if (_articles.Count > 100)
            _articles.RemoveAt(_articles.Count - 1);
    }

    public void PostFactionNews(string headline, string body, string source, string faction)
    {
        _articles.Insert(0, new NewsArticle
        {
            Headline = headline,
            Body = body,
            Source = source,
            Faction = faction,
            Timestamp = float.MaxValue,
            IsBreaking = false
        });

        if (_articles.Count > 100)
            _articles.RemoveAt(_articles.Count - 1);
    }

    public void Tick(float dt, float gameTime)
    {
        _lastGenericTick -= dt;
        if (_lastGenericTick <= 0f)
        {
            _lastGenericTick = GenericTickInterval;
            PostRandomGenericNews(gameTime);
        }
    }

    private void PostRandomGenericNews(float gameTime)
    {
        if (_galaxy.Systems.Count == 0) return;

        var sys = _galaxy.Systems[_rng.Next(_galaxy.Systems.Count)];
        string source = PickSource(sys);

        string[] templates =
        {
            $"Traffic controllers at {sys.Name} report a smooth trading day with no incidents.",
            $"A luxury passenger liner touring the {sys.Name} system has reported stunning views of the local nebula.",
            $"Pirate activity near {sys.Name} is reportedly down this cycle, attributed to increased patrols.",
            $"A rare celestial alignment in the {sys.Name} system has drawn sightseers from across the sector.",
            $"Engineers at {sys.Name} have completed upgrades to the local docking facilities.",
            $"A distress beacon from the {sys.Name} system turned out to be a malfunctioning probe. No crew at risk.",
        };

        string body = templates[_rng.Next(templates.Length)];
        string headline = $"{sys.Name} - System Report";

        _articles.Insert(0, new NewsArticle
        {
            Headline = headline,
            Body = body,
            Source = source,
            Faction = sys.Faction,
            Timestamp = gameTime,
            IsBreaking = false
        });

        if (_articles.Count > 100)
            _articles.RemoveAt(_articles.Count - 1);
    }

    private string PickSource(StarSystemData sys)
    {
        string[] federationSources = { "Federation News Network", "The Proxima Times", "Spica Star", "Regulus Dispatch" };
        string[] empireSources = { "Imperial Herald", "Voice of the Empire", "TrigCorp Media", "The Dark Star" };
        string[] independentSources = { "Free Worlds Press", "Centauri Chronicle", "Lyra Beacon", "Draco Gazette" };

        return (sys.Faction ?? "Independent") switch
        {
            "Terran Federation" => federationSources[_rng.Next(federationSources.Length)],
            "Trigor Empire" => empireSources[_rng.Next(empireSources.Length)],
            _ => independentSources[_rng.Next(independentSources.Length)]
        };
    }
}
