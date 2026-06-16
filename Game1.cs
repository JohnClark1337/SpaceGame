using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using SpaceGame.Models;
using SpaceGame.Systems;
using Color = Microsoft.Xna.Framework.Color;
using Vector2 = SpaceGame.Systems.Vector2;


namespace SpaceGame;

public class Game1 : Game
{
    private GraphicsDeviceManager _graphics;
    private SpriteBatch _spriteBatch = null!;
    private SpriteFont _font = null!;
    private SpriteFont _titleFont = null!;
    private Texture2D _pixel = null!;

    private readonly Player _player = new(new Vector2(0, 0));
    private readonly Galaxy _galaxy = new();
    private readonly Starfield _starfield = new();

    private KeyboardState _prevKeyboard;
    private MouseState _prevMouse;
    private string _statusMessage = "";
    private float _statusTimer;
    private GameTime _gameTime = new();

    private enum MenuType { None, SystemInfo, QuestBoard, UpgradeShop, Pause }
    private MenuType _currentMenu = MenuType.None;
    private StarSystemData? _menuSystem;
    private int _menuSelection;

    private enum ViewMode { Galaxy, System }
    private ViewMode _viewMode = ViewMode.Galaxy;
    private SystemScene _systemScene = null!;
    private Vector2 _galaxyPlayerPos;
    private Vector2 _galaxyPlayerVel;

    public GameTime GameTime => _gameTime;
    public int ViewWidth => ScreenWidth;
    public int ViewHeight => ScreenHeight;

    public List<UpgradeData> GetUpgradesForSystem(string systemId) =>
        _galaxy.GetAvailableUpgradesForSystem(systemId, _player);

    public List<QuestData> GetQuestsForSystem(string systemId) =>
        _galaxy.AvailableQuests.Where(q => q.GiverSystem == systemId).ToList();

    public void ExitToGalaxy()
    {
        _player.Position = _galaxyPlayerPos;
        _player.Velocity = _galaxyPlayerVel;
        _viewMode = ViewMode.Galaxy;
    }

    public void ShowNewGameMenu()
    {
        _player.Position = _galaxyPlayerPos;
        _player.Velocity = _galaxyPlayerVel;
        _viewMode = ViewMode.Galaxy;
        _currentMenu = MenuType.Pause;
        _menuSelection = 0;
    }

    private void NewGame()
    {
        _player.Velocity = Vector2.Zero;
        _player.Angle = 0;
        _player.Credits = 50;
        _player.Health = 50;
        _player.OwnedUpgrades.Clear();
        _player.CompletedQuests.Clear();
        _player.CurrentSystemId = null;

        _galaxy.LoadData();
        _galaxy.ActiveQuests.Clear();
        _galaxy.TargetSystem = null;

        var startSys = _galaxy.FindSystemById("proxima");
        if (startSys != null)
        {
            _player.Position = new Vector2(startSys.X, startSys.Y);
            _player.CurrentSystemId = startSys.Id;
            _galaxy.CurrentSystem = startSys;
        }
        else
        {
            _player.Position = Vector2.Zero;
        }

        _galaxyPlayerPos = _player.Position;
        _galaxyPlayerVel = Vector2.Zero;
        _viewMode = ViewMode.Galaxy;
        _currentMenu = MenuType.None;

        // Consume any held keys so they don't re-trigger this frame
        _prevKeyboard = Keyboard.GetState();

        _systemScene = new SystemScene(_player);
    }

    private const int ScreenWidth = 1280;
    private const int ScreenHeight = 800;

    public Game1()
    {
        _graphics = new GraphicsDeviceManager(this);
        _graphics.PreferredBackBufferWidth = ScreenWidth;
        _graphics.PreferredBackBufferHeight = ScreenHeight;
        Window.Title = "SpaceGame";
        Content.RootDirectory = "Content";
        IsMouseVisible = true;
    }

    protected override void Initialize()
    {
        _galaxy.LoadData();

        if (_galaxy.Systems.Count == 0)
            _statusMessage = "ERROR: No systems loaded! Check Data/*.json files.";
        else
            _statusMessage = $"Welcome to {_galaxy.Systems[0].Name}  -  Press E or click to dock";

        _statusTimer = 5f;

        var startSys = _galaxy.FindSystemById("proxima");
        if (startSys != null)
        {
            _player.Position = new Vector2(startSys.X, startSys.Y);
            _player.CurrentSystemId = startSys.Id;
            _galaxy.CurrentSystem = startSys;
        }

        base.Initialize();
    }

    protected override void LoadContent()
    {
        _spriteBatch = new SpriteBatch(GraphicsDevice);
        _font = Content.Load<SpriteFont>("Font");
        _titleFont = Content.Load<SpriteFont>("TitleFont");
        _pixel = new Texture2D(GraphicsDevice, 1, 1);
        _pixel.SetData(new[] { Color.White });

        _systemScene = new SystemScene(_player);
    }

    protected override void Update(GameTime gameTime)
    {
        try { UpdateInner(gameTime); }
        catch (Exception ex) { File.WriteAllText("crash.log", $"Update: {ex}"); throw; }
    }

