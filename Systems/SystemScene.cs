using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using SpaceGame.Models;
using Color = Microsoft.Xna.Framework.Color;
using Vector2 = SpaceGame.Systems.Vector2;

namespace SpaceGame.Systems;

public class SystemScene
{
    private StarSystemData _system = null!;
    private readonly Player _player;
    private Game1 _game = null!;

    private struct Body
    {
        public string Name;
        public float OrbitRadius;
        public float BodyRadius;
        public Color Color;
        public float OrbitSpeed;
        public float CurrentAngle;
        public float X, Y;
    }

    private Body _star;
    private List<Body> _planets = new();
    private Body _station;
    private float _stationHealth;
    private const float _stationMaxHealth = 100f;
    private Body _trainingFriendlyStation;
    private float _trainingFriendlyHealth;
    private Vector2 _trainingTargetPos;
    private bool _trainingFriendlyRespawning;
    private float _trainingFriendlyRespawnTimer;
    private bool _empireStationRespawning;
    private float _empireStationRespawnTimer;
    private bool _stationRespawning;
    private float _stationRespawnTimer;
    private bool _trainingHostile;
    private int _stationDefenseLevel;
    private bool _stationHasShield;
    private struct StationTurret
    {
        public Vector2 Position;
        public float Angle;
        public float Cooldown;
        public bool Active;
    }
    private List<StationTurret> _stationTurrets = new();
    private List<StationTurret> _friendlyTurrets = new();
    private struct RingSection
    {
        public float Angle;
        public float Health;
        public float MaxHealth;
        public bool Active;
        public float LaserCooldown;
        public float MissileCooldown;
    }
    private List<RingSection> _ringSections = new();
    private List<RingSection> _friendlyRingSections = new();
    private bool _friendlyHasShield;
    private const float RING_SECTION_MAX_HEALTH = 50f;
    private float _spawnRadius;
    private const float ZOOM = 3.75f;
    private float _systemRadius;
    private bool _docked;
    private int _dockedTab;
    private int _dockedQuestSelection;
    private int _dockedMarketSelection;
    private int _dockedMarketScroll;
    private int _dockedUpgradeSelection;
    private int _dockedNewsScroll;
    private int _dockedNewsSubTab;
    private float _dockedNewsScrollRepeat;
    private bool _dockedNewsScrollHeld;
    private bool _dockedNewsScrollDown;
    private float _dockedSellRepeat;
    private float _dockedBuyRepeat;
    private bool _nearEdge;
    private bool _nearPlanet;
    private bool _underAttack;
    private bool _initialized;
    private readonly HashSet<string> _spawnedQuestIds = new();
    private float _temperature;
    private bool _exploding;
    private float _explosionTimer;
    private Vector2 _explosionPos;
    private float _explosionAngle;
    private bool _gameOver;
    private List<ExplosionDebris> _debris = new();

    private Body _lifepod;
    private bool _lifepodActive;

    private bool _trainingPaused;
    private int _trainingMenuSelection;
    private bool _trainingInvincible;
    private bool _showEnemyList;
    private int _trainingSpawnSelection;

    private struct ExplosionDebris
    {
        public Vector2 Start;
        public Vector2 End;
        public Vector2 Velocity;
        public float AngularVelocity;
        public float CurrentAngle;
        public float Alpha;
    }

    private struct Particle { public Vector2 Pos; public float Brightness; public float SpeedMult; }
    private Particle[] _particles = new Particle[80];
    private bool _particlesInitialized;
    private Vector2 _waypointPosition;
    private int _waypointIndex;
    private List<(Vector2 pos, string name)> _waypointTargets = new();
    private int _targetIndex = -1;
    private Vector2 _targetPosition;

    private struct Bullet
    {
        public Vector2 Position;
        public Vector2 Velocity;
        public float Lifetime;
        public bool IsPlayerBullet;
        public float Damage;
    }
    private List<Bullet> _bullets = new();
    private const float BULLET_SPEED = 500f;
    private const float BULLET_LIFETIME = 1.5f;

    private struct Missile
    {
        public Vector2 Position;
        public Vector2 Velocity;
        public float Lifetime;
        public int TargetIndex;
        public bool IsEnemyMissile;
    }
    private List<Missile> _missiles = new();
    private float _weaponCooldown;
    private int _activeWeapon; // 0=base, 1=laser, 2=missile

    private struct LaserBeam
    {
        public Vector2 Start;
        public Vector2 End;
        public float Timer;
    }
    private List<LaserBeam> _laserBeams = new();

    private enum AiState { Idle, Orbit, Attack, Evade }
    private struct EnemyShip
    {
        public Vector2 Position;
        public Vector2 Velocity;
        public float Angle;
        public float Health;
        public float MaxHealth;
        public string Type;
        public float ShootCooldown;
        public AiState AiState;
        public float StateTimer;
        public float OrbitAngle;
        public float Shields;
        public float MaxShields;
        public float WeaponDamage;
        public float TurretCooldown1;
        public float TurretCooldown2;
        public float TurretCooldown3;
        public float MissileCooldown1;
        public float MissileCooldown2;
        public float BattleshipFrontHP;
        public float BattleshipFrontMaxHP;
        public float BattleshipMidHP;
        public float BattleshipMidMaxHP;
        public float BattleshipRearHP;
        public float BattleshipRearMaxHP;
        public bool MovementDisabled;
        public bool WeaponsDisabled;
        public string Faction;
    }
    private List<EnemyShip> _enemies = new();

    private struct EnemyExplosion
    {
        public Vector2 Position;
        public float Timer;
        public float Duration;
    }
    private List<EnemyExplosion> _enemyExplosions = new();

    public struct Asteroid
    {
        public Vector2 Position;
        public float Radius;
        public float Health;
        public int Seed;
    }
    private List<Asteroid> _asteroids = new();
    public List<Asteroid> Asteroids => _asteroids;

    private struct AsteroidLoot
    {
        public Vector2 Position;
        public float Lifetime;
        public string Type; // "resource" or "fuel_cell"
        public string ResourceId;
    }
    private List<AsteroidLoot> _asteroidLoot = new();
    private List<EnemyExplosion> _asteroidExplosions = new();

    private bool _showPickupDialog;
    private string _pickupMessage = "";
    private float _pickupTimer;

    public bool Docked => _docked;
    public bool TrainingMode { get; set; }
    private bool StationIsHostile => _trainingHostile || (_system.Hostility >= 3 && _system.Station != null);
    public StarSystemData? CurrentSystem => _system;
    public string StationName => _station.Name;

    public SystemScene(Player player)
    {
        _player = player;
    }

    public void EnterSystem(StarSystemData system, Game1 game)
    {
        _system = system;
        _game = game;
        _initialized = false;
        _docked = false;
        _spawnedQuestIds.Clear();
        _trainingPaused = false;
        _trainingInvincible = false;
        _showEnemyList = false;
        _gameOver = false;
        _exploding = false;
        _debris.Clear();
        _enemyExplosions.Clear();
        _missiles.Clear();
        _laserBeams.Clear();
        _bullets.Clear();
        _asteroids.Clear();
        _asteroidLoot.Clear();
        _asteroidExplosions.Clear();
        _showPickupDialog = false;
        ClearStationDefenses();
        _trainingFriendlyStation = default;
        _trainingFriendlyHealth = 0;
        _trainingTargetPos = Vector2.Zero;
        _trainingFriendlyRespawning = false;
        _trainingFriendlyRespawnTimer = 0f;
        _empireStationRespawning = false;
        _empireStationRespawnTimer = 0f;
        _stationRespawning = false;
        _stationRespawnTimer = 0f;
        _trainingHostile = false;
    }

    private void ClearStationDefenses()
    {
        _stationTurrets.Clear();
        _ringSections.Clear();
        _stationDefenseLevel = 0;
        _stationHasShield = false;
    }

    private void InitStationDefenses()
    {
        ClearStationDefenses();
        if (_system.Station == null && !TrainingMode) return;
        _stationDefenseLevel = TrainingMode ? _stationDefenseLevel : _system.Station.DefenseLevel;
        if (_stationDefenseLevel < 1) return;

        // Level 1: 3 basic gun turrets evenly spaced around station
        float r = _station.BodyRadius + 8f;
        for (int i = 0; i < 3; i++)
        {
            float a = MathF.PI * 2f / 3f * i;
            _stationTurrets.Add(new StationTurret
            {
                Position = new Vector2(MathF.Cos(a) * r, MathF.Sin(a) * r),
                Angle = a,
                Cooldown = 0f,
                Active = true
            });
        }

        // Levels 2-5: ring sections
        int sectionCount = Math.Clamp(_stationDefenseLevel - 1, 0, 4);
        if (sectionCount > 0)
        {
            float sectionAngleStep = MathF.PI * 2f / 4f;
            float startAngle = -sectionAngleStep / 2f;
            for (int i = 0; i < sectionCount; i++)
            {
                _ringSections.Add(new RingSection
                {
                    Angle = startAngle + sectionAngleStep * i,
                    Health = RING_SECTION_MAX_HEALTH,
                    MaxHealth = RING_SECTION_MAX_HEALTH,
                    Active = true,
                    LaserCooldown = 0f,
                    MissileCooldown = 0f
                });
            }
        }

        // Level 5: shield
        _stationHasShield = _stationDefenseLevel >= 5 && _ringSections.All(s => s.Active);
    }

    private void InitFriendlyStationDefenses()
    {
        _friendlyTurrets.Clear();
        _friendlyRingSections.Clear();
        _friendlyHasShield = false;
        int level = 5;
        // Level 1: 3 basic gun turrets evenly spaced around station
        float r = _trainingFriendlyStation.BodyRadius + 8f;
        for (int i = 0; i < 3; i++)
        {
            float a = MathF.PI * 2f / 3f * i;
            _friendlyTurrets.Add(new StationTurret
            {
                Position = new Vector2(MathF.Cos(a) * r, MathF.Sin(a) * r),
                Angle = a,
                Cooldown = 0f,
                Active = true
            });
        }
        // Levels 2-5: ring sections
        int sectionCount = Math.Clamp(level - 1, 0, 4);
        if (sectionCount > 0)
        {
            float sectionAngleStep = MathF.PI * 2f / 4f;
            float startAngle = -sectionAngleStep / 2f;
            for (int i = 0; i < sectionCount; i++)
            {
                _friendlyRingSections.Add(new RingSection
                {
                    Angle = startAngle + sectionAngleStep * i,
                    Health = RING_SECTION_MAX_HEALTH,
                    MaxHealth = RING_SECTION_MAX_HEALTH,
                    Active = true,
                    LaserCooldown = 0f,
                    MissileCooldown = 0f
                });
            }
        }
        _friendlyHasShield = level >= 5 && _friendlyRingSections.All(s => s.Active);
    }

    private (int credits, List<(string id, int qty)> resources) GetDefenseUpgradeCost(int currentLevel)
    {
        int nextLevel = currentLevel + 1;
        return nextLevel switch
        {
            1 => (500, new() { ("fe", 10), ("c", 15) }),
            2 => (1500, new() { ("ti", 8), ("cu", 10) }),
            3 => (3000, new() { ("si", 12), ("al", 15) }),
            4 => (6000, new() { ("li", 6), ("nd", 4) }),
            5 => (10000, new() { ("pt", 3), ("au", 4) }),
            _ => (0, new())
        };
    }

    private void UpgradeStationDefenses()
    {
        if (_system.Station == null || _stationDefenseLevel >= 5) return;
        var cost = GetDefenseUpgradeCost(_stationDefenseLevel);
        if (_player.Credits < cost.credits) return;
        foreach (var req in cost.resources)
        {
            var inv = _player.Resources.FirstOrDefault(r => r.Id == req.id);
            if (inv == null || inv.Quantity < req.qty) return;
        }

        _player.Credits -= cost.credits;
        foreach (var req in cost.resources)
        {
            var inv = _player.Resources.FirstOrDefault(r => r.Id == req.id);
            if (inv != null) inv.Quantity -= req.qty;
        }

        _stationDefenseLevel++;
        _system.Station.DefenseLevel = _stationDefenseLevel;
        InitStationDefenses();
        _pickupMessage = $"Station defenses upgraded to Level {_stationDefenseLevel}!";
        _pickupTimer = 2f;
        _showPickupDialog = true;
    }

    private void Initialize()
    {
        if (_initialized) return;
        _initialized = true;

        _star = new Body
        {
            Name = _system.Name,
            BodyRadius = _system.StarRadius,
            Color = ParseColor(_system.Color),
            X = 0, Y = 0
        };

        _planets.Clear();
        float angle = 0;
        foreach (var pd in _system.Planets)
        {
            _planets.Add(new Body
            {
                Name = pd.Name,
                OrbitRadius = pd.OrbitRadius,
                BodyRadius = pd.Radius,
                Color = ParseColor(pd.Color),
                OrbitSpeed = pd.OrbitSpeed,
                CurrentAngle = angle,
            });
            angle += 1.5f;
        }

        if (_system.Station != null && !TrainingMode)
        {
            var sd = _system.Station;
            _station = new Body
            {
                Name = sd.Name,
                OrbitRadius = sd.OrbitRadius,
                BodyRadius = sd.Radius,
                Color = Color.Silver,
                OrbitSpeed = sd.OrbitSpeed,
                CurrentAngle = sd.Angle,
            };
            _stationHealth = _stationMaxHealth;
            InitStationDefenses();
        }

        // Calculate system radius before training mode override
        _systemRadius = 0f;
        foreach (var p in _planets)
            if (p.OrbitRadius > _systemRadius) _systemRadius = p.OrbitRadius;
        if (_system.Station != null && _station.OrbitRadius > _systemRadius)
            _systemRadius = _station.OrbitRadius;
        _systemRadius = MathF.Max(MathF.Max(_systemRadius, 1f) * 1.1f, _star.BodyRadius * 2f);

        // Ensure station orbits outside overheating range
        if (_system.Station != null && !TrainingMode)
        {
            float warningNorm = MathF.Pow(0.25f, 1f / 0.5f);
            float minSafe = _star.BodyRadius + warningNorm * (_systemRadius - _star.BodyRadius);
            if (_station.OrbitRadius < minSafe)
            {
                _station.OrbitRadius = minSafe;
                if (_station.OrbitRadius * 1.1f > _systemRadius)
                    _systemRadius = _station.OrbitRadius * 1.1f;
            }
        }

        _spawnRadius = _systemRadius * 0.80f;
        _particlesInitialized = false;
        _nearEdge = false;
        _nearPlanet = false;
        _lifepodActive = false;
        _bullets.Clear();
        _asteroids.Clear();
        _asteroidLoot.Clear();
        _asteroidExplosions.Clear();
        _showPickupDialog = false;
        _enemies.Clear();
        _enemyExplosions.Clear();

        // Spawn near a corner of the map to avoid planets and star
        float cornerDist = _systemRadius * 0.85f;
        float[] cornerAngles = { MathF.PI / 4f, 3f * MathF.PI / 4f, 5f * MathF.PI / 4f, 7f * MathF.PI / 4f };
        float spawnAngle = cornerAngles[Random.Shared.Next(4)];
        _player.Position = new Vector2(
            MathF.Cos(spawnAngle) * cornerDist,
            MathF.Sin(spawnAngle) * cornerDist);
        _player.Velocity = Vector2.Zero;
        _player.Angle = spawnAngle + MathF.PI;

        _waypointTargets.Clear();
        _waypointIndex = 0;
        if (_system.Station != null)
        {
            string stationLabel = _station.Name;
            if (!StationIsHostile) stationLabel += " (Friendly)";
            _waypointTargets.Add((new Vector2(_station.X, _station.Y), stationLabel));
        }
        _waypointPosition = _waypointTargets.Count > 0
            ? _waypointTargets[0].pos
            : Vector2.Zero;

        int sw = _game.ViewWidth;
        int sh = _game.ViewHeight;
        for (int i = 0; i < _particles.Length; i++)
        {
            float b = RandF() * 0.5f + 0.3f;
            _particles[i] = new Particle
            {
                Pos = new Vector2(RandF() * sw, RandF() * sh),
                Brightness = b,
                SpeedMult = 0.5f + b * 1.5f
            };
        }
        _particlesInitialized = true;

        // Generate asteroids
        GenerateAsteroids();

        // Spawn enemies in hostile systems (scales with proximity to Trigor core)
        if (_system.Hostility >= 3 && !TrainingMode)
            SpawnHostileEnemies();

        // Spawn raiders for under-attack systems (non-hostile)
        if (_game != null && _game.IsSystemUnderAttack(_system.Id) && _system.Hostility < 3)
        {
            _underAttack = true;
            string? attacker = _game.GetAttackAttacker(_system.Id);
            bool isFederationAttack = attacker == "Atlas Federation";
            string faction = isFederationAttack ? "Atlas Federation" : "";

            // Fleet composition based on attacker
            int distFromCore = isFederationAttack
                ? GetHopDistance("atlas", _system.Id)
                : GetHopDistance("trigor", _system.Id);
            int count = 4 + Random.Shared.Next(3) + Math.Max(0, 4 - distFromCore);

            string[] typePool;
            if (distFromCore <= 1)
                typePool = new[] { "battleship", "destroyer", "missile_frigate", "cruiser", "gunship", "interceptor", "fighter", "scout" };
            else if (distFromCore <= 2)
                typePool = new[] { "destroyer", "missile_frigate", "cruiser", "gunship", "interceptor", "fighter", "scout" };
            else if (distFromCore <= 4)
                typePool = new[] { "cruiser", "gunship", "interceptor", "fighter", "scout" };
            else
                typePool = new[] { "gunship", "interceptor", "fighter", "scout" };

            for (int i = 0; i < count; i++)
            {
                float sa = RandF() * MathF.Tau;
                float spawnDist = 100f + RandF() * 300f;
                Vector2 pos = _player.Position + Vector2.FromAngle(sa) * spawnDist;
                float a = RandF() * MathF.Tau;
                string type = typePool[Random.Shared.Next(typePool.Length)];
                _enemies.Add(MakeEnemyShip(type, pos,
                    Vector2.FromAngle(a) * (80f + RandF() * 70f), a,
                    AiState.Attack, 2f, sa, faction));
            }
        }

        // Federation patrols in friendly systems
        if (!TrainingMode && _system.Hostility < 3)
            SpawnFederationPatrols();

        // Empire scouts in systems within 2 hops of Empire
        if (!TrainingMode)
            SpawnEmpireScouts();

        // Empire patrols in enemy territory
        if (!TrainingMode && (_system.Faction == "Trigor Empire" || _system.Hostility >= 3))
            SpawnEmpirePatrols();

        // Quest-defined spawns
        if (!TrainingMode)
            SpawnQuestEnemies();
    }

    private void SpawnQuestEnemies()
    {
        var galaxy = _game?.Galaxy;
        if (galaxy == null) return;

        foreach (var sd in galaxy.Spawns)
        {
            if (sd.SystemId != _system.Id) continue;

            if (sd.QuestCondition != null)
            {
                if (_spawnedQuestIds.Contains(sd.QuestCondition.QuestId)) continue;
                var quest = galaxy.ActiveQuests.FirstOrDefault(q => q.Id == sd.QuestCondition.QuestId);
                if (quest == null) continue;
                _spawnedQuestIds.Add(sd.QuestCondition.QuestId);
            }

            foreach (var ship in sd.Ships)
            {
                string faction = ship.Faction;
                AiState aiState = ship.AiState switch
                {
                    "Attack" => AiState.Attack,
                    "Idle" => AiState.Idle,
                    "Evade" => AiState.Evade,
                    _ => AiState.Orbit
                };

                for (int i = 0; i < ship.Count; i++)
                {
                    float spawnAngle = RandF() * MathF.Tau;
                    float spawnDist = 200f + RandF() * 400f;
                    Vector2 pos = _player.Position + Vector2.FromAngle(spawnAngle) * spawnDist;
                    float a = RandF() * MathF.Tau;
                    _enemies.Add(MakeEnemyShip(ship.Type, pos,
                        Vector2.FromAngle(a) * (80f + RandF() * 70f), a,
                        aiState, 0f, spawnAngle, faction));
                }
            }
        }
    }

    private static float RandF() { return (float)Random.Shared.NextDouble(); }

