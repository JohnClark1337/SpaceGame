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
    private RouteManager _routeManager = null!;
    private readonly Starfield _starfield = new();
    private float _aiTickTimer;

    public RouteManager RouteManager => _routeManager;
    public Galaxy Galaxy => _galaxy;

    private KeyboardState _prevKeyboard;
    private MouseState _prevMouse;
    private string _statusMessage = "";
    private float _statusTimer;
    private GameTime _gameTime = new();

    private enum MenuType { None, SystemInfo, QuestBoard, UpgradeShop, Pause, Controls }
    private MenuType _currentMenu = MenuType.None;
    private StarSystemData? _menuSystem;
    private int _menuSelection;

    private enum ViewMode { Galaxy, System }
    private ViewMode _viewMode = ViewMode.Galaxy;

    private enum Overlay { None, SystemMap, GalaxyMap, Inventory }
    private Overlay _overlay = Overlay.None;
    private SystemScene _systemScene = null!;
    private Vector2 _galaxyPlayerPos;
    private Vector2 _galaxyPlayerVel;

    private int _inventoryTab; // 0=quest items, 1=resources, 2=equipment
    private int _invScroll;

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
        _player.Resources.Clear();
        _player.QuestItems.Clear();
        _player.Equipment.Clear();
        _player.CurrentSystemId = null;

        _galaxy.LoadData();
        _routeManager.SetGalaxy(_galaxy);
        _aiTickTimer = 8f;
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
        _routeManager = new RouteManager(_galaxy);

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

        // Overlay toggle (T = system map, G = galaxy map, I = inventory)
        if (JustPressed(keyboard, Keys.T))
        {
            if (_overlay == Overlay.SystemMap)
                _overlay = Overlay.None;
            else if (_galaxy.CurrentSystem != null)
                _overlay = Overlay.SystemMap;
        }
        if (JustPressed(keyboard, Keys.G))
            _overlay = _overlay == Overlay.GalaxyMap ? Overlay.None : Overlay.GalaxyMap;

        if (JustPressed(keyboard, Keys.I))
        {
            if (_overlay == Overlay.Inventory)
                _overlay = Overlay.None;
            else
            {
                _overlay = Overlay.Inventory;
                _inventoryTab = 0;
                _invScroll = 0;
            }
        }

        if (_overlay != Overlay.None)
        {
            if (JustPressed(keyboard, Keys.Escape))
                _overlay = Overlay.None;

            if (_overlay == Overlay.Inventory)
            {
                if (JustPressed(keyboard, Keys.Left) || JustPressed(keyboard, Keys.Q))
                    _inventoryTab = (_inventoryTab - 1 + 3) % 3;
                if (JustPressed(keyboard, Keys.Right) || JustPressed(keyboard, Keys.E))
                    _inventoryTab = (_inventoryTab + 1) % 3;
                if (JustPressed(keyboard, Keys.Down) || JustPressed(keyboard, Keys.S))
                    _invScroll++;
                if (JustPressed(keyboard, Keys.Up) || JustPressed(keyboard, Keys.W))
                    _invScroll--;
                if (_invScroll < 0) _invScroll = 0;
            }

            _prevKeyboard = keyboard;
            _prevMouse = mouse;
            base.Update(gameTime);
            return;
        }

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

            // AI commander tick (every 4 seconds)
            if (_player.CurrentSystemId != null)
            {
                _aiTickTimer -= dt;
                if (_aiTickTimer <= 0f)
                {
                    _aiTickTimer = 4f;
                    _routeManager.AiTick(_player.CurrentSystemId);
                }
            }

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
                    else if (_menuSelection == 3) // Controls
                        _currentMenu = MenuType.Controls;
                    else if (_menuSelection == 4) // Quit
                        Exit();
                }

                int maxItem = 4;
                if (_menuSelection < 0) _menuSelection = maxItem;
                if (_menuSelection > maxItem) _menuSelection = 0;
            }
            else if (_currentMenu == MenuType.Controls)
            {
                if (JustPressed(keyboard, Keys.Escape))
                {
                    _currentMenu = MenuType.Pause;
                    _menuSelection = 3;
                }
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
                else if (_menuSelection == 3) // Controls
                    _currentMenu = MenuType.Controls;
                else if (_menuSelection == 4) // Quit
                    Exit();
            }
        }
        else if (_currentMenu == MenuType.Controls)
        {
            if (JustPressed(keyboard, Keys.Escape))
            {
                _currentMenu = MenuType.Pause;
                _menuSelection = 3;
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
        else if (_currentMenu == MenuType.Pause) maxItem = 4;
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

        if (_overlay == Overlay.SystemMap)
            DrawSystemMapOverlay();
        else if (_overlay == Overlay.GalaxyMap)
            DrawGalaxyMapOverlay();
        else if (_overlay == Overlay.Inventory)
            DrawInventoryOverlay();

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

                bool blocked = _routeManager.IsBlocked(sys.Id, connId);
                if (blocked)
                {
                    float pulse = MathF.Sin((float)_gameTime.TotalGameTime.TotalSeconds * 2f) * 0.2f + 0.6f;
                    DrawLine(x1, y1, x2, y2, new Color(200, 30, 30) * pulse);
                }
                else
                {
                    float lineAlpha = (nearby1 || nearby2) ? 0.5f : 0.2f;
                    DrawLine(x1, y1, x2, y2, new Color(60, 80, 120) * lineAlpha);
                }
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
                    DrawSpacedText(_font, prompt,
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
                DrawSpacedText(_font, hint,
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
            DrawSpacedText(_font, label,
                new Microsoft.Xna.Framework.Vector2(labelX, labelY),
                nearby ? Color.White : Color.White * 0.7f);

            // Distance label
            if (dist > 50f && dist < 600f)
            {
                string distLabel = $"{(int)dist}u";
                var distSize = _font.MeasureString(distLabel);
                DrawSpacedText(_font, distLabel,
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

        // Thrust flames (drawn first, underneath ship)
        float speed = _player.Velocity.Length();
        if (speed > 20f)
        {
            float flameBase = MathF.Min(speed / 5f, 12f);
            Vector2[] origins = {
                left * 0.67f + right * 0.33f,
                (left + right) * 0.5f,
                left * 0.33f + right * 0.67f
            };
            for (int i = 0; i < 3; i++)
            {
                float fLen = flameBase * (i == 1 ? 1f : 0.5f);
                var ftip = origins[i] + Vector2.FromAngle(angle + MathF.PI) * fLen;
                var fside1 = origins[i] + Vector2.FromAngle(angle + MathF.PI + 0.3f) * 3f;
                var fside2 = origins[i] + Vector2.FromAngle(angle + MathF.PI - 0.3f) * 3f;
                DrawLine(ftip.X, ftip.Y, fside1.X, fside1.Y, Color.Orange);
                DrawLine(ftip.X, ftip.Y, fside2.X, fside2.Y, Color.Orange);
            }
        }

        // Main triangle hull
        DrawLine(tip.X, tip.Y, left.X, left.Y, Color.White);
        DrawLine(tip.X, tip.Y, right.X, right.Y, Color.White);
        DrawLine(left.X, left.Y, right.X, right.Y, Color.White);

        // Small cockpit window near front
        float cp = 0.7f;
        var cockpit = center + Vector2.FromAngle(angle) * len * cp;
        float cs = 2f;
        var cf = cockpit + Vector2.FromAngle(angle) * cs;
        var cl = cockpit + Vector2.FromAngle(angle + 1.5f) * cs * 0.4f;
        var cr = cockpit + Vector2.FromAngle(angle - 1.5f) * cs * 0.4f;
        DrawLine(cf.X, cf.Y, cl.X, cl.Y, Color.Cyan * 0.6f);
        DrawLine(cf.X, cf.Y, cr.X, cr.Y, Color.Cyan * 0.6f);

        // Hull panel lines
        var rearMid = (left + right) * 0.5f;

        // Centerline from cockpit to rear
        DrawLine(cockpit.X, cockpit.Y, rearMid.X, rearMid.Y, Color.White * 0.5f);

        // Side lines parallel to hull edges
        float sideInset = len * 0.025f;
        var siL = cockpit + Vector2.FromAngle(angle + 1.5f) * sideInset;
        var siR = cockpit + Vector2.FromAngle(angle - 1.5f) * sideInset;
        float hullLen = (rearMid - cockpit).Length();
        var lEdge = (left - tip).Normalized();
        var rEdge = (right - tip).Normalized();
        var lEnd = siL + lEdge * hullLen * 0.8f;
        var rEnd = siR + rEdge * hullLen * 0.8f;
        DrawLine(siL.X, siL.Y, lEnd.X, lEnd.Y, Color.White * 0.35f);
        DrawLine(siR.X, siR.Y, rEnd.X, rEnd.Y, Color.White * 0.35f);
        DrawLine(lEnd.X, lEnd.Y, rEnd.X, rEnd.Y, Color.White * 0.35f);
    }

    private void DrawHUD(Vector2 center)
    {
        // Top-left info
        DrawSpacedText(_font, $"Credits: {_player.Credits}", new Microsoft.Xna.Framework.Vector2(10, 10), Color.Yellow);

        // AI status
        string diffStr = _routeManager.Difficulty.ToString();
        int blocked = _routeManager.CountBlocked;
        int maxBlocked = _routeManager.MaxBlocked;
        Color aiColor = blocked > 0 ? new Color(255, 150, 100) : Color.Gray * 0.6f;
        DrawSpacedText(_font, $"AI [{diffStr}]  Blockades: {blocked}/{maxBlocked}",
            new Microsoft.Xna.Framework.Vector2(10, 170), aiColor);

        if (_galaxy.CurrentSystem != null)
        {
            string info = $"Docked at: {_galaxy.CurrentSystem.Name} [{_galaxy.CurrentSystem.Faction}]";
            DrawSpacedText(_font, info, new Microsoft.Xna.Framework.Vector2(10, 30), Color.Cyan);
            DrawSpacedText(_font, "Press E / Click  -  Open station services", new Microsoft.Xna.Framework.Vector2(10, 50), Color.Gray * 0.7f);
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
                DrawSpacedText(_font, $"Nearest: {nearest.Name} ({dir})",
                    new Microsoft.Xna.Framework.Vector2(10, 30), Color.Gray * 0.8f);
                DrawSpacedText(_font, $"Distance: {(int)nearestDist}",
                    new Microsoft.Xna.Framework.Vector2(10, 50), Color.Gray * 0.6f);
            }
        }

        // Active quests
        float y = 80;
        if (_galaxy.ActiveQuests.Count > 0)
        {
            DrawSpacedText(_font, "--- Active Quests ---", new Microsoft.Xna.Framework.Vector2(10, y), Color.Gold);
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

                DrawSpacedText(_font, $"  {q.Name}: {status}", new Microsoft.Xna.Framework.Vector2(10, y), Color.White * 0.9f);
                y += 18;
            }
        }

        // Owned upgrades
        if (_player.OwnedUpgrades.Count > 0)
        {
            y += 10;
            DrawSpacedText(_font, "--- Upgrades ---", new Microsoft.Xna.Framework.Vector2(10, y), Color.Lime);
            y += 20;
            foreach (var upId in _player.OwnedUpgrades)
            {
                var up = _galaxy.AllUpgrades.FirstOrDefault(u => u.Id == upId);
                if (up != null)
                    DrawSpacedText(_font, $"  {up.Name}", new Microsoft.Xna.Framework.Vector2(10, y), Color.Lime * 0.8f);
                y += 18;
            }
        }

        // Controls hint
        string controls = "WASD/Arrows: Fly | Shift: Boost | E: Interact | Q: Accept Quest | ESC: Close";
        var controlsSize = _font.MeasureString(controls);
        DrawSpacedText(_font, controls,
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
            DrawSpacedText(_titleFont, "Paused",
                new Microsoft.Xna.Framework.Vector2(textX, textY), Color.Cyan);
            textY += 60;

            string[] options = { "New Game", "Save Game", "Load Game", "Controls", "Quit" };
            for (int i = 0; i < options.Length; i++)
            {
                bool selected = _menuSelection == i;
                Color c = selected ? Color.Yellow : Color.Gray;
                string prefix = selected ? "> " : "  ";
                DrawSpacedText(_font, prefix + options[i],
                    new Microsoft.Xna.Framework.Vector2(textX, textY), c);
                textY += 30;
            }

            textY += 20;
            DrawSpacedText(_font, "[Enter] Select  |  [ESC] Resume",
                new Microsoft.Xna.Framework.Vector2(textX, textY), Color.Gray * 0.6f);
        }
        else if (_currentMenu == MenuType.SystemInfo && _menuSystem != null)
        {
            var sys = _menuSystem;
            DrawSpacedText(_titleFont, sys.Name, new Microsoft.Xna.Framework.Vector2(textX, textY), ParseColor(sys.Color));
            textY += 50;

            DrawSpacedText(_font, sys.Description, new Microsoft.Xna.Framework.Vector2(textX, textY), Color.White * 0.8f);
            textY += 40;

            DrawSpacedText(_font, $"Faction: {sys.Faction ?? "None"}", new Microsoft.Xna.Framework.Vector2(textX, textY), Color.Cyan);
            textY += 22;
            DrawSpacedText(_font, $"Hostility Level: {sys.Hostility}/10", new Microsoft.Xna.Framework.Vector2(textX, textY),
                sys.Hostility > 3 ? Color.OrangeRed : Color.LimeGreen);
            textY += 22;

            if (sys.Services.Count > 0)
            {
                DrawSpacedText(_font, "Services: " + string.Join(", ", sys.Services), new Microsoft.Xna.Framework.Vector2(textX, textY), Color.Yellow * 0.9f);
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
                DrawSpacedText(_font, prefix + items[i], new Microsoft.Xna.Framework.Vector2(textX, textY), c);
                textY += 24;
            }
        }
        else if (_currentMenu == MenuType.UpgradeShop && _menuSystem != null)
        {
            var upgrades = _galaxy.GetAvailableUpgradesForSystem(_menuSystem.Id, _player);

            DrawSpacedText(_titleFont, $"{_menuSystem.Name}  -  Upgrade Shop",
                new Microsoft.Xna.Framework.Vector2(textX, textY), Color.Yellow);
            textY += 50;

            DrawSpacedText(_font, $"Your Credits: {_player.Credits}",
                new Microsoft.Xna.Framework.Vector2(textX, textY), Color.Gold);
            textY += 30;

            if (upgrades.Count == 0)
            {
                DrawSpacedText(_font, "No upgrades available.", new Microsoft.Xna.Framework.Vector2(textX, textY), Color.Gray);
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
                    DrawSpacedText(_font, $"{prefix}{up.Name}  -  {up.Cost}cr",
                        new Microsoft.Xna.Framework.Vector2(textX, textY), nameColor);
                    textY += 20;
                    DrawSpacedText(_font, $"     {up.Description}",
                        new Microsoft.Xna.Framework.Vector2(textX, textY), Color.White * 0.5f);
                    textY += 24;
                }
            }

            textY += 20;
            DrawSpacedText(_font, "[Enter] Buy  |  [ESC] Back",
                new Microsoft.Xna.Framework.Vector2(textX, textY), Color.Gray * 0.7f);
        }
        else if (_currentMenu == MenuType.QuestBoard && _menuSystem != null)
        {
            var quests = _galaxy.AvailableQuests
                .Where(q => q.GiverSystem == _menuSystem.Id)
                .ToList();

            DrawSpacedText(_titleFont, $"{_menuSystem.Name}  -  Quest Board",
                new Microsoft.Xna.Framework.Vector2(textX, textY), Color.Gold);
            textY += 50;

            if (quests.Count == 0)
            {
                DrawSpacedText(_font, "No quests available here.",
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
                    DrawSpacedText(_font, $"{prefix}{q.Name}",
                        new Microsoft.Xna.Framework.Vector2(textX, textY), c);
                    textY += 20;
                    DrawSpacedText(_font, $"     {q.Description}",
                        new Microsoft.Xna.Framework.Vector2(textX, textY), Color.White * 0.5f);
                    textY += 18;
                    DrawSpacedText(_font, $"     Reward: {q.RewardCredits}cr" +
                        (q.RewardUpgrade != null ? $" + {q.RewardUpgrade}" : ""),
                        new Microsoft.Xna.Framework.Vector2(textX, textY), Color.Yellow * 0.7f);
                    textY += 24;
                }
            }

            textY += 20;
            DrawSpacedText(_font, "[Enter] Accept  |  [ESC] Back",
                new Microsoft.Xna.Framework.Vector2(textX, textY), Color.Gray * 0.7f);
        }
        else if (_currentMenu == MenuType.Controls)
        {
            // Dim background
            _spriteBatch.Draw(_pixel, new Microsoft.Xna.Framework.Rectangle(0, 0, ScreenWidth, ScreenHeight),
                new Color(0, 0, 0, 200));

            string title = "Controls";
            var titleSz = _titleFont.MeasureString(title);
            DrawSpacedText(_titleFont, title,
                new Microsoft.Xna.Framework.Vector2((ScreenWidth - titleSz.X) / 2f, 30), Color.Cyan);

            string[] lines = {
                "Galaxy View",
                "  W / Up        Thrust forward",
                "  S / Down      Thrust backward",
                "  A / Left      Rotate left",
                "  D / Right     Rotate right",
                "  Shift         Boost",
                "  Enter         Enter system",
                "  Q             Accept quest",
                "",
                "System View",
                "  W / Up        Thrust forward",
                "  S / Down      Thrust backward",
                "  A / Left      Rotate left",
                "  D / Right     Rotate right",
                "  Shift         Boost",
                "  E             Dock / Undock",
                "",
                "General",
                "  ESC           Pause menu / Back",
                "  T             System map",
                "  G             Galaxy map",
                "  I             Inventory",
            };

            float lx = (ScreenWidth - 500) / 2f;
            float ly = 80;
            foreach (var line in lines)
            {
                bool isHeader = line.Length > 0 && line[0] != ' ';
                Color c = isHeader ? new Color(255, 200, 100) : Color.Gray * 0.9f;
                DrawSpacedText(_font, line,
                    new Microsoft.Xna.Framework.Vector2(lx, ly), c);
                ly += isHeader ? 24 : 20;
            }

            DrawSpacedText(_font, "[ESC] Back",
                new Microsoft.Xna.Framework.Vector2(ScreenWidth - 120, ScreenHeight - 30),
                Color.Gray * 0.6f);
        }
    }

    private void DrawSystemMapOverlay()
    {
        // Dim background
        _spriteBatch.Draw(_pixel, new Microsoft.Xna.Framework.Rectangle(0, 0, ScreenWidth, ScreenHeight),
            new Color(0, 0, 0, 200));

        var sys = _galaxy.CurrentSystem;
        if (sys == null)
        {
            var msg = "No system selected";
            var sz = _titleFont.MeasureString(msg);
            DrawSpacedText(_titleFont, msg,
                new Microsoft.Xna.Framework.Vector2((ScreenWidth - sz.X) / 2f, ScreenHeight / 2f - sz.Y / 2f),
                Color.Gray);
            return;
        }

        // Title
        string title = $"System: {sys.Name}";
        var titleSz = _titleFont.MeasureString(title);
        DrawSpacedText(_titleFont, title,
            new Microsoft.Xna.Framework.Vector2((ScreenWidth - titleSz.X) / 2f, 20), Color.Cyan);

        // Layout
        float cx = ScreenWidth / 2f;
        float cy = ScreenHeight / 2f + 20;
        float margin = MathF.Min(cx, cy) - 30;
        float systemRadius = 0f;
        foreach (var p in sys.Planets)
            if (p.OrbitRadius > systemRadius) systemRadius = p.OrbitRadius;
        if (sys.Station != null && sys.Station.OrbitRadius > systemRadius)
            systemRadius = sys.Station.OrbitRadius;
        systemRadius = MathF.Max(systemRadius, 1f) * 1.1f;
        float scale = margin / systemRadius;

        // Orbit rings
        foreach (var p in sys.Planets)
            DrawCircle(cx, cy, p.OrbitRadius * scale, new Color(60, 60, 80, 100));
        if (sys.Station != null)
            DrawCircle(cx, cy, sys.Station.OrbitRadius * scale, new Color(60, 80, 100, 100));

        // Star
        float starR = MathF.Max(sys.StarRadius * scale, 4f);
        FillCircle(cx, cy, starR, ParseColor(sys.Color) * 0.5f);
        DrawCircle(cx, cy, starR, ParseColor(sys.Color));

        // Planets
        float angle = 0;
        foreach (var p in sys.Planets)
        {
            float px = cx + MathF.Cos(angle) * p.OrbitRadius * scale;
            float py = cy + MathF.Sin(angle) * p.OrbitRadius * scale;
            float pr = MathF.Max(p.Radius * scale, 2f);
            FillCircle(px, py, pr, ParseColor(p.Color));

            var lblSz = _font.MeasureString(p.Name);
            DrawSpacedText(_font, p.Name,
                new Microsoft.Xna.Framework.Vector2(px - lblSz.X / 2f, py + pr + 4f),
                Color.White * 0.8f);
            angle += 1.5f;
        }

        // Station
        if (sys.Station != null)
        {
            float stAngle = sys.Station.Angle;
            float stx = cx + MathF.Cos(stAngle) * sys.Station.OrbitRadius * scale;
            float sty = cy + MathF.Sin(stAngle) * sys.Station.OrbitRadius * scale;
            float stR = MathF.Max(sys.Station.Radius * scale, 2f);
            FillCircle(stx, sty, stR, Color.LightBlue);

            var stLbl = sys.Station.Name;
            var stSz = _font.MeasureString(stLbl);
            DrawSpacedText(_font, stLbl,
                new Microsoft.Xna.Framework.Vector2(stx - stSz.X / 2f, sty + stR + 4f),
                Color.Cyan);
        }

        // Player position
        float plx = cx + _player.Position.X * scale;
        float ply = cy + _player.Position.Y * scale;
        FillCircle(plx, ply, 4f, Color.White);
        DrawCircle(plx, ply, 6f, Color.White);

        // Quest targets in this system
        float qy = ScreenHeight - 60;
        foreach (var quest in _galaxy.ActiveQuests)
        {
            if (quest.ObjectiveType == "travel" && quest.TargetSystem == sys.Id)
            {
                DrawSpacedText(_font, $"Quest: {quest.Name}",
                    new Microsoft.Xna.Framework.Vector2(30, qy), Color.Gold);
                qy += 20;
            }
        }

        // Close hint
        string hint = "[T] or [ESC] Close";
        var hintSz = _font.MeasureString(hint);
        DrawSpacedText(_font, hint,
            new Microsoft.Xna.Framework.Vector2(ScreenWidth - hintSz.X - 20, ScreenHeight - 30),
            Color.Gray * 0.7f);
    }

    private void DrawGalaxyMapOverlay()
    {
        // Dim background
        _spriteBatch.Draw(_pixel, new Microsoft.Xna.Framework.Rectangle(0, 0, ScreenWidth, ScreenHeight),
            new Color(0, 0, 0, 200));

        string title = "Galaxy Map";
        var titleSz = _titleFont.MeasureString(title);
        DrawSpacedText(_titleFont, title,
            new Microsoft.Xna.Framework.Vector2((ScreenWidth - titleSz.X) / 2f, 20), Color.Cyan);

        // Find bounds
        float minX = float.MaxValue, maxX = float.MinValue;
        float minY = float.MaxValue, maxY = float.MinValue;
        foreach (var sys in _galaxy.Systems)
        {
            if (sys.X < minX) minX = sys.X;
            if (sys.X > maxX) maxX = sys.X;
            if (sys.Y < minY) minY = sys.Y;
            if (sys.Y > maxY) maxY = sys.Y;
        }

        float rangeX = maxX - minX + 400;
        float rangeY = maxY - minY + 400;
        float mapAreaW = ScreenWidth - 80;
        float mapAreaH = ScreenHeight - 120;
        float scale = MathF.Min(mapAreaW / rangeX, mapAreaH / rangeY) * 0.9f;

        float cx = (minX + maxX) / 2f;
        float cy2 = (minY + maxY) / 2f;
        float originX = ScreenWidth / 2f;
        float originY = ScreenHeight / 2f + 10;

        // Connection lines
        var drawn = new HashSet<(string, string)>();
        foreach (var sys in _galaxy.Systems)
        {
            foreach (var conn in sys.Connections)
            {
                var key = string.Compare(sys.Id, conn, StringComparison.Ordinal) < 0
                    ? (sys.Id, conn) : (conn, sys.Id);
                if (drawn.Contains(key)) continue;
                drawn.Add(key);

                var other = _galaxy.FindSystemById(conn);
                if (other == null) continue;

                float x1 = originX + (sys.X - cx) * scale;
                float y1 = originY + (sys.Y - cy2) * scale;
                float x2 = originX + (other.X - cx) * scale;
                float y2 = originY + (other.Y - cy2) * scale;

                bool blocked = _routeManager.IsBlocked(sys.Id, conn);
                if (blocked)
                {
                    float pulse = MathF.Sin((float)_gameTime.TotalGameTime.TotalSeconds * 2f) * 0.2f + 0.6f;
                    DrawLine(x1, y1, x2, y2, new Color(200, 30, 30) * pulse);
                }
                else
                {
                    DrawLine(x1, y1, x2, y2, new Color(60, 80, 120, 80));
                }
            }
        }

        // Systems
        foreach (var sys in _galaxy.Systems)
        {
            float sx = originX + (sys.X - cx) * scale;
            float sy = originY + (sys.Y - cy2) * scale;

            Color color = ParseColor(sys.Color);
            FillCircle(sx, sy, MathF.Max(sys.Radius * scale * 0.6f, 3f), color * 0.8f);
            DrawCircle(sx, sy, MathF.Max(sys.Radius * scale * 0.6f, 3f), color);

            bool isCurrent = _player.CurrentSystemId == sys.Id;
            bool isQuest = _galaxy.ActiveQuests.Any(q => q.TargetSystem == sys.Id);

            if (isCurrent)
                DrawCircle(sx, sy, 8f, Color.Yellow);

            if (isQuest)
            {
                float pulse = MathF.Sin((float)_gameTime.TotalGameTime.TotalSeconds * 2f) * 0.3f + 0.7f;
                DrawCircle(sx, sy, 12f, Color.Gold * pulse);
            }

            // Name label
            var lblSz = _font.MeasureString(sys.Name);
            float lx = sx - lblSz.X / 2f;
            float ly = sy + MathF.Max(sys.Radius * scale * 0.6f, 3f) + 4f;
            _spriteBatch.Draw(_pixel,
                new Microsoft.Xna.Framework.Rectangle((int)lx - 3, (int)ly - 1,
                    (int)lblSz.X + 6, (int)lblSz.Y + 3),
                new Color(0, 0, 0, 140));
            DrawSpacedText(_font, sys.Name,
                new Microsoft.Xna.Framework.Vector2(lx, ly),
                isCurrent ? Color.White : Color.White * 0.8f);
        }

        // Player position
        float plx = originX + (_player.Position.X - cx) * scale;
        float ply2 = originY + (_player.Position.Y - cy2) * scale;
        FillCircle(plx, ply2, 4f, Color.White);
        DrawCircle(plx, ply2, 7f, Color.White);

        // Legend
        float legX = 20;
        float legY = ScreenHeight - 80;
        DrawSpacedText(_font, "Blockaded route  ---", new Microsoft.Xna.Framework.Vector2(legX, legY), new Color(200, 30, 30));
        DrawSpacedText(_font, "Quest target  O", new Microsoft.Xna.Framework.Vector2(legX, legY + 18), Color.Gold);
        DrawSpacedText(_font, "Current system  O", new Microsoft.Xna.Framework.Vector2(legX, legY + 36), Color.Yellow);

        // AI status on map
        string aiInfo = $"AI [{_routeManager.Difficulty}]  Blockades: {_routeManager.CountBlocked}/{_routeManager.MaxBlocked}";
        var aiSz = _font.MeasureString(aiInfo);
        DrawSpacedText(_font, aiInfo,
            new Microsoft.Xna.Framework.Vector2(ScreenWidth - aiSz.X - 20, 50),
            new Color(255, 150, 100));

        // Close hint
        string hint = "[G] or [ESC] Close";
        var hintSz = _font.MeasureString(hint);
        DrawSpacedText(_font, hint,
            new Microsoft.Xna.Framework.Vector2(ScreenWidth - hintSz.X - 20, ScreenHeight - 30),
            Color.Gray * 0.7f);
    }

    private void DrawInventoryOverlay()
    {
        _spriteBatch.Draw(_pixel, new Microsoft.Xna.Framework.Rectangle(0, 0, ScreenWidth, ScreenHeight),
            new Color(0, 0, 0, 180));

        int panelW = 800;
        int panelH = 600;
        int px = (ScreenWidth - panelW) / 2;
        int py = (ScreenHeight - panelH) / 2;

        _spriteBatch.Draw(_pixel, new Microsoft.Xna.Framework.Rectangle(px, py, panelW, panelH),
            new Color(15, 15, 35, 235));
        DrawRect(px, py, panelW, panelH, new Color(60, 60, 100));

        int textX = px + 20;
        int textY = py + 20;

        // Title
        DrawSpacedText(_titleFont, "Inventory",
            new Microsoft.Xna.Framework.Vector2(textX, textY), Color.Cyan);
        textY += 50;

        // Cargo usage
        int cap = _player.CargoCapacity;
        if (_player.OwnedUpgrades.Contains("cargo_v1")) cap = (int)(cap * 2.0f);
        int used = _player.UsedCargo;
        Color capColor = used > cap ? Color.Red : Color.Gray;
        DrawSpacedText(_font, $"Cargo: {used}/{cap}",
            new Microsoft.Xna.Framework.Vector2(px + panelW - 200, py + 25), capColor);
        if (used > cap)
        {
            DrawSpacedText(_font, "OVERWEIGHT!",
                new Microsoft.Xna.Framework.Vector2(px + panelW - 200, py + 45), Color.Red);
        }

        // Tabs
        string[] tabs = { "Quest Items", "Resources", "Equipment" };
        float tabX = textX;
        for (int i = 0; i < tabs.Length; i++)
        {
            Color tc = i == _inventoryTab ? Color.Yellow : Color.Gray;
            string tPrefix = i == _inventoryTab ? "[ " : "  ";
            string tSuffix = i == _inventoryTab ? " ]" : "  ";
            var sz = _font.MeasureString(tabs[i]);
            DrawSpacedText(_font, tPrefix + tabs[i] + tSuffix,
                new Microsoft.Xna.Framework.Vector2(tabX, textY), tc);
            tabX += sz.X + 30;
        }
        textY += 40;

        // Content
        if (_inventoryTab == 0)
            DrawInventoryQuestItems(new Microsoft.Xna.Framework.Vector2(textX, textY), px + panelW - 20);
        else if (_inventoryTab == 1)
            DrawInventoryResources(new Microsoft.Xna.Framework.Vector2(textX, textY), px + panelW - 20);
        else if (_inventoryTab == 2)
            DrawInventoryEquipment(new Microsoft.Xna.Framework.Vector2(textX, textY), px + panelW - 20);
    }

    private void DrawInventoryQuestItems(Microsoft.Xna.Framework.Vector2 pos, float rightX)
    {
        float textY = pos.Y;
        if (_player.QuestItems.Count == 0)
        {
            DrawSpacedText(_font, "No quest items.",
                new Microsoft.Xna.Framework.Vector2(pos.X, textY), Color.Gray);
            return;
        }

        for (int i = 0; i < _player.QuestItems.Count; i++)
        {
            var entry = _player.QuestItems[i];
            string label = $"{entry.Id.Replace('_', ' ')} x{entry.Quantity}";
            DrawSpacedText(_font, label,
                new Microsoft.Xna.Framework.Vector2(pos.X, textY), Color.Wheat);
            textY += 24;
        }
    }

    private void DrawInventoryResources(Microsoft.Xna.Framework.Vector2 pos, float rightX)
    {
        float textY = pos.Y;
        if (_player.Resources.Count == 0)
        {
            DrawSpacedText(_font, "No resources in cargo.",
                new Microsoft.Xna.Framework.Vector2(pos.X, textY), Color.Gray);
            return;
        }

        // Header
        DrawSpacedText(_font, "Item                          Qty    Value",
            new Microsoft.Xna.Framework.Vector2(pos.X, textY), Color.Gray * 0.6f);
        textY += 24;

        for (int i = 0; i < _player.Resources.Count; i++)
        {
            var entry = _player.Resources[i];
            var def = _galaxy.FindResource(entry.Id);
            string name = def?.Name ?? entry.Id;
            int totalValue = (def?.BasePrice ?? 0) * entry.Quantity;
            string line = $"{name,-30} {entry.Quantity,-5} {totalValue}cr";
            DrawSpacedText(_font, line,
                new Microsoft.Xna.Framework.Vector2(pos.X, textY), Color.White * 0.9f);
            textY += 22;
        }
    }

    private void DrawInventoryEquipment(Microsoft.Xna.Framework.Vector2 pos, float rightX)
    {
        float textY = pos.Y;

        string[] slotLabels = { "Weapon 1", "Weapon 2", "Shield", "Engine", "Utility 1", "Utility 2" };
        string[] slotKeys = { "weapon1", "weapon2", "shield", "engine", "utility1", "utility2" };

        for (int i = 0; i < slotKeys.Length; i++)
        {
            string slotLabel = slotLabels[i];
            bool filled = _player.Equipment.ContainsKey(slotKeys[i]);
            string equipName = "";
            if (filled)
            {
                var def = _galaxy.FindEquipment(_player.Equipment[slotKeys[i]]);
                equipName = def?.Name ?? _player.Equipment[slotKeys[i]];
            }

            DrawSpacedText(_font, $"{slotLabel}:",
                new Microsoft.Xna.Framework.Vector2(pos.X, textY), Color.Gray);
            string itemLabel = filled ? equipName : "--- empty ---";
            Color itemColor = filled ? Color.Lime : Color.Gray * 0.5f;
            DrawSpacedText(_font, itemLabel,
                new Microsoft.Xna.Framework.Vector2(pos.X + 120, textY), itemColor);
            textY += 26;
        }

        textY += 10;

        // Hint
        DrawSpacedText(_font, "[Q/E] Switch tab  |  [I] or [ESC] Close",
            new Microsoft.Xna.Framework.Vector2(pos.X, textY), Color.Gray * 0.5f);
    }

    private void DrawSpacedText(SpriteFont font, string text, Microsoft.Xna.Framework.Vector2 position, Color color)
    {
        if (text.Length == 0) return;

        string[] parts = text.Split(' ');
        if (parts.Length <= 1)
        {
            _spriteBatch.DrawString(font, text, position, color);
            return;
        }

        float spaceW = font.MeasureString(" ").X;
        if (spaceW < 1f) spaceW = font.MeasureString("M").X;

        float x = position.X;
        float y = position.Y;
        for (int i = 0; i < parts.Length; i++)
        {
            if (parts[i].Length > 0)
            {
                _spriteBatch.DrawString(font, parts[i], new Microsoft.Xna.Framework.Vector2(x, y), color);
                x += font.MeasureString(parts[i]).X + spaceW;
            }
            else
            {
                x += spaceW;
            }
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

        DrawSpacedText(_titleFont, msg,
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