    private void UpdateInner(GameTime gameTime)
    {
        _gameTime = gameTime;
        float dt = (float)gameTime.ElapsedGameTime.TotalSeconds;
        if (dt > 0.05f) dt = 0.05f;

        var keyboard = Keyboard.GetState();
        var mouse = Mouse.GetState();

        if (_viewMode == ViewMode.Galaxy)
        {
            HandleInput(dt, keyboard, mouse);

            if (_currentMenu == MenuType.None)
            {
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

                // Enter system — checked before _prevKeyboard is updated
                if (keyboard.IsKeyDown(Keys.Enter) && _prevKeyboard.IsKeyUp(Keys.Enter))
                {
                    var sys = _galaxy.FindSystemAtPosition(_player.Position, 80f);
                    if (sys != null)
                    {
                        _galaxyPlayerPos = _player.Position;
                        _galaxyPlayerVel = _player.Velocity;
                        _systemScene.EnterSystem(sys, this);
                        _viewMode = ViewMode.System;
                    }
                }
            }

            _prevKeyboard = keyboard;
            _prevMouse = mouse;

            _starfield.Update(dt, _player.Velocity);
            CheckSystemProximity();
        }
        else // ViewMode.System
        {
            if (_currentMenu == MenuType.Pause)
            {
                if (JustPressed(keyboard, Keys.Escape))
                    _currentMenu = MenuType.None;
                if (JustPressed(keyboard, Keys.Down) || JustPressed(keyboard, Keys.S))
                    _menuSelection++;
                if (JustPressed(keyboard, Keys.Up) || JustPressed(keyboard, Keys.W))
                    _menuSelection--;
                if (JustPressed(keyboard, Keys.Enter))
                {
                    if (_menuSelection == 0) // New Game
                        NewGame();
                    else if (_menuSelection == 3) // Quit
                        Exit();
                }

                int maxItem = 3;
                if (_menuSelection < 0) _menuSelection = maxItem;
                if (_menuSelection > maxItem) _menuSelection = 0;
            }
            else
            {
                if (JustPressed(keyboard, Keys.Escape) && !_systemScene.Docked)
                {
                    _currentMenu = MenuType.Pause;
                    _menuSelection = 0;
                }
                else
                {
                    _systemScene.Update(dt, keyboard, mouse);
                }
            }
            _prevKeyboard = keyboard;
            _prevMouse = mouse;
        }

        if (_statusTimer > 0) _statusTimer -= dt;

        base.Update(gameTime);
    }

    private void HandleInput(float dt, KeyboardState keyboard, MouseState mouse)
    {
        if (_currentMenu == MenuType.None)
        {
            if (JustPressed(keyboard, Keys.Escape))
            {
                _currentMenu = MenuType.Pause;
                _menuSelection = 0;
                return;
            }

            if (JustPressed(keyboard, Keys.Q))
            {
                var sys = _galaxy.FindSystemAtPosition(_player.Position, 80f);
                if (sys != null)
                {
                    var available = _galaxy.AvailableQuests
                        .Where(q => q.GiverSystem == sys.Id)
                        .ToList();
                    if (available.Count > 0)
                    {
                        _galaxy.AcceptQuest(available[0].Id);
                        SetStatus($"Accepted quest: {available[0].Name}");
                    }
                }
            }
        }
        else if (_currentMenu == MenuType.Pause)
        {
            if (JustPressed(keyboard, Keys.Escape))
            {
                _currentMenu = MenuType.None;
                return;
            }
            if (JustPressed(keyboard, Keys.Down) || JustPressed(keyboard, Keys.S))
                _menuSelection++;
            if (JustPressed(keyboard, Keys.Up) || JustPressed(keyboard, Keys.W))
                _menuSelection--;

            if (JustPressed(keyboard, Keys.Enter))
            {
                if (_menuSelection == 0) // New Game
                    NewGame();
                else if (_menuSelection == 3) // Quit
                    Exit();
            }
        }
        else if (_currentMenu == MenuType.SystemInfo)
        {
            if (JustPressed(keyboard, Keys.Down) || JustPressed(keyboard, Keys.S))
                _menuSelection++;
            if (JustPressed(keyboard, Keys.Up) || JustPressed(keyboard, Keys.W))
                _menuSelection--;

            if (JustPressed(keyboard, Keys.Enter) || JustPressed(keyboard, Keys.Space))
            {
                if (_menuSystem != null)
                {
                    if (_menuSystem.Services.Contains("market") || _menuSystem.Services.Contains("upgrades"))
                    {
                        _currentMenu = MenuType.UpgradeShop;
                        _menuSelection = 0;
                    }
                }
            }

            if (JustPressed(keyboard, Keys.Tab))
            {
                if (_menuSystem != null)
                {
                    var quests = _galaxy.AvailableQuests
                        .Where(q => q.GiverSystem == _menuSystem.Id)
                        .ToList();
                    if (quests.Count > 0)
                    {
                        _currentMenu = MenuType.QuestBoard;
                        _menuSelection = 0;
                    }
                }
            }
        }
        else if (_currentMenu == MenuType.UpgradeShop)
        {
            if (JustPressed(keyboard, Keys.Down) || JustPressed(keyboard, Keys.S))
                _menuSelection++;
            if (JustPressed(keyboard, Keys.Up) || JustPressed(keyboard, Keys.W))
                _menuSelection--;

            if (JustPressed(keyboard, Keys.Enter))
            {
                if (_menuSystem != null)
                {
                    var upgrades = _galaxy.GetAvailableUpgradesForSystem(_menuSystem.Id, _player);
                    if (_menuSelection >= 0 && _menuSelection < upgrades.Count)
                    {
                        var upgrade = upgrades[_menuSelection];
                        if (_player.Credits >= upgrade.Cost)
                        {
                            _player.Credits -= upgrade.Cost;
                            _player.OwnedUpgrades.Add(upgrade.Id);
                            SetStatus($"Purchased: {upgrade.Name}");
                        }
                        else
                        {
                            SetStatus("Not enough credits!");
                        }
                    }
                }
            }

            if (JustPressed(keyboard, Keys.Escape))
                _currentMenu = MenuType.SystemInfo;
        }
        else if (_currentMenu == MenuType.QuestBoard)
        {
            if (JustPressed(keyboard, Keys.Down) || JustPressed(keyboard, Keys.S))
                _menuSelection++;
            if (JustPressed(keyboard, Keys.Up) || JustPressed(keyboard, Keys.W))
                _menuSelection--;

            if (JustPressed(keyboard, Keys.Enter))
            {
                if (_menuSystem != null)
                {
                    var quests = _galaxy.AvailableQuests
                        .Where(q => q.GiverSystem == _menuSystem.Id)
                        .ToList();
                    if (_menuSelection >= 0 && _menuSelection < quests.Count)
                    {
                        _galaxy.AcceptQuest(quests[_menuSelection].Id);
                        SetStatus($"Accepted: {quests[_menuSelection].Name}");
                    }
                }
            }

            if (JustPressed(keyboard, Keys.Escape))
                _currentMenu = MenuType.SystemInfo;
        }

        int maxItem = 0;
        if (_currentMenu == MenuType.SystemInfo) maxItem = 2;
        else if (_currentMenu == MenuType.Pause) maxItem = 3;
        else if (_currentMenu == MenuType.UpgradeShop)
        {
            if (_menuSystem != null)
                maxItem = _galaxy.GetAvailableUpgradesForSystem(_menuSystem.Id, _player).Count - 1;
        }
        else if (_currentMenu == MenuType.QuestBoard)
        {
            if (_menuSystem != null)
                maxItem = _galaxy.AvailableQuests.Where(q => q.GiverSystem == _menuSystem.Id).Count() - 1;
        }

        if (_menuSelection < 0) _menuSelection = maxItem;
        if (_menuSelection > maxItem) _menuSelection = 0;
    }