    public void Update(float dt, KeyboardState keyboard, MouseState mouse)
    {
        Initialize();

        if (!TrainingMode && !_gameOver && !_exploding && !_docked)
            SpawnQuestEnemies();

        KeyboardState prevKb = _prevKeyboard;
        float t = (float)_game.GameTime.TotalGameTime.TotalSeconds;

        if (_gameOver)
        {
            if (TrainingMode)
            {
                Respawn();
            }
            else if (keyboard.IsKeyDown(Keys.Enter) && prevKb.IsKeyUp(Keys.Enter))
            {
                _gameOver = false;
                _game.ShowNewGameMenu();
            }
            return;
        }

        if (_exploding)
        {
            _explosionTimer += dt;
            for (int i = 0; i < _debris.Count; i++)
            {
                var d = _debris[i];
                d.Velocity *= 0.97f;
                d.CurrentAngle += d.AngularVelocity * dt;
                d.Start += d.Velocity * dt;
                d.End += d.Velocity * dt;
                d.Alpha -= dt * 0.5f;
                if (d.Alpha < 0) d.Alpha = 0;
                _debris[i] = d;
            }

            if (_explosionTimer > 2.5f)
            {
                _exploding = false;
                if (TrainingMode)
                    Respawn();
                else
                    _gameOver = true;
            }
            return;
        }

        if (!_docked)
        {
            for (int i = 0; i < _planets.Count; i++)
            {
                var p = _planets[i];
                p.CurrentAngle += p.OrbitSpeed * dt;
                p.X = MathF.Cos(p.CurrentAngle) * p.OrbitRadius;
                p.Y = MathF.Sin(p.CurrentAngle) * p.OrbitRadius;
                _planets[i] = p;
            }

            if (_system.Station != null)
            {
                if (!TrainingMode)
                {
                    _station.CurrentAngle += _station.OrbitSpeed * dt;
                    _station.X = MathF.Cos(_station.CurrentAngle) * _station.OrbitRadius;
                    _station.Y = MathF.Sin(_station.CurrentAngle) * _station.OrbitRadius;
                }
            }

            // Rebuild waypoint targets
            _waypointTargets.Clear();
            if (_system.Station != null && !TrainingMode)
            {
                string stationLabel = _station.Name;
                if (!StationIsHostile) stationLabel += " (Friendly)";
                _waypointTargets.Add((new Vector2(_station.X, _station.Y), stationLabel));
            }
            if (_game != null)
            {
                foreach (var q in _game.Galaxy.ActiveQuests)
                {
                    if (q.TargetSystem != _system.Id) continue;
                    if (q.ObjectiveType == "collect" && _lifepodActive)
                        _waypointTargets.Add((new Vector2(_lifepod.X, _lifepod.Y), q.Name));
                    else if ((q.ObjectiveType == "travel" || q.ObjectiveType == "deliver") && _system.Station != null)
                        _waypointTargets.Add((new Vector2(_station.X, _station.Y), q.Name));
                }
            }
            // Nearest asteroid waypoint
            if (_asteroids.Count > 0)
            {
                float minDist = float.MaxValue;
                Vector2 minPos = Vector2.Zero;
                foreach (var roid in _asteroids)
                {
                    float dd = Vector2.Distance(_player.Position, roid.Position);
                    if (dd < minDist)
                    {
                        minDist = dd;
                        minPos = roid.Position;
                    }
                }
                _waypointTargets.Add((minPos, "Asteroid"));
            }
            if (_waypointIndex >= _waypointTargets.Count)
                _waypointIndex = 0;
            _waypointPosition = _waypointTargets.Count > 0
                ? _waypointTargets[_waypointIndex].pos
                : (_system.Station != null && !TrainingMode ? new Vector2(_station.X, _station.Y) : Vector2.Zero);

            // Tab to cycle waypoint
            if (keyboard.IsKeyDown(Keys.Tab) && _prevKeyboard.IsKeyUp(Keys.Tab))
            {
                _waypointIndex = (_waypointIndex + 1) % Math.Max(1, _waypointTargets.Count);
                _waypointPosition = _waypointTargets.Count > 0
                    ? _waypointTargets[_waypointIndex].pos
                    : _waypointPosition;
            }

            // Combat target cycling
            if (keyboard.IsKeyDown(Keys.O) && _prevKeyboard.IsKeyUp(Keys.O))
            {
                CycleCombatTarget();
            }

            // Reset to nearest enemy
            if (keyboard.IsKeyDown(Keys.N) && _prevKeyboard.IsKeyUp(Keys.N))
            {
                AutoTargetNearestEnemy();
            }

            // Auto-target nearest enemy if no current target
            if (_targetIndex < 0 || _targetIndex >= _enemies.Count)
                AutoTargetNearestEnemy();

            // Update target position each frame
            if (_targetIndex >= 0 && _targetIndex < _enemies.Count)
                _targetPosition = _enemies[_targetIndex].Position;

            // Training mode input (F1 spawn menu, ESC pause) - intercepts normal update
            if (TrainingMode)
            {
                bool esc = keyboard.IsKeyDown(Keys.Escape) && _prevKeyboard.IsKeyUp(Keys.Escape);
                bool f1 = keyboard.IsKeyDown(Keys.F1) && _prevKeyboard.IsKeyUp(Keys.F1);
                bool down = (keyboard.IsKeyDown(Keys.Down) && _prevKeyboard.IsKeyUp(Keys.Down)) ||
                            (keyboard.IsKeyDown(Keys.S) && _prevKeyboard.IsKeyUp(Keys.S));
                bool up = (keyboard.IsKeyDown(Keys.Up) && _prevKeyboard.IsKeyUp(Keys.Up)) ||
                          (keyboard.IsKeyDown(Keys.W) && _prevKeyboard.IsKeyUp(Keys.W));
                bool enter = keyboard.IsKeyDown(Keys.Enter) && _prevKeyboard.IsKeyUp(Keys.Enter);

                int spawnCount = 24;

                if (esc)
                {
                    if (_showEnemyList)
                        _showEnemyList = false;
                    else
                    {
                        _trainingPaused = !_trainingPaused;
                        _trainingMenuSelection = 0;
                    }
                }
                if (f1)
                    _showEnemyList = !_showEnemyList;

                if (_showEnemyList)
                {
                    if (down)
                    {
                        _trainingSpawnSelection++;
                        if (_trainingSpawnSelection >= spawnCount)
                            _trainingSpawnSelection = 0;
                    }
                    if (up)
                    {
                        _trainingSpawnSelection--;
                        if (_trainingSpawnSelection < 0)
                            _trainingSpawnSelection = spawnCount - 1;
                    }
                    if (enter)
                    {
                        int enemyCount = 18;
                        if (_trainingSpawnSelection < enemyCount)
                        {
                            bool isFed = _trainingSpawnSelection >= 9;
                            SpawnTrainingEnemy(_trainingSpawnSelection % 9, isFed ? "Atlas Federation" : "");
                        }
                        else if (_trainingSpawnSelection < enemyCount + 5)
                            SpawnTrainingStation("empire", _trainingSpawnSelection - (enemyCount - 1));
                        else
                            SpawnTrainingStation("federation", _trainingSpawnSelection - (enemyCount + 4));
                    }
                    _prevKeyboard = keyboard;
                    return;
                }

                if (_trainingPaused)
                {
                    if (down)
                        _trainingMenuSelection++;
                    if (up)
                        _trainingMenuSelection--;
                    int max2 = 2;
                    if (_trainingMenuSelection < 0) _trainingMenuSelection = max2;
                    if (_trainingMenuSelection > max2) _trainingMenuSelection = 0;
                    if (enter)
                    {
                        if (_trainingMenuSelection == 0)
                            _trainingPaused = false;
                        else if (_trainingMenuSelection == 1)
                            _trainingInvincible = !_trainingInvincible;
                        else if (_trainingMenuSelection == 2)
                            _game.ExitTraining();
                    }
                    _prevKeyboard = keyboard;
                    return;
                }
            }

            bool w = keyboard.IsKeyDown(Keys.W) || keyboard.IsKeyDown(Keys.Up);
            bool s = keyboard.IsKeyDown(Keys.S) || keyboard.IsKeyDown(Keys.Down);
            bool a = keyboard.IsKeyDown(Keys.A) || keyboard.IsKeyDown(Keys.Left);
            bool d = keyboard.IsKeyDown(Keys.D) || keyboard.IsKeyDown(Keys.Right);
            bool boost = keyboard.IsKeyDown(Keys.LeftShift) || keyboard.IsKeyDown(Keys.RightShift);

            _player.Update(dt, w, s, a, d);

            if (boost)
            {
                _player.Velocity *= 1.02f;
                if (_player.Velocity.Length() > _player.MaxSpeed * 1.5f)
                    _player.Velocity = _player.Velocity.Normalized() * _player.MaxSpeed * 1.5f;
            }

            _player.Update(dt);

            // Fuel consumption while moving in system
            float speed = _player.Velocity.Length();
            if (!_trainingInvincible && speed > 10f && _player.Fuel > 0)
            {
                float fuelRate = 3f;
                _player.Fuel = MathF.Max(0, _player.Fuel - (speed / _player.MaxSpeed) * fuelRate * dt * _player.FuelEfficiency);
            }

            // Temperature (non-linear: slow rise far away, spikes near star)
            float dist = _player.Position.Length();
            float norm = MathHelper.Clamp(
                (dist - _star.BodyRadius) / (_systemRadius - _star.BodyRadius), 0f, 1f);
            _temperature = 1f - MathF.Pow(norm, 0.5f);

            if (!_trainingInvincible && !_exploding && (_temperature >= 1f || _player.Health <= 0))
            {
                _exploding = true;
                _explosionTimer = 0f;
                _explosionPos = _player.Position;
                _explosionAngle = _player.Angle;

                Vector2 screenCenter = new(_game.ViewWidth / 2f, _game.ViewHeight / 2f);
                _debris.Clear();
                for (int i = 0; i < 12; i++)
                {
                    float angle = RandF() * MathF.Tau;
                    float r = RandF() * 20f * ZOOM;
                    Vector2 offset = Vector2.FromAngle(angle) * r;
                    float lineLen = (3f + RandF() * 6f) * ZOOM;
                    Vector2 start = offset;
                    Vector2 end = offset + Vector2.FromAngle(RandF() * MathF.Tau) * lineLen;
                    Vector2 vel = Vector2.FromAngle(angle + (RandF() - 0.5f) * 0.5f) *
                        (80f + RandF() * 120f);
                    _debris.Add(new ExplosionDebris
                    {
                        Start = start,
                        End = end,
                        Velocity = vel,
                        AngularVelocity = (RandF() - 0.5f) * 4f,
                        CurrentAngle = RandF() * MathF.Tau,
                        Alpha = 1f
                    });
                }
                return;
            }

            // Planet proximity and collision
            _nearPlanet = false;
            for (int i = 0; i < _planets.Count; i++)
            {
                var p = _planets[i];
                float planetDist = Vector2.Distance(_player.Position, new Vector2(p.X, p.Y));
                float pr = p.BodyRadius;
                if (planetDist < pr + 30f)
                {
                    _player.Health = 0;
                    break;
                }
                if (planetDist < pr * 2.5f)
                    _nearPlanet = true;
            }

            // Lifepod: activate when quest is active, position if not yet placed
            bool rescueQuestActive = _game.Galaxy.ActiveQuests.Any(q => q.Id == "rescue_princess");
            bool alreadyHasPod = _player.QuestItems.Any(qi => qi.Id == "princess_lifepod");
            if (rescueQuestActive && !_lifepodActive && !alreadyHasPod)
            {
                PositionLifepod();
                _lifepodActive = true;
            }
            else if (!rescueQuestActive)
            {
                _lifepodActive = false;
            }

            // Lifepod pickup
            if (_lifepodActive)
            {
                float podDist = Vector2.Distance(_player.Position, new Vector2(_lifepod.X, _lifepod.Y));
                if (podDist < 100f && keyboard.IsKeyDown(Keys.E) && prevKb.IsKeyUp(Keys.E))
                {
                    _player.QuestItems.Add(new InventoryEntry { Id = "princess_lifepod", Quantity = 1 });
                    _lifepodActive = false;
                }
            }

            float distToStation = Vector2.Distance(_player.Position, new Vector2(_station.X, _station.Y));
            if (distToStation < 150f && !StationIsHostile)
            {
                if (keyboard.IsKeyDown(Keys.E) && prevKb.IsKeyUp(Keys.E))
                {
                    _docked = true;
                    _player.Fuel = _player.MaxFuel;
                    _player.Health = _player.MaxHealth;
                }
            }

            // Training mode: dock at friendly station
            if (TrainingMode && _trainingFriendlyHealth > 0)
            {
                float distToFriendly = Vector2.Distance(_player.Position, new Vector2(_trainingFriendlyStation.X, _trainingFriendlyStation.Y));
                if (distToFriendly < 150f)
                {
                    if (keyboard.IsKeyDown(Keys.E) && prevKb.IsKeyUp(Keys.E))
                    {
                        _docked = true;
                        _player.Fuel = _player.MaxFuel;
                        _player.Health = _player.MaxHealth;
                    }
                }
            }

            // Fuel exchange prompt (when out of fuel)
            if (_player.Fuel <= 0 && !_exploding && !_gameOver)
            {
                if (!_docked)
                {
                    if (keyboard.IsKeyDown(Keys.Y) && prevKb.IsKeyUp(Keys.Y) && _player.Health > _player.MaxHealth / 4 && !_trainingInvincible)
                    {
                        _player.Health -= _player.MaxHealth / 4;
                        _player.Fuel += _player.MaxFuel / 4;
                    }
                    if (keyboard.IsKeyDown(Keys.C) && prevKb.IsKeyUp(Keys.C) && _player.HasEnergyCanister)
                    {
                        if (_player.Fuel >= _player.MaxFuel)
                        {
                            _pickupMessage = "Fuel Already Full";
                            _pickupTimer = 2f;
                            _showPickupDialog = true;
                        }
                        else
                        {
                            _player.UseEnergyCanister();
                        }
                    }
                }
            }

            // Consumable usage keys (always available)
            if (!_docked && !_gameOver && !_exploding)
            {
                if (keyboard.IsKeyDown(Keys.R) && prevKb.IsKeyUp(Keys.R))
                {
                    var repairEntry = _player.Consumables.FirstOrDefault(c => c.Id.StartsWith("repair_kit_") && c.Quantity > 0);
                    if (repairEntry != null)
                    {
                        var def = _game.Galaxy.FindConsumable(repairEntry.Id);
                        if (def != null)
                            _player.UseConsumable(repairEntry.Id, def.EffectValue, def.EffectType);
                    }
                }
                if (keyboard.IsKeyDown(Keys.F) && prevKb.IsKeyUp(Keys.F))
                {
                    var fuelEntry = _player.Consumables.FirstOrDefault(c => (c.Id.StartsWith("fuel_cell") || c.Id.StartsWith("energy_canister")) && c.Quantity > 0);
                    if (fuelEntry != null)
                    {
                        var def = _game.Galaxy.FindConsumable(fuelEntry.Id);
                        if (def != null)
                            _player.UseConsumable(fuelEntry.Id, def.EffectValue, def.EffectType);
                    }
                }
            }

            // Update motion particles
            Vector2 particleVel = _player.Velocity * ZOOM * dt;
            for (int i = 0; i < _particles.Length; i++)
            {
                var p = _particles[i];
                p.Pos -= particleVel * p.SpeedMult;
                if (p.Pos.X < -10) { p.Pos.X = _game.ViewWidth + 10; p.Pos.Y = RandF() * _game.ViewHeight; p.Brightness = RandF() * 0.5f + 0.3f; p.SpeedMult = 0.5f + p.Brightness * 1.5f; }
                if (p.Pos.X > _game.ViewWidth + 10) { p.Pos.X = -10; p.Pos.Y = RandF() * _game.ViewHeight; p.Brightness = RandF() * 0.5f + 0.3f; p.SpeedMult = 0.5f + p.Brightness * 1.5f; }
                if (p.Pos.Y < -10) { p.Pos.Y = _game.ViewHeight + 10; p.Pos.X = RandF() * _game.ViewWidth; p.Brightness = RandF() * 0.5f + 0.3f; p.SpeedMult = 0.5f + p.Brightness * 1.5f; }
                if (p.Pos.Y > _game.ViewHeight + 10) { p.Pos.Y = -10; p.Pos.X = RandF() * _game.ViewWidth; p.Brightness = RandF() * 0.5f + 0.3f; p.SpeedMult = 0.5f + p.Brightness * 1.5f; }
                _particles[i] = p;
            }

            // Determine available weapons
            bool hasLaser = false;
            bool hasMissile = false;
            for (int wi = 0; wi < 2; wi++)
            {
                string? equipId = _player.GetEquippedWeapon(wi);
                if (equipId == null) continue;
                var def = _game.Galaxy.FindEquipment(equipId);
                if (def == null) continue;
                if (def.EffectType == "weapon_damage") hasLaser = true;
                if (def.EffectType == "weapon_missile") hasMissile = true;
            }
            // Clamp active weapon to available (safety for equipment changes)
            if (_activeWeapon == 1 && !hasLaser) _activeWeapon = 0;
            if (_activeWeapon == 2 && !hasMissile) _activeWeapon = 0;

            // --- Weapon switching (number keys) ---
            if (keyboard.IsKeyDown(Keys.D1) && prevKb.IsKeyUp(Keys.D1))
                _activeWeapon = 0;
            if (keyboard.IsKeyDown(Keys.D2) && prevKb.IsKeyUp(Keys.D2) && hasLaser)
                _activeWeapon = 1;
            if (keyboard.IsKeyDown(Keys.D3) && prevKb.IsKeyUp(Keys.D3) && hasMissile)
                _activeWeapon = 2;

            // --- Combat: player shooting ---
            if (!_exploding && !_docked)
            {
                _weaponCooldown -= dt;
                bool spaceHeld = keyboard.IsKeyDown(Keys.Space);
                bool spacePressed = keyboard.IsKeyDown(Keys.Space) && prevKb.IsKeyUp(Keys.Space);
                float muzzleLen = 16f;
                var muzzlePos = _player.Position + Vector2.FromAngle(_player.Angle) * muzzleLen;

                if (_activeWeapon == 0)
                {
                    // Base weapon (bullet)
                    if (spacePressed && _weaponCooldown <= 0f)
                    {
                        _weaponCooldown = 0.25f;
                        float bulletDmg = 1f;
                        for (int wi = 0; wi < 2; wi++)
                        {
                            string? eid = _player.GetEquippedWeapon(wi);
                            if (eid != null)
                            {
                                var def = _game.Galaxy.FindEquipment(eid);
                                if (def != null && def.EffectType == "weapon_bullet")
                                    bulletDmg = def.EffectValue;
                            }
                        }
                        _bullets.Add(new Bullet
                        {
                            Position = muzzlePos,
                            Velocity = Vector2.FromAngle(_player.Angle) * BULLET_SPEED + _player.Velocity * 0.5f,
                            Lifetime = BULLET_LIFETIME,
                            IsPlayerBullet = true,
                            Damage = bulletDmg
                        });
                    }
                }
                else if (_activeWeapon == 1 && hasLaser)
                {
                    // Laser beam - fires forward from ship nose
                    if (spaceHeld && _weaponCooldown <= 0f && (_trainingInvincible || _player.Fuel > 0))
                    {
                        _weaponCooldown = 0.1f;
                        if (!_trainingInvincible)
                            _player.Fuel = MathF.Max(0, _player.Fuel - 1.667f * dt * _player.FuelEfficiency);

                        float range = 18f;
                        Vector2 beamEnd = muzzlePos + Vector2.FromAngle(_player.Angle) * range;

                        _laserBeams.Add(new LaserBeam
                        {
                            Start = muzzlePos,
                            End = beamEnd,
                            Timer = 0.08f
                        });

                        // Check line-circle collision with enemies
                        float beamDmg = 1.5f;
                        for (int wi = 0; wi < 2; wi++)
                        {
                            string? eid = _player.GetEquippedWeapon(wi);
                            if (eid != null)
                            {
                                var def = _game.Galaxy.FindEquipment(eid);
                                if (def != null && def.EffectType == "weapon_damage")
                                    beamDmg = def.EffectValue;
                            }
                        }
                        for (int ei = _enemies.Count - 1; ei >= 0; ei--)
                        {
                            var e = _enemies[ei];
                            if (e.Faction == "Atlas Federation" && _system.Hostility < 3) continue;
                            if (LineCircleIntersect(muzzlePos, beamEnd, e.Position, 16f))
                            {
                                DamageEnemy(ei, beamDmg, e.Position);
                            }
                        }

                        // Laser-asteroid collision
                        for (int ai = _asteroids.Count - 1; ai >= 0; ai--)
                        {
                            var ast = _asteroids[ai];
                            if (LineCircleIntersect(muzzlePos, beamEnd, ast.Position, ast.Radius))
                            {
                                ast.Health -= beamDmg;
                                if (ast.Health <= 0f)
                                {
                                    _asteroidExplosions.Add(new EnemyExplosion
                                    {
                                        Position = ast.Position,
                                        Timer = 0f,
                                        Duration = 0.6f
                                    });
                                    GenerateLoot(ast.Position);
                                    _asteroids.RemoveAt(ai);
                                    RespawnAsteroid();
                                }
                                else
                                    _asteroids[ai] = ast;
                            }
                        }

                        // Laser-station collision
                        if (StationIsHostile)
                        {
                            Vector2 stationPos = new(_station.X, _station.Y);
                            if (LineCircleIntersect(muzzlePos, beamEnd, stationPos, _station.BodyRadius))
                            {
                                DamageStation(beamDmg);
                            }
                        }
                    }
                }
                else if (_activeWeapon == 2 && hasMissile)
                {
                    // Missile launcher
                    if (spacePressed && _weaponCooldown <= 0f)
                    {
                        if (_player.ConsumeMissileAmmo())
                        {
                            _weaponCooldown = 1.5f;

                            int targetIdx = -1;
                            float closest = 650f;
                            for (int ei = 0; ei < _enemies.Count; ei++)
                            {
                                float md = Vector2.Distance(muzzlePos, _enemies[ei].Position);
                                if (md < closest)
                                {
                                    closest = md;
                                    targetIdx = ei;
                                }
                            }

                            _missiles.Add(new Missile
                            {
                                Position = muzzlePos,
                                Velocity = Vector2.FromAngle(_player.Angle) * 200f + _player.Velocity * 0.5f,
                                Lifetime = 3f,
                                TargetIndex = targetIdx
                            });
                        }
                    }
                }
            }

            // Update laser beams
            for (int i = _laserBeams.Count - 1; i >= 0; i--)
            {
                var lb = _laserBeams[i];
                lb.Timer -= dt;
                if (lb.Timer <= 0f)
                    _laserBeams.RemoveAt(i);
                else
                    _laserBeams[i] = lb;
            }

            // Update missiles
            for (int i = _missiles.Count - 1; i >= 0; i--)
            {
                var m = _missiles[i];
                m.Lifetime -= dt;
                bool remove = false;

                if (m.Lifetime <= 0f)
                    remove = true;

                // Homing toward target (player missiles only)
                if (!remove && !m.IsEnemyMissile && m.TargetIndex >= 0 && m.TargetIndex < _enemies.Count)
                {
                    var target = _enemies[m.TargetIndex];
                    if (target.Faction == "Atlas Federation" && _system.Hostility < 3)
                    {
                        remove = true;
                    }
                    else
                    {
                        Vector2 toTarget = target.Position - m.Position;
                        float homingDist = toTarget.Length();
                        if (homingDist > 1f)
                        {
                            Vector2 dir = toTarget.Normalized();
                            m.Velocity += dir * dt * 150f;
                            float maxMSpeed = 300f;
                            if (m.Velocity.Length() > maxMSpeed)
                                m.Velocity = m.Velocity.Normalized() * maxMSpeed;
                        }

                        // Check collision with target
                        if (homingDist < 20f)
                        {
                            float missileDmg = 5f;
                            for (int wi = 0; wi < 2; wi++)
                            {
                                string? eid = _player.GetEquippedWeapon(wi);
                                if (eid != null)
                                {
                                    var def = _game.Galaxy.FindEquipment(eid);
                                    if (def != null && def.EffectType == "weapon_missile")
                                        missileDmg = def.EffectValue;
                                }
                            }
                            DamageEnemy(m.TargetIndex, missileDmg, m.Position);
                            remove = true;
                        }
                    }
                }

                // Enemy missile homing toward player
                if (!remove && m.IsEnemyMissile)
                {
                    Vector2 toPlayer = _player.Position - m.Position;
                    float homingDist = toPlayer.Length();
                    if (homingDist > 1f)
                    {
                        Vector2 dir = toPlayer.Normalized();
                        m.Velocity += dir * dt * 130f;
                        float maxMSpeed = 250f;
                        if (m.Velocity.Length() > maxMSpeed)
                            m.Velocity = m.Velocity.Normalized() * maxMSpeed;
                    }

                    // Check collision with player
                    if (!_trainingInvincible && homingDist < 22f)
                    {
                        _player.TakeDamage(5);
                        _enemyExplosions.Add(new EnemyExplosion
                        {
                            Position = m.Position,
                            Timer = 0f,
                            Duration = 0.4f
                        });
                        remove = true;
                    }

                    // Check collision with friendly station in training mode
                    if (!remove && TrainingMode && _trainingFriendlyHealth > 0)
                    {
                        Vector2 fPos = new(_trainingFriendlyStation.X, _trainingFriendlyStation.Y);
                        if (Vector2.Distance(m.Position, fPos) < _trainingFriendlyStation.BodyRadius + 5f)
                        {
                            DamageFriendlyStation(5f);
                            remove = true;
                        }
                    }
                }

                // Missile-asteroid collision
                if (!remove)
                {
                    for (int ai = _asteroids.Count - 1; ai >= 0; ai--)
                    {
                        var ast = _asteroids[ai];
                        if (Vector2.Distance(m.Position, ast.Position) < ast.Radius + 5f)
                        {
                            ast.Health -= 5f;
                            if (ast.Health <= 0f)
                            {
                                _asteroidExplosions.Add(new EnemyExplosion
                                {
                                    Position = ast.Position,
                                    Timer = 0f,
                                    Duration = 0.6f
                                });
                                GenerateLoot(ast.Position);
                                _asteroids.RemoveAt(ai);
                                RespawnAsteroid();
                            }
                            else
                                _asteroids[ai] = ast;
                            remove = true;
                            break;
                        }
                    }
                }

                // Missile-station collision
                if (!remove && StationIsHostile)
                {
                    Vector2 stationPos = new(_station.X, _station.Y);
                    if (Vector2.Distance(m.Position, stationPos) < _station.BodyRadius + 5f)
                    {
                        remove = true;
                        DamageStation(5f);
                    }
                }

                else if (!remove)
                {
                    // No target - drift
                    m.Velocity *= 0.98f;
                }

                if (!remove)
                {
                    m.Position += m.Velocity * dt;
                    _missiles[i] = m;
                }
                else
                {
                    _missiles.RemoveAt(i);
                }
            }

            // Update bullets
            for (int i = _bullets.Count - 1; i >= 0; i--)
            {
                var b = _bullets[i];
                b.Position += b.Velocity * dt;
                b.Lifetime -= dt;
                if (b.Lifetime <= 0f)
                    _bullets.RemoveAt(i);
                else
                    _bullets[i] = b;
            }

            // Update enemy explosions
            for (int i = _enemyExplosions.Count - 1; i >= 0; i--)
            {
                var ex = _enemyExplosions[i];
                ex.Timer += dt;
                if (ex.Timer >= ex.Duration)
                    _enemyExplosions.RemoveAt(i);
                else
                    _enemyExplosions[i] = ex;
            }

            // Update asteroid explosions
            for (int i = _asteroidExplosions.Count - 1; i >= 0; i--)
            {
                var ex = _asteroidExplosions[i];
                ex.Timer += dt;
                if (ex.Timer >= ex.Duration)
                    _asteroidExplosions.RemoveAt(i);
                else
                    _asteroidExplosions[i] = ex;
            }

            // Update loot lifetime
            for (int i = _asteroidLoot.Count - 1; i >= 0; i--)
            {
                var loot = _asteroidLoot[i];
                loot.Lifetime -= dt;
                if (loot.Lifetime <= 0f)
                    _asteroidLoot.RemoveAt(i);
                else
                    _asteroidLoot[i] = loot;
            }

            // Loot pickup
            if (!_docked && !_showPickupDialog)
            {
                for (int i = _asteroidLoot.Count - 1; i >= 0; i--)
                {
                    var loot = _asteroidLoot[i];
                    if (Vector2.Distance(_player.Position, loot.Position) < 40f &&
                        keyboard.IsKeyDown(Keys.E) && prevKb.IsKeyUp(Keys.E))
                    {
                        if (loot.Type == "fuel_cell")
                        {
                            var existing = _player.Consumables.FirstOrDefault(c => c.Id == "fuel_cell");
                            if (existing != null)
                                existing.Quantity++;
                            else
                                _player.Consumables.Add(new InventoryEntry { Id = "fuel_cell", Quantity = 1 });
                            _pickupMessage = "Picked up Fuel Cell (use in Inventory)";
                        }
                        else
                        {
                            var existing = _player.Resources.FirstOrDefault(r => r.Id == loot.ResourceId);
                            if (existing != null)
                                existing.Quantity++;
                            else
                                _player.Resources.Add(new InventoryEntry { Id = loot.ResourceId, Quantity = 1 });
                            var resDef = _game?.Galaxy?.FindResource(loot.ResourceId);
                            string resName = resDef?.Name ?? loot.ResourceId;
                            _pickupMessage = $"Picked up {resName}";
                        }
                        _asteroidLoot.RemoveAt(i);
                        _showPickupDialog = true;
                        _pickupTimer = 3f;
                    }
                }
            }

            // Pickup dialog timer
            if (_showPickupDialog)
            {
                _pickupTimer -= dt;
                if (_pickupTimer <= 0f)
                    _showPickupDialog = false;
            }

            // Enemy AI
            for (int i = 0; i < _enemies.Count; i++)
            {
                var e = _enemies[i];
                UpdateEnemyAI(ref e, dt);
                _enemies[i] = e;
            }

            // Station turrets
            if (StationIsHostile && !_docked && _stationHealth > 0)
            {
                UpdateStationTurrets(dt);
            }
            if (TrainingMode && !_docked && _trainingFriendlyHealth > 0)
            {
                UpdateFriendlyStationTurrets(dt);
            }

            // Bullet-enemy collisions
            for (int i = _bullets.Count - 1; i >= 0; i--)
            {
                var b = _bullets[i];
                if (!b.IsPlayerBullet) continue;
                bool hit = false;
                for (int j = _enemies.Count - 1; j >= 0; j--)
                {
                    var e = _enemies[j];
                    if (e.Faction == "Atlas Federation" && _system.Hostility < 3) continue;
                    float colRadius = 16f;
                    if (e.Type == "battleship") colRadius = 40f;
                    else if (e.Type == "destroyer") colRadius = 24f;
                    else if (e.Type == "missile_frigate") colRadius = 20f;
                    if (Vector2.Distance(b.Position, e.Position) < colRadius)
                    {
                        DamageEnemy(j, b.Damage > 0f ? b.Damage : 1f, b.Position);
                        hit = true;
                        break;
                    }
                }
                if (hit)
                    _bullets.RemoveAt(i);
            }

            // Bullet-asteroid collisions
            for (int i = _bullets.Count - 1; i >= 0; i--)
            {
                var b = _bullets[i];
                if (!b.IsPlayerBullet) continue;
                bool hit = false;
                for (int j = _asteroids.Count - 1; j >= 0; j--)
                {
                    var ast2 = _asteroids[j];
                    if (Vector2.Distance(b.Position, ast2.Position) < ast2.Radius + 5f)
                    {
                        ast2.Health -= 1f;
                        if (ast2.Health <= 0f)
                        {
                            _asteroidExplosions.Add(new EnemyExplosion
                            {
                                Position = ast2.Position,
                                Timer = 0f,
                                Duration = 0.6f
                            });
                            GenerateLoot(ast2.Position);
                            _asteroids.RemoveAt(j);
                            RespawnAsteroid();
                        }
                        else
                            _asteroids[j] = ast2;
                        hit = true;
                        break;
                    }
                }
                if (hit)
                    _bullets.RemoveAt(i);
            }

            // Bullet-station collision
            if (StationIsHostile)
            {
                Vector2 stationPos = new(_station.X, _station.Y);
                for (int i = _bullets.Count - 1; i >= 0; i--)
                {
                    var b = _bullets[i];
                    if (!b.IsPlayerBullet) continue;
                    if (Vector2.Distance(b.Position, stationPos) < _station.BodyRadius + 5f)
                    {
                        _bullets.RemoveAt(i);
                        DamageStation(1f);
                    }
                }
            }

            // Training mode: enemy bullets hit friendly station
            if (TrainingMode && _trainingFriendlyHealth > 0)
            {
                Vector2 friendlyPos = new(_trainingFriendlyStation.X, _trainingFriendlyStation.Y);
                for (int i = _bullets.Count - 1; i >= 0; i--)
                {
                    var b = _bullets[i];
                    if (b.IsPlayerBullet) continue;
                    if (Vector2.Distance(b.Position, friendlyPos) < _trainingFriendlyStation.BodyRadius + 5f)
                    {
                        _bullets.RemoveAt(i);
                        DamageFriendlyStation(2f);
                    }
                }
            }

            // Enemy bullet - player collision
            for (int i = _bullets.Count - 1; i >= 0; i--)
            {
                var b = _bullets[i];
                if (b.IsPlayerBullet) continue;
                if (!_trainingInvincible && Vector2.Distance(b.Position, _player.Position) < 22f)
                {
                    _player.TakeDamage(b.Damage > 0f ? b.Damage : 2f);
                    _bullets.RemoveAt(i);
                }
            }

            // Under-attack repel: all enemies dead → attack repelled
            if (_underAttack && _enemies.Count == 0 && !_exploding && !_docked)
            {
                _underAttack = false;
                _game?.RepelAttack(_system.Id);
                _pickupMessage = "Raiders eliminated! System defense successful!";
                _pickupTimer = 3f;
                _showPickupDialog = true;
            }

            // Station respawn timers (training + non-training)
            if (TrainingMode)
            {
                if (_trainingFriendlyRespawning)
                {
                    _trainingFriendlyRespawnTimer -= dt;
                    if (_trainingFriendlyRespawnTimer <= 0f)
                    {
                        _trainingFriendlyRespawning = false;
                        _trainingFriendlyHealth = _stationMaxHealth;
                        InitFriendlyStationDefenses();
                        _pickupMessage = "Atlas Federation Station restored!";
                        _pickupTimer = 2f;
                        _showPickupDialog = true;
                    }
                }
                if (_empireStationRespawning)
                {
                    _empireStationRespawnTimer -= dt;
                    if (_empireStationRespawnTimer <= 0f)
                    {
                        _empireStationRespawning = false;
                        _stationHealth = _stationMaxHealth;
                        _stationDefenseLevel = 5;
                        InitStationDefenses();
                        _pickupMessage = "Empire Station restored!";
                        _pickupTimer = 2f;
                        _showPickupDialog = true;
                    }
                }
            }

            if (_stationRespawning)
            {
                _stationRespawnTimer -= dt;
                if (_stationRespawnTimer <= 0f)
                {
                    _stationRespawning = false;
                    _stationHealth = _stationMaxHealth;
                    InitStationDefenses();
                    _pickupMessage = "Station defenses restored!";
                    _pickupTimer = 2f;
                    _showPickupDialog = true;
                }
            }

            if (TrainingMode)
            {
                float h = _systemRadius;
                var pos = _player.Position;
                if (pos.Length() > h)
                {
                    pos = pos.Normalized() * h;
                    _player.Velocity = Vector2.Zero;
                }
                _player.Position = pos;
                _nearEdge = false;
            }
            else
            {
                float distFromCenter = _player.Position.Length();
                _nearEdge = distFromCenter > _systemRadius * 0.85f;
                if (distFromCenter > _systemRadius)
                {
                    _game.ExitToGalaxy();
                    return;
                }
            }
        }
        else
        {
            bool escDocked = keyboard.IsKeyDown(Keys.Escape) && _prevKeyboard.IsKeyUp(Keys.Escape);
            bool leftDocked = (keyboard.IsKeyDown(Keys.Left) && _prevKeyboard.IsKeyUp(Keys.Left)) ||
                              (keyboard.IsKeyDown(Keys.A) && _prevKeyboard.IsKeyUp(Keys.A));
            bool rightDocked = (keyboard.IsKeyDown(Keys.Right) && _prevKeyboard.IsKeyUp(Keys.Right)) ||
                               (keyboard.IsKeyDown(Keys.D) && _prevKeyboard.IsKeyUp(Keys.D));
            bool downDocked = (keyboard.IsKeyDown(Keys.Down) && _prevKeyboard.IsKeyUp(Keys.Down)) ||
                              (keyboard.IsKeyDown(Keys.S) && _prevKeyboard.IsKeyUp(Keys.S));
            bool upDocked = (keyboard.IsKeyDown(Keys.Up) && _prevKeyboard.IsKeyUp(Keys.Up)) ||
                            (keyboard.IsKeyDown(Keys.W) && _prevKeyboard.IsKeyUp(Keys.W));
            bool enterDocked = keyboard.IsKeyDown(Keys.Enter) && _prevKeyboard.IsKeyUp(Keys.Enter);

            if (escDocked)
            {
                _docked = false;
                return;
            }

            // Main tab switching (always Left/Right)
            if (leftDocked)
            {
                _dockedTab = (_dockedTab - 1 + 4) % 4;
                _dockedQuestSelection = 0;
                _dockedMarketSelection = 0;
                _dockedMarketScroll = 0;
                _dockedNewsScroll = 0;
            }
            if (rightDocked)
            {
                _dockedTab = (_dockedTab + 1) % 4;
                _dockedQuestSelection = 0;
                _dockedMarketSelection = 0;
                _dockedMarketScroll = 0;
                _dockedNewsScroll = 0;
            }

            // News sub-tab switching (Tab / Shift+Tab)
            if (_dockedTab == 3)
            {
                bool tabPressed = keyboard.IsKeyDown(Keys.Tab) && _prevKeyboard.IsKeyUp(Keys.Tab);
                bool shiftTabPressed = tabPressed && (keyboard.IsKeyDown(Keys.LeftShift) || keyboard.IsKeyDown(Keys.RightShift));
                if (shiftTabPressed)
                {
                    _dockedNewsSubTab = (_dockedNewsSubTab - 1 + 4) % 4;
                    _dockedNewsScroll = 0;
                }
                else if (tabPressed)
                {
                    _dockedNewsSubTab = (_dockedNewsSubTab + 1) % 4;
                    _dockedNewsScroll = 0;
                }
            }

            bool backDocked = keyboard.IsKeyDown(Keys.Back) && _prevKeyboard.IsKeyUp(Keys.Back);

            if (_dockedTab == 0)
            {
                // Market tab: Enter=buy, Backspace=sell
                var economy = _game.Galaxy.Economy;
                var allResources = _game.Galaxy.AllResources;
                var stationResources = allResources
                    .Where(r => economy.HasResource(_system.Id, r.Id))
                    .OrderBy(r => r.Category)
                    .ThenBy(r => r.Name)
                    .ToList();
                var playerResourceIds = _player.Resources.Select(r => r.Id).ToHashSet();
                var playerOnlyResources = allResources
                    .Where(r => !economy.HasResource(_system.Id, r.Id) && playerResourceIds.Contains(r.Id))
                    .OrderBy(r => r.Category)
                    .ThenBy(r => r.Name)
                    .ToList();
                var resources = stationResources.Concat(playerOnlyResources).ToList();
                int marketItemCount = resources.Count + 2; // +1 energy canister, +1 fuel cell
                if (downDocked)
                    _dockedMarketSelection = (_dockedMarketSelection + 1) % Math.Max(1, marketItemCount);
                if (upDocked)
                    _dockedMarketSelection = (_dockedMarketSelection - 1 + Math.Max(1, marketItemCount)) % Math.Max(1, marketItemCount);

                // Sell: edge trigger or held with cooldown
                bool sellPressed = backDocked || (keyboard.IsKeyDown(Keys.Back) && _dockedSellRepeat <= 0f);
                if (sellPressed && resources.Count > 0 && _dockedMarketSelection < resources.Count)
                {
                    var res = resources[_dockedMarketSelection];
                    economy.Sell(_player, _system.Id, res.Id, 1);
                    _dockedSellRepeat = 0.12f;
                }
                if (keyboard.IsKeyDown(Keys.Back))
                    _dockedSellRepeat -= dt;
                else
                    _dockedSellRepeat = 0f;

                // Buy: edge trigger or held with cooldown
                bool buyPressed = enterDocked || (keyboard.IsKeyDown(Keys.Enter) && _dockedBuyRepeat <= 0f);
                if (buyPressed && marketItemCount > 0)
                {
                    if (_dockedMarketSelection < resources.Count)
                    {
                        var res = resources[_dockedMarketSelection];
                        if (economy.HasResource(_system.Id, res.Id))
                            economy.Buy(_player, _system.Id, res.Id, 1);
                    }
                    else
                    {
                        // Buy consumables
                        int consumableIdx = _dockedMarketSelection - resources.Count;
                        if (consumableIdx == 0) // energy canister
                        {
                            int canisterCost = 50;
                            if (_player.Credits >= canisterCost && _player.UsedCargo < _player.CargoCapacity)
                            {
                                _player.Credits -= canisterCost;
                                var existing = _player.Consumables.FirstOrDefault(c => c.Id == "energy_canister");
                                if (existing != null)
                                    existing.Quantity++;
                                else
                                    _player.Consumables.Add(new InventoryEntry { Id = "energy_canister", Quantity = 1 });
                            }
                        }
                        else if (consumableIdx == 1) // fuel cell
                        {
                            int fuelCellCost = 25;
                            if (_player.Credits >= fuelCellCost && _player.UsedCargo < _player.CargoCapacity)
                            {
                                _player.Credits -= fuelCellCost;
                                var existing = _player.Consumables.FirstOrDefault(c => c.Id == "fuel_cell");
                                if (existing != null)
                                    existing.Quantity++;
                                else
                                    _player.Consumables.Add(new InventoryEntry { Id = "fuel_cell", Quantity = 1 });
                            }
                        }
                    }
                    _dockedBuyRepeat = 0.12f;
                }
                if (keyboard.IsKeyDown(Keys.Enter))
                    _dockedBuyRepeat -= dt;
                else
                    _dockedBuyRepeat = 0f;
            }
            else if (_dockedTab == 1)
            {
                // Upgrades tab
                var upgrades = _game.GetUpgradesForSystem(_system.Id);
                var equipment = _game.Galaxy.GetAvailableEquipmentForSystem(_system.Id, _player);
                int upgradeCount = upgrades.Count;
                int equipCount = equipment.Count;
                int totalItems = upgradeCount + equipCount;
                if (totalItems > 0)
                {
                    if (downDocked)
                        _dockedUpgradeSelection = (_dockedUpgradeSelection + 1) % totalItems;
                    if (upDocked)
                        _dockedUpgradeSelection = (_dockedUpgradeSelection - 1 + totalItems) % totalItems;

                    if (enterDocked)
                    {
                        if (_dockedUpgradeSelection < upgradeCount)
                        {
                            var up = upgrades[_dockedUpgradeSelection];
                            if (_player.Credits >= up.Cost)
                            {
                                _player.Credits -= up.Cost;
                                if (!_player.OwnedUpgrades.Contains(up.Id))
                                    _player.OwnedUpgrades.Add(up.Id);
                            }
                        }
                        else
                        {
                            int ei = _dockedUpgradeSelection - upgradeCount;
                            var eq = equipment[ei];
                            if (_player.Credits >= eq.Cost)
                            {
                                _player.Credits -= eq.Cost;
                                _player.UnequippedEquipment.Add(new InventoryEntry { Id = eq.Id, Quantity = 1 });
                            }
                        }
                    }
                }
            }
            else if (_dockedTab == 2)
            {
                // Quests tab
                var available = _game.GetQuestsForSystem(_system.Id);
                var activeHere = _game.Galaxy.ActiveQuests
                    .Where(q => q.GiverSystem == _system.Id)
                    .ToList();
                var activeTarget = _game.Galaxy.ActiveQuests
                    .Where(q => q.TargetSystem == _system.Id &&
                        (q.ObjectiveType == "travel" || q.ObjectiveType == "deliver"))
                    .ToList();
                var completable = activeHere
                    .Where(q => _game.Galaxy.IsQuestObjectiveMet(q, _player))
                    .Concat(activeTarget)
                    .ToList();
                int selectableCount = available.Count + completable.Count;
                if (selectableCount > 0)
                {
                    if (downDocked)
                        _dockedQuestSelection = (_dockedQuestSelection + 1) % selectableCount;
                    if (upDocked)
                        _dockedQuestSelection = (_dockedQuestSelection - 1 + selectableCount) % selectableCount;

                    if (enterDocked)
                    {
                        if (_dockedQuestSelection < available.Count)
                        {
                            var accepted = available[_dockedQuestSelection];
                            _game.Galaxy.AcceptQuest(accepted.Id);
                            _game.ShowQuestDialogs(accepted, "on_accept");
                            _dockedQuestSelection = 0;
                        }
                        else
                        {
                            int ci = _dockedQuestSelection - available.Count;
                            if (ci < completable.Count)
                            {
                                var completed = completable[ci];
                                _game.Galaxy.CompleteQuest(completed, _player);
                                _game.ShowQuestDialogs(completed, "on_complete");
                                _dockedQuestSelection = 0;
                            }
                        }
                    }
                }
            }
            else
            {
                // News tab: scroll using up/down with key repeat
                bool downHeld = keyboard.IsKeyDown(Keys.Down) || keyboard.IsKeyDown(Keys.S);
                bool upHeld = keyboard.IsKeyDown(Keys.Up) || keyboard.IsKeyDown(Keys.W);
                bool downPressed = downHeld && _prevKeyboard.IsKeyUp(Keys.Down) && _prevKeyboard.IsKeyUp(Keys.S);
                bool upPressed = upHeld && _prevKeyboard.IsKeyUp(Keys.Up) && _prevKeyboard.IsKeyUp(Keys.W);

                if (downPressed)
                {
                    _dockedNewsScroll++;
                    _dockedNewsScrollRepeat = 0.1f;
                    _dockedNewsScrollHeld = true;
                    _dockedNewsScrollDown = true;
                }
                else if (upPressed)
                {
                    _dockedNewsScroll--;
                    _dockedNewsScrollRepeat = 0.1f;
                    _dockedNewsScrollHeld = true;
                    _dockedNewsScrollDown = false;
                }
                else if (_dockedNewsScrollHeld)
                {
                    bool stillDown = _dockedNewsScrollDown ? downHeld : upHeld;
                    _dockedNewsScrollRepeat -= (float)_game.GameTime.ElapsedGameTime.TotalSeconds;
                    if (stillDown && _dockedNewsScrollRepeat <= 0)
                    {
                        if (_dockedNewsScrollDown) _dockedNewsScroll++;
                        else _dockedNewsScroll--;
                        _dockedNewsScrollRepeat = 0.015f;
                    }
                    else if (!stillDown)
                    {
                        _dockedNewsScrollHeld = false;
                    }
                }

                if (_dockedNewsScroll < 0) _dockedNewsScroll = 0;
            }

            // Station defense upgrade (player-owned only)
            if (!TrainingMode && _system.Faction == "Independent" && _system.Station != null &&
                _stationDefenseLevel < 5 &&
                keyboard.IsKeyDown(Keys.U) && _prevKeyboard.IsKeyUp(Keys.U))
            {
                UpgradeStationDefenses();
            }
        }

        _prevKeyboard = keyboard;
    }

