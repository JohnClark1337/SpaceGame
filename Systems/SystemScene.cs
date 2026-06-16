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
    private float _spawnRadius;
    private const float ZOOM = 3.75f;
    private float _systemRadius;
    private bool _docked;
    private bool _nearEdge;
    private bool _initialized;
    private float _temperature;
    private bool _exploding;
    private float _explosionTimer;
    private Vector2 _explosionPos;
    private float _explosionAngle;
    private bool _gameOver;
    private List<ExplosionDebris> _debris = new();

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

    public bool Docked => _docked;
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
        }

        _systemRadius = 0f;
        foreach (var p in _planets)
            if (p.OrbitRadius > _systemRadius) _systemRadius = p.OrbitRadius;
        if (_system.Station != null && _station.OrbitRadius > _systemRadius)
            _systemRadius = _station.OrbitRadius;
        _systemRadius = MathF.Max(_systemRadius, 1f) * 1.1f;

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

        float spawnAngle = RandF() * MathF.Tau;
        _player.Position = new Vector2(
            MathF.Cos(spawnAngle) * _spawnRadius,
            MathF.Sin(spawnAngle) * _spawnRadius);
        _player.Velocity = Vector2.Zero;
        _player.Angle = spawnAngle + MathF.PI;

        _waypointPosition = _system.Station != null
            ? new Vector2(_station.X, _station.Y)
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
    }

    private static float RandF() { return (float)Random.Shared.NextDouble(); }

    public void Update(float dt, KeyboardState keyboard, MouseState mouse)
    {
        Initialize();

        float t = (float)_game.GameTime.TotalGameTime.TotalSeconds;

        if (_gameOver)
        {
            if (JustPressed(keyboard, Keys.Enter))
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
                _waypointPosition = new Vector2(_station.X, _station.Y);
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

            // Temperature (non-linear: slow rise far away, spikes near star)
            float dist = _player.Position.Length();
            float norm = MathHelper.Clamp(
                (dist - _star.BodyRadius) / (_systemRadius - _star.BodyRadius), 0f, 1f);
            _temperature = 1f - MathF.Pow(norm, 0.5f);

            if (_temperature >= 1f)
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

            float distToStation = Vector2.Distance(_player.Position, new Vector2(_station.X, _station.Y));
            if (distToStation < 150f)
            {
                if (JustPressed(keyboard, Keys.E))
                {
                    _docked = true;
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

            float distFromCenter = _player.Position.Length();
            _nearEdge = distFromCenter > _systemRadius * 0.85f;
            if (distFromCenter > _systemRadius)
            {
                _game.ExitToGalaxy();
                return;
            }
        }
        else
        {
            if (JustPressed(keyboard, Keys.Escape))
                _docked = false;
        }
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
            sb.DrawString(font, p.Name,
                new Microsoft.Xna.Framework.Vector2(px - labelSize.X / 2f, py + pr + 4f),
                Color.White * 0.6f);
        }

        // Station
        float sx = starScreenX + _station.X * ZOOM;
        float sy = starScreenY + _station.Y * ZOOM;
        DrawStation(sb, pixel, font, sx, sy, _station, t);

        // Player ship (always at center)
        DrawShip(sb, pixel, center, t);

        // Docked menu / HUD
        if (_docked)
            DrawDockedMenu(sb, pixel, font, titleFont, screenW, screenH, t);
        else
            DrawSystemHUD(sb, font, screenW, screenH, center, sx, sy);

        // Waypoint arrow
        if (!_docked)
            DrawWaypointArrow(sb, pixel, font, center);

        // Mini-map
        DrawMiniMap(sb, pixel, font, screenW, screenH);

        // Exit warning
        if (_nearEdge && !_docked)
        {
            string msg = "Exiting System";
            var msgSize = titleFont.MeasureString(msg);
            float msgX = (screenW - msgSize.X) / 2f;
            float msgY = screenH * 0.3f;

            byte flash = (byte)((MathF.Sin(t * 3f) * 0.3f + 0.5f) * 255);
            Color c = new Color(255, 0, 0, (int)flash);

            sb.DrawString(titleFont, msg,
                new Microsoft.Xna.Framework.Vector2(msgX, msgY), c);
        }

        // Temperature bar (bottom-left)
        if (!_exploding && !_gameOver && !_docked)
        {
            int barX = 40;
            int barY = screenH - 200;
            int barW = 14;
            int barH = 120;

            var tempLabel = font.MeasureString("TEMP");
            sb.DrawString(font, "TEMP",
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
            sb.DrawString(font, pctStr,
                new Microsoft.Xna.Framework.Vector2(barX + barW / 2 - pctSize.X / 2, barY + barH + 4),
                Color.Gray * 0.7f);

            // Health bar (to the right of temp bar)
            int hbX = barX + barW + 28;
            int hbY = barY;
            int hbW = 14;
            int hbH = barH;

            var hpLabel = font.MeasureString("HP");
            sb.DrawString(font, "HP",
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
            sb.DrawString(font, hpStr,
                new Microsoft.Xna.Framework.Vector2(hbX + hbW / 2 - hpSize.X / 2, hbY + hbH + 4),
                Color.Gray * 0.7f);
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

            sb.DrawString(titleFont, msg,
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
            // Dim overlay
            sb.Draw(pixel, new Microsoft.Xna.Framework.Rectangle(0, 0, screenW, screenH),
                new Color(0, 0, 0, 200));

            string msg = "GAME OVER";
            var msgSize = titleFont.MeasureString(msg);
            float msgX = (screenW - msgSize.X) / 2f;
            float msgY = screenH * 0.35f;
            sb.DrawString(titleFont, msg,
                new Microsoft.Xna.Framework.Vector2(msgX, msgY), Color.Red);

            string sub = "Press ENTER to continue";
            var subSize = font.MeasureString(sub);
            float subX = (screenW - subSize.X) / 2f;
            sb.DrawString(font, sub,
                new Microsoft.Xna.Framework.Vector2(subX, msgY + 60), Color.Gray);
        }
    }

    private void DrawStation(SpriteBatch sb, Texture2D pixel, SpriteFont font,
        float sx, float sy, Body station, float t)
    {
        float pulse = MathF.Sin(t * 2f) * 0.05f + 1f;
        float r = station.BodyRadius * ZOOM * pulse;

        for (int i = 4; i >= 0; i--)
        {
            float gr = r + i * 5f * ZOOM;
            float ga = 0.03f + i * 0.04f;
            FillCircle(sb, pixel, sx, sy, gr, Color.LightBlue * ga);
        }

        // Main body
        FillCircle(sb, pixel, sx, sy, r, Color.LightBlue * 0.6f);
        DrawCircle(sb, pixel, sx, sy, r, Color.Cyan);

        // Antenna
        float antH = 10f * ZOOM * pulse;
        DrawLine(sb, pixel, sx, sy - r, sx, sy - r - antH, Color.Cyan);
        DrawLine(sb, pixel, sx - 4, sy - r - antH, sx + 4, sy - r - antH, Color.Cyan);

        // Label
        var label = station.Name;
        var labelSize = font.MeasureString(label);
        sb.DrawString(font, label,
            new Microsoft.Xna.Framework.Vector2(sx - labelSize.X / 2f, sy + r + 4f),
            Color.Cyan);

        // Dock prompt
        float dist = Vector2.Distance(_player.Position, new Vector2(station.X, station.Y));
        if (dist < 150f && !_docked)
        {
            string prompt = "[E] Dock";
            var ps = font.MeasureString(prompt);
            byte flash = (byte)((MathF.Sin(t * 4f) * 0.3f + 0.7f) * 255);
            sb.DrawString(font, prompt,
                new Microsoft.Xna.Framework.Vector2(sx - ps.X / 2f, sy - r - 22f),
                new Color(255, 255, 100, (int)flash));
        }
    }

    private void DrawShip(SpriteBatch sb, Texture2D pixel, Vector2 pos, float t)
    {
        float angle = _player.Angle;
        float len = 18f * ZOOM;

        var tip = pos + Vector2.FromAngle(angle) * len;
        var left = pos + Vector2.FromAngle(angle + 2.4f) * len * 0.65f;
        var right = pos + Vector2.FromAngle(angle - 2.4f) * len * 0.65f;

        DrawLine(sb, pixel, tip.X, tip.Y, left.X, left.Y, Color.White);
        DrawLine(sb, pixel, tip.X, tip.Y, right.X, right.Y, Color.White);
        DrawLine(sb, pixel, left.X, left.Y, right.X, right.Y, Color.White);

        float speed = _player.Velocity.Length();
        if (speed > 20f)
        {
            float flameLen = MathF.Min(speed / 5f, 12f) * ZOOM;
            var flameTip = pos + Vector2.FromAngle(angle + MathF.PI) * (flameLen + 5f * ZOOM);
            var flameLeft = pos + Vector2.FromAngle(angle + 2.8f) * 5f * ZOOM;
            var flameRight = pos + Vector2.FromAngle(angle - 2.8f) * 5f * ZOOM;

            DrawLine(sb, pixel, flameTip.X, flameTip.Y, flameLeft.X, flameLeft.Y, Color.Orange);
            DrawLine(sb, pixel, flameTip.X, flameTip.Y, flameRight.X, flameRight.Y, Color.Orange);
        }
    }

    private void DrawDockedMenu(SpriteBatch sb, Texture2D pixel, SpriteFont font, SpriteFont titleFont,
        int screenW, int screenH, float t)
    {
        // Dim background
        sb.Draw(pixel, new Microsoft.Xna.Framework.Rectangle(0, 0, screenW, screenH),
            new Color(0, 0, 0, 180));

        int panelW = 600;
        int panelH = 400;
        int px = (screenW - panelW) / 2;
        int py = (screenH - panelH) / 2;

        sb.Draw(pixel, new Microsoft.Xna.Framework.Rectangle(px, py, panelW, panelH),
            new Color(10, 10, 30, 230));
        DrawRect(sb, pixel, px, py, panelW, panelH, new Color(60, 60, 100));

        int textX = px + 20;
        int textY = py + 20;

        sb.DrawString(titleFont, $"{_station.Name}",
            new Microsoft.Xna.Framework.Vector2(textX, textY), Color.Cyan);
        textY += 50;

        sb.DrawString(font, $"Credits: {_player.Credits}",
            new Microsoft.Xna.Framework.Vector2(textX, textY), Color.Yellow);
        textY += 30;

        bool hasUpgrades = _system.Services.Contains("upgrades") || _system.Services.Contains("market");
        if (hasUpgrades)
        {
            sb.DrawString(titleFont, "--- Upgrades Available ---",
                new Microsoft.Xna.Framework.Vector2(textX, textY), Color.Lime);
            textY += 30;

            var upgrades = _game.GetUpgradesForSystem(_system.Id);
            if (upgrades.Count == 0)
            {
                sb.DrawString(font, "  None available",
                    new Microsoft.Xna.Framework.Vector2(textX, textY), Color.Gray);
                textY += 24;
            }
            else
            {
                foreach (var up in upgrades)
                {
                    bool canAfford = _player.Credits >= up.Cost;
                    Color c = canAfford ? Color.White : Color.Gray * 0.5f;
                    sb.DrawString(font, $"  {up.Name} - {up.Cost}cr",
                        new Microsoft.Xna.Framework.Vector2(textX, textY), c);
                    textY += 18;
                    sb.DrawString(font, $"    {up.Description}",
                        new Microsoft.Xna.Framework.Vector2(textX, textY), Color.White * 0.4f);
                    textY += 22;
                }
            }
        }
        else
        {
            sb.DrawString(font, "No services available at this station.",
                new Microsoft.Xna.Framework.Vector2(textX, textY), Color.Gray);
            textY += 24;
        }

        textY += 20;

        var quests = _game.GetQuestsForSystem(_system.Id);
        if (quests.Count > 0)
        {
            sb.DrawString(titleFont, "--- Quests ---",
                new Microsoft.Xna.Framework.Vector2(textX, textY), Color.Gold);
            textY += 30;

            foreach (var q in quests)
            {
                sb.DrawString(font, $"  {q.Name} - {q.RewardCredits}cr",
                    new Microsoft.Xna.Framework.Vector2(textX, textY), Color.White);
                textY += 18;
                sb.DrawString(font, $"    {q.Description}",
                    new Microsoft.Xna.Framework.Vector2(textX, textY), Color.White * 0.5f);
                textY += 24;
            }
        }

        sb.DrawString(font, "[E] Buy selected  |  [Q] Accept quest  |  [ESC] Undock",
            new Microsoft.Xna.Framework.Vector2(textX, screenH - py - 30), Color.Gray * 0.7f);
    }

    private void DrawSystemHUD(SpriteBatch sb, SpriteFont font, int screenW, int screenH,
        Vector2 shipPos, float stationX, float stationY)
    {
        sb.DrawString(font, $"{_system.Name} - Interior",
            new Microsoft.Xna.Framework.Vector2(10, 10), Color.Cyan);

        float distFromCenter = _player.Position.Length();
        sb.DrawString(font, $"Distance from star: {(int)distFromCenter}u",
            new Microsoft.Xna.Framework.Vector2(10, 30), Color.Gray * 0.7f);

        float distToStation = Vector2.Distance(_player.Position, new Vector2(_station.X, _station.Y));
        sb.DrawString(font, $"Distance to station: {(int)distToStation}u",
            new Microsoft.Xna.Framework.Vector2(10, 50), Color.Gray * 0.7f);

        sb.DrawString(font, "[ESC] Pause",
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
        sb.DrawString(font, distText,
            new Microsoft.Xna.Framework.Vector2(arrowPos.X - ts.X / 2f, arrowPos.Y + arrowSize + 3f),
            c * 0.7f);
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
            FillCircle(sb, pixel, stx, sty, stR, Color.LightBlue);
        }

        // Player as moving icon
        float plx = cx + _player.Position.X * scale;
        float ply = cy + _player.Position.Y * scale;
        FillCircle(sb, pixel, plx, ply, 3f, Color.White);

        // Waypoint icon label below mini-map
        float iconY = my + mapSize + 6;
        FillCircle(sb, pixel, mx + 4, iconY + 3, 3f, Color.LightBlue);
        DrawLine(sb, pixel, mx + 4, iconY, mx + 4, iconY + 2, Color.Cyan);
        string wpLabel = _system.Station?.Name ?? "None";
        sb.DrawString(font, wpLabel,
            new Microsoft.Xna.Framework.Vector2(mx + 10, iconY - 2), Color.LightBlue * 0.8f);
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
}