    private void CheckSystemProximity()
    {
        var nearby = _galaxy.FindSystemAtPosition(_player.Position, 80f);
        if (nearby != null)
        {
            if (_player.CurrentSystemId != nearby.Id)
            {
                _player.CurrentSystemId = nearby.Id;
                _galaxy.CurrentSystem = nearby;
                _galaxy.CheckQuestProgress(_player);
                _galaxy.RefreshAvailableQuests(_player);
            }
        }
        else
        {
            _galaxy.CurrentSystem = null;
        }
    }

    private void SetStatus(string msg)
    {
        _statusMessage = msg;
        _statusTimer = 3f;
    }

    private bool JustPressed(KeyboardState keyboard, Keys key) =>
        keyboard.IsKeyDown(key) && _prevKeyboard.IsKeyUp(key);

    private bool JustClicked(MouseState mouse)
    {
        bool clicked = mouse.LeftButton == ButtonState.Pressed &&
                       _prevMouse.LeftButton == ButtonState.Released;
        return clicked;
    }

    protected override void Draw(GameTime gameTime)
    {
        try { DrawInner(gameTime); }
        catch (Exception ex) { File.WriteAllText("crash.log", $"Draw: {ex}"); throw; }
    }

    private void DrawInner(GameTime gameTime)
    {
        GraphicsDevice.Clear(Color.Black);

        _spriteBatch.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend);

        if (_viewMode == ViewMode.Galaxy)
        {
            Vector2 screenCenter = new(ScreenWidth / 2f, ScreenHeight / 2f);
            Vector2 offset = screenCenter - _player.Position;

            DrawStarfield(offset);
            DrawConnectionLines(offset);
            DrawSystems(offset, screenCenter);
            DrawShip(screenCenter);
            DrawHUD(screenCenter);

            if (_currentMenu != MenuType.None)
                DrawMenu();
        }
        else
        {
            _systemScene.Draw(_spriteBatch, _pixel, _font, _titleFont);

            if (_currentMenu != MenuType.None)
                DrawMenu();
        }

        if (_statusTimer > 0)
            DrawStatusMessage();

        _spriteBatch.End();