    private KeyboardState _prevKeyboard;
    private bool JustPressed(KeyboardState keyboard, Keys key)
    {
        bool result = keyboard.IsKeyDown(key) && _prevKeyboard.IsKeyUp(key);
        _prevKeyboard = keyboard;
        return result;
    }

    public void Draw(SpriteBatch sb, Texture2D pixel, SpriteFont font, SpriteFont titleFont)
    {
        Initialize();

        int screenW = _game.ViewWidth;
        int screenH = _game.ViewHeight;
        Vector2 center = new(screenW / 2f, screenH / 2f);
        float t = (float)_game.GameTime.TotalGameTime.TotalSeconds;

        // Ship-centered offset
        Vector2 viewOffset = _player.Position * ZOOM;

        // Deep space background (fixed on screen)
        for (int i = 0; i < 60; i++)
        {
            float bgsx = (MathF.Sin(i * 127.1f + 311.7f) * 0.5f + 0.5f) * screenW;
            float bgsy = (MathF.Sin(i * 269.5f + 183.3f) * 0.5f + 0.5f) * screenH;
            byte b = (byte)((MathF.Sin(i * 73.7f) * 0.3f + 0.7f) * 120);
            sb.Draw(pixel, new Microsoft.Xna.Framework.Rectangle((int)bgsx, (int)bgsy, 1, 1), new Color(b, b, b, b));
        }

        // Motion particles
        if (_particlesInitialized)
        {
            foreach (var pt in _particles)
            {
                byte b = (byte)(pt.Brightness * 180);
                sb.Draw(pixel, new Microsoft.Xna.Framework.Rectangle((int)pt.Pos.X, (int)pt.Pos.Y, 4, 4),
                    new Color(b, b, b, b));
            }
        }

        // Star
        float starPulse = MathF.Sin(t * 0.8f) * 0.1f + 1f;
        float starScreenX = center.X - viewOffset.X;
        float starScreenY = center.Y - viewOffset.Y;
        float starR = _star.BodyRadius * ZOOM * starPulse;
        for (int i = 6; i >= 0; i--)
        {
            float r = starR + i * 14f * ZOOM;
            float alpha = 0.02f + i * 0.07f;
            FillCircle(sb, pixel, starScreenX, starScreenY, r, _star.Color * MathF.Min(alpha, 0.5f));
        }
        FillCircle(sb, pixel, starScreenX, starScreenY, starR * 0.8f, _star.Color * 0.95f);
        DrawCircle(sb, pixel, starScreenX, starScreenY, starR * 0.8f, _star.Color);

        // Orbit rings
        foreach (var p in _planets)
        {
            DrawCircleGC(sb, pixel, starScreenX, starScreenY, p.OrbitRadius * ZOOM, new Color(60, 60, 80, 80));
        }
        if (_system.Station != null)
            DrawCircleGC(sb, pixel, starScreenX, starScreenY, _station.OrbitRadius * ZOOM, new Color(60, 60, 80, 80));

        // Planets
        foreach (var p in _planets)
        {
            float px = starScreenX + p.X * ZOOM;
            float py = starScreenY + p.Y * ZOOM;
            float pr = p.BodyRadius * ZOOM;
            for (int i = 3; i >= 0; i--)
            {
                float r = pr + i * 4f * ZOOM;
                float alpha = 0.05f + i * 0.05f;
                FillCircle(sb, pixel, px, py, r, p.Color * alpha);
            }
            FillCircle(sb, pixel, px, py, pr * 0.8f, p.Color * 0.9f);
            DrawCircle(sb, pixel, px, py, pr * 0.8f, p.Color);

            var labelSize = font.MeasureString(p.Name);
            DrawSpacedText(sb, font, p.Name,
                new Microsoft.Xna.Framework.Vector2(px - labelSize.X / 2f, py + pr + 4f),
                Color.White * 0.6f);
        }

        // Station
        float sx = 0f, sy = 0f;
        if (_stationHealth > 0)
        {
            sx = starScreenX + _station.X * ZOOM;
            sy = starScreenY + _station.Y * ZOOM;
            DrawStation(sb, pixel, font, sx, sy, _station, t);
        }

        // Training mode: render friendly station
        if (TrainingMode && _trainingFriendlyHealth > 0)
        {
            float fx = starScreenX + _trainingFriendlyStation.X * ZOOM;
            float fy = starScreenY + _trainingFriendlyStation.Y * ZOOM;
            DrawStation(sb, pixel, font, fx, fy, _trainingFriendlyStation, t, useFriendlyData: true);
        }

        // Lifepod
        if (_lifepodActive)
        {
            float lx = starScreenX + _lifepod.X * ZOOM;
            float ly = starScreenY + _lifepod.Y * ZOOM;
            float lr = 6f * ZOOM;
            float pulse = MathF.Sin(t * 3f) * 0.15f + 1f;
            for (int i = 4; i >= 0; i--)
            {
                float gr = lr * pulse + i * 4f * ZOOM;
                float ga = 0.03f + i * 0.05f;
                FillCircle(sb, pixel, lx, ly, gr, Color.Lime * ga);
            }
            FillCircle(sb, pixel, lx, ly, lr * pulse * 0.8f, Color.Lime * 0.7f);
            DrawCircle(sb, pixel, lx, ly, lr * pulse * 0.8f, Color.Lime);

            float dist = Vector2.Distance(_player.Position, new Vector2(_lifepod.X, _lifepod.Y));
            if (dist < 150f)
            {
                string prompt = "[E] Pick up lifepod";
                var promptSz = font.MeasureString(prompt);
                DrawSpacedText(sb, font, prompt,
                    new Microsoft.Xna.Framework.Vector2(lx - promptSz.X / 2f, ly + lr * pulse + 10f),
                    Color.White * (0.7f + MathF.Sin(t * 4f) * 0.3f));
            }
        }

        // Asteroids
        foreach (var ast in _asteroids)
        {
            float ax = center.X + (ast.Position.X - _player.Position.X) * ZOOM;
            float ay = center.Y + (ast.Position.Y - _player.Position.Y) * ZOOM;
            float asr = ast.Radius * ZOOM;
            float pulse = MathF.Sin(t * 0.5f + ast.Seed) * 0.1f + 0.9f;

            // Compute irregular polygon vertices
            int segs = 10;
            var verts = new Vector2[segs];
            for (int si = 0; si < segs; si++)
            {
                float aAngle = MathF.PI * 2f * si / segs + ast.Seed * 0.01f;
                float variation = 0.8f + MathF.Sin(ast.Seed + aAngle * 3f) * 0.2f;
                float r = asr * variation * pulse;
                verts[si] = new Vector2(
                    ax + MathF.Cos(aAngle) * r,
                    ay + MathF.Sin(aAngle) * r);
            }

            // Fill with triangles from center
            Color fillColor = new Color(110, 90, 70);
            for (int si = 0; si < segs; si++)
            {
                int next = (si + 1) % segs;
                FillTriangle(sb, pixel, ax, ay,
                    verts[si].X, verts[si].Y,
                    verts[next].X, verts[next].Y, fillColor);
            }

            // Interior crack lines
            int crackCount = 3 + (ast.Seed & 3);
            for (int ci = 0; ci < crackCount; ci++)
            {
                float cAngle = MathF.PI * 2f * ci / crackCount + ast.Seed * 0.1f;
                float innerR = asr * (0.15f + ((ast.Seed + ci * 7) % 7) * 0.06f);
                float outerR = asr * (0.4f + ((ast.Seed + ci * 13) % 7) * 0.08f);
                float bend = 0.2f + ((ast.Seed + ci * 3) % 5) * 0.15f;
                float x1 = ax + MathF.Cos(cAngle) * innerR;
                float y1 = ay + MathF.Sin(cAngle) * innerR;
                float mx = ax + MathF.Cos(cAngle + bend) * (innerR + outerR) * 0.5f;
                float my = ay + MathF.Sin(cAngle + bend) * (innerR + outerR) * 0.5f;
                float x2 = ax + MathF.Cos(cAngle + bend * 0.7f) * outerR;
                float y2 = ay + MathF.Sin(cAngle + bend * 0.7f) * outerR;
                Color crackColor = new Color(55, 45, 35);
                DrawLine(sb, pixel, x1, y1, mx, my, crackColor);
                DrawLine(sb, pixel, mx, my, x2, y2, crackColor);
            }

            // Opaque outline
            for (int si = 0; si < segs; si++)
            {
                int next = (si + 1) % segs;
                DrawLine(sb, pixel, verts[si].X, verts[si].Y, verts[next].X, verts[next].Y, new Color(160, 140, 110));
            }
        }

        // Bullets
        foreach (var b in _bullets)
        {
            float bx = center.X + (b.Position.X - _player.Position.X) * ZOOM;
            float by = center.Y + (b.Position.Y - _player.Position.Y) * ZOOM;
            Color bColor = b.IsPlayerBullet ? Color.Cyan : Color.Orange;
            sb.Draw(pixel, new Microsoft.Xna.Framework.Rectangle((int)bx, (int)by, 4, 4), bColor * 0.9f);
        }

        // Laser beams (thick red lines)
        foreach (var lb in _laserBeams)
        {
            float lsx = center.X + (lb.Start.X - _player.Position.X) * ZOOM;
            float lsy = center.Y + (lb.Start.Y - _player.Position.Y) * ZOOM;
            float lex = center.X + (lb.End.X - _player.Position.X) * ZOOM;
            float ley = center.Y + (lb.End.Y - _player.Position.Y) * ZOOM;
            float alpha = lb.Timer / 0.08f;
            Color beamColor = new Color(255, 40, 0) * alpha;
            // Thick red beam: three parallel lines
            float beamW = 3f;
            float bdx = lex - lsx;
            float bdy = ley - lsy;
            float blen = MathF.Sqrt(bdx * bdx + bdy * bdy);
            if (blen > 1f)
            {
                float bnx = bdx / blen;
                float bny = bdy / blen;
                float pnx = -bny;
                float pny = bnx;
                for (int li = -1; li <= 1; li++)
                {
                    float ox = pnx * li * beamW;
                    float oy = pny * li * beamW;
                    DrawLine(sb, pixel, lsx + ox, lsy + oy, lex + ox, ley + oy, beamColor);
                }
            }
        }

        // Missiles
        foreach (var m in _missiles)
        {
            float mx = center.X + (m.Position.X - _player.Position.X) * ZOOM;
            float my = center.Y + (m.Position.Y - _player.Position.Y) * ZOOM;
            float angle = MathF.Atan2(m.Velocity.Y, m.Velocity.X);
            float cosA = MathF.Cos(angle);
            float sinA = MathF.Sin(angle);
            float fx = cosA, fy = sinA;
            float px = -sinA, py = cosA;

            // Body rectangle dimensions (screen pixels)
            float bodyLen = 14f, bodyW = 3f, halfW = bodyW / 2f;

            // Body corners
            float bcx = mx - fx * bodyLen * 0.5f;
            float bcy = my - fy * bodyLen * 0.5f;
            float bxl = bcx - px * halfW, byl = bcy - py * halfW;
            float bxr = bcx + px * halfW, byr = bcy + py * halfW;
            float fcx = mx + fx * bodyLen * 0.5f;
            float fcy = my + fy * bodyLen * 0.5f;
            float fxl = fcx - px * halfW, fyl = fcy - py * halfW;
            float fxr = fcx + px * halfW, fyr = fcy + py * halfW;

            // Body (left edge, right edge, back edge)
            DrawLine(sb, pixel, bxl, byl, fxl, fyl, Color.Silver);
            DrawLine(sb, pixel, bxr, byr, fxr, fyr, Color.Silver);
            DrawLine(sb, pixel, bxl, byl, bxr, byr, Color.Silver);

            // Nose cone (red triangle at front)
            float noseLen = 5f;
            float ntx = fcx + fx * noseLen;
            float nty = fcy + fy * noseLen;
            DrawLine(sb, pixel, fxl, fyl, ntx, nty, Color.Red);
            DrawLine(sb, pixel, fxr, fyr, ntx, nty, Color.Red);

            // Flame (orange flickering at back)
            float flameLen = 6f + (float)Random.Shared.NextDouble() * 4f;
            float flx = bcx - fx * flameLen;
            float fly = bcy - fy * flameLen;
            DrawLine(sb, pixel, bxl, byl, flx, fly, Color.Orange);
            DrawLine(sb, pixel, bxr, byr, flx, fly, Color.Orange);
        }

        // Enemy explosions
        foreach (var ex in _enemyExplosions)
        {
            float progress = ex.Timer / ex.Duration;
            float alpha = 1f - progress;
            float rx = center.X + (ex.Position.X - _player.Position.X) * ZOOM;
            float ry = center.Y + (ex.Position.Y - _player.Position.Y) * ZOOM;
            for (int i = 4; i >= 0; i--)
            {
                float r = (4f + i * 5f + progress * 30f) * ZOOM;
                float a = (0.05f + i * 0.06f) * alpha;
                FillCircle(sb, pixel, rx, ry, r, new Color(255, 140, 0) * a);
            }
        }

        // Asteroid explosions
        foreach (var ex in _asteroidExplosions)
        {
            float progress = ex.Timer / ex.Duration;
            float alpha = 1f - progress;
            float rx = center.X + (ex.Position.X - _player.Position.X) * ZOOM;
            float ry = center.Y + (ex.Position.Y - _player.Position.Y) * ZOOM;
            for (int i = 4; i >= 0; i--)
            {
                float r = (4f + i * 5f + progress * 25f) * ZOOM;
                float a = (0.04f + i * 0.05f) * alpha;
                FillCircle(sb, pixel, rx, ry, r, new Color(180, 140, 80) * a);
            }
        }

        // Player ship (always at center)
        DrawShip(sb, pixel, center, t, 1f, Color.White, _player.Angle, _player.Velocity);

        // Enemies
        foreach (var e in _enemies)
        {
            float ex = center.X + (e.Position.X - _player.Position.X) * ZOOM;
            float ey = center.Y + (e.Position.Y - _player.Position.Y) * ZOOM;
            Vector2 ePos = new(ex, ey);
            DrawEnemyShip(sb, pixel, ePos, t, e);
        }

        // Asteroid loot
        foreach (var loot in _asteroidLoot)
        {
            float lx = center.X + (loot.Position.X - _player.Position.X) * ZOOM;
            float ly = center.Y + (loot.Position.Y - _player.Position.Y) * ZOOM;
            float pulse = MathF.Sin(t * 3f) * 0.3f + 0.7f;
            if (loot.Type == "fuel_cell")
            {
                // Square
                int size = (int)(24 * pulse);
                sb.Draw(pixel, new Microsoft.Xna.Framework.Rectangle((int)lx - size / 2, (int)ly - size / 2, size, size), Color.Yellow * 0.9f);
            }
            else
            {
                // Circle (resource orb)
                float r = 18f * pulse;
                FillCircle(sb, pixel, lx, ly, r, Color.Cyan * 0.8f);
            }
        }

        // Pickup notification dialog
        if (_showPickupDialog)
        {
            float pw = 400f;
            float ph = 60f;
            float px2 = (screenW - pw) / 2f;
            float py2 = screenH * 0.7f;
            sb.Draw(pixel, new Microsoft.Xna.Framework.Rectangle((int)px2, (int)py2, (int)pw, (int)ph), new Color(0, 0, 0, 200));
            var msgSz = font.MeasureString(_pickupMessage);
            DrawSpacedText(sb, font, _pickupMessage,
                new Microsoft.Xna.Framework.Vector2(px2 + (pw - msgSz.X) / 2f, py2 + (ph - msgSz.Y) / 2f),
                Color.Lime);
        }

        // Docked menu / HUD
        if (_docked)
            DrawDockedMenu(sb, pixel, font, titleFont, screenW, screenH, t);
        else
        {
            DrawSystemHUD(sb, font, screenW, screenH, center, sx, sy);

            // Weapon indicator (top of screen)
            bool hasLaser = false, hasMissile = false;
            for (int wi = 0; wi < 2; wi++)
            {
                string? eid = _player.GetEquippedWeapon(wi);
                if (eid == null) continue;
                var edef = _game.Galaxy.FindEquipment(eid);
                if (edef == null) continue;
                if (edef.EffectType == "weapon_damage") hasLaser = true;
                if (edef.EffectType == "weapon_missile") hasMissile = true;
            }

            string[] wepNames = { "Base", hasLaser ? "Laser" : "", hasMissile ? "Missile" : "" };
            float wepX = 20f;
            float wepY = 40f;
            for (int i = 0; i < 3; i++)
            {
                if (i == 1 && !hasLaser) continue;
                if (i == 2 && !hasMissile) continue;
                string label = $"[{i + 1}] {wepNames[i]}";
                var sz = font.MeasureString(label);
                Color wepColor = i == _activeWeapon ? Color.White : Color.Gray * 0.7f;
                if (i == _activeWeapon)
                {
                    sb.Draw(pixel,
                        new Microsoft.Xna.Framework.Rectangle((int)wepX - 4, (int)wepY - 2,
                            (int)sz.X + 8, (int)sz.Y + 4),
                        new Color(40, 60, 90, 200));
                    DrawRect(sb, pixel, wepX - 4, wepY - 2, sz.X + 8, sz.Y + 4, Color.LightBlue);
                }
                DrawSpacedText(sb, font, label,
                    new Microsoft.Xna.Framework.Vector2(wepX, wepY), wepColor);
                wepX += sz.X + 12;
            }
        }

        // Waypoint arrow
        if (!_docked)
            DrawWaypointArrow(sb, pixel, font, center);

        // Combat target arrow (red, only when enemies exist)
        if (!_docked && _enemies.Count > 0 && _targetIndex >= 0 && _targetIndex < _enemies.Count)
            DrawCombatArrow(sb, pixel, font, center);

        // Mini-map
        DrawMiniMap(sb, pixel, font, screenW, screenH);

        // Exit warning
        if (_nearEdge && !_docked && !_exploding && !_gameOver)
        {
            string msg = "Exiting System";
            var msgSize = titleFont.MeasureString(msg);
            float msgX = (screenW - msgSize.X) / 2f;
            float msgY = screenH * 0.3f;

            byte flash = (byte)((MathF.Sin(t * 3f) * 0.3f + 0.5f) * 255);
            Color c = new Color(255, 0, 0, (int)flash);

            DrawSpacedText(sb, titleFont, msg,
                new Microsoft.Xna.Framework.Vector2(msgX, msgY), c);
        }

        // Collision warning
        if (_nearPlanet && !_docked && !_exploding && !_gameOver)
        {
            string msg = "Collision Warning";
            var msgSize = titleFont.MeasureString(msg);
            float msgX = (screenW - msgSize.X) / 2f;
            float msgY = screenH * 0.38f;

            byte flash = (byte)((MathF.Sin(t * 4f) * 0.3f + 0.5f) * 255);
            DrawSpacedText(sb, titleFont, msg,
                new Microsoft.Xna.Framework.Vector2(msgX, msgY),
                new Color(255, 200, 0, (int)flash));
        }

        // Temperature bar (bottom-left)
        if (!_exploding && !_gameOver && !_docked)
        {
            int barX = 40;
            int barY = screenH - 200;
            int barW = 14;
            int barH = 120;

            var tempLabel = font.MeasureString("TEMP");
            DrawSpacedText(sb, font, "TEMP",
                new Microsoft.Xna.Framework.Vector2(barX + barW / 2 - tempLabel.X / 2, barY - 22), Color.Gray * 0.7f);

            // Background
            sb.Draw(pixel, new Microsoft.Xna.Framework.Rectangle(barX, barY, barW, barH),
                new Color(30, 0, 0, 180));
            DrawRect(sb, pixel, barX, barY, barW, barH, Color.Gray * 0.3f);

            // Fill
            int fillH = (int)(barH * _temperature);
            int fillY = barY + barH - fillH;
            Color barColor = _temperature > 0.75f
                ? new Color(255, 0, 0, 220)
                : new Color(200, Math.Max(0, (int)(255 - _temperature * 255)), 0, 200);
            sb.Draw(pixel, new Microsoft.Xna.Framework.Rectangle(barX + 2, fillY, barW - 4, fillH),
                barColor);

            // Percentage label
            int pct = (int)(_temperature * 100);
            string pctStr = $"{pct}%";
            var pctSize = font.MeasureString(pctStr);
            DrawSpacedText(sb, font, pctStr,
                new Microsoft.Xna.Framework.Vector2(barX + barW / 2 - pctSize.X / 2, barY + barH + 4),
                Color.Gray * 0.7f);

            // Health bar (to the right of temp bar)
            int hbX = barX + barW + 28;
            int hbY = barY;
            int hbW = 14;
            int hbH = barH;

            var hpLabel = font.MeasureString("HP");
            DrawSpacedText(sb, font, "HP",
                new Microsoft.Xna.Framework.Vector2(hbX + hbW / 2 - hpLabel.X / 2, hbY - 22), Color.Gray * 0.7f);

            sb.Draw(pixel, new Microsoft.Xna.Framework.Rectangle(hbX, hbY, hbW, hbH),
                new Color(0, 20, 0, 180));
            DrawRect(sb, pixel, hbX, hbY, hbW, hbH, Color.Gray * 0.3f);

            float healthPct = (float)_player.Health / _player.MaxHealth;
            int hFillH = (int)(hbH * healthPct);
            int hFillY = hbY + hbH - hFillH;
            sb.Draw(pixel, new Microsoft.Xna.Framework.Rectangle(hbX + 2, hFillY, hbW - 4, hFillH),
                new Color(0, 200, 0, 220));

            string hpStr = $"{_player.Health}";
            var hpSize = font.MeasureString(hpStr);
            DrawSpacedText(sb, font, hpStr,
                new Microsoft.Xna.Framework.Vector2(hbX + hbW / 2 - hpSize.X / 2, hbY + hbH + 4),
                Color.Gray * 0.7f);

            // Fuel bar (to the right of HP bar)
            int fbX = hbX + hbW + 28;
            int fbY = barY;
            int fbW = 14;
            int fbH = barH;

            var fuelLabel = font.MeasureString("FUL");
            DrawSpacedText(sb, font, "FUL",
                new Microsoft.Xna.Framework.Vector2(fbX + fbW / 2 - fuelLabel.X / 2, fbY - 22), Color.Gray * 0.7f);

            sb.Draw(pixel, new Microsoft.Xna.Framework.Rectangle(fbX, fbY, fbW, fbH),
                new Color(20, 20, 0, 180));
            DrawRect(sb, pixel, fbX, fbY, fbW, fbH, Color.Gray * 0.3f);

            float fuelPct = _player.Fuel / _player.MaxFuel;
            int fFillH = (int)(fbH * fuelPct);
            int fFillY = fbY + fbH - fFillH;
            sb.Draw(pixel, new Microsoft.Xna.Framework.Rectangle(fbX + 2, fFillY, fbW - 4, fFillH),
                new Color(200, 200, 0, 220));

            string fuelStr = $"{(int)_player.Fuel}";
            var fuelSize = font.MeasureString(fuelStr);
            DrawSpacedText(sb, font, fuelStr,
                new Microsoft.Xna.Framework.Vector2(fbX + fbW / 2 - fuelSize.X / 2, fbY + fbH + 4),
                Color.Gray * 0.7f);

            // Missile count indicator (to the right of fuel bar)
            int missX = fbX + fbW + 28;
            bool hasMissileLauncher = false;
            string? w0 = _player.GetEquippedWeapon(0);
            string? w1 = _player.GetEquippedWeapon(1);
            if (w0 != null)
            {
                var def0 = _game.Galaxy.FindEquipment(w0);
                if (def0 != null && def0.EffectType == "weapon_missile") hasMissileLauncher = true;
            }
            if (!hasMissileLauncher && w1 != null)
            {
                var def1 = _game.Galaxy.FindEquipment(w1);
                if (def1 != null && def1.EffectType == "weapon_missile") hasMissileLauncher = true;
            }
            if (hasMissileLauncher)
            {
                var msLabel = font.MeasureString("MSL");
                DrawSpacedText(sb, font, "MSL",
                    new Microsoft.Xna.Framework.Vector2(missX + fbW / 2 - msLabel.X / 2, fbY - 22), Color.Gray * 0.7f);

                int missCount = _player.MissileAmmoCount;
                string missStr = $"{missCount}";
                var missSize = font.MeasureString(missStr);
                Color missColor = missCount > 0 ? Color.Orange : Color.Gray * 0.4f;
                DrawSpacedText(sb, font, missStr,
                    new Microsoft.Xna.Framework.Vector2(missX + fbW / 2 - missSize.X / 2, fbY + fbH / 2 - missSize.Y / 2),
                    missColor);
            }
        }

        // Overheating warning
        if (_temperature >= 0.75f && !_exploding && !_gameOver && !_docked)
        {
            string msg = "OVERHEATING";
            var msgSize = titleFont.MeasureString(msg);
            float msgX = (screenW - msgSize.X) / 2f;

            byte flash = (byte)((MathF.Sin(t * 6f) * 0.4f + 0.6f) * 255);
            Color c = new Color(255, 80, 0, (int)flash);

            // Background bar for readability
            float bgW = msgSize.X + 20;
            float bgH = msgSize.Y + 10;
            sb.Draw(pixel, new Microsoft.Xna.Framework.Rectangle(
                (int)(msgX - 10), (int)(screenH * 0.5f - 5), (int)bgW, (int)bgH),
                new Color(20, 0, 0, 180));

            DrawSpacedText(sb, titleFont, msg,
                new Microsoft.Xna.Framework.Vector2(msgX, screenH * 0.5f), c);
        }

        // Explosion debris (screen-centered)
        if (_exploding)
        {
            for (int i = 0; i < _debris.Count; i++)
            {
                var d = _debris[i];
                Color debrisColor = new Color(255, 180, 50, (int)(d.Alpha * 255));
                DrawLine(sb, pixel,
                    center.X + d.Start.X, center.Y + d.Start.Y,
                    center.X + d.End.X, center.Y + d.End.Y,
                    debrisColor);
            }
        }

        // Game Over
        if (_gameOver)
        {
            if (!TrainingMode)
            {
                // Dim overlay
                sb.Draw(pixel, new Microsoft.Xna.Framework.Rectangle(0, 0, screenW, screenH),
                    new Color(0, 0, 0, 200));

                string msg = "GAME OVER";
                var msgSize = titleFont.MeasureString(msg);
                float msgX = (screenW - msgSize.X) / 2f;
                float msgY = screenH * 0.35f;
                DrawSpacedText(sb, titleFont, msg,
                    new Microsoft.Xna.Framework.Vector2(msgX, msgY), Color.Red);

                string sub = "Press ENTER to continue";
                var subSize = font.MeasureString(sub);
                float subX = (screenW - subSize.X) / 2f;
                DrawSpacedText(sb, font, sub,
                    new Microsoft.Xna.Framework.Vector2(subX, msgY + 60), Color.Gray);
            }
        }

        // Fuel exchange prompt (out of fuel)
        if (_player.Fuel <= 0 && !_exploding && !_gameOver && !_docked)
        {
            sb.Draw(pixel, new Microsoft.Xna.Framework.Rectangle(0, 0, screenW, screenH),
                new Color(0, 0, 0, 140));

            string msg = "OUT OF FUEL";
            var msgSize = titleFont.MeasureString(msg);
            float msgX = (screenW - msgSize.X) / 2f;
            DrawSpacedText(sb, titleFont, msg,
                new Microsoft.Xna.Framework.Vector2(msgX, screenH * 0.32f), new Color(255, 200, 0));

            float optY = screenH * 0.38f + 30;
            if (_player.Health > _player.MaxHealth / 4)
            {
                string sub = "[Y] Exchange 1/4 HP for 1/4 fuel";
                var subSize = font.MeasureString(sub);
                float subX = (screenW - subSize.X) / 2f;
                DrawSpacedText(sb, font, sub,
                    new Microsoft.Xna.Framework.Vector2(subX, optY), Color.White);
                optY += 28;
            }

            if (_player.HasEnergyCanister)
            {
                string sub2 = "[C] Use Energy Canister";
                var sub2Size = font.MeasureString(sub2);
                float sub2X = (screenW - sub2Size.X) / 2f;
                DrawSpacedText(sb, font, sub2,
                    new Microsoft.Xna.Framework.Vector2(sub2X, optY), Color.Orange);
            }
        }

        // Training mode label
        if (TrainingMode && !_docked && !_exploding && !_gameOver)
        {
            string label = "TRAINING MODE";
            var labelSz = font.MeasureString(label);
            float lx = screenW - labelSz.X - 20f;
            float ly = 20f;
            DrawSpacedText(sb, font, label,
                new Microsoft.Xna.Framework.Vector2(lx, ly), Color.Yellow * 0.7f);

            string tip = "[F1] Spawn Menu  [ESC] Pause";
            var tipSz = font.MeasureString(tip);
            DrawSpacedText(sb, font, tip,
                new Microsoft.Xna.Framework.Vector2(lx + labelSz.X - tipSz.X, ly + 22f), Color.Gray * 0.6f);
        }

        // Training pause overlay
        if (_trainingPaused)
        {
            sb.Draw(pixel, new Microsoft.Xna.Framework.Rectangle(0, 0, screenW, screenH),
                new Color(0, 0, 0, 180));

            string title = "TRAINING PAUSED";
            var tSz = titleFont.MeasureString(title);
            float tX = (screenW - tSz.X) / 2f;
            float tY = screenH * 0.3f;
            DrawSpacedText(sb, titleFont, title,
                new Microsoft.Xna.Framework.Vector2(tX, tY), Color.Yellow);

            string[] opts = { "Resume", $"Invincibility: {(_trainingInvincible ? "ON" : "OFF")}", "End Training" };
            for (int i = 0; i < opts.Length; i++)
            {
                bool sel = i == _trainingMenuSelection;
                Color c = sel ? Color.Yellow : Color.Gray;
                var oSz = font.MeasureString(opts[i]);
                float oX = (screenW - oSz.X) / 2f;
                float oY = tY + 80f + i * 36f;
                if (sel)
                    DrawSpacedText(sb, titleFont, "> " + opts[i],
                        new Microsoft.Xna.Framework.Vector2(oX - oSz.X, oY), c);
                else
                    DrawSpacedText(sb, font, opts[i],
                        new Microsoft.Xna.Framework.Vector2(oX, oY), c);
            }
        }

        // Training spawn menu overlay
        if (_showEnemyList)
        {
            sb.Draw(pixel, new Microsoft.Xna.Framework.Rectangle(0, 0, screenW, screenH),
                new Color(0, 0, 0, 160));

            string hdr = "SPAWN MENU";
            var hSz = titleFont.MeasureString(hdr);
            float hX = (screenW - hSz.X) / 2f;
            float hY = screenH * 0.12f;
            DrawSpacedText(sb, titleFont, hdr,
                new Microsoft.Xna.Framework.Vector2(hX, hY), Color.Yellow);

            // Build display list with section headers
            var lines = new List<(string text, bool isHeader, int? itemIndex)>();
            lines.Add(("-- Trigor Empire Ships --", true, null));
            string[] trigorNames = { "Pirate Scout", "Raider Fighter", "Mercenary Gunship", "Syndicate Cruiser", "Void Dreadnought",
                                     "Interceptor", "Missile Frigate", "Destroyer", "Battleship" };
            int trigorCount = trigorNames.Length;
            for (int i = 0; i < trigorCount; i++)
                lines.Add((trigorNames[i], false, i));
            lines.Add(("-- Atlas Federation Ships --", true, null));
            string[] fedNames = { "Fed. Scout", "Fed. Fighter", "Fed. Gunship", "Fed. Cruiser", "Fed. Dreadnought",
                                  "Fed. Interceptor", "Fed. Missile Frigate", "Fed. Destroyer", "Fed. Battleship" };
            for (int i = 0; i < fedNames.Length; i++)
                lines.Add((fedNames[i], false, trigorCount + i));
            int enemyCount = trigorCount + fedNames.Length;
            lines.Add(("-- Stations --", true, null));
            string[] stationNames = { "Empire Station L1", "Empire Station L2", "Empire Station L3", "Empire Station L4", "Empire Station L5",
                                      "Atlas Federation Station L1", "Atlas Federation Station L2", "Atlas Federation Station L3", "Atlas Federation Station L4", "Atlas Federation Station L5" };
            for (int i = 0; i < stationNames.Length; i++)
                lines.Add((stationNames[i], false, enemyCount + i));

            float startY = hY + 50f;
            for (int i = 0; i < lines.Count; i++)
            {
                var (text, isHeader, itemIndex) = lines[i];
                float y = startY + i * 24f;
                if (isHeader)
                {
                    var sSz = font.MeasureString(text);
                    float sX = (screenW - sSz.X) / 2f;
                    DrawSpacedText(sb, font, text,
                        new Microsoft.Xna.Framework.Vector2(sX, y), Color.Gold);
                }
                else
                {
                    bool selected = itemIndex.HasValue && itemIndex.Value == _trainingSpawnSelection;
                    Color c = selected ? Color.White : Color.Gray * 0.8f;
                    string prefix = selected ? "> " : "  ";
                    float indent = 60f;
                    DrawSpacedText(sb, font, prefix + text,
                        new Microsoft.Xna.Framework.Vector2(indent, y), c);
                }
            }

            string close = "[F1] or [ESC] to close  [Enter] to spawn";
            var cSz = font.MeasureString(close);
            float cX = (screenW - cSz.X) / 2f;
            DrawSpacedText(sb, font, close,
                new Microsoft.Xna.Framework.Vector2(cX, screenH * 0.85f), Color.White * 0.5f);
        }
    }

