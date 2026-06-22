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
    private float _spawnRadius;
    private const float ZOOM = 3.75f;
    private float _systemRadius;
    private bool _docked;
    private int _dockedTab;
    private int _dockedQuestSelection;
    private int _dockedMarketSelection;
    private int _dockedUpgradeSelection;
    private bool _nearEdge;
    private bool _nearPlanet;
    private bool _initialized;
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
    private bool _showEnemyList;

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

    private struct Bullet
    {
        public Vector2 Position;
        public Vector2 Velocity;
        public float Lifetime;
        public bool IsPlayerBullet;
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
        _trainingPaused = false;
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

        if (_system.Station != null)
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
        }

        _systemRadius = 0f;
        foreach (var p in _planets)
            if (p.OrbitRadius > _systemRadius) _systemRadius = p.OrbitRadius;
        if (_system.Station != null && _station.OrbitRadius > _systemRadius)
            _systemRadius = _station.OrbitRadius;
        _systemRadius = MathF.Max(MathF.Max(_systemRadius, 1f) * 1.1f, _star.BodyRadius * 2f);

        // Ensure station orbits outside overheating range
        if (_system.Station != null)
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
            _waypointTargets.Add((new Vector2(_station.X, _station.Y), _station.Name));
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
        if (_system.Hostility >= 3)
            SpawnHostileEnemies();
    }

    private static float RandF() { return (float)Random.Shared.NextDouble(); }

    public void Update(float dt, KeyboardState keyboard, MouseState mouse)
    {
        Initialize();

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
                _station.CurrentAngle += _station.OrbitSpeed * dt;
                _station.X = MathF.Cos(_station.CurrentAngle) * _station.OrbitRadius;
                _station.Y = MathF.Sin(_station.CurrentAngle) * _station.OrbitRadius;
            }

            // Rebuild waypoint targets
            _waypointTargets.Clear();
            if (_system.Station != null)
                _waypointTargets.Add((new Vector2(_station.X, _station.Y), _station.Name));
            if (_game != null)
            {
                foreach (var q in _game.Galaxy.ActiveQuests)
                {
                    if (q.TargetSystem != _system.Id) continue;
                    if (q.ObjectiveType == "collect" && _lifepodActive)
                        _waypointTargets.Add((new Vector2(_lifepod.X, _lifepod.Y), q.Name));
                    else if (q.ObjectiveType == "travel")
                        _waypointTargets.Add((Vector2.Zero, q.Name));
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
                : (_system.Station != null ? new Vector2(_station.X, _station.Y) : Vector2.Zero);

            // Tab to cycle waypoint
            if (keyboard.IsKeyDown(Keys.Tab) && _prevKeyboard.IsKeyUp(Keys.Tab))
            {
                _waypointIndex = (_waypointIndex + 1) % Math.Max(1, _waypointTargets.Count);
                _waypointPosition = _waypointTargets.Count > 0
                    ? _waypointTargets[_waypointIndex].pos
                    : _waypointPosition;
            }

            // Training mode input (F1, ESC pause) - intercepts normal update
            if (TrainingMode)
            {
                bool esc = keyboard.IsKeyDown(Keys.Escape) && _prevKeyboard.IsKeyUp(Keys.Escape);
                bool f1 = keyboard.IsKeyDown(Keys.F1) && _prevKeyboard.IsKeyUp(Keys.F1);
                bool down = (keyboard.IsKeyDown(Keys.Down) && _prevKeyboard.IsKeyUp(Keys.Down)) ||
                            (keyboard.IsKeyDown(Keys.S) && _prevKeyboard.IsKeyUp(Keys.S));
                bool up = (keyboard.IsKeyDown(Keys.Up) && _prevKeyboard.IsKeyUp(Keys.Up)) ||
                          (keyboard.IsKeyDown(Keys.W) && _prevKeyboard.IsKeyUp(Keys.W));
                bool enter = keyboard.IsKeyDown(Keys.Enter) && _prevKeyboard.IsKeyUp(Keys.Enter);

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

                if (esc || f1 || _showEnemyList)
                {
                    _prevKeyboard = keyboard;
                    return;
                }

                if (_trainingPaused)
                {
                    if (down)
                        _trainingMenuSelection++;
                    if (up)
                        _trainingMenuSelection--;
                    int max2 = 1;
                    if (_trainingMenuSelection < 0) _trainingMenuSelection = max2;
                    if (_trainingMenuSelection > max2) _trainingMenuSelection = 0;
                    if (enter)
                    {
                        if (_trainingMenuSelection == 0)
                            _trainingPaused = false;
                        else if (_trainingMenuSelection == 1)
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
            if (speed > 10f && _player.Fuel > 0)
            {
                float fuelRate = 3f;
                _player.Fuel = MathF.Max(0, _player.Fuel - (speed / _player.MaxSpeed) * fuelRate * dt * _player.FuelEfficiency);
            }

            // Temperature (non-linear: slow rise far away, spikes near star)
            float dist = _player.Position.Length();
            float norm = MathHelper.Clamp(
                (dist - _star.BodyRadius) / (_systemRadius - _star.BodyRadius), 0f, 1f);
            _temperature = 1f - MathF.Pow(norm, 0.5f);

            if (!_exploding && (_temperature >= 1f || _player.Health <= 0))
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
            if (distToStation < 150f && _system.Hostility < 3)
            {
                if (keyboard.IsKeyDown(Keys.E) && prevKb.IsKeyUp(Keys.E))
                {
                    _docked = true;
                    _player.Fuel = _player.MaxFuel;
                    _player.Health = _player.MaxHealth;
                }
            }

            // Fuel exchange prompt (when out of fuel)
            if (_player.Fuel <= 0 && !_exploding && !_gameOver)
            {
                if (!_docked)
                {
                    if (keyboard.IsKeyDown(Keys.Y) && prevKb.IsKeyUp(Keys.Y) && _player.Health > _player.MaxHealth / 4)
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
                        _bullets.Add(new Bullet
                        {
                            Position = muzzlePos,
                            Velocity = Vector2.FromAngle(_player.Angle) * BULLET_SPEED + _player.Velocity * 0.5f,
                            Lifetime = BULLET_LIFETIME,
                            IsPlayerBullet = true
                        });
                    }
                }
                else if (_activeWeapon == 1 && hasLaser)
                {
                    // Laser beam - fires forward from ship nose
                    if (spaceHeld && _weaponCooldown <= 0f && _player.Fuel > 0)
                    {
                        _weaponCooldown = 0.1f;
                        _player.Fuel = MathF.Max(0, _player.Fuel - 1.667f * dt);

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
                        for (int ei = _enemies.Count - 1; ei >= 0; ei--)
                        {
                            var e = _enemies[ei];
                            if (LineCircleIntersect(muzzlePos, beamEnd, e.Position, 16f))
                            {
                                e.Health -= beamDmg;
                                if (e.Health <= 0f)
                                {
                                    _enemyExplosions.Add(new EnemyExplosion
                                    {
                                        Position = e.Position,
                                        Timer = 0f,
                                        Duration = 0.6f
                                    });
                                    _enemies.RemoveAt(ei);
                                }
                                else
                                {
                                    _enemies[ei] = e;
                                }
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
                        if (_system.Hostility >= 3 && _system.Station != null)
                        {
                            Vector2 stationPos = new(_station.X, _station.Y);
                            if (LineCircleIntersect(muzzlePos, beamEnd, stationPos, _station.BodyRadius))
                            {
                                _stationHealth -= beamDmg;
                                if (_stationHealth <= 0f)
                                {
                                    _stationHealth = 0f;
                                    _system.Hostility = 0;
                                    _system.Faction = "Independent";
                                    _enemyExplosions.Add(new EnemyExplosion
                                    {
                                        Position = stationPos,
                                        Timer = 0f,
                                        Duration = 1.2f
                                    });
                                    _pickupMessage = "Station Captured!";
                                    _pickupTimer = 3f;
                                    _showPickupDialog = true;
                                }
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

                // Homing toward target
                if (!remove && m.TargetIndex >= 0 && m.TargetIndex < _enemies.Count)
                {
                    var target = _enemies[m.TargetIndex];
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
                        target.Health -= 5f;
                        if (target.Health <= 0f)
                        {
                            _enemyExplosions.Add(new EnemyExplosion
                            {
                                Position = target.Position,
                                Timer = 0f,
                                Duration = 0.6f
                            });
                            _enemies.RemoveAt(m.TargetIndex);
                        }
                        else
                        {
                            _enemies[m.TargetIndex] = target;
                        }
                        remove = true;
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
                if (!remove && _system.Hostility >= 3 && _system.Station != null)
                {
                    Vector2 stationPos = new(_station.X, _station.Y);
                    if (Vector2.Distance(m.Position, stationPos) < _station.BodyRadius + 5f)
                    {
                        _stationHealth -= 5f;
                        remove = true;
                        if (_stationHealth <= 0f)
                        {
                            _stationHealth = 0f;
                            _system.Hostility = 0;
                            _system.Faction = "Independent";
                            _enemyExplosions.Add(new EnemyExplosion
                            {
                                Position = stationPos,
                                Timer = 0f,
                                Duration = 1.2f
                            });
                            _pickupMessage = "Station Captured!";
                            _pickupTimer = 3f;
                            _showPickupDialog = true;
                        }
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

            // Bullet-enemy collisions
            for (int i = _bullets.Count - 1; i >= 0; i--)
            {
                var b = _bullets[i];
                if (!b.IsPlayerBullet) continue;
                bool hit = false;
                for (int j = _enemies.Count - 1; j >= 0; j--)
                {
                    var e = _enemies[j];
                    if (Vector2.Distance(b.Position, e.Position) < 16f)
                    {
                        e.Health -= 1f;
                        if (e.Health <= 0f)
                        {
                            _enemyExplosions.Add(new EnemyExplosion
                            {
                                Position = e.Position,
                                Timer = 0f,
                                Duration = 0.6f
                            });
                            _enemies.RemoveAt(j);
                        }
                        else
                            _enemies[j] = e;
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
            if (_system.Hostility >= 3 && _system.Station != null)
            {
                Vector2 stationPos = new(_station.X, _station.Y);
                for (int i = _bullets.Count - 1; i >= 0; i--)
                {
                    var b = _bullets[i];
                    if (!b.IsPlayerBullet) continue;
                    if (Vector2.Distance(b.Position, stationPos) < _station.BodyRadius + 5f)
                    {
                        _stationHealth -= 1f;
                        _bullets.RemoveAt(i);
                        if (_stationHealth <= 0f)
                        {
                            _stationHealth = 0f;
                            _system.Hostility = 0;
                            _system.Faction = "Independent";
                            _enemyExplosions.Add(new EnemyExplosion
                            {
                                Position = stationPos,
                                Timer = 0f,
                                Duration = 1.2f
                            });
                            _pickupMessage = "Station Captured!";
                            _pickupTimer = 3f;
                            _showPickupDialog = true;
                        }
                    }
                }
            }

            // Enemy bullet - player collision
            for (int i = _bullets.Count - 1; i >= 0; i--)
            {
                var b = _bullets[i];
                if (b.IsPlayerBullet) continue;
                if (Vector2.Distance(b.Position, _player.Position) < 22f)
                {
                    _player.Health -= 2;
                    _bullets.RemoveAt(i);
                }
            }

            // Enemy-player collision (ramming) - disabled
            //for (int i = 0; i < _enemies.Count; i++)
            //{
            //    var e = _enemies[i];
            //    if (Vector2.Distance(e.Position, _player.Position) < 25f * ZOOM)
            //    {
            //        _player.Health = 0;
            //        break;
            //    }
            //}

            // Spawn enemies in training mode
            if (TrainingMode && _enemies.Count == 0 && !_exploding && !_docked)
                SpawnScouts();

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

            if (leftDocked)
            {
                _dockedTab = (_dockedTab - 1 + 3) % 3;
                _dockedQuestSelection = 0;
                _dockedMarketSelection = 0;
            }
            if (rightDocked)
            {
                _dockedTab = (_dockedTab + 1) % 3;
                _dockedQuestSelection = 0;
                _dockedMarketSelection = 0;
            }

            if (_dockedTab == 0)
            {
                // Market tab: Enter=buy, Backspace=sell
                var economy = _game.Galaxy.Economy;
                var resources = _game.Galaxy.AllResources
                    .Where(r => economy.HasResource(_system.Id, r.Id))
                    .ToList();
                int marketItemCount = resources.Count + 2; // +1 energy canister, +1 fuel cell
                bool backDocked = keyboard.IsKeyDown(Keys.Back) && _prevKeyboard.IsKeyUp(Keys.Back);
                if (downDocked)
                    _dockedMarketSelection = (_dockedMarketSelection + 1) % Math.Max(1, marketItemCount);
                if (upDocked)
                    _dockedMarketSelection = (_dockedMarketSelection - 1 + Math.Max(1, marketItemCount)) % Math.Max(1, marketItemCount);
                if (enterDocked && marketItemCount > 0)
                {
                    if (_dockedMarketSelection < resources.Count)
                    {
                        var res = resources[_dockedMarketSelection];
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
                }
                if (backDocked && resources.Count > 0 && _dockedMarketSelection < resources.Count)
                {
                    var res = resources[_dockedMarketSelection];
                    economy.Sell(_player, _system.Id, res.Id, 1);
                }
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
            else
            {
                // Quests tab
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
                            _game.Galaxy.AcceptQuest(available[_dockedQuestSelection].Id);
                            _dockedQuestSelection = 0;
                        }
                        else
                        {
                            int ci = _dockedQuestSelection - available.Count;
                            if (ci < completable.Count)
                            {
                                _game.Galaxy.CompleteQuest(completable[ci], _player);
                                _dockedQuestSelection = 0;
                            }
                        }
                    }
                }
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
        float sx = starScreenX + _station.X * ZOOM;
        float sy = starScreenY + _station.Y * ZOOM;
        DrawStation(sb, pixel, font, sx, sy, _station, t);

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
            DrawShip(sb, pixel, ePos, t, 0.5f, new Color(255, 140, 0), e.Angle, e.Velocity);

            // Health bar
            float hpPct = e.Health / e.MaxHealth;
            int barW = 24;
            int barH = 3;
            int barX = (int)ex - barW / 2;
            int barY = (int)ey + 14;
            sb.Draw(pixel, new Microsoft.Xna.Framework.Rectangle(barX, barY, barW, barH), new Color(40, 0, 0, 180));
            sb.Draw(pixel, new Microsoft.Xna.Framework.Rectangle(barX, barY, (int)(barW * hpPct), barH), Color.Red);
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

            string tip = "[F1] Enemy List  [ESC] Pause";
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

            string[] opts = { "Resume", "End Training" };
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

        // Training enemy list overlay
        if (_showEnemyList)
        {
            sb.Draw(pixel, new Microsoft.Xna.Framework.Rectangle(0, 0, screenW, screenH),
                new Color(0, 0, 0, 160));

            string hdr = "TARGET LIST";
            var hSz = titleFont.MeasureString(hdr);
            float hX = (screenW - hSz.X) / 2f;
            float hY = screenH * 0.15f;
            DrawSpacedText(sb, titleFont, hdr,
                new Microsoft.Xna.Framework.Vector2(hX, hY), Color.Yellow);

            string[] enemies = {
                "1. Pirate Scout - Easy",
                "2. Raider Fighter - Easy",
                "3. Mercenary Gunship - Medium",
                "4. Syndicate Cruiser - Hard",
                "5. Void Dreadnought - Extreme"
            };
            float startY = hY + 60f;
            for (int i = 0; i < enemies.Length; i++)
            {
                var eSz = font.MeasureString(enemies[i]);
                float eX = (screenW - eSz.X) / 2f;
                DrawSpacedText(sb, font, enemies[i],
                    new Microsoft.Xna.Framework.Vector2(eX, startY + i * 30f), Color.Gray * 0.8f);
            }

            string close = "[F1] or [ESC] to close";
            var cSz = font.MeasureString(close);
            float cX = (screenW - cSz.X) / 2f;
            DrawSpacedText(sb, font, close,
                new Microsoft.Xna.Framework.Vector2(cX, screenH * 0.85f), Color.White * 0.5f);
        }
    }

    private void DrawStation(SpriteBatch sb, Texture2D pixel, SpriteFont font,
        float sx, float sy, Body station, float t)
    {
        float pulse = MathF.Sin(t * 2f) * 0.05f + 1f;
        float r = station.BodyRadius * ZOOM * pulse;

        bool isEnemy = _system.Hostility >= 3;
        Color baseColor = isEnemy ? new Color(200, 60, 60) : Color.LightBlue;
        Color glowColor = isEnemy ? new Color(255, 80, 80) : Color.Cyan;
        Color dimGlow = isEnemy ? new Color(180, 40, 40) : Color.LightBlue;

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

        // Label
        var label = station.Name;
        var labelSize = font.MeasureString(label);
        DrawSpacedText(sb, font, label,
            new Microsoft.Xna.Framework.Vector2(sx - labelSize.X / 2f, sy + r + 4f),
            glowColor);

        // Health bar (enemy stations) or dock prompt (friendly)
        if (isEnemy)
        {
            // Health bar above station
            int barW = (int)(r * 1.5f);
            int barH = 4;
            float barX = sx - barW / 2f;
            float barY = sy - r - antH - 14f;
            float hpPct = _stationHealth / _stationMaxHealth;
            sb.Draw(pixel, new Microsoft.Xna.Framework.Rectangle((int)barX, (int)barY, barW, barH), new Color(40, 0, 0, 180));
            sb.Draw(pixel, new Microsoft.Xna.Framework.Rectangle((int)barX, (int)barY, (int)(barW * hpPct), barH), Color.Red);

            float dist = Vector2.Distance(_player.Position, new Vector2(station.X, station.Y));
            if (dist < 200f && !_docked)
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
            float dist = Vector2.Distance(_player.Position, new Vector2(station.X, station.Y));
            if (dist < 150f && !_docked)
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
        textY += 35;

        // Tab bar
        string[] tabs = { "  Market  ", "  Upgrades  ", "  Quests  " };
        int tabX = textX;
        for (int i = 0; i < tabs.Length; i++)
        {
            Color tc = i == _dockedTab ? Color.White : Color.Gray * 0.5f;
            Color bg = i == _dockedTab ? new Color(40, 40, 70) : Color.Transparent;
            var sz = font.MeasureString(tabs[i]);
            sb.Draw(pixel, new Microsoft.Xna.Framework.Rectangle(tabX, textY, (int)sz.X + 8, (int)sz.Y + 4), bg);
            DrawSpacedText(sb, font, tabs[i],
                new Microsoft.Xna.Framework.Vector2(tabX + 4, textY + 2), tc);
            tabX += (int)sz.X + 12;
        }
        textY += 40;

        // Tab content
        if (_dockedTab == 0)
            DrawMarketTab(sb, pixel, font, titleFont, textX, textY, panelW, screenW, screenH, px, py);
        else if (_dockedTab == 1)
            DrawUpgradesTab(sb, pixel, font, titleFont, textX, textY, panelW, screenW, screenH, px, py);
        else
            DrawQuestsTab(sb, pixel, font, titleFont, textX, textY, panelW, screenW, screenH, px, py);

        string foot = "[Left/Right] Tab  [Up/Dn] Select  [Enter] Buy  [BkSp] Sell  [ESC] Undock";
        DrawSpacedText(sb, font, foot,
            new Microsoft.Xna.Framework.Vector2(textX, screenH - py - 30), Color.Gray * 0.7f);
    }

    private void DrawMarketTab(SpriteBatch sb, Texture2D pixel, SpriteFont font, SpriteFont titleFont,
        int textX, int textY, int panelW, int screenW, int screenH, int px, int py)
    {
        var economy = _game.Galaxy.Economy;
        var resources = _game.Galaxy.AllResources
            .Where(r => economy.HasResource(_system.Id, r.Id))
            .OrderBy(r => r.Category)
            .ThenBy(r => r.Name)
            .ToList();

        DrawSpacedText(sb, titleFont, "--- Market ---",
            new Microsoft.Xna.Framework.Vector2(textX, textY), Color.Lime);
        textY += 30;

        // Column positions
        int cSel = 0;
        int cName = 30;
        int cBuy = 180;
        int cSell = 260;
        int cStock = 340;
        int cCargo = 420;
        int cHint = 500;

        // Header row background
        int headerH = 22;
        sb.Draw(pixel, new Microsoft.Xna.Framework.Rectangle(textX, textY, panelW - 40, headerH),
            new Color(30, 30, 60));
        DrawSpacedText(sb, font, "Item",
            new Microsoft.Xna.Framework.Vector2(textX + cName, textY + 2), Color.Gold);
        DrawSpacedText(sb, font, "Buy",
            new Microsoft.Xna.Framework.Vector2(textX + cBuy, textY + 2), Color.Gold);
        DrawSpacedText(sb, font, "Sell",
            new Microsoft.Xna.Framework.Vector2(textX + cSell, textY + 2), Color.Gold);
        DrawSpacedText(sb, font, "Stock",
            new Microsoft.Xna.Framework.Vector2(textX + cStock, textY + 2), Color.Gold);
        DrawSpacedText(sb, font, "Cargo",
            new Microsoft.Xna.Framework.Vector2(textX + cCargo, textY + 2), Color.Gold);
        sb.Draw(pixel, new Microsoft.Xna.Framework.Rectangle(textX, textY + headerH, panelW - 40, 1),
            Color.Gold * 0.5f);
        textY += headerH + 6;

        int rowH = 20;
        string? lastCategory = null;
        for (int i = 0; i < resources.Count; i++)
        {
            var r = resources[i];
            bool selected = i == _dockedMarketSelection;
            int stock = economy.GetStock(_system.Id, r.Id);
            int buyPrice = economy.GetBuyPrice(_system.Id, r.Id);
            int sellPrice = economy.GetSellPrice(_system.Id, r.Id);
            var playerEntry = _player.Resources.FirstOrDefault(e => e.Id == r.Id);
            int playerQty = playerEntry?.Quantity ?? 0;

            // Category separator
            if (r.Category != lastCategory)
            {
                lastCategory = r.Category;
                DrawSpacedText(sb, font, r.Category.ToUpper(),
                    new Microsoft.Xna.Framework.Vector2(textX, textY), Color.Gray * 0.5f);
                textY += rowH;
            }

            // Row background for selected item
            if (selected)
                sb.Draw(pixel, new Microsoft.Xna.Framework.Rectangle(textX, textY, panelW - 40, rowH - 1),
                    new Color(40, 50, 70));

            Color c = selected ? Color.White : Color.LightGray;

            // Selection marker
            DrawSpacedText(sb, font, selected ? ">" : " ",
                new Microsoft.Xna.Framework.Vector2(textX + cSel, textY), c);

            // Name with symbol
            DrawSpacedText(sb, font, $"[{r.Symbol}] {r.Name}",
                new Microsoft.Xna.Framework.Vector2(textX + cName, textY), c);

            // Buy price (green tint if affordable)
            Color buyC = selected && _player.Credits >= buyPrice ? Color.Lime : c;
            DrawSpacedText(sb, font, $"{buyPrice}cr",
                new Microsoft.Xna.Framework.Vector2(textX + cBuy, textY), buyC);

            // Sell price
            Color sellC = selected && playerQty > 0 ? Color.Orange : c;
            DrawSpacedText(sb, font, $"{sellPrice}cr",
                new Microsoft.Xna.Framework.Vector2(textX + cSell, textY), sellC);

            // Stock
            string stockStr = stock > 0 ? $"{stock}" : "-";
            DrawSpacedText(sb, font, stockStr,
                new Microsoft.Xna.Framework.Vector2(textX + cStock, textY), c);

            // Player cargo
            string cargoStr = playerQty > 0 ? $"{playerQty}" : "-";
            DrawSpacedText(sb, font, cargoStr,
                new Microsoft.Xna.Framework.Vector2(textX + cCargo, textY), c);

            // Action hint
            if (selected)
            {
                bool canBuy = _player.Credits >= buyPrice && _player.UsedCargo + r.Volume <= _player.CargoCapacity;
                bool canSell = playerQty > 0;
                string hint = "";
                if (canBuy) hint += "[Enter] Buy  ";
                if (canSell) hint += "[BkSp] Sell";
                if (hint.Length > 0)
                    DrawSpacedText(sb, font, hint.Trim(),
                        new Microsoft.Xna.Framework.Vector2(textX + cHint, textY), Color.Lime * 0.7f);
            }

            textY += rowH;
        }

        // Separator
        textY += 8;
        sb.Draw(pixel, new Microsoft.Xna.Framework.Rectangle(textX, textY, panelW - 40, 1), Color.Gray * 0.3f);
        textY += 8;

        // Energy canister row
        bool canBuyCan = _player.Credits >= 50 && _player.UsedCargo < _player.CargoCapacity;
        bool canSelected = _dockedMarketSelection == resources.Count;
        Color canColor = canSelected ? Color.White : Color.LightGray;
        if (canSelected)
            sb.Draw(pixel, new Microsoft.Xna.Framework.Rectangle(textX, textY, panelW - 40, rowH - 1),
                new Color(40, 50, 70));
        DrawSpacedText(sb, font, canSelected ? ">" : " ",
            new Microsoft.Xna.Framework.Vector2(textX + cSel, textY), canColor);
        DrawSpacedText(sb, font, "[Energy Canister]",
            new Microsoft.Xna.Framework.Vector2(textX + cName, textY), canColor);
        DrawSpacedText(sb, font, "50cr",
            new Microsoft.Xna.Framework.Vector2(textX + cBuy, textY), canSelected && canBuyCan ? Color.Lime : canColor);
        DrawSpacedText(sb, font, "---",
            new Microsoft.Xna.Framework.Vector2(textX + cSell, textY), Color.Gray * 0.5f);
        DrawSpacedText(sb, font, "---",
            new Microsoft.Xna.Framework.Vector2(textX + cStock, textY), Color.Gray * 0.5f);
        int canQty = _player.Consumables.FirstOrDefault(c => c.Id == "energy_canister")?.Quantity ?? 0;
        DrawSpacedText(sb, font, canQty > 0 ? $"{canQty}" : "-",
            new Microsoft.Xna.Framework.Vector2(textX + cCargo, textY), canColor);
        if (canSelected && canBuyCan)
            DrawSpacedText(sb, font, "[Enter] Buy",
                new Microsoft.Xna.Framework.Vector2(textX + cHint, textY), Color.Lime * 0.7f);
        textY += rowH;

        // Fuel cell row
        bool canBuyFC = _player.Credits >= 25 && _player.UsedCargo < _player.CargoCapacity;
        bool fcSelected = _dockedMarketSelection == resources.Count + 1;
        Color fcColor = fcSelected ? Color.White : Color.LightGray;
        if (fcSelected)
            sb.Draw(pixel, new Microsoft.Xna.Framework.Rectangle(textX, textY, panelW - 40, rowH - 1),
                new Color(40, 50, 70));
        DrawSpacedText(sb, font, fcSelected ? ">" : " ",
            new Microsoft.Xna.Framework.Vector2(textX + cSel, textY), fcColor);
        DrawSpacedText(sb, font, "[Fuel Cell]",
            new Microsoft.Xna.Framework.Vector2(textX + cName, textY), fcColor);
        DrawSpacedText(sb, font, "25cr",
            new Microsoft.Xna.Framework.Vector2(textX + cBuy, textY), fcSelected && canBuyFC ? Color.Lime : fcColor);
        DrawSpacedText(sb, font, "---",
            new Microsoft.Xna.Framework.Vector2(textX + cSell, textY), Color.Gray * 0.5f);
        DrawSpacedText(sb, font, "---",
            new Microsoft.Xna.Framework.Vector2(textX + cStock, textY), Color.Gray * 0.5f);
        int fcQty = _player.Consumables.FirstOrDefault(c => c.Id == "fuel_cell")?.Quantity ?? 0;
        DrawSpacedText(sb, font, fcQty > 0 ? $"{fcQty}" : "-",
            new Microsoft.Xna.Framework.Vector2(textX + cCargo, textY), fcColor);
        if (fcSelected && canBuyFC)
            DrawSpacedText(sb, font, "[Enter] Buy",
                new Microsoft.Xna.Framework.Vector2(textX + cHint, textY), Color.Lime * 0.7f);
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
            Color stColor = _system.Hostility >= 3 ? new Color(200, 60, 60) : Color.LightBlue;
            FillCircle(sb, pixel, stx, sty, stR, stColor);
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
                FillCircle(sb, pixel, ex, ey, 2.5f, new Color(255, 140, 0));
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

    private void SpawnScouts()
    {
        int count = 3;
        for (int i = 0; i < count; i++)
        {
            float spawnAngle = RandF() * MathF.Tau;
            float spawnDist = 300f + RandF() * 300f;
            Vector2 pos = _player.Position + Vector2.FromAngle(spawnAngle) * spawnDist;
            float a = RandF() * MathF.Tau;
            _enemies.Add(new EnemyShip
            {
                Position = pos,
                Velocity = Vector2.FromAngle(a) * (100f + RandF() * 50f),
                Angle = a,
                Health = 3f,
                MaxHealth = 3f,
                Type = "scout",
                ShootCooldown = RandF() * 2f,
                AiState = AiState.Idle,
                StateTimer = RandF() * 2f,
                OrbitAngle = spawnAngle
            });
        }
    }

    private void SpawnHostileEnemies()
    {
        int count = 3;
        var trigorSys = _game?.Galaxy?.FindSystemById("trigor");
        if (trigorSys != null)
        {
            float dx = _system.X - trigorSys.X;
            float dy = _system.Y - trigorSys.Y;
            float dist = MathF.Sqrt(dx * dx + dy * dy);
            float maxDist = 15000f;
            float t = MathF.Max(0f, MathF.Min(1f, dist / maxDist));
            // t=0 (at trigor) → count=9, t=1 (far) → count=3
            count = 3 + (int)((1f - t) * 6f);
        }

        for (int i = 0; i < count; i++)
        {
            float spawnAngle = RandF() * MathF.Tau;
            float spawnDist = 200f + RandF() * 400f;
            Vector2 pos = _player.Position + Vector2.FromAngle(spawnAngle) * spawnDist;
            float a = RandF() * MathF.Tau;
            _enemies.Add(new EnemyShip
            {
                Position = pos,
                Velocity = Vector2.FromAngle(a) * (80f + RandF() * 70f),
                Angle = a,
                Health = 3f,
                MaxHealth = 3f,
                Type = "scout",
                ShootCooldown = RandF() * 2f,
                AiState = AiState.Attack,
                StateTimer = 0f,
                OrbitAngle = spawnAngle
            });
        }
    }

    private void UpdateEnemyAI(ref EnemyShip e, float dt)
    {
        Vector2 toPlayer = _player.Position - e.Position;
        float distToPlayer = toPlayer.Length();
        Vector2 dirToPlayer = distToPlayer > 0.01f ? toPlayer.Normalized() : Vector2.Zero;

        e.StateTimer -= dt;
        e.ShootCooldown -= dt;

        float maxSpeed = 112f;

        switch (e.AiState)
        {
            case AiState.Idle:
            {
                if (e.StateTimer <= 0f)
                {
                    e.Angle = RandF() * MathF.Tau;
                    e.StateTimer = 1.5f + RandF() * 2f;
                }
                e.Velocity += Vector2.FromAngle(e.Angle) * dt * 100f;
                if (e.Velocity.Length() > maxSpeed * 0.7f)
                    e.Velocity = e.Velocity.Normalized() * maxSpeed * 0.7f;
                e.Angle = MathF.Atan2(e.Velocity.Y, e.Velocity.X);

                if (distToPlayer < 500f)
                {
                    e.AiState = AiState.Orbit;
                    e.OrbitAngle = MathF.Atan2(
                        e.Position.Y - _player.Position.Y,
                        e.Position.X - _player.Position.X);
                    e.StateTimer = 2f + RandF() * 2f;
                }
                break;
            }
            case AiState.Orbit:
            {
                e.OrbitAngle += dt * 0.5f;
                float targetDist = 150f + RandF() * 80f;
                Vector2 orbitTarget = _player.Position + Vector2.FromAngle(e.OrbitAngle) * targetDist;
                Vector2 toTarget = orbitTarget - e.Position;
                float tDist = toTarget.Length();
                if (tDist > 10f)
                {
                    e.Velocity += toTarget.Normalized() * dt * 200f;
                    if (e.Velocity.Length() > maxSpeed)
                        e.Velocity = e.Velocity.Normalized() * maxSpeed;
                }
                else
                {
                    e.Velocity *= 0.95f;
                }
                if (e.Velocity.Length() > 10f)
                    e.Angle = MathF.Atan2(e.Velocity.Y, e.Velocity.X);

                if (distToPlayer < 400f && e.StateTimer <= 0f)
                {
                    e.AiState = AiState.Attack;
                    e.StateTimer = 1.5f + RandF() * 1.5f;
                }
                break;
            }
            case AiState.Attack:
            {
                e.Velocity += dirToPlayer * dt * 250f;
                if (e.Velocity.Length() > maxSpeed * 1.2f)
                    e.Velocity = e.Velocity.Normalized() * maxSpeed * 1.2f;
                if (e.Velocity.Length() > 10f)
                    e.Angle = MathF.Atan2(e.Velocity.Y, e.Velocity.X);

                if (distToPlayer < 350f && e.ShootCooldown <= 0f)
                {
                    e.ShootCooldown = 1.5f + RandF() * 0.5f;
                    Vector2 bulletVel = Vector2.FromAngle(e.Angle) * BULLET_SPEED * 0.8f + e.Velocity * 0.3f;
                    _bullets.Add(new Bullet
                    {
                        Position = e.Position,
                        Velocity = bulletVel,
                        Lifetime = BULLET_LIFETIME * 0.8f,
                        IsPlayerBullet = false
                    });
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