        base.Draw(gameTime);
    }

    private void DrawStarfield(Vector2 offset)
    {
        foreach (var star in _starfield.Stars)
        {
            float sx = star.X + offset.X;
            float sy = star.Y + offset.Y;
            if (sx >= 0 && sx < ScreenWidth && sy >= 0 && sy < ScreenHeight)
            {
                float brightness = star.Brightness;
                byte b = (byte)(brightness * 255);
                _spriteBatch.Draw(_pixel, new Microsoft.Xna.Framework.Rectangle(
                    (int)sx, (int)sy, star.Size, star.Size),
                    new Color(b, b, b, b));
            }
        }
    }

    private void DrawConnectionLines(Vector2 offset)
    {
        var drawn = new HashSet<(string, string)>();
        foreach (var sys in _galaxy.Systems)
        {
            foreach (var connId in sys.Connections)
            {
                var key = string.Compare(sys.Id, connId, StringComparison.Ordinal) < 0
                    ? (sys.Id, connId) : (connId, sys.Id);
                if (drawn.Contains(key)) continue;
                drawn.Add(key);

                var other = _galaxy.FindSystemById(connId);
                if (other == null) continue;

                float x1 = sys.X + offset.X;
                float y1 = sys.Y + offset.Y;
                float x2 = other.X + offset.X;
                float y2 = other.Y + offset.Y;

                bool nearby1 = Vector2.Distance(_player.Position, new Vector2(sys.X, sys.Y)) < 300f;
                bool nearby2 = Vector2.Distance(_player.Position, new Vector2(other.X, other.Y)) < 300f;
                float lineAlpha = (nearby1 || nearby2) ? 0.5f : 0.2f;
                DrawLine(x1, y1, x2, y2, new Color(60, 80, 120) * lineAlpha);
            }
        }
    }

    private void DrawSystems(Vector2 offset, Vector2 screenCenter)
    {
        foreach (var sys in _galaxy.Systems)
        {
            float sx = sys.X + offset.X;
            float sy = sys.Y + offset.Y;

            if (sx < -150 || sx > ScreenWidth + 150 || sy < -150 || sy > ScreenHeight + 150)
                continue;

            Color color = ParseColor(sys.Color);
            float t = (float)_gameTime.TotalGameTime.TotalSeconds;
            float pulse = MathF.Sin(t * 1.5f + sys.X * 0.01f) * 0.12f + 1f;
            float drawRadius = sys.Radius * pulse;

            float dist = Vector2.Distance(_player.Position, new Vector2(sys.X, sys.Y));
            bool nearby = dist < 120f;
            float nearbyGlow = nearby ? (1f - dist / 120f) * 0.5f : 0f;

            var label = sys.Name;
            var labelSize = _font.MeasureString(label);

            // Outer glow - big and bright
            for (int i = 5; i >= 0; i--)
            {
                float r = drawRadius + i * 10f + nearbyGlow * 20f;
                float alpha = 0.02f + i * 0.06f + nearbyGlow;
                FillCircle(sx, sy, r, color * MathF.Min(alpha, 0.5f));
            }

            // Core - filled circle
            FillCircle(sx, sy, drawRadius * 0.7f, color * 0.9f);

            // Core edge
            DrawCircle(sx, sy, drawRadius * 0.7f, color);

            // Proximity highlight ring and dock prompt
            if (nearby)
            {
                float ringAlpha = MathF.Min(1f, (120f - dist) / 60f);
                DrawCircle(sx, sy, drawRadius + 8f, Color.Yellow * ringAlpha);
                DrawCircle(sx, sy, drawRadius + 12f, Color.Yellow * ringAlpha * 0.4f);

                if (dist < 80f)
                {
                    string prompt = "[Enter] Enter System";
                    var ps = _font.MeasureString(prompt);
                    float px2 = sx - ps.X / 2f;
                    float py2 = sy - drawRadius - 20f;
                    byte flash = (byte)((MathF.Sin(t * 4f) * 0.3f + 0.7f) * 255);
                    _spriteBatch.Draw(_pixel,
                        new Microsoft.Xna.Framework.Rectangle((int)px2 - 4, (int)py2 - 2,
                            (int)ps.X + 8, (int)ps.Y + 4),
                        new Color(0, 0, 0, 160));
                    _spriteBatch.DrawString(_font, prompt,
                        new Microsoft.Xna.Framework.Vector2(px2, py2),
                        new Color(255, 255, 100, (int)flash));
                }
            }
            else if (dist < 400f)
            {
                // Show distance hint for nearby systems
                string hint = $"{sys.Name}  -  {(int)dist}u";
                var hintSize = _font.MeasureString(hint);
                float hx = sx - hintSize.X / 2f;
                float hy = sy + drawRadius + labelSize.Y + 20f;
                _spriteBatch.DrawString(_font, hint,
                    new Microsoft.Xna.Framework.Vector2(hx, hy),
                    Color.Gray * MathF.Max(0.2f, 1f - dist / 400f));
            }

            // Label with background box
            float labelX = sx - labelSize.X / 2f;
            float labelY = sy + drawRadius + 6f;
            byte bg = (byte)(nearby ? 60 : 20);
            _spriteBatch.Draw(_pixel,
                new Microsoft.Xna.Framework.Rectangle((int)labelX - 4, (int)labelY - 2,
                    (int)labelSize.X + 8, (int)labelSize.Y + 4),
                new Color(0, 0, 0, (int)bg));
            _spriteBatch.DrawString(_font, label,
                new Microsoft.Xna.Framework.Vector2(labelX, labelY),
                nearby ? Color.White : Color.White * 0.7f);

            // Distance label
            if (dist > 50f && dist < 600f)
            {
                string distLabel = $"{(int)dist}u";
                var distSize = _font.MeasureString(distLabel);
                _spriteBatch.DrawString(_font, distLabel,
                    new Microsoft.Xna.Framework.Vector2(sx - distSize.X / 2f, labelY + labelSize.Y + 2),
                    Color.Gray * MathF.Max(0.2f, 1f - dist / 600f));
            }
        }

        // Quest target indicators
        foreach (var quest in _galaxy.ActiveQuests)
        {
            if (quest.ObjectiveType == "travel" && quest.TargetSystem != null)
            {
                var target = _galaxy.FindSystemById(quest.TargetSystem);
                if (target != null)
                {
                    float tx = target.X + offset.X;
                    float ty = target.Y + offset.Y;
                    float pulse2 = MathF.Sin((float)_gameTime.TotalGameTime.TotalSeconds * 2f) * 0.3f + 0.7f;
                    DrawCircle(tx, ty, target.Radius + 16f, Color.Gold * pulse2);
                    DrawCircle(tx, ty, target.Radius + 22f, Color.Gold * pulse2 * 0.3f);

                    // Marker arrow above
                    float arrowY = ty - target.Radius - 26f;
                    DrawLine(tx - 6, arrowY + 8, tx, arrowY, Color.Gold * pulse2);
                    DrawLine(tx + 6, arrowY + 8, tx, arrowY, Color.Gold * pulse2);
                }
            }
        }
    }

    private void FillCircle(float cx, float cy, float r, Color color)
    {
        int segments = Math.Max(16, (int)(r * 0.8f));
        float angleStep = MathF.PI * 2 / segments;
        for (int i = 0; i < segments; i++)
        {
            float a1 = angleStep * i;
            float a2 = angleStep * (i + 1);
            float x1 = cx + MathF.Cos(a1) * r;
            float y1 = cy + MathF.Sin(a1) * r;
            float x2 = cx + MathF.Cos(a2) * r;
            float y2 = cy + MathF.Sin(a2) * r;
            DrawLine(cx, cy, x1, y1, color);
            DrawLine(x1, y1, x2, y2, color);
        }
    }

    private void DrawShip(Vector2 center)
    {
        float angle = _player.Angle;
        float len = 18f;

        var tip = center + Vector2.FromAngle(angle) * len;
        var left = center + Vector2.FromAngle(angle + 2.4f) * len * 0.65f;
        var right = center + Vector2.FromAngle(angle - 2.4f) * len * 0.65f;

        DrawLine(tip.X, tip.Y, left.X, left.Y, Color.White);
        DrawLine(tip.X, tip.Y, right.X, right.Y, Color.White);
        DrawLine(left.X, left.Y, right.X, right.Y, Color.White);

        // Thrust flame
        float speed = _player.Velocity.Length();
        if (speed > 20f)
        {
            float flameLen = MathF.Min(speed / 5f, 12f);
            var flameTip = center + Vector2.FromAngle(angle + MathF.PI) * (flameLen + 5f);
            var flameLeft = center + Vector2.FromAngle(angle + 2.8f) * 5f;
            var flameRight = center + Vector2.FromAngle(angle - 2.8f) * 5f;

            DrawLine(flameTip.X, flameTip.Y, flameLeft.X, flameLeft.Y, Color.Orange);
            DrawLine(flameTip.X, flameTip.Y, flameRight.X, flameRight.Y, Color.Orange);
        }
    }

    private void DrawHUD(Vector2 center)
    {
        // Top-left info
        _spriteBatch.DrawString(_font, $"Credits: {_player.Credits}", new Microsoft.Xna.Framework.Vector2(10, 10), Color.Yellow);

        if (_galaxy.CurrentSystem != null)
        {
            string info = $"Docked at: {_galaxy.CurrentSystem.Name} [{_galaxy.CurrentSystem.Faction}]";
            _spriteBatch.DrawString(_font, info, new Microsoft.Xna.Framework.Vector2(10, 30), Color.Cyan);
            _spriteBatch.DrawString(_font, "Press E / Click  -  Open station services", new Microsoft.Xna.Framework.Vector2(10, 50), Color.Gray * 0.7f);
        }
        else
        {
            // Show nearest system
            float nearestDist = float.MaxValue;
            StarSystemData? nearest = null;
            foreach (var sys in _galaxy.Systems)
            {
                float d = Vector2.Distance(_player.Position, new Vector2(sys.X, sys.Y));
                if (d < nearestDist) { nearestDist = d; nearest = sys; }
            }

            if (nearest != null)
            {
                string dir = GetDirection(_player.Position, new Vector2(nearest.X, nearest.Y));
                _spriteBatch.DrawString(_font, $"Nearest: {nearest.Name} ({dir})",
                    new Microsoft.Xna.Framework.Vector2(10, 30), Color.Gray * 0.8f);
                _spriteBatch.DrawString(_font, $"Distance: {(int)nearestDist}",
                    new Microsoft.Xna.Framework.Vector2(10, 50), Color.Gray * 0.6f);
            }
        }

        // Active quests
        float y = 80;
        if (_galaxy.ActiveQuests.Count > 0)
        {
            _spriteBatch.DrawString(_font, "--- Active Quests ---", new Microsoft.Xna.Framework.Vector2(10, y), Color.Gold);
            y += 20;
            foreach (var q in _galaxy.ActiveQuests)
            {
                string status;
                if (q.ObjectiveType == "travel" && q.TargetSystem != null)
                {
                    var target = _galaxy.FindSystemById(q.TargetSystem);
                    float dist = target != null
                        ? Vector2.Distance(_player.Position, new Vector2(target.X, target.Y))
                        : 0;
                    status = $"Travel to {target?.Name ?? q.TargetSystem} [{dist:F0}u]";
                }
                else
                    status = q.Description;

                _spriteBatch.DrawString(_font, $"  {q.Name}: {status}", new Microsoft.Xna.Framework.Vector2(10, y), Color.White * 0.9f);
                y += 18;
            }
        }

        // Owned upgrades
        if (_player.OwnedUpgrades.Count > 0)
        {
            y += 10;
            _spriteBatch.DrawString(_font, "--- Upgrades ---", new Microsoft.Xna.Framework.Vector2(10, y), Color.Lime);
            y += 20;
            foreach (var upId in _player.OwnedUpgrades)
            {
                var up = _galaxy.AllUpgrades.FirstOrDefault(u => u.Id == upId);
                if (up != null)
                    _spriteBatch.DrawString(_font, $"  {up.Name}", new Microsoft.Xna.Framework.Vector2(10, y), Color.Lime * 0.8f);
                y += 18;
            }
        }

        // Controls hint
        string controls = "WASD/Arrows: Fly | Shift: Boost | E: Interact | Q: Accept Quest | ESC: Close";
        var controlsSize = _font.MeasureString(controls);
        _spriteBatch.DrawString(_font, controls,
            new Microsoft.Xna.Framework.Vector2(ScreenWidth / 2f - controlsSize.X / 2f, ScreenHeight - 25),
            Color.Gray * 0.5f);

        // Minimap
        DrawMinimap();
    }

    private void DrawMinimap()
    {
        int mapX = ScreenWidth - 210;
        int mapY = 10;
        int mapW = 200;
        int mapH = 150;

        // Background
        _spriteBatch.Draw(_pixel, new Microsoft.Xna.Framework.Rectangle(mapX, mapY, mapW, mapH),
            new Color(10, 10, 20, 180));

        // Border
        DrawRect(mapX, mapY, mapW, mapH, new Color(60, 60, 80));

        // Find bounds of all systems
        float minX = float.MaxValue, maxX = float.MinValue;
        float minY = float.MaxValue, maxY = float.MinValue;
        foreach (var sys in _galaxy.Systems)
        {
            if (sys.X < minX) minX = sys.X;
            if (sys.X > maxX) maxX = sys.X;
            if (sys.Y < minY) minY = sys.Y;
            if (sys.Y > maxY) maxY = sys.Y;
        }

        float rangeX = maxX - minX + 200;
        float rangeY = maxY - minY + 200;
        float scale = MathF.Min(mapW / rangeX, mapH / rangeY) * 0.9f;

        float cx = (minX + maxX) / 2f;
        float cy = (minY + maxY) / 2f;

        foreach (var sys in _galaxy.Systems)
        {
            float sx = mapX + mapW / 2f + (sys.X - cx) * scale;
            float sy = mapY + mapH / 2f + (sys.Y - cy) * scale;
            Color c = ParseColor(sys.Color);
            DrawCircle(sx, sy, 3f, c);

            bool isCurrent = _player.CurrentSystemId == sys.Id;
            bool isQuest = _galaxy.ActiveQuests.Any(q => q.TargetSystem == sys.Id);
            if (isCurrent)
                DrawCircle(sx, sy, 5f, Color.Yellow);
            if (isQuest)
                DrawCircle(sx, sy, 5f, Color.Gold * 0.6f);
        }

        // Player dot
        float px = mapX + mapW / 2f + (_player.Position.X - cx) * scale;
        float py = mapY + mapH / 2f + (_player.Position.Y - cy) * scale;
        DrawCircle(px, py, 3f, Color.White);
    }

    private void DrawMenu()
    {
        // Dim background
        _spriteBatch.Draw(_pixel, new Microsoft.Xna.Framework.Rectangle(0, 0, ScreenWidth, ScreenHeight),
            new Color(0, 0, 0, 140));

        int panelW = 700;
        int panelH = 500;
        int px = (ScreenWidth - panelW) / 2;
        int py = (ScreenHeight - panelH) / 2;

        // Panel bg
        _spriteBatch.Draw(_pixel, new Microsoft.Xna.Framework.Rectangle(px, py, panelW, panelH),
            new Color(20, 20, 40, 230));
        DrawRect(px, py, panelW, panelH, new Color(80, 80, 120));

        int textX = px + 20;
        int textY = py + 20;

        if (_currentMenu == MenuType.Pause)
        {
            _spriteBatch.DrawString(_titleFont, "Paused",
                new Microsoft.Xna.Framework.Vector2(textX, textY), Color.Cyan);
            textY += 60;

            string[] options = { "New Game", "Save Game", "Load Game", "Quit" };
            for (int i = 0; i < options.Length; i++)
            {
                bool selected = _menuSelection == i;
                Color c = selected ? Color.Yellow : Color.Gray;
                string prefix = selected ? "> " : "  ";
                _spriteBatch.DrawString(_font, prefix + options[i],
                    new Microsoft.Xna.Framework.Vector2(textX, textY), c);
                textY += 30;
            }

            textY += 20;
            _spriteBatch.DrawString(_font, "[Enter] Select  |  [ESC] Resume",
                new Microsoft.Xna.Framework.Vector2(textX, textY), Color.Gray * 0.6f);
        }
        else if (_currentMenu == MenuType.SystemInfo && _menuSystem != null)
        {
            var sys = _menuSystem;
            _spriteBatch.DrawString(_titleFont, sys.Name, new Microsoft.Xna.Framework.Vector2(textX, textY), ParseColor(sys.Color));
            textY += 50;

            _spriteBatch.DrawString(_font, sys.Description, new Microsoft.Xna.Framework.Vector2(textX, textY), Color.White * 0.8f);
            textY += 40;

            _spriteBatch.DrawString(_font, $"Faction: {sys.Faction ?? "None"}", new Microsoft.Xna.Framework.Vector2(textX, textY), Color.Cyan);
            textY += 22;
            _spriteBatch.DrawString(_font, $"Hostility Level: {sys.Hostility}/10", new Microsoft.Xna.Framework.Vector2(textX, textY),
                sys.Hostility > 3 ? Color.OrangeRed : Color.LimeGreen);
            textY += 22;

            if (sys.Services.Count > 0)
            {
                _spriteBatch.DrawString(_font, "Services: " + string.Join(", ", sys.Services), new Microsoft.Xna.Framework.Vector2(textX, textY), Color.Yellow * 0.9f);
                textY += 30;
            }

            textY += 20;

            bool hasUpgrades = sys.Services.Contains("upgrades") || sys.Services.Contains("market");
            bool hasQuests = _galaxy.AvailableQuests.Any(q => q.GiverSystem == sys.Id);

            var items = new List<string>();
            if (hasUpgrades) items.Add("Enter Shop [Enter]");
            if (hasQuests) items.Add("View Quests [Tab]");
            items.Add("Close [ESC]");

            for (int i = 0; i < items.Count; i++)
            {
                bool selected = _menuSelection == i;
                Color c = selected ? Color.Yellow : Color.Gray;
                string prefix = selected ? "> " : "  ";
                _spriteBatch.DrawString(_font, prefix + items[i], new Microsoft.Xna.Framework.Vector2(textX, textY), c);
                textY += 24;
            }
        }
        else if (_currentMenu == MenuType.UpgradeShop && _menuSystem != null)
        {
            var upgrades = _galaxy.GetAvailableUpgradesForSystem(_menuSystem.Id, _player);

            _spriteBatch.DrawString(_titleFont, $"{_menuSystem.Name}  -  Upgrade Shop",
                new Microsoft.Xna.Framework.Vector2(textX, textY), Color.Yellow);
            textY += 50;

            _spriteBatch.DrawString(_font, $"Your Credits: {_player.Credits}",
                new Microsoft.Xna.Framework.Vector2(textX, textY), Color.Gold);
            textY += 30;

            if (upgrades.Count == 0)
            {
                _spriteBatch.DrawString(_font, "No upgrades available.", new Microsoft.Xna.Framework.Vector2(textX, textY), Color.Gray);
            }
            else
            {
                for (int i = 0; i < upgrades.Count; i++)
                {
                    var up = upgrades[i];
                    bool selected = _menuSelection == i;
                    bool canAfford = _player.Credits >= up.Cost;
                    Color nameColor = selected ? (canAfford ? Color.Lime : Color.Red) : (canAfford ? Color.White : Color.Gray * 0.5f);
                    string prefix = selected ? "> " : "  ";
                    _spriteBatch.DrawString(_font, $"{prefix}{up.Name}  -  {up.Cost}cr",
                        new Microsoft.Xna.Framework.Vector2(textX, textY), nameColor);
                    textY += 20;
                    _spriteBatch.DrawString(_font, $"     {up.Description}",
                        new Microsoft.Xna.Framework.Vector2(textX, textY), Color.White * 0.5f);
                    textY += 24;
                }
            }

            textY += 20;
            _spriteBatch.DrawString(_font, "[Enter] Buy  |  [ESC] Back",
                new Microsoft.Xna.Framework.Vector2(textX, textY), Color.Gray * 0.7f);
        }
        else if (_currentMenu == MenuType.QuestBoard && _menuSystem != null)
        {
            var quests = _galaxy.AvailableQuests
                .Where(q => q.GiverSystem == _menuSystem.Id)
                .ToList();

            _spriteBatch.DrawString(_titleFont, $"{_menuSystem.Name}  -  Quest Board",
                new Microsoft.Xna.Framework.Vector2(textX, textY), Color.Gold);
            textY += 50;

            if (quests.Count == 0)
            {
                _spriteBatch.DrawString(_font, "No quests available here.",
                    new Microsoft.Xna.Framework.Vector2(textX, textY), Color.Gray);
            }
            else
            {
                for (int i = 0; i < quests.Count; i++)
                {
                    var q = quests[i];
                    bool selected = _menuSelection == i;
                    Color c = selected ? Color.Yellow : Color.White;
                    string prefix = selected ? "> " : "  ";
                    _spriteBatch.DrawString(_font, $"{prefix}{q.Name}",
                        new Microsoft.Xna.Framework.Vector2(textX, textY), c);
                    textY += 20;
                    _spriteBatch.DrawString(_font, $"     {q.Description}",
                        new Microsoft.Xna.Framework.Vector2(textX, textY), Color.White * 0.5f);
                    textY += 18;
                    _spriteBatch.DrawString(_font, $"     Reward: {q.RewardCredits}cr" +
                        (q.RewardUpgrade != null ? $" + {q.RewardUpgrade}" : ""),
                        new Microsoft.Xna.Framework.Vector2(textX, textY), Color.Yellow * 0.7f);
                    textY += 24;
                }
            }

            textY += 20;
            _spriteBatch.DrawString(_font, "[Enter] Accept  |  [ESC] Back",
                new Microsoft.Xna.Framework.Vector2(textX, textY), Color.Gray * 0.7f);
        }
    }

    private void DrawStatusMessage()
    {
        string msg = _statusMessage;
        float alpha = MathF.Min(1f, _statusTimer * 2f);
        var size = _titleFont.MeasureString(msg);
        float x = (ScreenWidth - size.X) / 2f;
        float y = ScreenHeight / 3f;

        // Background bar
        _spriteBatch.Draw(_pixel,
            new Microsoft.Xna.Framework.Rectangle((int)x - 10, (int)y - 6,
                (int)size.X + 20, (int)size.Y + 12),
            new Color(0, 0, 0, (int)(160 * alpha)));

        _spriteBatch.DrawString(_titleFont, msg,
            new Microsoft.Xna.Framework.Vector2(x, y), Color.White * alpha);
    }

    private void DrawLine(float x1, float y1, float x2, float y2, Color color)
    {
        float dx = x2 - x1;
        float dy = y2 - y1;
        float len = MathF.Sqrt(dx * dx + dy * dy);
        if (len < 1f) return;

        float angle = MathF.Atan2(dy, dx);
        _spriteBatch.Draw(_pixel, new Microsoft.Xna.Framework.Vector2(x1, y1), null, color, angle,
            Microsoft.Xna.Framework.Vector2.Zero, new Microsoft.Xna.Framework.Vector2(len / _pixel.Width, 1f),
            SpriteEffects.None, 0);
    }

    private void DrawRect(float x, float y, float w, float h, Color color)
    {
        DrawLine(x, y, x + w, y, color);
        DrawLine(x + w, y, x + w, y + h, color);
        DrawLine(x + w, y + h, x, y + h, color);
        DrawLine(x, y + h, x, y, color);
    }

    private void DrawCircle(float cx, float cy, float r, Color color)
    {
        int segments = Math.Max(8, (int)(r * 0.5f));
        for (int i = 0; i < segments; i++)
        {
            float a1 = MathF.PI * 2 * i / segments;
            float a2 = MathF.PI * 2 * (i + 1) / segments;
            DrawLine(
                cx + MathF.Cos(a1) * r, cy + MathF.Sin(a1) * r,
                cx + MathF.Cos(a2) * r, cy + MathF.Sin(a2) * r,
                color);
        }
    }

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

    private static string GetDirection(Vector2 from, Vector2 to)
    {
        float dx = to.X - from.X;
        float dy = to.Y - from.Y;
        float angle = MathF.Atan2(dy, dx);

        if (angle < -MathF.PI * 0.75f) return "W";
        if (angle < -MathF.PI * 0.25f) return "NW";
        if (angle < MathF.PI * 0.25f) return "N";
        if (angle < MathF.PI * 0.75f) return "NE";
        return "E";
    }
}