    private void DrawStation(SpriteBatch sb, Texture2D pixel, SpriteFont font,
        float sx, float sy, Body station, float t, bool useFriendlyData = false)
    {
        float pulse = MathF.Sin(t * 2f) * 0.05f + 1f;
        float r = station.BodyRadius * ZOOM * pulse;

        bool isEnemy = useFriendlyData ? false : (StationIsHostile || _underAttack);
        Color baseColor = isEnemy ? new Color(200, 60, 60) : Color.LightBlue;
        Color glowColor = isEnemy ? new Color(255, 80, 80) : Color.Cyan;
        Color dimGlow = isEnemy ? new Color(180, 40, 40) : Color.LightBlue;

        var turrets = useFriendlyData ? _friendlyTurrets : _stationTurrets;
        var ringSections = useFriendlyData ? _friendlyRingSections : _ringSections;
        bool hasShield = useFriendlyData ? _friendlyHasShield : _stationHasShield;
        float stationHealth = useFriendlyData ? _trainingFriendlyHealth : _stationHealth;

        for (int i = 4; i >= 0; i--)
        {
            float gr = r + i * 5f * ZOOM;
            float ga = 0.03f + i * 0.04f;
            FillCircle(sb, pixel, sx, sy, gr, dimGlow * ga);
        }

        // Main body
        FillCircle(sb, pixel, sx, sy, r, baseColor * 0.6f);
        DrawCircle(sb, pixel, sx, sy, r, glowColor);

        // Antenna
        float antH = 10f * ZOOM * pulse;
        DrawLine(sb, pixel, sx, sy - r, sx, sy - r - antH, glowColor);
        DrawLine(sb, pixel, sx - 4, sy - r - antH, sx + 4, sy - r - antH, glowColor);

        // Ring sections (defense levels 2-5)
        float ringR = r + 8f * ZOOM;
        float ringW = 6f * ZOOM;
        int arcSteps = 8;
        foreach (var rs in ringSections)
        {
            if (!rs.Active) continue;
            float segAngle = MathF.PI * 2f / 4f;
            float startA = rs.Angle - segAngle / 2f;
            float endA = rs.Angle + segAngle / 2f;
            for (int s = 0; s < arcSteps; s++)
            {
                float t1 = startA + (endA - startA) * s / arcSteps;
                float t2 = startA + (endA - startA) * (s + 1) / arcSteps;
                float x1 = sx + MathF.Cos(t1) * ringR;
                float y1 = sy + MathF.Sin(t1) * ringR;
                float x2 = sx + MathF.Cos(t2) * ringR;
                float y2 = sy + MathF.Sin(t2) * ringR;
                DrawLine(sb, pixel, x1, y1, x2, y2, new Color(180, 120, 60));
            }
            // Inner glow line
            float innerR = ringR - ringW * 0.3f;
            for (int s = 0; s < arcSteps; s++)
            {
                float t1 = startA + (endA - startA) * s / arcSteps;
                float t2 = startA + (endA - startA) * (s + 1) / arcSteps;
                float x1 = sx + MathF.Cos(t1) * innerR;
                float y1 = sy + MathF.Sin(t1) * innerR;
                float x2 = sx + MathF.Cos(t2) * innerR;
                float y2 = sy + MathF.Sin(t2) * innerR;
                DrawLine(sb, pixel, x1, y1, x2, y2, new Color(255, 200, 100) * 0.5f);
            }

            // Ring section health bar
            float midAngle = (startA + endA) / 2f;
            float barX = sx + MathF.Cos(midAngle) * (ringR + 12f * ZOOM);
            float barY = sy + MathF.Sin(midAngle) * (ringR + 12f * ZOOM);
            int barW = (int)(40f * ZOOM);
            int barH = (int)(4f * ZOOM);
            float hpPct = rs.Health / RING_SECTION_MAX_HEALTH;
            // Background
            sb.Draw(pixel, new Microsoft.Xna.Framework.Rectangle((int)(barX - barW / 2f), (int)barY, barW, barH), new Color(40, 0, 0, 180));
            // Fill
            Color barColor = hpPct > 0.5f ? Color.Yellow : (hpPct > 0.25f ? new Color(255, 140, 0) : Color.Red);
            sb.Draw(pixel, new Microsoft.Xna.Framework.Rectangle((int)(barX - barW / 2f), (int)barY, (int)(barW * hpPct), barH), barColor);
        }

        // Shield (level 5, all sections active)
        if (hasShield)
        {
            float shieldR = ringR + 6f * ZOOM;
            float shieldPulse = MathF.Sin(t * 3f) * 0.03f + 0.12f;
            DrawCircle(sb, pixel, sx, sy, shieldR, new Color(80, 180, 255) * shieldPulse);
            DrawCircle(sb, pixel, sx, sy, shieldR + 2f, new Color(60, 140, 255) * shieldPulse * 0.5f);
        }

        // Turret dots on station body
        foreach (var turret in turrets)
        {
            if (!turret.Active) continue;
            float tx = sx + turret.Position.X;
            float ty = sy + turret.Position.Y;
            FillCircle(sb, pixel, tx, ty, 3f * ZOOM, new Color(180, 80, 80));
            DrawCircle(sb, pixel, tx, ty, 3f * ZOOM, new Color(255, 120, 120));
        }

        // Label
        var label = station.Name;
        var labelSize = font.MeasureString(label);
        DrawSpacedText(sb, font, label,
            new Microsoft.Xna.Framework.Vector2(sx - labelSize.X / 2f, sy + r + 4f),
            glowColor);

        // Health bar (enemy stations) or dock prompt (friendly)
        if (isEnemy || useFriendlyData)
        {
            // Health bar above station
            int barW = (int)(r * 1.5f);
            int barH = 4;
            float barX = sx - barW / 2f;
            float barY = sy - r - antH - 14f;
            float hpPct = stationHealth / _stationMaxHealth;
            Color bg = useFriendlyData ? new Color(0, 0, 40, 180) : new Color(40, 0, 0, 180);
            Color fg = useFriendlyData ? Color.Cyan : Color.Red;
            sb.Draw(pixel, new Microsoft.Xna.Framework.Rectangle((int)barX, (int)barY, barW, barH), bg);
            sb.Draw(pixel, new Microsoft.Xna.Framework.Rectangle((int)barX, (int)barY, (int)(barW * hpPct), barH), fg);
        }

        float dockDist = Vector2.Distance(_player.Position, new Vector2(station.X, station.Y));
        if (isEnemy)
        {
            if (dockDist < 200f && !_docked)
            {
                string warn = "Enemy Station";
                var ws = font.MeasureString(warn);
                byte flash = (byte)((MathF.Sin(t * 4f) * 0.3f + 0.7f) * 255);
                DrawSpacedText(sb, font, warn,
                    new Microsoft.Xna.Framework.Vector2(sx - ws.X / 2f, sy - r - antH - 30f),
                    new Color(255, 100, 100, (int)flash));
            }
        }
        else
        {
            // Dock prompt
            if (dockDist < 150f && !_docked)
            {
                string prompt = "[E] Dock";
                var ps = font.MeasureString(prompt);
                byte flash = (byte)((MathF.Sin(t * 4f) * 0.3f + 0.7f) * 255);
                DrawSpacedText(sb, font, prompt,
                    new Microsoft.Xna.Framework.Vector2(sx - ps.X / 2f, sy - r - 22f),
                    new Color(255, 255, 100, (int)flash));
            }
        }
    }

    private void DrawShip(SpriteBatch sb, Texture2D pixel, Vector2 pos, float t,
        float scale, Color color, float angle, Vector2 velocity)
    {
        float len = 18f * ZOOM * scale;

        var tip = pos + Vector2.FromAngle(angle) * len;
        var left = pos + Vector2.FromAngle(angle + 2.4f) * len * 0.65f;
        var right = pos + Vector2.FromAngle(angle - 2.4f) * len * 0.65f;

        // Thrust flames (drawn first, underneath ship)
        float speed = velocity.Length();
        if (speed > 20f)
        {
            float flameBase = MathF.Min(speed / 5f, 12f) * ZOOM;
            Vector2[] origins = {
                left * 0.67f + right * 0.33f,
                (left + right) * 0.5f,
                left * 0.33f + right * 0.67f
            };
            for (int i = 0; i < 3; i++)
            {
                float fLen = flameBase * (i == 1 ? 1f : 0.5f);
                var ftip = origins[i] + Vector2.FromAngle(angle + MathF.PI) * fLen;
                var fside1 = origins[i] + Vector2.FromAngle(angle + MathF.PI + 0.3f) * 3f * ZOOM;
                var fside2 = origins[i] + Vector2.FromAngle(angle + MathF.PI - 0.3f) * 3f * ZOOM;
                Color flameColor = scale >= 1f ? Color.Orange : color * 0.7f;
                DrawLine(sb, pixel, ftip.X, ftip.Y, fside1.X, fside1.Y, flameColor);
                DrawLine(sb, pixel, ftip.X, ftip.Y, fside2.X, fside2.Y, flameColor);
            }
        }

        // Main triangle hull
        DrawLine(sb, pixel, tip.X, tip.Y, left.X, left.Y, color);
        DrawLine(sb, pixel, tip.X, tip.Y, right.X, right.Y, color);
        DrawLine(sb, pixel, left.X, left.Y, right.X, right.Y, color);

        // Small cockpit window near front
        float cp = 0.7f;
        var cockpit = pos + Vector2.FromAngle(angle) * len * cp;
        float cs = 2f * ZOOM;
        var cf = cockpit + Vector2.FromAngle(angle) * cs;
        var cl = cockpit + Vector2.FromAngle(angle + 1.5f) * cs * 0.4f;
        var cr = cockpit + Vector2.FromAngle(angle - 1.5f) * cs * 0.4f;
        Color cockpitColor = scale >= 1f ? Color.Cyan * 0.6f : color * 0.5f;
        DrawLine(sb, pixel, cf.X, cf.Y, cl.X, cl.Y, cockpitColor);
        DrawLine(sb, pixel, cf.X, cf.Y, cr.X, cr.Y, cockpitColor);

        // Hull panel lines
        var rearMid = (left + right) * 0.5f;
        float hullLen = (rearMid - cockpit).Length();

        // Centerline from cockpit to rear
        DrawLine(sb, pixel, cockpit.X, cockpit.Y, rearMid.X, rearMid.Y, color * 0.5f);

        // Side lines parallel to hull edges
        float sideInset = len * 0.025f;
        var siL = cockpit + Vector2.FromAngle(angle + 1.5f) * sideInset;
        var siR = cockpit + Vector2.FromAngle(angle - 1.5f) * sideInset;
        var lEdge = (left - tip).Normalized();
        var rEdge = (right - tip).Normalized();
        var lEnd = siL + lEdge * hullLen * 0.8f;
        var rEnd = siR + rEdge * hullLen * 0.8f;
        DrawLine(sb, pixel, siL.X, siL.Y, lEnd.X, lEnd.Y, color * 0.35f);
        DrawLine(sb, pixel, siR.X, siR.Y, rEnd.X, rEnd.Y, color * 0.35f);
        DrawLine(sb, pixel, lEnd.X, lEnd.Y, rEnd.X, rEnd.Y, color * 0.35f);
    }

    private void DrawEnemyShip(SpriteBatch sb, Texture2D pixel, Vector2 pos, float t, EnemyShip e)
    {
        float baseScale;
        Color color;
        bool isFederation = e.Faction == "Atlas Federation";
        switch (e.Type)
        {
            case "interceptor":
                baseScale = 0.5f; color = isFederation ? new Color(200, 220, 255) : Color.White; break;
            case "missile_frigate":
                baseScale = 0.9f; color = isFederation ? new Color(120, 180, 240) : new Color(140, 140, 160); break;
            case "destroyer":
                baseScale = 1.3f; color = isFederation ? new Color(60, 120, 220) : new Color(100, 120, 180); break;
            case "battleship":
                baseScale = 2.0f; color = isFederation ? new Color(40, 80, 200) : new Color(160, 80, 60); break;
            default:
                baseScale = 0.5f; color = isFederation ? new Color(80, 160, 255) : new Color(255, 140, 0); break;
        }
        float scale = baseScale * ZOOM;
        float len = 18f * scale;

        Vector2 forward = Vector2.FromAngle(e.Angle);
        Vector2 right = Vector2.FromAngle(e.Angle - 1.5708f);

        if (e.Type == "battleship")
        {
            DrawBattleship(sb, pixel, pos, t, e, scale, len, forward, right, color);
        }
        else if (e.Type == "destroyer")
        {
            DrawDestroyer(sb, pixel, pos, t, e, scale, len, forward, right, color);
        }
        else if (e.Type == "missile_frigate")
        {
            DrawMissileFrigate(sb, pixel, pos, t, e, scale, len, forward, right, color);
        }
        else if (e.Type == "interceptor")
        {
            DrawShip(sb, pixel, pos, t, 0.5f, color, e.Angle, e.Velocity);
        }
        else
        {
            DrawShip(sb, pixel, pos, t, 0.5f, color, e.Angle, e.Velocity);
        }

        // Draw shields bar (if shielded)
        if (e.MaxShields > 0f)
        {
            int barW = 24;
            int barH = 2;
            int barX = (int)pos.X - barW / 2;
            int barY = (int)pos.Y - (int)(len * 1.1f) - 4;
            float shieldPct = e.Shields / e.MaxShields;
            Color bg = isFederation ? new Color(0, 20, 40, 180) : new Color(0, 0, 40, 180);
            sb.Draw(pixel, new Microsoft.Xna.Framework.Rectangle(barX, barY, barW, barH), bg);
            sb.Draw(pixel, new Microsoft.Xna.Framework.Rectangle(barX, barY, (int)(barW * shieldPct), barH), isFederation ? Color.LightBlue : Color.Cyan);
        }

        // Health bar
        float hpPct = e.Health / e.MaxHealth;
        if (e.Type == "battleship")
        {
            // Show combined health of all 3 sections
            float totalMax = e.BattleshipFrontMaxHP + e.BattleshipMidMaxHP + e.BattleshipRearMaxHP;
            float totalHP = Math.Max(0, e.BattleshipFrontHP) + Math.Max(0, e.BattleshipMidHP) + Math.Max(0, e.BattleshipRearHP);
            hpPct = totalMax > 0 ? totalHP / totalMax : 0f;
        }
        int hpBarW = 24;
        int hpBarH = 3;
        int hpBarX = (int)pos.X - hpBarW / 2;
        int hpBarY = (int)pos.Y + (int)(len * 0.8f) + 2;
        Color hpBg = isFederation ? new Color(0, 20, 40, 180) : new Color(40, 0, 0, 180);
        sb.Draw(pixel, new Microsoft.Xna.Framework.Rectangle(hpBarX, hpBarY, hpBarW, hpBarH), hpBg);
        sb.Draw(pixel, new Microsoft.Xna.Framework.Rectangle(hpBarX, hpBarY, (int)(hpBarW * hpPct), hpBarH), isFederation ? Color.LightBlue : Color.Red);
    }

    private void DrawMissileFrigate(SpriteBatch sb, Texture2D pixel, Vector2 pos, float t,
        EnemyShip e, float scale, float len, Vector2 forward, Vector2 right, Color color)
    {
        // Boxy hull — rectangle with angled front
        float halfW = len * 0.45f;
        float halfH = len * 0.35f;
        var nose = pos + forward * halfW;
        var rear = pos - forward * halfW;
        var rTop = pos + right * halfH - forward * halfW * 0.3f;
        var rBot = pos - right * halfH - forward * halfW * 0.3f;
        var nTop = nose + right * halfH * 0.6f;
        var nBot = nose - right * halfH * 0.6f;

        // Hull outline
        DrawLine(sb, pixel, nTop.X, nTop.Y, rTop.X, rTop.Y, color);
        DrawLine(sb, pixel, nBot.X, nBot.Y, rBot.X, rBot.Y, color);
        DrawLine(sb, pixel, nTop.X, nTop.Y, nBot.X, nBot.Y, color);
        DrawLine(sb, pixel, rTop.X, rTop.Y, rBot.X, rBot.Y, color);
        DrawLine(sb, pixel, nose.X, nose.Y, nTop.X, nTop.Y, color);
        DrawLine(sb, pixel, nose.X, nose.Y, nBot.X, nBot.Y, color);

        // Missile tubes on sides
        float tubeLen = halfW * 0.7f;
        float tubeW = 3f * ZOOM;
        for (int side = -1; side <= 1; side += 2)
        {
            var tubeBase = pos + right * halfH * 0.8f * side - forward * halfW * 0.2f;
            var tubeTip = tubeBase + forward * tubeLen;
            DrawLine(sb, pixel, tubeBase.X, tubeBase.Y, tubeTip.X, tubeTip.Y, new Color(180, 180, 180));
            var tSide = tubeBase + right * tubeW * 0.5f * side;
            DrawLine(sb, pixel, tSide.X, tSide.Y, tSide.X + forward.X * tubeLen, tSide.Y + forward.Y * tubeLen, new Color(120, 120, 120));
        }

        // Cockpit
        var cockpit = pos + forward * halfW * 0.2f;
        float cs = 2f * ZOOM;
        var cf = cockpit + forward * cs;
        var cl = cockpit + right * cs * 0.4f;
        var cr = cockpit - right * cs * 0.4f;
        DrawLine(sb, pixel, cf.X, cf.Y, cl.X, cl.Y, Color.Cyan * 0.5f);
        DrawLine(sb, pixel, cf.X, cf.Y, cr.X, cr.Y, Color.Cyan * 0.5f);

        // Thrust
        float speed = e.Velocity.Length();
        if (speed > 10f)
        {
            float flameBase = MathF.Min(speed / 5f, 10f) * ZOOM;
            var fOrigin = rear;
            var fTip = fOrigin - forward * flameBase;
            var fSide1 = fOrigin - forward * flameBase * 0.3f + right * 3f * ZOOM;
            var fSide2 = fOrigin - forward * flameBase * 0.3f - right * 3f * ZOOM;
            DrawLine(sb, pixel, fTip.X, fTip.Y, fSide1.X, fSide1.Y, Color.Orange * 0.7f);
            DrawLine(sb, pixel, fTip.X, fTip.Y, fSide2.X, fSide2.Y, Color.Orange * 0.7f);
        }
    }

    private void DrawDestroyer(SpriteBatch sb, Texture2D pixel, Vector2 pos, float t,
        EnemyShip e, float scale, float len, Vector2 forward, Vector2 right, Color color)
    {
        // Longer, rounded hull
        float halfW = len * 0.5f;
        float halfH = len * 0.25f;
        var nose = pos + forward * halfW;
        var rear = pos - forward * halfW;
        var rTop = pos + right * halfH - forward * halfW * 0.1f;
        var rBot = pos - right * halfH - forward * halfW * 0.1f;
        var nTop = nose - right * halfH * 0.1f;
        var nBot = nose + right * halfH * 0.1f;

        // Hull — tapered oval shape
        DrawLine(sb, pixel, nTop.X, nTop.Y, rTop.X, rTop.Y, color);
        DrawLine(sb, pixel, nBot.X, nBot.Y, rBot.X, rBot.Y, color);
        DrawLine(sb, pixel, nTop.X, nTop.Y, nBot.X, nBot.Y, color);
        DrawLine(sb, pixel, rTop.X, rTop.Y, rBot.X, rBot.Y, color);
        // Rounded nose
        DrawLine(sb, pixel, nose.X, nose.Y, nTop.X, nTop.Y, color);
        DrawLine(sb, pixel, nose.X, nose.Y, nBot.X, nBot.Y, color);
        // Rear
        DrawLine(sb, pixel, rTop.X, rTop.Y, rear.X, rear.Y, color * 0.6f);
        DrawLine(sb, pixel, rBot.X, rBot.Y, rear.X, rear.Y, color * 0.6f);
        DrawLine(sb, pixel, rear.X, rear.Y, rear.X + right.X * halfH, rear.Y + right.Y * halfH, color * 0.4f);

        // Front turret
        float turretSize = 4f * ZOOM;
        var t1Pos = pos + forward * halfW * 0.4f;
        DrawLine(sb, pixel, t1Pos.X - right.X * turretSize, t1Pos.Y - right.Y * turretSize,
            t1Pos.X + right.X * turretSize, t1Pos.Y + right.Y * turretSize, Color.Gray);
        DrawLine(sb, pixel, t1Pos.X - forward.X * turretSize * 0.3f, t1Pos.Y - forward.Y * turretSize * 0.3f,
            t1Pos.X + forward.X * turretSize * 0.3f, t1Pos.Y + forward.Y * turretSize * 0.3f, Color.Gray);

        // Rear turret
        var t2Pos = pos - forward * halfW * 0.4f;
        DrawLine(sb, pixel, t2Pos.X - right.X * turretSize, t2Pos.Y - right.Y * turretSize,
            t2Pos.X + right.X * turretSize, t2Pos.Y + right.Y * turretSize, Color.Gray);
        DrawLine(sb, pixel, t2Pos.X - forward.X * turretSize * 0.3f, t2Pos.Y - forward.Y * turretSize * 0.3f,
            t2Pos.X + forward.X * turretSize * 0.3f, t2Pos.Y + forward.Y * turretSize * 0.3f, Color.Gray);

        // Missile tube in front
        var mTube = nose - forward * 3f * ZOOM;
        DrawLine(sb, pixel, mTube.X - right.X * 2f * ZOOM, mTube.Y - right.Y * 2f * ZOOM,
            mTube.X + right.X * 2f * ZOOM, mTube.Y + right.Y * 2f * ZOOM, new Color(120, 120, 120));

        // Cockpit
        var cockpit = pos + forward * halfW * 0.1f;
        float cs = 2f * ZOOM;
        var cf = cockpit + forward * cs;
        var cl = cockpit + right * cs * 0.5f;
        var cr = cockpit - right * cs * 0.5f;
        DrawLine(sb, pixel, cf.X, cf.Y, cl.X, cl.Y, Color.Cyan * 0.4f);
        DrawLine(sb, pixel, cf.X, cf.Y, cr.X, cr.Y, Color.Cyan * 0.4f);

        // Thrust
        float speed = e.Velocity.Length();
        if (speed > 10f)
        {
            float flameBase = MathF.Min(speed / 4f, 10f) * ZOOM;
            var fOrigin = rear;
            var fTip = fOrigin - forward * flameBase;
            var fSide1 = fOrigin - forward * flameBase * 0.3f + right * 4f * ZOOM;
            var fSide2 = fOrigin - forward * flameBase * 0.3f - right * 4f * ZOOM;
            DrawLine(sb, pixel, fTip.X, fTip.Y, fSide1.X, fSide1.Y, Color.Orange * 0.6f);
            DrawLine(sb, pixel, fTip.X, fTip.Y, fSide2.X, fSide2.Y, Color.Orange * 0.6f);
        }
    }

    private void DrawBattleship(SpriteBatch sb, Texture2D pixel, Vector2 pos, float t,
        EnemyShip e, float scale, float len, Vector2 forward, Vector2 right, Color color)
    {
        // 3 sections: rear, mid, front — each separated by a small gap
        float sectionLen = len * 0.55f;
        float gap = 3f * ZOOM;
        float halfH = len * 0.2f;

        // Rear section
        if (e.BattleshipRearHP > 0f)
        {
            Vector2 rearPos = pos - forward * (sectionLen + gap * 0.5f);
            DrawBattleshipSection(sb, pixel, rearPos, forward, right, sectionLen, halfH, color, e.BattleshipRearHP > 0f);
        }

        // Mid section
        if (e.BattleshipMidHP > 0f)
        {
            Vector2 midPos = pos;
            DrawBattleshipSection(sb, pixel, midPos, forward, right, sectionLen, halfH, color, e.BattleshipMidHP > 0f);
            // Gun turrets on mid section
            float turretSize = 3f * ZOOM;
            var t1Pos = midPos + right * halfH * 0.6f;
            var t2Pos = midPos - right * halfH * 0.6f;
            var t3Pos = midPos + forward * sectionLen * 0.3f;
            FillCircle(sb, pixel, t1Pos.X, t1Pos.Y, turretSize, Color.Gray * 0.8f);
            FillCircle(sb, pixel, t2Pos.X, t2Pos.Y, turretSize, Color.Gray * 0.8f);
            FillCircle(sb, pixel, t3Pos.X, t3Pos.Y, turretSize * 0.6f, Color.Gray * 0.6f);
        }

        // Front section
        if (e.BattleshipFrontHP > 0f)
        {
            Vector2 frontPos = pos + forward * (sectionLen + gap * 0.5f);
            DrawBattleshipSection(sb, pixel, frontPos, forward, right, sectionLen, halfH, color, e.BattleshipFrontHP > 0f);
            // Rounded nose extension
            Vector2 nose = frontPos + forward * sectionLen * 0.5f;
            Vector2 nMid = frontPos + forward * sectionLen * 0.35f;
            DrawLine(sb, pixel, nMid.X - right.X * halfH * 0.5f, nMid.Y - right.Y * halfH * 0.5f,
                nose.X, nose.Y, color);
            DrawLine(sb, pixel, nMid.X + right.X * halfH * 0.5f, nMid.Y + right.Y * halfH * 0.5f,
                nose.X, nose.Y, color);
        }

        // Thrust (rear section only)
        if (!e.MovementDisabled && e.Velocity.Length() > 10f)
        {
            Vector2 rearEnd = pos - forward * (sectionLen * 1.5f + gap);
            float flameBase = MathF.Min(e.Velocity.Length() / 4f, 12f) * ZOOM;
            var fTip = rearEnd - forward * flameBase;
            var fSide1 = rearEnd - forward * flameBase * 0.3f + right * 5f * ZOOM;
            var fSide2 = rearEnd - forward * flameBase * 0.3f - right * 5f * ZOOM;
            DrawLine(sb, pixel, fTip.X, fTip.Y, fSide1.X, fSide1.Y, Color.Orange * 0.5f);
            DrawLine(sb, pixel, fTip.X, fTip.Y, fSide2.X, fSide2.Y, Color.Orange * 0.5f);
        }
    }

    private void DrawBattleshipSection(SpriteBatch sb, Texture2D pixel, Vector2 center,
        Vector2 forward, Vector2 right, float sLen, float halfH, Color color, bool active)
    {
        Color c = active ? color : color * 0.3f;
        Vector2 fwd = center + forward * sLen * 0.5f;
        Vector2 aft = center - forward * sLen * 0.5f;
        Vector2 fwdT = fwd + right * halfH;
        Vector2 fwdB = fwd - right * halfH;
        Vector2 aftT = aft + right * halfH;
        Vector2 aftB = aft - right * halfH;

        DrawLine(sb, pixel, fwdT.X, fwdT.Y, aftT.X, aftT.Y, c);
        DrawLine(sb, pixel, fwdB.X, fwdB.Y, aftB.X, aftB.Y, c);
        DrawLine(sb, pixel, fwdT.X, fwdT.Y, fwdB.X, fwdB.Y, c);
        DrawLine(sb, pixel, aftT.X, aftT.Y, aftB.X, aftB.Y, c);
        // Centerline
        DrawLine(sb, pixel, fwd.X, fwd.Y, aft.X, aft.Y, c * 0.4f);
    }

    private void DrawDockedMenu(SpriteBatch sb, Texture2D pixel, SpriteFont font, SpriteFont titleFont,
        int screenW, int screenH, float t)
    {
        if (_game == null) return;
        sb.Draw(pixel, new Microsoft.Xna.Framework.Rectangle(0, 0, screenW, screenH),
            new Color(0, 0, 0, 180));

        int panelW = 640;
        int panelH = screenH - 80;
        int px = (screenW - panelW) / 2;
        int py = (screenH - panelH) / 2;

        sb.Draw(pixel, new Microsoft.Xna.Framework.Rectangle(px, py, panelW, panelH),
            new Color(10, 10, 30, 230));
        DrawRect(sb, pixel, px, py, panelW, panelH, new Color(60, 60, 100));

        int textX = px + 20;
        int textY = py + 20;

        DrawSpacedText(sb, titleFont, $"{_station.Name}",
            new Microsoft.Xna.Framework.Vector2(textX, textY), Color.Cyan);
        textY += 40;

        DrawSpacedText(sb, font, $"Credits: {_player.Credits}  |  Cargo: {_player.UsedCargo}/{_player.CargoCapacity}",
            new Microsoft.Xna.Framework.Vector2(textX, textY), Color.Yellow);
        textY += 22;

        // Station defense status & upgrade (player-owned stations only)
        if (!TrainingMode && _system.Faction == "Independent" && _system.Station != null)
        {
            int defLvl = _stationDefenseLevel;
            Color defColor = defLvl == 0 ? Color.Gray : defLvl >= 5 ? Color.Gold : Color.LightBlue;
            DrawSpacedText(sb, font, $"Station Defenses: Level {defLvl}/5",
                new Microsoft.Xna.Framework.Vector2(textX, textY), defColor);
            textY += 16;

            if (defLvl < 5)
            {
                var cost = GetDefenseUpgradeCost(defLvl);
                bool canAfford = _player.Credits >= cost.credits;
                foreach (var req in cost.resources)
                {
                    var inv = _player.Resources.FirstOrDefault(r => r.Id == req.id);
                    int have = inv?.Quantity ?? 0;
                    if (have < req.qty) canAfford = false;
                }

                string costStr = $"Upgrade cost: {cost.credits}cr";
                foreach (var req in cost.resources)
                {
                    var res = _game.Galaxy.FindResource(req.id);
                    var inv = _player.Resources.FirstOrDefault(r => r.Id == req.id);
                    int have = inv?.Quantity ?? 0;
                    costStr += $"  {res?.Name ?? req.id} {have}/{req.qty}";
                }

                Color costColor = canAfford ? Color.Lime : Color.Red;
                DrawSpacedText(sb, font, costStr,
                    new Microsoft.Xna.Framework.Vector2(textX + 20, textY), costColor);
                textY += 16;

                if (canAfford)
                {
                    string prompt = "[U] Upgrade Station Defenses";
                    byte flash = (byte)((MathF.Sin(t * 4f) * 0.3f + 0.7f) * 255);
                    DrawSpacedText(sb, font, prompt,
                        new Microsoft.Xna.Framework.Vector2(textX + 20, textY),
                        new Color(255, 255, 100, (int)flash));
                }
                else
                {
                    DrawSpacedText(sb, font, "Gather resources from trade or quests to upgrade.",
                        new Microsoft.Xna.Framework.Vector2(textX + 20, textY), Color.Gray * 0.6f);
                }
                textY += 22;
            }
            else
            {
                DrawSpacedText(sb, font, "  Maximum defenses achieved. Shield active.",
                    new Microsoft.Xna.Framework.Vector2(textX + 20, textY), Color.Gold * 0.7f);
                textY += 22;
            }
        }

        textY += 13;

        // Tab bar
        string[] tabs = { "Market", "Upgrades", "Quests", "News" };
        int tabX = textX;
        int tabPadding = 12;
        for (int i = 0; i < tabs.Length; i++)
        {
            Color tc = i == _dockedTab ? Color.White : Color.Gray * 0.5f;
            Color bg = i == _dockedTab ? new Color(40, 40, 70) : Color.Transparent;
            var sz = font.MeasureString(tabs[i]);
            int tabW = (int)sz.X + tabPadding * 2;
            sb.Draw(pixel, new Microsoft.Xna.Framework.Rectangle(tabX, textY, tabW, (int)sz.Y + 6), bg);
            DrawSpacedText(sb, font, tabs[i],
                new Microsoft.Xna.Framework.Vector2(tabX + (tabW - (int)sz.X) / 2f, textY + 3), tc);
            tabX += tabW + 6;
        }
        textY += 40;

        // Tab content
        if (_dockedTab == 0)
            DrawMarketTab(sb, pixel, font, titleFont, textX, textY, panelW, screenW, screenH, px, py);
        else if (_dockedTab == 1)
            DrawUpgradesTab(sb, pixel, font, titleFont, textX, textY, panelW, screenW, screenH, px, py);
        else if (_dockedTab == 2)
            DrawQuestsTab(sb, pixel, font, titleFont, textX, textY, panelW, screenW, screenH, px, py);
        else
            DrawNewsTab(sb, pixel, font, titleFont, textX, textY, panelW, panelH, screenW, screenH, px, py);

        string foot = "[Left/Right] Tab  [Up/Dn] Select  [Enter] Buy  [BkSp] Sell  [ESC] Undock";
        DrawSpacedText(sb, font, foot,
            new Microsoft.Xna.Framework.Vector2(textX, screenH - py - 30), Color.Gray * 0.7f);
    }