public class Starfield
{
    public struct Star
    {
        public float X, Y;
        public int Size;
        public float Brightness;
        public float Parallax;
    }

    public Star[] Stars;
    private readonly Random _rng = new();

    public Starfield()
    {
        Stars = new Star[300];
        for (int i = 0; i < Stars.Length; i++)
        {
            Stars[i] = new Star
            {
                X = (float)_rng.NextDouble() * 20000f - 10000f,
                Y = (float)_rng.NextDouble() * 20000f - 10000f,
                Size = _rng.Next(1, 3),
                Brightness = 0.3f + (float)_rng.NextDouble() * 0.7f,
                Parallax = 0.1f + (float)_rng.NextDouble() * 0.3f,
            };
        }
    }

    public void Update(float dt, Vector2 playerVelocity)
    {
        for (int i = 0; i < Stars.Length; i++)
        {
            Stars[i].X -= playerVelocity.X * Stars[i].Parallax * dt;
            Stars[i].Y -= playerVelocity.Y * Stars[i].Parallax * dt;

            if (Stars[i].X > 10000f) Stars[i].X -= 20000f;
            if (Stars[i].X < -10000f) Stars[i].X += 20000f;
            if (Stars[i].Y > 10000f) Stars[i].Y -= 20000f;
            if (Stars[i].Y < -10000f) Stars[i].Y += 20000f;
        }
    }
}