    private void DrawMarketTab(SpriteBatch sb, Texture2D pixel, SpriteFont font, SpriteFont titleFont,
        int textX, int textY, int panelW, int screenW, int screenH, int px, int py)
    {
        var economy = _game.Galaxy.Economy;
        var allResources = _game.Galaxy.AllResources;
        var stationResources = allResources
            .Where(r => economy.HasResource(_system.Id, r.Id))
            .OrderBy(r => r.Category)
            .ThenBy(r => r.Name)
            .ToList();
        var playerResourceIds = _player.Resources.Select(r => r.Id).ToHashSet();
        var playerOnlyResources = allResources
            .Where(r => !economy.HasResource(_system.Id, r.Id) && playerResourceIds.Contains(r.Id))
            .OrderBy(r => r.Category)
            .ThenBy(r => r.Name)
            .ToList();
        var resources = stationResources.Concat(playerOnlyResources).ToList();

        int contentTop = textY;
        int contentBottom = screenH - py - 40;
        int contentH = contentBottom - contentTop;
        int rowH = 20;

        // Estimate total content height and clamp scroll
        // Count category headers
        var cats = new HashSet<string>();
        foreach (var r in resources) cats.Add(r.Category ?? "");
        int totalRows = resources.Count + cats.Count + 2 + 1; // resources + headers + canister + fuel cell + separator
        int totalContentH = 30 + 22 + 6 + totalRows * rowH + 8 + 8; // header + rows + spacing
        int maxScroll = Math.Max(0, totalContentH - contentH);
        _dockedMarketScroll = Math.Min(_dockedMarketScroll, maxScroll);
        if (_dockedMarketSelection == 0) _dockedMarketScroll = 0;

        int listTop = textY + 30 + 22 + 6; // after header and column headers

        // Adjust scroll to keep selected item visible
        int selEstY = listTop;
        if (_dockedMarketSelection < resources.Count)
        {
            string? cat = null;
            int count = 0;
            for (int i = 0; i <= _dockedMarketSelection; i++)
            {
                if (resources[i].Category != cat)
                {
                    cat = resources[i].Category;
                    selEstY += rowH;
                    count++;
                }
                if (i < _dockedMarketSelection)
                    selEstY += rowH;
            }
            selEstY += rowH; // the selected item itself ends here
        }
        else
        {
            selEstY += (resources.Count + cats.Count) * rowH + 8 + 8;
            int conIdx = _dockedMarketSelection - resources.Count;
            selEstY += (conIdx + 1) * rowH; // canister or fuel cell row
        }

        if (selEstY - _dockedMarketScroll > contentBottom)
            _dockedMarketScroll = selEstY - contentBottom;
        if (selEstY - _dockedMarketScroll - rowH < listTop)
            _dockedMarketScroll = Math.Max(0, selEstY - rowH - listTop);

        int drawY = listTop - _dockedMarketScroll;
        int headerY = textY;

        // Column positions
        int cSel = 0;
        int cName = 30;
        int cBuy = 180;
        int cSell = 260;
        int cStock = 340;
        int cCargo = 420;
        int cHint = 500;

        bool inBounds(int y) => y + 20 > listTop && y < contentBottom;

        DrawSpacedText(sb, titleFont, "--- Market ---",
            new Microsoft.Xna.Framework.Vector2(textX, headerY), Color.Lime);
        headerY += 30;

        // Header row background (always visible)
        int headerH = 22;
        sb.Draw(pixel, new Microsoft.Xna.Framework.Rectangle(textX, headerY, panelW - 40, headerH),
            new Color(30, 30, 60));
        DrawSpacedText(sb, font, "Item",
            new Microsoft.Xna.Framework.Vector2(textX + cName, headerY + 2), Color.Gold);
        DrawSpacedText(sb, font, "Buy",
            new Microsoft.Xna.Framework.Vector2(textX + cBuy, headerY + 2), Color.Gold);
        DrawSpacedText(sb, font, "Sell",
            new Microsoft.Xna.Framework.Vector2(textX + cSell, headerY + 2), Color.Gold);
        DrawSpacedText(sb, font, "Stock",
            new Microsoft.Xna.Framework.Vector2(textX + cStock, headerY + 2), Color.Gold);
        DrawSpacedText(sb, font, "Cargo",
            new Microsoft.Xna.Framework.Vector2(textX + cCargo, headerY + 2), Color.Gold);
        sb.Draw(pixel, new Microsoft.Xna.Framework.Rectangle(textX, headerY + headerH, panelW - 40, 1),
            Color.Gold * 0.5f);

        string? lastCategory = null;
        for (int i = 0; i < resources.Count; i++)
        {
            var r = resources[i];
            bool selected = i == _dockedMarketSelection;
            bool isStationResource = economy.HasResource(_system.Id, r.Id);
            int stock = isStationResource ? economy.GetStock(_system.Id, r.Id) : 0;
            int buyPrice = isStationResource ? economy.GetBuyPrice(_system.Id, r.Id) : 0;
            int sellPrice = economy.GetSellPrice(_system.Id, r.Id);
            var playerEntry = _player.Resources.FirstOrDefault(e => e.Id == r.Id);
            int playerQty = playerEntry?.Quantity ?? 0;

            // Category separator
            if (r.Category != lastCategory)
            {
                lastCategory = r.Category;
                if (inBounds(drawY))
                    DrawSpacedText(sb, font, r.Category.ToUpper(),
                        new Microsoft.Xna.Framework.Vector2(textX, drawY), Color.Gray * 0.5f);
                drawY += rowH;
            }

            // Row background for selected item
            bool vis = inBounds(drawY);

            if (selected && vis)
                sb.Draw(pixel, new Microsoft.Xna.Framework.Rectangle(textX, drawY, panelW - 40, rowH - 1),
                    new Color(40, 50, 70));

            Color c = selected ? Color.White : Color.LightGray;
            if (!isStationResource) c *= 0.7f;

            if (vis)
            {
                DrawSpacedText(sb, font, selected ? ">" : " ",
                    new Microsoft.Xna.Framework.Vector2(textX + cSel, drawY), c);
                DrawSpacedText(sb, font, $"[{r.Symbol}] {r.Name}",
                    new Microsoft.Xna.Framework.Vector2(textX + cName, drawY), c);
                if (isStationResource)
                {
                    Color buyC = selected && _player.Credits >= buyPrice ? Color.Lime : c;
                    DrawSpacedText(sb, font, $"{buyPrice}cr",
                        new Microsoft.Xna.Framework.Vector2(textX + cBuy, drawY), buyC);
                }
                else
                {
                    DrawSpacedText(sb, font, "---",
                        new Microsoft.Xna.Framework.Vector2(textX + cBuy, drawY), Color.Gray * 0.5f);
                }
                Color sellC = selected && playerQty > 0 ? Color.Orange : c;
                DrawSpacedText(sb, font, $"{sellPrice}cr",
                    new Microsoft.Xna.Framework.Vector2(textX + cSell, drawY), sellC);
                string stockStr = stock > 0 ? $"{stock}" : "-";
                DrawSpacedText(sb, font, stockStr,
                    new Microsoft.Xna.Framework.Vector2(textX + cStock, drawY), c);
                string cargoStr = playerQty > 0 ? $"{playerQty}" : "-";
                DrawSpacedText(sb, font, cargoStr,
                    new Microsoft.Xna.Framework.Vector2(textX + cCargo, drawY), c);
                if (selected)
                {
                    bool canBuy = isStationResource && _player.Credits >= buyPrice && _player.UsedCargo + r.Volume <= _player.CargoCapacity;
                    bool canSell = playerQty > 0;
                    string hint = "";
                    if (canBuy) hint += "[Enter] Buy  ";
                    if (canSell) hint += "[BkSp] Sell";
                    if (hint.Length > 0)
                        DrawSpacedText(sb, font, hint.Trim(),
                            new Microsoft.Xna.Framework.Vector2(textX + cHint, drawY), Color.Lime * 0.7f);
                }
            }

            drawY += rowH;
        }

        // Separator
        drawY += 8;
        if (inBounds(drawY))
            sb.Draw(pixel, new Microsoft.Xna.Framework.Rectangle(textX, drawY, panelW - 40, 1), Color.Gray * 0.3f);
        drawY += 8;

        // Energy canister row
        bool canBuyCan = _player.Credits >= 50 && _player.UsedCargo < _player.CargoCapacity;
        bool canSelected = _dockedMarketSelection == resources.Count;
        Color canColor = canSelected ? Color.White : Color.LightGray;
        bool canVis = inBounds(drawY);
        if (canSelected && canVis)
            sb.Draw(pixel, new Microsoft.Xna.Framework.Rectangle(textX, drawY, panelW - 40, rowH - 1),
                new Color(40, 50, 70));
        if (canVis)
        {
            DrawSpacedText(sb, font, canSelected ? ">" : " ",
                new Microsoft.Xna.Framework.Vector2(textX + cSel, drawY), canColor);
            DrawSpacedText(sb, font, "[Energy Canister]",
                new Microsoft.Xna.Framework.Vector2(textX + cName, drawY), canColor);
            DrawSpacedText(sb, font, "50cr",
                new Microsoft.Xna.Framework.Vector2(textX + cBuy, drawY), canSelected && canBuyCan ? Color.Lime : canColor);
            DrawSpacedText(sb, font, "---",
                new Microsoft.Xna.Framework.Vector2(textX + cSell, drawY), Color.Gray * 0.5f);
            DrawSpacedText(sb, font, "---",
                new Microsoft.Xna.Framework.Vector2(textX + cStock, drawY), Color.Gray * 0.5f);
        }
        int canQty = _player.Consumables.FirstOrDefault(c => c.Id == "energy_canister")?.Quantity ?? 0;
        if (canVis)
        {
            DrawSpacedText(sb, font, canQty > 0 ? $"{canQty}" : "-",
                new Microsoft.Xna.Framework.Vector2(textX + cCargo, drawY), canColor);
            if (canSelected && canBuyCan)
                DrawSpacedText(sb, font, "[Enter] Buy",
                    new Microsoft.Xna.Framework.Vector2(textX + cHint, drawY), Color.Lime * 0.7f);
        }
        drawY += rowH;

        // Fuel cell row
        bool canBuyFC = _player.Credits >= 25 && _player.UsedCargo < _player.CargoCapacity;
        bool fcSelected = _dockedMarketSelection == resources.Count + 1;
        Color fcColor = fcSelected ? Color.White : Color.LightGray;
        bool fcVis = inBounds(drawY);
        if (fcSelected && fcVis)
            sb.Draw(pixel, new Microsoft.Xna.Framework.Rectangle(textX, drawY, panelW - 40, rowH - 1),
                new Color(40, 50, 70));
        if (fcVis)
        {
            DrawSpacedText(sb, font, fcSelected ? ">" : " ",
                new Microsoft.Xna.Framework.Vector2(textX + cSel, drawY), fcColor);
            DrawSpacedText(sb, font, "[Fuel Cell]",
                new Microsoft.Xna.Framework.Vector2(textX + cName, drawY), fcColor);
            DrawSpacedText(sb, font, "25cr",
                new Microsoft.Xna.Framework.Vector2(textX + cBuy, drawY), fcSelected && canBuyFC ? Color.Lime : fcColor);
            DrawSpacedText(sb, font, "---",
                new Microsoft.Xna.Framework.Vector2(textX + cSell, drawY), Color.Gray * 0.5f);
            DrawSpacedText(sb, font, "---",
                new Microsoft.Xna.Framework.Vector2(textX + cStock, drawY), Color.Gray * 0.5f);
        }
        int fcQty = _player.Consumables.FirstOrDefault(c => c.Id == "fuel_cell")?.Quantity ?? 0;
        if (fcVis)
        {
            DrawSpacedText(sb, font, fcQty > 0 ? $"{fcQty}" : "-",
                new Microsoft.Xna.Framework.Vector2(textX + cCargo, drawY), fcColor);
            if (fcSelected && canBuyFC)
                DrawSpacedText(sb, font, "[Enter] Buy",
                    new Microsoft.Xna.Framework.Vector2(textX + cHint, drawY), Color.Lime * 0.7f);
        }
    }

    private void DrawUpgradesTab(SpriteBatch sb, Texture2D pixel, SpriteFont font, SpriteFont titleFont,
        int textX, int textY, int panelW, int screenW, int screenH, int px, int py)
    {
        var upgrades = _game.GetUpgradesForSystem(_system.Id);
        var equipment = _game.Galaxy.GetAvailableEquipmentForSystem(_system.Id, _player);
        int rowH = 20;
        int sel = _dockedUpgradeSelection;

        // Upgrades section
        DrawSpacedText(sb, titleFont, "--- Upgrades ---",
            new Microsoft.Xna.Framework.Vector2(textX, textY), Color.Gold);
        textY += 30;

        if (upgrades.Count > 0)
        {
            for (int i = 0; i < upgrades.Count; i++)
            {
                var up = upgrades[i];
                bool selected = sel == i;
                bool canAfford = _player.Credits >= up.Cost;
                Color c = selected ? (canAfford ? Color.Yellow : Color.Gray) : (canAfford ? Color.White : Color.Gray * 0.5f);
                if (selected)
                    sb.Draw(pixel, new Microsoft.Xna.Framework.Rectangle(textX, textY, panelW - 40, rowH - 1),
                        new Color(40, 50, 70));
                DrawSpacedText(sb, font, selected ? ">" : " ",
                    new Microsoft.Xna.Framework.Vector2(textX, textY), c);
                DrawSpacedText(sb, font, $"{up.Name} - {up.Cost}cr",
                    new Microsoft.Xna.Framework.Vector2(textX + 16, textY), c);
                textY += rowH;
                DrawSpacedText(sb, font, up.Description,
                    new Microsoft.Xna.Framework.Vector2(textX + 32, textY), Color.White * 0.4f);
                textY += 16;
            }
        }
        else
        {
            DrawSpacedText(sb, font, "  None available",
                new Microsoft.Xna.Framework.Vector2(textX, textY), Color.Gray);
            textY += 24;
        }

        // Equipment section
        textY += 10;
        DrawSpacedText(sb, titleFont, "--- Equipment ---",
            new Microsoft.Xna.Framework.Vector2(textX, textY), Color.Gold);
        textY += 30;

        if (equipment.Count > 0)
        {
            for (int i = 0; i < equipment.Count; i++)
            {
                var eq = equipment[i];
                bool selected = sel == upgrades.Count + i;
                bool canAfford = _player.Credits >= eq.Cost;
                Color c = selected ? (canAfford ? Color.Yellow : Color.Gray) : (canAfford ? Color.White : Color.Gray * 0.5f);
                if (selected)
                    sb.Draw(pixel, new Microsoft.Xna.Framework.Rectangle(textX, textY, panelW - 40, rowH - 1),
                        new Color(40, 50, 70));
                DrawSpacedText(sb, font, selected ? ">" : " ",
                    new Microsoft.Xna.Framework.Vector2(textX, textY), c);
                DrawSpacedText(sb, font, $"{eq.Name} - {eq.Cost}cr",
                    new Microsoft.Xna.Framework.Vector2(textX + 16, textY), c);
                textY += rowH;
                string slotLabel = eq.Slot.Substring(0, 1).ToUpper() + eq.Slot.Substring(1);
                DrawSpacedText(sb, font, $"  [{slotLabel}] {eq.Description}",
                    new Microsoft.Xna.Framework.Vector2(textX + 32, textY), Color.White * 0.4f);
                textY += 16;
            }
        }
        else
        {
            DrawSpacedText(sb, font, "  None available",
                new Microsoft.Xna.Framework.Vector2(textX, textY), Color.Gray);
            textY += 24;
        }
    }
    private void DrawQuestsTab(SpriteBatch sb, Texture2D pixel, SpriteFont font, SpriteFont titleFont,
        int textX, int textY, int panelW, int screenW, int screenH, int px, int py)
    {
        var available = _game.GetQuestsForSystem(_system.Id);
        var activeHere = _game.Galaxy.ActiveQuests
            .Where(q => q.GiverSystem == _system.Id)
            .ToList();
        var activeTarget = _game.Galaxy.ActiveQuests
            .Where(q => q.ObjectiveType == "travel" && q.TargetSystem == _system.Id)
            .ToList();
        var completable = activeHere
            .Where(q => _game.Galaxy.IsQuestObjectiveMet(q, _player))
            .Concat(activeTarget)
            .ToList();
        var inProgress = activeHere
            .Where(q => !_game.Galaxy.IsQuestObjectiveMet(q, _player))
            .ToList();
        var done = _player.CompletedQuests
            .Select(id => _game.Galaxy.AllQuests.FirstOrDefault(q => q.Id == id))
            .Where(q => q != null && q.GiverSystem == _system.Id)
            .Cast<QuestData>()
            .ToList();

        int sel = _dockedQuestSelection;
        int maxBodyW = panelW - 60;
        bool hasAny = available.Count > 0 || completable.Count > 0 || inProgress.Count > 0 || done.Count > 0;

        if (hasAny)
        {
            DrawSpacedText(sb, titleFont, "--- Quests ---",
                new Microsoft.Xna.Framework.Vector2(textX, textY), Color.Gold);
            textY += 30;

            for (int i = 0; i < available.Count; i++)
            {
                var q = available[i];
                bool selected = sel == i;
                Color c = selected ? Color.Yellow : Color.White;
                string prefix = selected ? "> " : "  ";
                DrawSpacedText(sb, font, $"{prefix}{q.Name} - {q.RewardCredits}cr",
                    new Microsoft.Xna.Framework.Vector2(textX, textY), c);
                textY += 18;
                textY = WrapAndDraw(sb, font, q.Description, textX + 16, textY,
                    maxBodyW - 16, selected ? Color.Yellow * 0.6f : Color.White * 0.6f, 14);
                textY += 22;
            }

            for (int i = 0; i < completable.Count; i++)
            {
                var q = completable[i];
                bool selected = sel == available.Count + i;
                Color c = selected ? Color.Lime : Color.Lime * 0.7f;
                string prefix = selected ? "> " : "  ";
                DrawSpacedText(sb, font, $"{prefix}{q.Name} - {q.RewardCredits}cr",
                    new Microsoft.Xna.Framework.Vector2(textX, textY), c);
                textY += 18;
                DrawSpacedText(sb, font, $"  Objective complete - [Turn In]",
                    new Microsoft.Xna.Framework.Vector2(textX, textY), c * 0.7f);
                textY += 22;
            }

            foreach (var q in inProgress)
            {
                DrawSpacedText(sb, font, $"  {q.Name}",
                    new Microsoft.Xna.Framework.Vector2(textX, textY), Color.White * 0.5f);
                textY += 18;
                string status = q.ObjectiveType == "travel"
                    ? $"Travel to {q.TargetSystem}"
                    : $"Find {q.TargetItem} in {q.TargetSystem}";
                textY = WrapAndDraw(sb, font, status, textX + 16, textY,
                    maxBodyW - 16, Color.White * 0.3f, 14);
                textY += 22;
            }

            foreach (var q in done)
            {
                DrawSpacedText(sb, font, $"  {q.Name} [Completed]",
                    new Microsoft.Xna.Framework.Vector2(textX, textY), Color.Gray * 0.5f);
                textY += 22;
            }
        }
    }

    private void DrawNewsTab(SpriteBatch sb, Texture2D pixel, SpriteFont font, SpriteFont titleFont,
        int textX, int textY, int panelW, int panelH, int screenW, int screenH, int px, int py)
    {
        var allArticles = _game.Galaxy.NewsService.Articles;
        int maxBodyW = panelW - 70;

        DrawSpacedText(sb, titleFont, "--- Galaxy News ---",
            new Microsoft.Xna.Framework.Vector2(textX, textY), Color.Lime);
        textY += 36;

        // Sub-tab bar
        string[] subTabs = { "Breaking", "Allied", "Neutral", "Hostile" };
        Color[] subTabColors =
        {
            new Color(255, 60, 60),   // Breaking - red
            new Color(100, 220, 100), // Allied - green
            new Color(200, 200, 80),  // Neutral - yellow
            new Color(255, 120, 60)   // Hostile - orange
        };
        int subTabX = textX;
        int subTabPadding = 10;
        for (int i = 0; i < subTabs.Length; i++)
        {
            Color tc = i == _dockedNewsSubTab ? subTabColors[i] : Color.Gray * 0.5f;
            Color bg = i == _dockedNewsSubTab ? new Color(40, 40, 70) : Color.Transparent;
            var sz = font.MeasureString(subTabs[i]);
            int tabW = (int)sz.X + subTabPadding * 2;
            sb.Draw(pixel, new Microsoft.Xna.Framework.Rectangle(subTabX, textY, tabW, (int)sz.Y + 6), bg);
            DrawSpacedText(sb, font, subTabs[i],
                new Microsoft.Xna.Framework.Vector2(subTabX + (tabW - (int)sz.X) / 2f, textY + 3), tc);
            subTabX += tabW + 6;
        }
        textY += 36;

        // Determine station faction for Allied/Hostile filtering
        string stationFaction = _system.Faction ?? "";
        string opposingFaction = stationFaction switch
        {
            "Atlas Federation" => "Trigor Empire",
            "Trigor Empire" => "Atlas Federation",
            _ => ""
        };

        // Filter articles by sub-tab
        var articles = allArticles.Where(a =>
        {
            return _dockedNewsSubTab switch
            {
                0 => a.IsBreaking,
                1 => !string.IsNullOrEmpty(a.Faction) && a.Faction == stationFaction,
                2 => string.IsNullOrEmpty(a.Faction) || a.Faction == "Independent",
                3 => !string.IsNullOrEmpty(opposingFaction) && a.Faction == opposingFaction,
                _ => true
            };
        }).ToList();

        if (articles.Count == 0)
        {
            DrawSpacedText(sb, font, "  No news available.",
                new Microsoft.Xna.Framework.Vector2(textX, textY), Color.Gray);
            return;
        }

        // Content area bounds
        int contentTop = textY;
        int contentBottom = py + panelH - 50;
        int contentH = contentBottom - contentTop;
        int contentRight = px + panelW - 20;

        // Pre-layout: measure every article's total height
        var heights = new int[articles.Count];
        int totalH = 0;
        int lineH = 14;
        for (int i = 0; i < articles.Count; i++)
        {
            int h = 0;
            if (articles[i].IsBreaking) h += 16;
            h += 18;
            var bodyLines = _game.WordWrap(font, articles[i].Body, maxBodyW - 16);
            h += bodyLines.Count * lineH;
            h += 2 + lineH; // source line
            heights[i] = h;
            totalH += h;
        }

        // Clamp scroll offset (pixel-based)
        int maxScroll = Math.Max(0, totalH - contentH);
        if (_dockedNewsScroll > maxScroll) _dockedNewsScroll = maxScroll;
        if (_dockedNewsScroll < 0) _dockedNewsScroll = 0;

        // Find first visible article
        int drawY = contentTop;
        int firstIdx = 0;
        int accumulated = 0;
        for (int i = 0; i < articles.Count; i++)
        {
            if (accumulated + heights[i] > _dockedNewsScroll)
            {
                firstIdx = i;
                drawY = contentTop - (_dockedNewsScroll - accumulated);
                break;
            }
            accumulated += heights[i];
        }

        // Draw visible articles
        for (int i = firstIdx; i < articles.Count; i++)
        {
            if (drawY > contentBottom) break;

            var article = articles[i];
            int remainingH = contentBottom - drawY;

            // Breaking label
            if (article.IsBreaking && remainingH > 0)
            {
                if (drawY >= contentTop)
                {
                    float pulse = MathF.Sin((float)_game.GameTime.TotalGameTime.TotalSeconds * 3f) * 0.2f + 0.8f;
                    DrawSpacedText(sb, font, "BREAKING:",
                        new Microsoft.Xna.Framework.Vector2(textX, drawY), new Color(255, 60, 60) * pulse);
                }
                drawY += 16;
                remainingH -= 16;
            }
            if (remainingH <= 0) break;

            // Headline
            if (drawY >= contentTop)
            {
                Color headlineColor = article.Faction switch
                {
                    "Atlas Federation" => new Color(100, 180, 255),
                    "Trigor Empire" => new Color(255, 100, 60),
                    _ => Color.White
                };
                DrawSpacedText(sb, font, article.Headline,
                    new Microsoft.Xna.Framework.Vector2(textX + 8, drawY), headlineColor);
            }
            drawY += 18;
            remainingH -= 18;
            if (remainingH <= 0) break;

            // Body (wrapped)
            var bodyLines = _game.WordWrap(font, article.Body, maxBodyW - 16);
            foreach (var bl in bodyLines)
            {
                if (remainingH <= 0) break;
                if (drawY >= contentTop)
                {
                    DrawSpacedText(sb, font, bl,
                        new Microsoft.Xna.Framework.Vector2(textX + 16, drawY), Color.White * 0.7f);
                }
                drawY += lineH;
                remainingH -= lineH;
            }
            if (remainingH <= 0) break;

            // Source line
            if (drawY >= contentTop)
            {
                string sourceLine = $"-- {article.Source}";
                if (article.Faction != null)
                    sourceLine += $" [{article.Faction}]";
                DrawSpacedText(sb, font, sourceLine,
                    new Microsoft.Xna.Framework.Vector2(textX + 16, drawY), Color.Gray * 0.4f);
            }
            drawY += lineH + 2;
            remainingH -= lineH + 2;
            if (remainingH <= 0) break;

            // Separator line between articles
            if (i + 1 < articles.Count && drawY >= contentTop && drawY + 2 <= contentBottom)
            {
                DrawLine(sb, pixel, textX, drawY, contentRight, drawY, new Color(60, 60, 90) * 0.3f);
                drawY += 4;
            }
        }

        // Scroll bar
        if (maxScroll > 0)
        {
            float scrollBarX = contentRight + 4;
            float scrollAreaH = contentH;
            float thumbH = Math.Max(20f, scrollAreaH * scrollAreaH / totalH);
            float thumbY = contentTop + _dockedNewsScroll * (scrollAreaH - thumbH) / maxScroll;
            DrawLine(sb, pixel, scrollBarX, contentTop, scrollBarX, contentBottom, new Color(60, 60, 100) * 0.5f);
            DrawRect(sb, pixel, scrollBarX - 2, thumbY, 4, thumbH, new Color(120, 120, 180) * 0.7f);
        }
    }

    private void DrawSystemHUD(SpriteBatch sb, SpriteFont font, int screenW, int screenH,
        Vector2 shipPos, float stationX, float stationY)
    {
        DrawSpacedText(sb, font, $"{_system.Name} - Interior",
            new Microsoft.Xna.Framework.Vector2(10, 10), Color.Cyan);

        float distFromCenter = _player.Position.Length();
        DrawSpacedText(sb, font, $"Distance from star: {(int)distFromCenter}u",
            new Microsoft.Xna.Framework.Vector2(10, 30), Color.Gray * 0.7f);

        float distToStation = Vector2.Distance(_player.Position, new Vector2(_station.X, _station.Y));
        DrawSpacedText(sb, font, $"Distance to station: {(int)distToStation}u",
            new Microsoft.Xna.Framework.Vector2(10, 50), Color.Gray * 0.7f);

        DrawSpacedText(sb, font, "[ESC] Pause",
            new Microsoft.Xna.Framework.Vector2(10, screenH - 30), Color.Gray * 0.5f);
    }

    private void DrawWaypointArrow(SpriteBatch sb, Texture2D pixel, SpriteFont font, Vector2 center)
    {
        Vector2 dir = _waypointPosition - _player.Position;
        float dist = dir.Length();
        if (dist < 1f) return;
        dir = dir.Normalized();

        float arrowRadius = 55f;
        Vector2 arrowPos = center + dir * arrowRadius;
        float arrowSize = 10f;

        Vector2 tip = arrowPos + dir * arrowSize;
        Vector2 left = arrowPos + Vector2.FromAngle(MathF.Atan2(dir.Y, dir.X) + 2.3f) * arrowSize * 0.5f;
        Vector2 right = arrowPos + Vector2.FromAngle(MathF.Atan2(dir.Y, dir.X) - 2.3f) * arrowSize * 0.5f;

        Color c = Color.Yellow;
        DrawLine(sb, pixel, tip.X, tip.Y, left.X, left.Y, c);
        DrawLine(sb, pixel, tip.X, tip.Y, right.X, right.Y, c);

        string distText = $"{(int)dist}u";
        var ts = font.MeasureString(distText);
        DrawSpacedText(sb, font, distText,
            new Microsoft.Xna.Framework.Vector2(arrowPos.X - ts.X / 2f, arrowPos.Y + arrowSize + 3f),
            c * 0.7f);

        // Waypoint target name
        if (_waypointIndex < _waypointTargets.Count)
        {
            string wpName = _waypointTargets[_waypointIndex].name;
            var ns = font.MeasureString(wpName);
            DrawSpacedText(sb, font, wpName,
                new Microsoft.Xna.Framework.Vector2(arrowPos.X - ns.X / 2f, arrowPos.Y + arrowSize + 18f),
                c * 0.5f);
        }
    }

    private void AutoTargetNearestEnemy()
    {
        float closest = float.MaxValue;
        int closestIdx = -1;
        for (int i = 0; i < _enemies.Count; i++)
        {
            float d = Vector2.Distance(_player.Position, _enemies[i].Position);
            if (d < closest)
            {
                closest = d;
                closestIdx = i;
            }
        }
        if (closestIdx >= 0)
        {
            _targetIndex = closestIdx;
            _targetPosition = _enemies[closestIdx].Position;
        }
    }

    private void CycleCombatTarget()
    {
        if (_enemies.Count == 0) return;
        _targetIndex = (_targetIndex + 1) % _enemies.Count;
        if (_targetIndex >= 0 && _targetIndex < _enemies.Count)
            _targetPosition = _enemies[_targetIndex].Position;
    }

    private void DrawCombatArrow(SpriteBatch sb, Texture2D pixel, SpriteFont font, Vector2 center)
    {
        Vector2 dir = _targetPosition - _player.Position;
        float dist = dir.Length();
        if (dist < 1f) return;
        dir = dir.Normalized();

        float arrowRadius = 55f;
        Vector2 arrowPos = center + dir * arrowRadius;
        float arrowSize = 12f;

        Vector2 tip = arrowPos + dir * arrowSize;
        Vector2 left = arrowPos + Vector2.FromAngle(MathF.Atan2(dir.Y, dir.X) + 2.3f) * arrowSize * 0.5f;
        Vector2 right = arrowPos + Vector2.FromAngle(MathF.Atan2(dir.Y, dir.X) - 2.3f) * arrowSize * 0.5f;

        bool isFriendlyFed = _targetIndex >= 0 && _targetIndex < _enemies.Count
            && _enemies[_targetIndex].Faction == "Atlas Federation" && _system.Hostility < 3;
        Color c = isFriendlyFed ? Color.DodgerBlue : Color.Red;
        DrawLine(sb, pixel, tip.X, tip.Y, left.X, left.Y, c);
        DrawLine(sb, pixel, tip.X, tip.Y, right.X, right.Y, c);

        string distText = $"{(int)dist}u";
        var ts = font.MeasureString(distText);
        DrawSpacedText(sb, font, distText,
            new Microsoft.Xna.Framework.Vector2(arrowPos.X - ts.X / 2f, arrowPos.Y + arrowSize + 3f),
            c * 0.7f);

        // Target name
        if (_targetIndex >= 0 && _targetIndex < _enemies.Count)
        {
            string targetName = _enemies[_targetIndex].Type;
            var ns = font.MeasureString(targetName);
            DrawSpacedText(sb, font, targetName,
                new Microsoft.Xna.Framework.Vector2(arrowPos.X - ns.X / 2f, arrowPos.Y + arrowSize + 18f),
                c * 0.5f);
        }
    }

    private void DrawMiniMap(SpriteBatch sb, Texture2D pixel, SpriteFont font, int screenW, int screenH)
    {
        int mapSize = 150;
        int mx = screenW - mapSize - 10;
        int my = 10;
        float halfMap = mapSize / 2f;
        float cx = mx + halfMap;
        float cy = my + halfMap;
        float scale = halfMap / _systemRadius;

        // Background
        sb.Draw(pixel, new Microsoft.Xna.Framework.Rectangle(mx, my, mapSize, mapSize),
            new Color(0, 0, 0, 160));
        DrawRect(sb, pixel, mx, my, mapSize, mapSize, new Color(80, 80, 120));

        // Exit boundary circle (may be outside view)
        DrawCircleGC(sb, pixel, cx, cy, _systemRadius * scale, new Color(80, 40, 40, 120));

        // Star at center
        float starMapR = MathF.Max(_star.BodyRadius * scale, 3f);
        FillCircle(sb, pixel, cx, cy, starMapR, _star.Color * 0.5f);
        DrawCircle(sb, pixel, cx, cy, starMapR, _star.Color);

        // Planets
        foreach (var p in _planets)
        {
            float px = cx + p.X * scale;
            float py = cy + p.Y * scale;
            float pr = MathF.Max(p.BodyRadius * scale, 1.5f);
            if (px >= mx - pr && px <= mx + mapSize + pr && py >= my - pr && py <= my + mapSize + pr)
                FillCircle(sb, pixel, px, py, pr, p.Color);
        }

        // Station
        if (_system.Station != null)
        {
            float stx = cx + _station.X * scale;
            float sty = cy + _station.Y * scale;
            float stR = MathF.Max(_station.BodyRadius * scale, 2f);
            Color stColor = StationIsHostile ? new Color(200, 60, 60) : Color.LightBlue;
            FillCircle(sb, pixel, stx, sty, stR, stColor);

            // Training mode: draw friendly station too
            if (TrainingMode && _trainingFriendlyHealth > 0)
            {
                float ftx = cx + _trainingFriendlyStation.X * scale;
                float fty = cy + _trainingFriendlyStation.Y * scale;
                FillCircle(sb, pixel, ftx, fty, stR, Color.LightBlue);
            }
        }

        // Player as moving icon
        float plx = cx + _player.Position.X * scale;
        float ply = cy + _player.Position.Y * scale;
        FillCircle(sb, pixel, plx, ply, 3f, Color.White);

        // Enemies on minimap
        foreach (var e in _enemies)
        {
            float ex = cx + e.Position.X * scale;
            float ey = cy + e.Position.Y * scale;
            if (ex >= mx - 2 && ex <= mx + mapSize + 2 && ey >= my - 2 && ey <= my + mapSize + 2)
            {
                bool isFriendly = e.Faction == "Atlas Federation" && _system.Hostility < 3;
                FillCircle(sb, pixel, ex, ey, 2.5f, isFriendly ? Color.DodgerBlue : new Color(255, 140, 0));
            }
        }

        // Asteroids on minimap (small white squares)
        float asSize = 1.5f;
        foreach (var ast in _asteroids)
        {
            float ax = cx + ast.Position.X * scale;
            float ay = cy + ast.Position.Y * scale;
            if (ax >= mx - asSize && ax <= mx + mapSize + asSize && ay >= my - asSize && ay <= my + mapSize + asSize)
                sb.Draw(pixel, new Microsoft.Xna.Framework.Rectangle((int)(ax - asSize), (int)(ay - asSize), (int)(asSize * 2), (int)(asSize * 2)), Color.White * 0.6f);
        }

        // Quest objective markers on minimap (actual positions)
        if (_game != null)
        {
            foreach (var q in _game.Galaxy.ActiveQuests)
            {
                if (q.TargetSystem != _system.Id) continue;
                Vector2 qPos;
                if (q.ObjectiveType == "collect" && _lifepodActive)
                    qPos = new Vector2(_lifepod.X, _lifepod.Y);
                else if ((q.ObjectiveType == "travel" || q.ObjectiveType == "deliver") && _system.Station != null)
                    qPos = new Vector2(_station.X, _station.Y);
                else
                    continue;
                float qx = cx + qPos.X * scale;
                float qy = cy + qPos.Y * scale;
                if (qx < mx - 3 || qx > mx + mapSize + 3 || qy < my - 3 || qy > my + mapSize + 3)
                    continue;
                float qs = 4f;
                var qc = Color.Gold;
                DrawLine(sb, pixel, qx, qy - qs, qx + qs * 0.6f, qy, qc);
                DrawLine(sb, pixel, qx + qs * 0.6f, qy, qx, qy + qs, qc);
                DrawLine(sb, pixel, qx, qy + qs, qx - qs * 0.6f, qy, qc);
                DrawLine(sb, pixel, qx - qs * 0.6f, qy, qx, qy - qs, qc);
            }
        }

        // Waypoint icon label below mini-map
        float iconY = my + mapSize + 6;
        FillCircle(sb, pixel, mx + 4, iconY + 3, 3f, Color.LightBlue);
        DrawLine(sb, pixel, mx + 4, iconY, mx + 4, iconY + 2, Color.Cyan);
        string wpLabel = _waypointIndex < _waypointTargets.Count
            ? _waypointTargets[_waypointIndex].name : "None";
        DrawSpacedText(sb, font, wpLabel,
            new Microsoft.Xna.Framework.Vector2(mx + 10, iconY - 2), Color.LightBlue * 0.8f);
        if (_waypointTargets.Count > 1)
        {
            DrawSpacedText(sb, font, "[Tab]",
                new Microsoft.Xna.Framework.Vector2(mx + 10, iconY + 12), Color.Gray * 0.4f);
        }
    }

    // Drawing helpers
    private static Color ParseColor(string hex)
    {
        if (hex.StartsWith("#")) hex = hex[1..];
        if (hex.Length == 6)
        {
            int r = Convert.ToInt32(hex[..2], 16);
            int g = Convert.ToInt32(hex[2..4], 16);
            int b = Convert.ToInt32(hex[4..], 16);
            return new Color(r, g, b);
        }
        return Color.White;
    }

    private void PositionLifepod()
    {
        float margin = _star.BodyRadius + 100f;
        float minDistFromPlanet = 200f;
        for (int attempt = 0; attempt < 50; attempt++)
        {
            float angle = RandF() * MathF.Tau;
            float dist = margin + RandF() * (_systemRadius - margin - 50f);
            float px = MathF.Cos(angle) * dist;
            float py = MathF.Sin(angle) * dist;

            bool overlaps = false;
            foreach (var p in _planets)
            {
                float pd = Vector2.Distance(new Vector2(px, py), new Vector2(p.X, p.Y));
                if (pd < minDistFromPlanet)
                {
                    overlaps = true;
                    break;
                }
            }
            if (_system.Station != null)
            {
                float sd = Vector2.Distance(new Vector2(px, py), new Vector2(_station.X, _station.Y));
                if (sd < minDistFromPlanet)
                    overlaps = true;
            }

            if (!overlaps)
            {
                _lifepod = new Body
                {
                    Name = "Lifepod",
                    X = px,
                    Y = py,
                    BodyRadius = 6f,
                    Color = Color.Lime
                };
                return;
            }
        }
        // Fallback: place near center but not on star
        _lifepod = new Body
        {
            Name = "Lifepod",
            X = margin,
            Y = margin,
            BodyRadius = 6f,
            Color = Color.Lime
        };
    }

    private void Respawn()
    {
        _gameOver = false;
        _exploding = false;
        _debris.Clear();
        _player.Health = _player.MaxHealth;
        _temperature = 0f;

        float angle = (MathF.Floor(RandF() * 4f) * MathF.PI / 4f) + MathF.PI / 4f;
        Vector2 spawnPos = Vector2.FromAngle(angle) * _systemRadius * 0.85f;

        foreach (var p in _planets)
        {
            float d = Vector2.Distance(spawnPos, new Vector2(p.X, p.Y));
            if (d < p.BodyRadius + 150f)
            {
                spawnPos = Vector2.FromAngle(angle + 0.5f) * _systemRadius * 0.85f;
                break;
            }
        }

        _player.Position = spawnPos;
        _player.Velocity = Vector2.Zero;
    }

    public void PopulateTrainingInventory()
    {
        _player.Resources.Clear();
        _player.QuestItems.Clear();
        _player.Consumables.Clear();
        _player.UnequippedEquipment.Clear();
        _player.Equipment.Clear();

        var allResources = _game.Galaxy.AllResources;
        foreach (var r in allResources)
            _player.Resources.Add(new InventoryEntry { Id = r.Id, Quantity = 10 });

        var allEquipment = _game.Galaxy.AllEquipment;
        int weaponSlot = 0;
        foreach (var eq in allEquipment)
        {
            if (eq.Slot == "weapon")
            {
                if (weaponSlot < 2)
                {
                    string key = weaponSlot == 0 ? "weapon1" : "weapon2";
                    _player.Equipment[key] = eq.Id;
                    weaponSlot++;
                }
            }
            else if (eq.Slot == "shield")
                _player.Equipment["shield"] = eq.Id;
            else if (eq.Slot == "engine")
                _player.Equipment["engine"] = eq.Id;
            else if (eq.Slot == "utility")
            {
                if (!_player.Equipment.ContainsKey("utility1"))
                    _player.Equipment["utility1"] = eq.Id;
                else if (!_player.Equipment.ContainsKey("utility2"))
                    _player.Equipment["utility2"] = eq.Id;
            }
        }

        // Add extra weapons to UnequippedEquipment for practice
        _player.UnequippedEquipment.Add(new InventoryEntry { Id = "laser_cannon_mk1", Quantity = 1 });
        _player.UnequippedEquipment.Add(new InventoryEntry { Id = "missile_launcher", Quantity = 1 });

        if (!_player.QuestItems.Any(q => q.Id == "princess_lifepod"))
            _player.QuestItems.Add(new InventoryEntry { Id = "princess_lifepod", Quantity = 1 });
    }

    private void GenerateAsteroids()
    {
        int count = 10 + Random.Shared.Next(11); // 10-20
        float safeMargin = _star.BodyRadius * 1.5f;
        for (int i = 0; i < count; i++)
        {
            for (int attempt = 0; attempt < 20; attempt++)
            {
                float angle = RandF() * MathF.PI * 2f;
                float dist = safeMargin + RandF() * (_systemRadius - safeMargin);
                Vector2 pos = new(MathF.Cos(angle) * dist, MathF.Sin(angle) * dist);

                // Check not too close to planets or station
                bool blocked = false;
                if (_planets.Any(p => Vector2.Distance(pos, new Vector2(p.X, p.Y)) < p.BodyRadius * 2.5f))
                    blocked = true;
                if (!blocked && _system.Station != null &&
                    Vector2.Distance(pos, new Vector2(_station.X, _station.Y)) < _station.BodyRadius * 3f)
                    blocked = true;

                if (!blocked)
                {
                    float radius = 16f + RandF() * 24f; // 16-40
                    _asteroids.Add(new Asteroid
                    {
                        Position = pos,
                        Radius = radius,
                        Health = 3f,
                        Seed = Random.Shared.Next()
                    });
                    break;
                }
            }
        }
    }

    private void RespawnAsteroid()
    {
        float safeMargin = _star.BodyRadius * 1.5f;
        for (int attempt = 0; attempt < 20; attempt++)
        {
            float angle = RandF() * MathF.PI * 2f;
            float dist = safeMargin + RandF() * (_systemRadius - safeMargin);
            Vector2 pos = new(MathF.Cos(angle) * dist, MathF.Sin(angle) * dist);
            bool blocked = false;
            if (_planets.Any(p => Vector2.Distance(pos, new Vector2(p.X, p.Y)) < p.BodyRadius * 2.5f))
                blocked = true;
            if (!blocked && _system.Station != null &&
                Vector2.Distance(pos, new Vector2(_station.X, _station.Y)) < _station.BodyRadius * 3f)
                blocked = true;
            if (!blocked)
            {
                float radius = 16f + RandF() * 24f;
                _asteroids.Add(new Asteroid
                {
                    Position = pos,
                    Radius = radius,
                    Health = 3f,
                    Seed = Random.Shared.Next()
                });
                return;
            }
        }
    }

    private void GenerateLoot(Vector2 position)
    {
        if (Random.Shared.Next(4) == 0) // 25% fuel cell
        {
            _asteroidLoot.Add(new AsteroidLoot
            {
                Position = position,
                Lifetime = 30f,
                Type = "fuel_cell",
                ResourceId = "fuel_cell"
            });
        }
        else // 75% resource, weighted by station economy
        {
            var allResources = _game?.Galaxy?.AllResources;
            var economy = _game?.Galaxy?.Economy;
            if (allResources != null && allResources.Count > 0 && economy != null)
            {
                float totalWeight = 0f;
                var weights = new List<float>();
                var resIds = new List<string>();
                foreach (var res in allResources)
                {
                    if (res.Id == "fuel_cell") continue;
                    int stock = economy.GetStock(_system.Id, res.Id);
                    float weight = stock > 0 ? stock + 10f : 0.5f;
                    weights.Add(weight);
                    resIds.Add(res.Id);
                    totalWeight += weight;
                }

                if (totalWeight > 0f)
                {
                    float roll = RandF() * totalWeight;
                    float acc = 0f;
                    for (int i = 0; i < resIds.Count; i++)
                    {
                        acc += weights[i];
                        if (roll <= acc)
                        {
                            _asteroidLoot.Add(new AsteroidLoot
                            {
                                Position = position,
                                Lifetime = 30f,
                                Type = "resource",
                                ResourceId = resIds[i]
                            });
                            return;
                        }
                    }
                }
            }
        }
    }

    private void SpawnTrainingStation(string kind, int level)
    {
        if (level < 1 || level > 5) return;

        float spawnAngle = RandF() * MathF.Tau;
        float spawnDist = 400f + RandF() * 200f;
        Vector2 pos = _player.Position + Vector2.FromAngle(spawnAngle) * spawnDist;

        if (kind == "empire")
        {
            _station = new Body
            {
                Name = $"Empire Station L{level}",
                BodyRadius = 50f,
                Color = Color.Red,
                X = pos.X,
                Y = pos.Y
            };
            _stationHealth = _stationMaxHealth;
            _stationDefenseLevel = level;
            _trainingHostile = true;
            InitStationDefenses();
        }
        else
        {
            _trainingFriendlyStation = new Body
            {
                Name = $"Atlas Federation Station L{level}",
                BodyRadius = 50f,
                Color = Color.LightBlue,
                X = pos.X,
                Y = pos.Y
            };
            _trainingFriendlyHealth = _stationMaxHealth;
            _trainingTargetPos = pos;
            InitFriendlyStationDefenses();
        }
    }

    private EnemyShip MakeEnemyShip(string type, Vector2 pos, Vector2 vel, float angle, AiState aiState, float stateTimer, float orbitAngle, string faction = "")
    {
        float hp = 3f, shields = 0f, wepDmg = 2f, cd = 2f;
        float bFront = 0f, bMid = 0f, bRear = 0f;
        bool hasMissiles = false;
        switch (type)
        {
            case "scout":         hp = 3f; cd = 2.0f; break;
            case "fighter":       hp = 5f; cd = 1.5f; break;
            case "gunship":       hp = 8f; cd = 1.2f; break;
            case "cruiser":       hp = 15f; cd = 1.0f; break;
            case "dreadnought":   hp = 25f; cd = 0.8f; break;
            case "interceptor":   hp = 3f; shields = 3f; wepDmg = 4f; cd = 2.0f; break;
            case "missile_frigate": hp = 10f; shields = 5f; cd = 2.5f; hasMissiles = true; break;
            case "destroyer":     hp = 20f; shields = 8f; cd = 2.0f; hasMissiles = true; break;
            case "battleship":    hp = 30f; shields = 15f; cd = 3.0f; bFront = 30f; bMid = 20f; bRear = 15f; hasMissiles = true; break;
        }
        var ship = new EnemyShip
        {
            Position = pos, Velocity = vel, Angle = angle,
            Health = hp, MaxHealth = hp,
            Shields = shields, MaxShields = shields,
            WeaponDamage = wepDmg,
            Type = type, ShootCooldown = cd,
            AiState = aiState, StateTimer = stateTimer, OrbitAngle = orbitAngle,
            BattleshipFrontHP = bFront, BattleshipFrontMaxHP = bFront,
            BattleshipMidHP = bMid, BattleshipMidMaxHP = bMid,
            BattleshipRearHP = bRear, BattleshipRearMaxHP = bRear,
            MovementDisabled = false, WeaponsDisabled = false,
            Faction = faction
        };
        if (hasMissiles)
        {
            ship.MissileCooldown1 = 1f + RandF() * 2f;
            ship.MissileCooldown2 = 3f + RandF() * 2f;
        }
        return ship;
    }

    private void SpawnTrainingEnemy(int typeIndex, string faction = "")
    {
        string[] typeNames = { "scout", "fighter", "gunship", "cruiser", "dreadnought",
                               "interceptor", "missile_frigate", "destroyer", "battleship" };
        if (typeIndex < 0 || typeIndex >= typeNames.Length) return;

        float spawnAngle = RandF() * MathF.Tau;
        float spawnDist = 300f + RandF() * 200f;
        Vector2 pos = _player.Position + Vector2.FromAngle(spawnAngle) * spawnDist;
        float a = RandF() * MathF.Tau;

        _enemies.Add(MakeEnemyShip(typeNames[typeIndex], pos,
            Vector2.FromAngle(a) * (80f + RandF() * 70f), a,
            AiState.Attack, 0f, spawnAngle, faction));
    }

    private int GetHopDistance(string fromId, string toId)
    {
        if (fromId == toId) return 0;
        var visited = new HashSet<string> { fromId };
        var queue = new Queue<(string id, int dist)>();
        queue.Enqueue((fromId, 0));
        while (queue.Count > 0)
        {
            var (cur, dist) = queue.Dequeue();
            var sys = _game?.Galaxy?.FindSystemById(cur);
            if (sys == null) continue;
            foreach (var conn in sys.Connections)
            {
                if (conn == toId) return dist + 1;
                if (visited.Add(conn))
                    queue.Enqueue((conn, dist + 1));
            }
        }
        return 99;
    }

    private void SpawnFederationPatrols()
    {
        if (_game?.Galaxy == null || TrainingMode) return;

        // Determine hop distance from Atlas
        int distFromAtlas = GetHopDistance("atlas", _system.Id);
        int count;
        if (_system.Id == "atlas")
        {
            // Atlas defense fleet: heavy presence
            count = 6 + Random.Shared.Next(3);
        }
        else if (distFromAtlas <= 1)
            count = 3 + Random.Shared.Next(2);
        else if (distFromAtlas <= 3)
            count = 2 + Random.Shared.Next(2);
        else if (distFromAtlas <= 5)
            count = 1 + Random.Shared.Next(1);
        else
            return; // too far from Atlas, no patrols

        string[] typePool;
        if (_system.Id == "atlas")
            typePool = new[] { "battleship", "destroyer", "destroyer", "missile_frigate", "cruiser", "gunship", "interceptor", "interceptor", "fighter" };
        else if (distFromAtlas <= 1)
            typePool = new[] { "destroyer", "missile_frigate", "gunship", "interceptor", "fighter", "fighter" };
        else if (distFromAtlas <= 3)
            typePool = new[] { "missile_frigate", "gunship", "interceptor", "fighter", "scout" };
        else
            typePool = new[] { "gunship", "interceptor", "fighter", "scout" };

        for (int i = 0; i < count; i++)
        {
            float spawnAngle = RandF() * MathF.Tau;
            float spawnDist = 250f + RandF() * 350f;
            Vector2 pos = _player.Position + Vector2.FromAngle(spawnAngle) * spawnDist;
            float a = RandF() * MathF.Tau;
            string type = typePool[Random.Shared.Next(typePool.Length)];
            _enemies.Add(MakeEnemyShip(type, pos,
                Vector2.FromAngle(a) * (60f + RandF() * 60f), a,
                AiState.Orbit, RandF() * 2f, spawnAngle, "Atlas Federation"));
        }
    }

    private void SpawnEmpireScouts()
    {
        if (_game?.Galaxy == null || TrainingMode) return;
        if (_system.Faction == "Trigor Empire") return;
        if (_system.Hostility >= 3) return;

        int distFromEmpire = GetHopDistance("trigor", _system.Id);
        if (distFromEmpire > 2) return;

        // Spawn 1-3 scouts in systems within 2 hops of Empire
        int count = 1 + Random.Shared.Next(3);
        for (int i = 0; i < count; i++)
        {
            float spawnAngle = RandF() * MathF.Tau;
            float spawnDist = 300f + RandF() * 200f;
            Vector2 pos = _player.Position + Vector2.FromAngle(spawnAngle) * spawnDist;
            float a = RandF() * MathF.Tau;
            string type = Random.Shared.NextDouble() < 0.3f ? "interceptor" : "scout";
            _enemies.Add(MakeEnemyShip(type, pos,
                Vector2.FromAngle(a) * (100f + RandF() * 50f), a,
                AiState.Idle, RandF() * 3f, spawnAngle, ""));
        }

        // Notify the player
        string sysName = _system.Name;
        _game?.SetStatusMessage($"Enemy Forces Spotted in {sysName}!", 4f);
    }

    private void SpawnEmpirePatrols()
    {
        if (_game?.Galaxy == null || TrainingMode) return;
        if (_system.Faction != "Trigor Empire" && _system.Hostility < 3) return;

        int distFromTrigor = GetHopDistance("trigor", _system.Id);
        int count;
        if (distFromTrigor <= 0)
            count = 5 + Random.Shared.Next(3); // Trigor itself
        else if (distFromTrigor <= 1)
            count = 3 + Random.Shared.Next(2);
        else if (distFromTrigor <= 2)
            count = 2 + Random.Shared.Next(2);
        else
            count = 1 + Random.Shared.Next(2);

        string[] typePool;
        if (distFromTrigor <= 0)
            typePool = new[] { "battleship", "destroyer", "destroyer", "missile_frigate", "cruiser", "gunship", "interceptor", "fighter", "scout" };
        else if (distFromTrigor <= 1)
            typePool = new[] { "destroyer", "missile_frigate", "cruiser", "gunship", "interceptor", "fighter", "scout" };
        else if (distFromTrigor <= 2)
            typePool = new[] { "cruiser", "gunship", "interceptor", "fighter", "scout" };
        else
            typePool = new[] { "gunship", "interceptor", "fighter", "scout" };

        for (int i = 0; i < count; i++)
        {
            float spawnAngle = RandF() * MathF.Tau;
            float spawnDist = 200f + RandF() * 400f;
            Vector2 pos = _player.Position + Vector2.FromAngle(spawnAngle) * spawnDist;
            float a = RandF() * MathF.Tau;
            string type = typePool[Random.Shared.Next(typePool.Length)];
            _enemies.Add(MakeEnemyShip(type, pos,
                Vector2.FromAngle(a) * (80f + RandF() * 70f), a,
                AiState.Orbit, RandF() * 2f, spawnAngle, ""));
        }
    }

    private void SpawnScouts()
    {
        int count = 3;
        Vector2 origin = _player.Position;
        AiState startState = AiState.Idle;
        if (TrainingMode)
        {
            count = 10;
            origin = new Vector2(_station.X, _station.Y);
            startState = AiState.Attack;
        }
        for (int i = 0; i < count; i++)
        {
            float spawnAngle = RandF() * MathF.Tau;
            float spawnDist = 300f + RandF() * 300f;
            Vector2 pos = origin + Vector2.FromAngle(spawnAngle) * spawnDist;
            float a = RandF() * MathF.Tau;
            _enemies.Add(MakeEnemyShip("scout", pos,
                Vector2.FromAngle(a) * (100f + RandF() * 50f), a,
                startState, RandF() * 2f, spawnAngle));
        }
    }

    private void SpawnHostileEnemies()
    {
        bool isFederation = _system?.Faction == "Atlas Federation";
        int count = 3;
        float variety = 0f; // 0 = only scouts, 1 = full mix
        if (!isFederation)
        {
            var trigorSys = _game?.Galaxy?.FindSystemById("trigor");
            if (trigorSys != null)
            {
                float dx = _system.X - trigorSys.X;
                float dy = _system.Y - trigorSys.Y;
                float dist = MathF.Sqrt(dx * dx + dy * dy);
                float maxDist = 15000f;
                float t = MathF.Max(0f, MathF.Min(1f, dist / maxDist));
                count = 3 + (int)((1f - t) * 6f);
                variety = 1f - t;
            }
        }
        else
        {
            count = 4 + Random.Shared.Next(3);
            variety = 0.8f;
        }

        string faction = isFederation ? "Atlas Federation" : "";
        string[] typePool;
        if (isFederation)
            typePool = new[] { "scout", "fighter", "gunship", "interceptor", "missile_frigate", "destroyer", "battleship" };
        else if (variety > 0.7f)
            typePool = new[] { "scout", "fighter", "gunship", "interceptor", "missile_frigate", "destroyer" };
        else if (variety > 0.4f)
            typePool = new[] { "scout", "fighter", "gunship", "interceptor" };
        else
            typePool = new[] { "scout", "fighter" };

        for (int i = 0; i < count; i++)
        {
            float spawnAngle = RandF() * MathF.Tau;
            float spawnDist = 200f + RandF() * 400f;
            Vector2 pos = _player.Position + Vector2.FromAngle(spawnAngle) * spawnDist;
            float a = RandF() * MathF.Tau;
            string type = typePool[Random.Shared.Next(typePool.Length)];
            _enemies.Add(MakeEnemyShip(type, pos,
                Vector2.FromAngle(a) * (80f + RandF() * 70f), a,
                AiState.Attack, 0f, spawnAngle, faction));
        }
    }

    private void DamageEnemy(int index, float damage, Vector2 hitPos)
    {
        if (index < 0 || index >= _enemies.Count) return;
        var e = _enemies[index];

        if (e.Type == "battleship")
        {
            // Determine which section was hit based on hit position relative to ship angle
            Vector2 forward = Vector2.FromAngle(e.Angle);
            Vector2 toHit = hitPos - e.Position;
            float along = toHit.X * forward.X + toHit.Y * forward.Y;
            float sectionLen = 18f * 2f * 0.55f; // battleship scale * section proportion

            if (along > sectionLen * 0.3f)
            {
                // Front section
                if (e.BattleshipFrontHP > 0f)
                {
                    e.BattleshipFrontHP -= damage;
                    if (e.BattleshipFrontHP <= 0f)
                    {
                        // Front destroyed = whole ship blows up
                        _enemyExplosions.Add(new EnemyExplosion { Position = e.Position, Timer = 0f, Duration = 0.8f });
                        _game?.Galaxy?.RecordDestroyKill(_system.Id, e.Type);
                        _enemies.RemoveAt(index);
                        return;
                    }
                }
            }
            else if (along < -sectionLen * 0.3f)
            {
                // Rear section
                if (e.BattleshipRearHP > 0f)
                {
                    e.BattleshipRearHP -= damage;
                    if (e.BattleshipRearHP <= 0f)
                    {
                        e.MovementDisabled = true;
                        e.BattleshipRearHP = 0f;
                    }
                }
            }
            else
            {
                // Mid section
                if (e.BattleshipMidHP > 0f)
                {
                    e.BattleshipMidHP -= damage;
                    if (e.BattleshipMidHP <= 0f)
                    {
                        e.WeaponsDisabled = true;
                        e.BattleshipMidHP = 0f;
                    }
                }
            }
            _enemies[index] = e;
            return;
        }

        // Shield absorption
        if (e.MaxShields > 0f && e.Shields > 0f)
        {
            e.Shields -= damage;
            if (e.Shields < 0f)
            {
                // Overflow damage goes to HP
                e.Health += e.Shields; // e.Shields is now negative
                e.Shields = 0f;
            }
        }
        else
        {
            e.Health -= damage;
        }

        // Check for destruction
        if (e.Health <= 0f)
        {
            _enemyExplosions.Add(new EnemyExplosion { Position = e.Position, Timer = 0f, Duration = 0.6f });
            _game?.Galaxy?.RecordDestroyKill(_system.Id, e.Type);
            _enemies.RemoveAt(index);
        }
        else
        {
            _enemies[index] = e;
        }
    }

    private void UpdateEnemyAI(ref EnemyShip e, float dt)
    {
        // Friendly Federation ships patrol the station; others target the player (or training target)
        bool isFriendlyFed = e.Faction == "Atlas Federation" && _system != null && _system.Hostility < 3;
        Vector2 targetPos;
        if (isFriendlyFed && !TrainingMode)
            targetPos = new Vector2(_station.X, _station.Y);
        else if (_trainingTargetPos.X != 0 || _trainingTargetPos.Y != 0)
            targetPos = _trainingTargetPos;
        else
            targetPos = _player.Position;
        Vector2 toTarget = targetPos - e.Position;
        float distToPlayer = toTarget.Length();
        Vector2 dirToPlayer = distToPlayer > 0.01f ? toTarget.Normalized() : Vector2.Zero;

        e.StateTimer -= dt;
        e.ShootCooldown -= dt;
        e.TurretCooldown1 -= dt;
        e.TurretCooldown2 -= dt;
        e.TurretCooldown3 -= dt;
        e.MissileCooldown1 -= dt;
        e.MissileCooldown2 -= dt;

        // Type-specific speed and behavior
        float maxSpeed = 112f;
        float acceleration = 250f;
        float orbitRadius = 150f;
        float engageRange = 350f;

        if (e.Type == "interceptor")
        {
            maxSpeed = 140f;
            acceleration = 300f;
        }
        else if (e.Type == "missile_frigate")
        {
            maxSpeed = 70f;
            acceleration = 120f;
            orbitRadius = 200f;
            engageRange = 400f;
        }
        else if (e.Type == "destroyer")
        {
            maxSpeed = 60f;
            acceleration = 100f;
            orbitRadius = 180f;
            engageRange = 380f;
        }
        else if (e.Type == "battleship")
        {
            maxSpeed = 40f;
            acceleration = 60f;
            orbitRadius = 250f;
            engageRange = 450f;
        }

        // Battleship: movement disabled if rear section destroyed
        if (e.MovementDisabled)
        {
            e.Velocity *= 0.98f;
            if (e.Velocity.Length() < 5f)
                e.Velocity = Vector2.Zero;
        }

        switch (e.AiState)
        {
            case AiState.Idle:
            {
                if (e.StateTimer <= 0f)
                {
                    e.Angle = RandF() * MathF.Tau;
                    e.StateTimer = 1.5f + RandF() * 2f;
                }
                if (!e.MovementDisabled)
                    e.Velocity += Vector2.FromAngle(e.Angle) * dt * acceleration * 0.4f;
                if (e.Velocity.Length() > maxSpeed * 0.7f)
                    e.Velocity = e.Velocity.Normalized() * maxSpeed * 0.7f;
                if (e.Velocity.Length() > 10f)
                    e.Angle = MathF.Atan2(e.Velocity.Y, e.Velocity.X);

                if (!isFriendlyFed && distToPlayer < 500f)
                {
                    e.AiState = AiState.Orbit;
                    e.OrbitAngle = MathF.Atan2(
                        e.Position.Y - targetPos.Y,
                        e.Position.X - targetPos.X);
                    e.StateTimer = 2f + RandF() * 2f;
                }
                break;
            }
            case AiState.Orbit:
            {
                e.OrbitAngle += dt * 0.5f;
                float targetDist = orbitRadius + RandF() * 50f;
                Vector2 orbitTarget = targetPos + Vector2.FromAngle(e.OrbitAngle) * targetDist;
                Vector2 toOrbitTarget = orbitTarget - e.Position;
                float tDist = toOrbitTarget.Length();
                if (tDist > 10f && !e.MovementDisabled)
                {
                    e.Velocity += toOrbitTarget.Normalized() * dt * acceleration * 0.8f;
                    if (e.Velocity.Length() > maxSpeed)
                        e.Velocity = e.Velocity.Normalized() * maxSpeed;
                }
                else
                {
                    e.Velocity *= 0.95f;
                }
                if (e.Velocity.Length() > 10f)
                    e.Angle = MathF.Atan2(e.Velocity.Y, e.Velocity.X);

                if (!isFriendlyFed && distToPlayer < engageRange + 50f && e.StateTimer <= 0f)
                {
                    e.AiState = AiState.Attack;
                    e.StateTimer = 1.5f + RandF() * 1.5f;
                }
                break;
            }
            case AiState.Attack:
            {
                if (!e.MovementDisabled)
                {
                    e.Velocity += dirToPlayer * dt * acceleration;
                    if (e.Velocity.Length() > maxSpeed * 1.2f)
                        e.Velocity = e.Velocity.Normalized() * maxSpeed * 1.2f;
                }
                if (e.Velocity.Length() > 10f)
                    e.Angle = MathF.Atan2(e.Velocity.Y, e.Velocity.X);

                bool weaponsOk = !e.WeaponsDisabled;

                // Primary gun fire
                if (weaponsOk && distToPlayer < engageRange && e.ShootCooldown <= 0f)
                {
                    e.ShootCooldown = 1.5f + RandF() * 0.5f;
                    Vector2 bulletVel = Vector2.FromAngle(e.Angle) * BULLET_SPEED * 0.8f + e.Velocity * 0.3f;
                    _bullets.Add(new Bullet
                    {
                        Position = e.Position,
                        Velocity = bulletVel,
                        Lifetime = BULLET_LIFETIME * 0.8f,
                        IsPlayerBullet = false,
                        Damage = e.WeaponDamage
                    });
                }

                // Destroyer: front + rear turrets
                if (e.Type == "destroyer" && weaponsOk && distToPlayer < engageRange + 30f)
                {
                    // Front turret
                    if (e.TurretCooldown1 <= 0f)
                    {
                        e.TurretCooldown1 = 0.6f;
                        Vector2 fwd = Vector2.FromAngle(e.Angle);
                        Vector2 muzzle = e.Position + fwd * 18f * 1.3f * 0.4f;
                        Vector2 bv = fwd * BULLET_SPEED * 0.8f + e.Velocity * 0.3f;
                        _bullets.Add(new Bullet { Position = muzzle, Velocity = bv, Lifetime = BULLET_LIFETIME * 0.8f, IsPlayerBullet = false, Damage = e.WeaponDamage });
                    }
                    // Rear turret
                    if (e.TurretCooldown2 <= 0f)
                    {
                        e.TurretCooldown2 = 0.6f;
                        Vector2 rev = Vector2.FromAngle(e.Angle + MathF.PI);
                        Vector2 muzzle = e.Position + rev * 18f * 1.3f * 0.4f;
                        Vector2 bv = rev * BULLET_SPEED * 0.8f + e.Velocity * 0.3f;
                        _bullets.Add(new Bullet { Position = muzzle, Velocity = bv, Lifetime = BULLET_LIFETIME * 0.8f, IsPlayerBullet = false, Damage = e.WeaponDamage });
                    }
                }

                // Battleship: 3 gun turrets
                if (e.Type == "battleship" && weaponsOk && distToPlayer < engageRange + 50f)
                {
                    float fwdAngle = e.Angle;
                    // Turret 1: forward-left
                    if (e.TurretCooldown1 <= 0f)
                    {
                        e.TurretCooldown1 = 0.5f;
                        Vector2 dir = Vector2.FromAngle(fwdAngle - 0.3f);
                        Vector2 muzzle = e.Position + dir * 18f * 2f * 0.5f;
                        _bullets.Add(new Bullet { Position = muzzle, Velocity = dir * BULLET_SPEED * 0.8f + e.Velocity * 0.3f, Lifetime = BULLET_LIFETIME * 0.8f, IsPlayerBullet = false, Damage = e.WeaponDamage });
                    }
                    // Turret 2: forward-right
                    if (e.TurretCooldown2 <= 0f)
                    {
                        e.TurretCooldown2 = 0.5f;
                        Vector2 dir = Vector2.FromAngle(fwdAngle + 0.3f);
                        Vector2 muzzle = e.Position + dir * 18f * 2f * 0.5f;
                        _bullets.Add(new Bullet { Position = muzzle, Velocity = dir * BULLET_SPEED * 0.8f + e.Velocity * 0.3f, Lifetime = BULLET_LIFETIME * 0.8f, IsPlayerBullet = false, Damage = e.WeaponDamage });
                    }
                    // Turret 3: straight forward (mid section)
                    if (e.TurretCooldown3 <= 0f)
                    {
                        e.TurretCooldown3 = 0.5f;
                        Vector2 dir = Vector2.FromAngle(fwdAngle);
                        Vector2 muzzle = e.Position + dir * 18f * 2f * 0.1f;
                        _bullets.Add(new Bullet { Position = muzzle, Velocity = dir * BULLET_SPEED * 0.8f + e.Velocity * 0.3f, Lifetime = BULLET_LIFETIME * 0.8f, IsPlayerBullet = false, Damage = e.WeaponDamage });
                    }
                }

                // Missile fire for ships with missile launchers
                if (weaponsOk && distToPlayer < engageRange + 100f)
                {
                    if (e.MissileCooldown1 <= 0f)
                    {
                        float cd = 0f;
                        if (e.Type == "missile_frigate") cd = 4f;
                        else if (e.Type == "destroyer") cd = 5f;
                        else if (e.Type == "battleship") cd = 6f;
                        if (cd > 0f)
                        {
                            e.MissileCooldown1 = cd;
                            Vector2 dir = Vector2.FromAngle(e.Angle);
                            Vector2 muzzle = e.Position + dir * 18f * 0.8f;
                            _missiles.Add(new Missile
                            {
                                Position = muzzle,
                                Velocity = dir * 150f + e.Velocity * 0.3f,
                                Lifetime = 3.5f,
                                TargetIndex = -1,
                                IsEnemyMissile = true
                            });
                        }
                    }
                    // Second missile launcher (frigate and battleship)
                    if (e.MissileCooldown2 <= 0f && (e.Type == "missile_frigate" || e.Type == "battleship"))
                    {
                        float cd = e.Type == "missile_frigate" ? 5f : 7f;
                        e.MissileCooldown2 = cd;
                        Vector2 dir = Vector2.FromAngle(e.Angle + (e.Type == "battleship" ? 0.2f : -0.15f));
                        Vector2 muzzle = e.Position + dir * 18f * 0.8f;
                        _missiles.Add(new Missile
                        {
                            Position = muzzle,
                            Velocity = dir * 150f + e.Velocity * 0.3f,
                            Lifetime = 3.5f,
                            TargetIndex = -1,
                            IsEnemyMissile = true
                        });
                    }
                }

                if (e.StateTimer <= 0f)
                {
                    e.AiState = AiState.Orbit;
                    e.StateTimer = 3f + RandF() * 3f;
                    e.OrbitAngle = MathF.Atan2(
                        e.Position.Y - _player.Position.Y,
                        e.Position.X - _player.Position.X);
                }
                break;
            }
        }

        if (!e.MovementDisabled)
            e.Position += e.Velocity * dt;

        float distFromCenter = e.Position.Length();
        if (distFromCenter > _systemRadius * 0.9f)
        {
            Vector2 dir = e.Position.Normalized();
            e.Position = dir * _systemRadius * 0.9f;
            float velDot = e.Velocity.X * dir.X + e.Velocity.Y * dir.Y;
            if (velDot > 0f)
                e.Velocity -= dir * velDot * 1.5f;
        }
    }

    private void UpdateStationTurrets(float dt)
    {
        Vector2 stationPos = new(_station.X, _station.Y);
        Vector2 toPlayer = _player.Position - stationPos;
        float distToPlayer = toPlayer.Length();
        if (distToPlayer < 1f) return;
        Vector2 dirToPlayer = toPlayer.Normalized();
        float angleToPlayer = MathF.Atan2(dirToPlayer.Y, dirToPlayer.X);

        // Basic gun turrets (level 1)
        float gunRange = 500f;
        for (int i = 0; i < _stationTurrets.Count; i++)
        {
            var t = _stationTurrets[i];
            if (!t.Active) continue;
            t.Cooldown -= dt;
            if (t.Cooldown <= 0f && distToPlayer < gunRange)
            {
                t.Cooldown = 0.25f;
                Vector2 worldPos = stationPos + t.Position;
                Vector2 bulletVel = dirToPlayer * BULLET_SPEED;
                _bullets.Add(new Bullet
                {
                    Position = worldPos,
                    Velocity = bulletVel,
                    Lifetime = BULLET_LIFETIME,
                    IsPlayerBullet = false,
                    Damage = 2f
                });
            }
            _stationTurrets[i] = t;
        }

        // Ring section turrets (levels 2-5)
        float laserRange = 450f;
        float missileRange = 500f;
        float ringR = _station.BodyRadius + 16f;
        for (int i = 0; i < _ringSections.Count; i++)
        {
            var rs = _ringSections[i];
            if (!rs.Active) continue;

            // Laser
            rs.LaserCooldown -= dt;
            if (rs.LaserCooldown <= 0f && distToPlayer < laserRange)
            {
                rs.LaserCooldown = 0.4f;
                Vector2 turretPos = stationPos + Vector2.FromAngle(rs.Angle) * ringR;
                Vector2 beamEnd = turretPos + dirToPlayer * 25f;
                _laserBeams.Add(new LaserBeam
                {
                    Start = turretPos,
                    End = beamEnd,
                    Timer = 0.08f
                });
                if (!_trainingInvincible && LineCircleIntersect(turretPos, beamEnd, _player.Position, 22f))
                {
                    _player.TakeDamage(1);
                }
            }

            // Missile
            rs.MissileCooldown -= dt;
            if (rs.MissileCooldown <= 0f && distToPlayer < missileRange)
            {
                rs.MissileCooldown = 2f;
                Vector2 turretPos = stationPos + Vector2.FromAngle(rs.Angle) * ringR;
                Vector2 missileVel = dirToPlayer * 200f;
                _missiles.Add(new Missile
                {
                    Position = turretPos,
                    Velocity = missileVel,
                    Lifetime = 3f,
                    TargetIndex = -1
                });
            }

            _ringSections[i] = rs;
        }
    }

    private void UpdateFriendlyStationTurrets(float dt)
    {
        Vector2 stationPos = new(_trainingFriendlyStation.X, _trainingFriendlyStation.Y);
        if (_enemies.Count == 0) return;

        // Find nearest enemy
        EnemyShip nearest = default;
        float nearestDist = float.MaxValue;
        for (int i = 0; i < _enemies.Count; i++)
        {
            float d = Vector2.Distance(_enemies[i].Position, stationPos);
            if (d < nearestDist)
            {
                nearestDist = d;
                nearest = _enemies[i];
            }
        }

        Vector2 toTarget = nearest.Position - stationPos;
        if (toTarget.Length() < 1f) return;
        Vector2 dirToTarget = toTarget.Normalized();

        // Basic gun turrets
        float gunRange = 500f;
        for (int i = 0; i < _friendlyTurrets.Count; i++)
        {
            var t = _friendlyTurrets[i];
            if (!t.Active) continue;
            t.Cooldown -= dt;
            if (t.Cooldown <= 0f && nearestDist < gunRange)
            {
                t.Cooldown = 0.25f;
                Vector2 worldPos = stationPos + t.Position;
                Vector2 bulletVel = dirToTarget * BULLET_SPEED;
                _bullets.Add(new Bullet
                {
                    Position = worldPos,
                    Velocity = bulletVel,
                    Lifetime = BULLET_LIFETIME,
                    IsPlayerBullet = false,
                    Damage = 2f
                });
            }
            _friendlyTurrets[i] = t;
        }

        // Ring section turrets
        float laserRange = 450f;
        float missileRange = 500f;
        float ringR = _trainingFriendlyStation.BodyRadius + 16f;
        for (int i = 0; i < _friendlyRingSections.Count; i++)
        {
            var rs = _friendlyRingSections[i];
            if (!rs.Active) continue;

            // Laser
            rs.LaserCooldown -= dt;
            if (rs.LaserCooldown <= 0f && nearestDist < laserRange)
            {
                rs.LaserCooldown = 0.4f;
                Vector2 turretPos = stationPos + Vector2.FromAngle(rs.Angle) * ringR;
                Vector2 beamEnd = turretPos + dirToTarget * 25f;
                _laserBeams.Add(new LaserBeam
                {
                    Start = turretPos,
                    End = beamEnd,
                    Timer = 0.08f
                });
            }

            // Missile
            rs.MissileCooldown -= dt;
            if (rs.MissileCooldown <= 0f && nearestDist < missileRange)
            {
                rs.MissileCooldown = 2f;
                Vector2 turretPos = stationPos + Vector2.FromAngle(rs.Angle) * ringR;
                Vector2 missileVel = dirToTarget * 200f;
                _missiles.Add(new Missile
                {
                    Position = turretPos,
                    Velocity = missileVel,
                    Lifetime = 3f,
                    TargetIndex = -1
                });
            }

            _friendlyRingSections[i] = rs;
        }
    }

    private void DamageStation(float damage)
    {
        if (_stationHasShield)
        {
            _stationHasShield = false;
            return;
        }

        // Damage nearest ring section
        Vector2 playerDir = (_player.Position - new Vector2(_station.X, _station.Y)).Normalized();
        float playerAngle = MathF.Atan2(playerDir.Y, playerDir.X);
        for (int i = 0; i < _ringSections.Count; i++)
        {
            var rs = _ringSections[i];
            if (!rs.Active) continue;
            float angleDiff = MathF.Abs(MathHelper.WrapAngle(rs.Angle - playerAngle));
            if (angleDiff < MathF.PI / 4f)
            {
                rs.Health -= damage;
                if (rs.Health <= 0f)
                {
                    rs.Active = false;
                    _stationHasShield = false;
                    // Ring section explosion
                    float ringR = _station.BodyRadius + 8f + 8f;
                    Vector2 ringPos = new Vector2(_station.X, _station.Y) + Vector2.FromAngle(rs.Angle) * ringR;
                    _enemyExplosions.Add(new EnemyExplosion
                    {
                        Position = ringPos,
                        Timer = 0f,
                        Duration = 1.2f
                    });
                }
                _ringSections[i] = rs;
                return;
            }
        }

        // No ring section hit — damage station health
        _stationHealth -= damage;
        if (_stationHealth <= 0f)
        {
            _stationHealth = 0f;
            if (TrainingMode)
            {
                _empireStationRespawning = true;
                _empireStationRespawnTimer = 15f;
                Vector2 stationPos = new(_station.X, _station.Y);
                _enemyExplosions.Add(new EnemyExplosion
                {
                    Position = stationPos,
                    Timer = 0f,
                    Duration = 1.2f
                });
            }
            else
            {
                _system.Hostility = 0;
                _system.Faction = "Independent";
                _system.Station.DefenseLevel = 0;
                Vector2 stationPos = new(_station.X, _station.Y);
                _enemyExplosions.Add(new EnemyExplosion
                {
                    Position = stationPos,
                    Timer = 0f,
                    Duration = 1.2f
                });
                _stationRespawning = true;
                _stationRespawnTimer = 15f;
                _pickupMessage = "Station Captured! Respawning defenses...";
                _pickupTimer = 3f;
                _showPickupDialog = true;
            }
        }
    }

    private void DamageFriendlyStation(float damage)
    {
        if (_friendlyHasShield)
        {
            _friendlyHasShield = false;
            return;
        }

        // Damage the first active ring section
        for (int i = 0; i < _friendlyRingSections.Count; i++)
        {
            var rs = _friendlyRingSections[i];
            if (!rs.Active) continue;
            rs.Health -= damage;
            if (rs.Health <= 0f)
            {
                rs.Active = false;
                _friendlyHasShield = false;
                // Ring section explosion
                float ringR = _trainingFriendlyStation.BodyRadius + 8f + 8f;
                Vector2 ringPos = new Vector2(_trainingFriendlyStation.X, _trainingFriendlyStation.Y) + Vector2.FromAngle(rs.Angle) * ringR;
                _enemyExplosions.Add(new EnemyExplosion
                {
                    Position = ringPos,
                    Timer = 0f,
                    Duration = 1.2f
                });
            }
            _friendlyRingSections[i] = rs;
            return;
        }

        // No ring sections — damage station health
        _trainingFriendlyHealth -= damage;
        if (_trainingFriendlyHealth <= 0f)
        {
            _trainingFriendlyHealth = 0f;
            _trainingFriendlyRespawning = true;
            _trainingFriendlyRespawnTimer = 15f;
            Vector2 stationPos = new(_trainingFriendlyStation.X, _trainingFriendlyStation.Y);
            _enemyExplosions.Add(new EnemyExplosion
            {
                Position = stationPos,
                Timer = 0f,
                Duration = 1.2f
            });
        }
    }

    private static bool LineCircleIntersect(Vector2 lineStart, Vector2 lineEnd, Vector2 circleCenter, float circleRadius)
    {
        Vector2 d = lineEnd - lineStart;
        Vector2 f = circleCenter - lineStart;
        float a = d.X * d.X + d.Y * d.Y;
        float b = 2f * (f.X * d.X + f.Y * d.Y);
        float c = f.X * f.X + f.Y * f.Y - circleRadius * circleRadius;
        float disc = b * b - 4f * a * c;
        if (disc < 0f) return false;
        disc = MathF.Sqrt(disc);
        float t1 = (-b - disc) / (2f * a);
        float t2 = (-b + disc) / (2f * a);
        return (t1 >= 0f && t1 <= 1f) || (t2 >= 0f && t2 <= 1f) || (t1 < 0f && t2 > 1f);
    }

    private static void DrawLine(SpriteBatch sb, Texture2D pixel, float x1, float y1, float x2, float y2, Color color)
    {
        float dx = x2 - x1;
        float dy = y2 - y1;
        float len = MathF.Sqrt(dx * dx + dy * dy);
        if (len < 1f) return;
        float angle = MathF.Atan2(dy, dx);
        sb.Draw(pixel, new Microsoft.Xna.Framework.Vector2(x1, y1), null, color, angle,
            Microsoft.Xna.Framework.Vector2.Zero, new Microsoft.Xna.Framework.Vector2(len / pixel.Width, 1f),
            SpriteEffects.None, 0);
    }

    private static void FillTriangle(SpriteBatch sb, Texture2D pixel, float x0, float y0, float x1, float y1, float x2, float y2, Color color)
    {
        // Rasterize filled triangle by scanning horizontal spans
        float minY = MathF.Min(y0, MathF.Min(y1, y2));
        float maxY = MathF.Max(y0, MathF.Max(y1, y2));
        int iMinY = (int)minY;
        int iMaxY = (int)maxY;
        for (int y = iMinY; y <= iMaxY; y++)
        {
            float fy = y + 0.5f;
            float xLeft = float.MaxValue, xRight = float.MinValue;
            void edge(float vx0, float vy0, float vx1, float vy1)
            {
                if ((vy0 < fy && vy1 >= fy) || (vy1 < fy && vy0 >= fy))
                {
                    float t = (fy - vy0) / (vy1 - vy0);
                    float ex = vx0 + t * (vx1 - vx0);
                    if (ex < xLeft) xLeft = ex;
                    if (ex > xRight) xRight = ex;
                }
            }
            edge(x0, y0, x1, y1);
            edge(x1, y1, x2, y2);
            edge(x2, y2, x0, y0);
            if (xLeft < xRight)
                sb.Draw(pixel, new Microsoft.Xna.Framework.Rectangle((int)xLeft, y, (int)(xRight - xLeft + 1), 1), color);
        }
    }

    private static void DrawRect(SpriteBatch sb, Texture2D pixel, float x, float y, float w, float h, Color color)
    {
        DrawLine(sb, pixel, x, y, x + w, y, color);
        DrawLine(sb, pixel, x + w, y, x + w, y + h, color);
        DrawLine(sb, pixel, x + w, y + h, x, y + h, color);
        DrawLine(sb, pixel, x, y + h, x, y, color);
    }

    private static void DrawCircle(SpriteBatch sb, Texture2D pixel, float cx, float cy, float r, Color color)
    {
        int segments = Math.Clamp((int)(r * 0.5f), 8, 64);
        for (int i = 0; i < segments; i++)
        {
            float a1 = MathF.PI * 2 * i / segments;
            float a2 = MathF.PI * 2 * (i + 1) / segments;
            DrawLine(sb, pixel, cx + MathF.Cos(a1) * r, cy + MathF.Sin(a1) * r,
                cx + MathF.Cos(a2) * r, cy + MathF.Sin(a2) * r, color);
        }
    }

    private static void DrawCircleGC(SpriteBatch sb, Texture2D pixel, float cx, float cy, float r, Color color)
    {
        int segments = Math.Clamp((int)(r * 0.3f), 16, 64);
        for (int i = 0; i < segments; i++)
        {
            float a1 = MathF.PI * 2 * i / segments;
            float a2 = MathF.PI * 2 * (i + 1) / segments;
            DrawLine(sb, pixel, cx + MathF.Cos(a1) * r, cy + MathF.Sin(a1) * r,
                cx + MathF.Cos(a2) * r, cy + MathF.Sin(a2) * r, color);
        }
    }

    private static void FillCircle(SpriteBatch sb, Texture2D pixel, float cx, float cy, float r, Color color)
    {
        int segments = Math.Clamp((int)(r * 0.8f), 16, 64);
        float angleStep = MathF.PI * 2 / segments;
        for (int i = 0; i < segments; i++)
        {
            float a1 = angleStep * i;
            float a2 = angleStep * (i + 1);
            float x1 = cx + MathF.Cos(a1) * r;
            float y1 = cy + MathF.Sin(a1) * r;
            float x2 = cx + MathF.Cos(a2) * r;
            float y2 = cy + MathF.Sin(a2) * r;
            DrawLine(sb, pixel, cx, cy, x1, y1, color);
            DrawLine(sb, pixel, x1, y1, x2, y2, color);
        }
    }

    private static int WrapAndDraw(SpriteBatch sb, SpriteFont font, string text, int x, int y,
        int maxWidth, Color color, int lineSpacing)
    {
        if (string.IsNullOrEmpty(text)) return y;

        string[] words = text.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        if (words.Length == 0) return y;

        float spaceW = 8f;

        var lines = new List<string>();
        string currentLine = words[0];

        for (int i = 1; i < words.Length; i++)
        {
            string testLine = currentLine + " " + words[i];
            float w = 0;
            string[] testParts = testLine.Split(' ');
            foreach (var p in testParts)
            {
                if (p.Length > 0)
                    w += font.MeasureString(p).X + (w > 0 ? spaceW : 0);
            }
            if (w > maxWidth)
            {
                lines.Add(currentLine);
                currentLine = words[i];
            }
            else
            {
                currentLine = testLine;
            }
        }
        if (currentLine.Length > 0) lines.Add(currentLine);

        foreach (var line in lines)
        {
            DrawSpacedText(sb, font, line, new Microsoft.Xna.Framework.Vector2(x, y), color);
            y += lineSpacing;
        }
        return y;
    }

    private static void DrawSpacedText(SpriteBatch sb, SpriteFont font, string text, Microsoft.Xna.Framework.Vector2 position, Color color)
    {
        if (text.Length == 0) return;

        string[] parts = text.Split(' ');
        if (parts.Length <= 1)
        {
            sb.DrawString(font, text, position, color);
            return;
        }

        float spaceW = 8f;

        float x = position.X;
        float y = position.Y;
        for (int i = 0; i < parts.Length; i++)
        {
            if (parts[i].Length > 0)
            {
                sb.DrawString(font, parts[i], new Microsoft.Xna.Framework.Vector2(x, y), color);
                x += font.MeasureString(parts[i]).X + spaceW;
            }
            else
            {
                x += spaceW;
            }
        }
    }
}

