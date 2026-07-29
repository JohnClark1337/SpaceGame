using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using SpaceGame.Drawing;
using SpaceGame.Models;
using SpaceGame.Services;
using SpaceGame.Systems;
using System.Text.Json;
using Color = Microsoft.Xna.Framework.Color;
using Vector2 = SpaceGame.Systems.Vector2;
using XnaVector2 = Microsoft.Xna.Framework.Vector2;

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
    private LlmService? _llmService;
    private GalaxyRenderer _galaxyRenderer = null!;
    private MenuRenderer _menuRenderer = null!;
    private OverlayRenderer _overlayRenderer = null!;
    private AttackManager _attackManager = null!;
    private AiManager _aiManager = null!;
    private bool _aiManagersInitialized;

    private int _initialIndependentCount;
    private readonly List<GalacticBroadcast> _pendingBroadcasts = new();
    private bool _showBroadcastDialog;
    private int _lastNotifiedBroadcastCount;
    private readonly Queue<QuestDialog> _questDialogQueue = new();
    private bool _showQuestDialog;
    private QuestDialog? _currentQuestDialog;
    private List<string> _questDialogWrappedLines = new();
    private int _questDialogScroll;
    private int _broadcastScroll;
    private int _broadcastTab;

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
    private bool _showQuestLog;
    private int _questLogSelection;
    private SystemScene _systemScene = null!;
    private Vector2 _galaxyPlayerPos;
    private Vector2 _galaxyPlayerVel;

    private struct PlayerSnapshot
    {
        public int Credits; public int Health; public int MaxHealth; public float Fuel; public float MaxFuel;
        public int CargoCapacity; public string? CurrentSystemId; public Vector2 Position; public Vector2 Velocity;
        public List<string> OwnedUpgrades; public List<string> CompletedQuests;
        public List<InventoryEntry> Resources; public List<InventoryEntry> QuestItems; public List<InventoryEntry> Consumables;
        public List<InventoryEntry> UnequippedEquipment; public Dictionary<string, string> Equipment;
        public float BaseMaxSpeed; public float BaseThrust; public float BaseRotationSpeed; public float Angle;
    }
    private PlayerSnapshot? _trainingPlayerSnapshot;

    private int _selectedConnectionIndex;
    private bool _isTraveling;
    private string? _travelDestId;
    private float _travelLerp;
    private Vector2 _travelStartPos;
    private Vector2 _travelEndPos;
    private const float TravelDuration = 2f;

    private int _inventoryTab;
    private int _invScroll;
    private int _invSelection;
    private string _invMsgText = "";
    private float _invMsgTimer;
    private int _priceScroll;
    private int _systemInfoScroll;
    private int _systemInfoMaxScroll;
    private int _controlsScroll;
    private bool _equipSelectMode;
    private int _equipSelectSlotIdx;
    private int _equipSelectCursor;

    public GameTime GameTime => _gameTime;
    public int ViewWidth => ScreenWidth;
    public int ViewHeight => ScreenHeight;

    public List<UpgradeData> GetUpgradesForSystem(string systemId) =>
        _galaxy.GetAvailableUpgradesForSystem(systemId, _player);

    public List<QuestData> GetQuestsForSystem(string systemId) =>
        _galaxy.AvailableQuests.Where(q => q.GiverSystem == systemId).ToList();

    public void ShowQuestDialogs(QuestData quest, string trigger)
    {
        foreach (var d in quest.Dialogs)
            if (d.Trigger == trigger)
                _questDialogQueue.Enqueue(d);
        if (_questDialogQueue.Count > 0 && !_showQuestDialog)
        {
            _currentQuestDialog = _questDialogQueue.Dequeue();
            _showQuestDialog = true;
        }
    }

    public void ExitToGalaxy()
    {
        var sys = _galaxy.FindSystemById(_player.CurrentSystemId ?? "proxima");
        if (sys != null)
        {
            _player.Position = new Vector2(sys.X, sys.Y);
            _player.Velocity = Vector2.Zero;
        }
        _isTraveling = false;
        _viewMode = ViewMode.Galaxy;
    }

    public void EnterTraining()
    {
        _galaxyPlayerPos = _player.Position;
        _galaxyPlayerVel = _player.Velocity;

        _trainingPlayerSnapshot = new PlayerSnapshot
        {
            Credits = _player.Credits, Health = _player.Health, MaxHealth = _player.MaxHealth,
            Fuel = _player.Fuel, MaxFuel = _player.MaxFuel, CargoCapacity = _player.CargoCapacity,
            Angle = _player.Angle, CurrentSystemId = _player.CurrentSystemId,
            Position = _player.Position, Velocity = _player.Velocity,
            OwnedUpgrades = new List<string>(_player.OwnedUpgrades),
            CompletedQuests = new List<string>(_player.CompletedQuests),
            Resources = _player.Resources.Select(r => new InventoryEntry { Id = r.Id, Quantity = r.Quantity }).ToList(),
            QuestItems = _player.QuestItems.Select(r => new InventoryEntry { Id = r.Id, Quantity = r.Quantity }).ToList(),
            Consumables = _player.Consumables.Select(r => new InventoryEntry { Id = r.Id, Quantity = r.Quantity }).ToList(),
            UnequippedEquipment = _player.UnequippedEquipment.Select(r => new InventoryEntry { Id = r.Id, Quantity = r.Quantity }).ToList(),
            Equipment = new Dictionary<string, string>(_player.Equipment),
            BaseMaxSpeed = _player.BaseMaxSpeed, BaseThrust = _player.BaseThrust,
            BaseRotationSpeed = _player.BaseRotationSpeed,
        };

        var sys = _galaxy.FindSystemById(_player.CurrentSystemId ?? "proxima");
        if (sys != null)
        {
            _systemScene.TrainingMode = true;
            _systemScene.EnterSystem(sys, this);
            _systemScene.PopulateTrainingInventory();
            _viewMode = ViewMode.System;
            _currentMenu = MenuType.None;
            _prevKeyboard = Keyboard.GetState();
        }
    }

    public void ExitTraining()
    {
        _systemScene.TrainingMode = false;

        if (_trainingPlayerSnapshot.HasValue)
        {
            var snap = _trainingPlayerSnapshot.Value;
            _player.Credits = snap.Credits; _player.Health = snap.Health; _player.MaxHealth = snap.MaxHealth;
            _player.Fuel = snap.Fuel; _player.MaxFuel = snap.MaxFuel; _player.CargoCapacity = snap.CargoCapacity;
            _player.Angle = snap.Angle; _player.CurrentSystemId = snap.CurrentSystemId;
            _player.Position = snap.Position; _player.Velocity = snap.Velocity;
            _player.OwnedUpgrades = snap.OwnedUpgrades; _player.CompletedQuests = snap.CompletedQuests;
            _player.Resources = snap.Resources; _player.QuestItems = snap.QuestItems;
            _player.Consumables = snap.Consumables; _player.UnequippedEquipment = snap.UnequippedEquipment;
            _player.Equipment = snap.Equipment; _player.BaseMaxSpeed = snap.BaseMaxSpeed;
            _player.BaseThrust = snap.BaseThrust; _player.BaseRotationSpeed = snap.BaseRotationSpeed;
            _trainingPlayerSnapshot = null;
        }

        var sys = _galaxy.FindSystemById(_player.CurrentSystemId ?? "proxima");
        if (sys != null)
        {
            _player.Position = new Vector2(sys.X, sys.Y);
            _player.Velocity = Vector2.Zero;
        }
        _viewMode = ViewMode.Galaxy;
        _currentMenu = MenuType.None;
        _prevKeyboard = Keyboard.GetState();
    }

    public void ShowNewGameMenu()
    {
        _player.Position = _galaxyPlayerPos;
        _player.Velocity = _galaxyPlayerVel;
        _viewMode = ViewMode.Galaxy;
        _currentMenu = MenuType.Pause;
        _menuSelection = 0;
    }

    private static string SavePath =>
        Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "save.json");

    public void SaveGame()
    {
        if (_systemScene != null && _systemScene.TrainingMode)
        {
            SetStatus("Cannot save during training mode.");
            _statusTimer = 2f;
            return;
        }

        var data = new SaveGameData
        {
            PlayerPosition = _player.Position, PlayerVelocity = _player.Velocity, PlayerAngle = _player.Angle,
            Credits = _player.Credits, Health = _player.Health, MaxHealth = _player.MaxHealth,
            Fuel = _player.Fuel, MaxFuel = _player.MaxFuel, CargoCapacity = _player.CargoCapacity,
            OwnedUpgrades = new List<string>(_player.OwnedUpgrades),
            CompletedQuests = new List<string>(_player.CompletedQuests),
            CurrentSystemId = _player.CurrentSystemId,
            Resources = _player.Resources.Select(r => new InventoryEntry { Id = r.Id, Quantity = r.Quantity }).ToList(),
            QuestItems = _player.QuestItems.Select(r => new InventoryEntry { Id = r.Id, Quantity = r.Quantity }).ToList(),
            Consumables = _player.Consumables.Select(r => new InventoryEntry { Id = r.Id, Quantity = r.Quantity }).ToList(),
            UnequippedEquipment = _player.UnequippedEquipment.Select(r => new InventoryEntry { Id = r.Id, Quantity = r.Quantity }).ToList(),
            Equipment = new Dictionary<string, string>(_player.Equipment),
            BaseMaxSpeed = _player.BaseMaxSpeed, BaseThrust = _player.BaseThrust,
            BaseRotationSpeed = _player.BaseRotationSpeed,
            Systems = _galaxy.Systems.Select(sys => new SystemSaveData
            {
                Id = sys.Id, Hostility = sys.Hostility, Faction = sys.Faction,
                DefenseLevel = sys.Station?.DefenseLevel ?? 0, StationAngle = sys.Station?.Angle ?? 0f
            }).ToList(),
            ActiveQuests = _galaxy.ActiveQuests.Select(q => CloneQuest(q)).ToList(),
            AvailableQuests = _galaxy.AvailableQuests.Select(q => CloneQuest(q)).ToList(),
            AllQuests = _galaxy.AllQuests.Select(q => CloneQuest(q)).ToList(),
            CurrentSystemIdRef = _galaxy.CurrentSystem?.Id,
            TargetSystemId = _galaxy.TargetSystem?.Id,
            BlockedRoutes = _routeManager.BlockedRoutes.ToList(),
            AiDifficulty = (int)_routeManager.Difficulty,
            Markets = _galaxy.Economy.Markets.ToDictionary(kv => kv.Key, kv => new SystemMarketState
            {
                Stocks = new Dictionary<string, float>(kv.Value.Stocks),
                Demands = new Dictionary<string, float>(kv.Value.Demands),
                ProductionRates = new Dictionary<string, float>(kv.Value.ProductionRates)
            }),
            NewsArticles = _galaxy.NewsService.Articles.ToList(),
            ActiveAttacks = _attackManager.GetSaveData(),
            GalaxyPlayerPos = _galaxyPlayerPos, GalaxyPlayerVel = _galaxyPlayerVel,
            AiTickTimer = _aiManager.AiTickTimer, AiCaptureTimer = _aiManager.AiCaptureTimer,
            FederationAiTimer = _aiManager.FederationAiTimer, AiDefenseTimer = _aiManager.AiDefenseTimer,
            InitialIndependentCount = _aiManager.InitialIndependentCount,
            IsInSystemView = _viewMode == ViewMode.System
        };

        string json = JsonSerializer.Serialize(data, new JsonSerializerOptions { WriteIndented = true });
        File.WriteAllText(SavePath, json);
        SetStatus("Game saved.");
        _statusTimer = 2f;
    }

    public bool LoadGame()
    {
        if (!File.Exists(SavePath))
        {
            SetStatus("No save file found.");
            _statusTimer = 2f;
            return false;
        }

        try
        {
            string json = File.ReadAllText(SavePath);
            var data = JsonSerializer.Deserialize<SaveGameData>(json, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
            if (data == null)
            {
                SetStatus("Save file corrupt.");
                _statusTimer = 2f;
                return false;
            }

            _player.Position = data.PlayerPosition; _player.Velocity = data.PlayerVelocity;
            _player.Angle = data.PlayerAngle; _player.Credits = data.Credits;
            _player.Health = data.Health; _player.MaxHealth = data.MaxHealth;
            _player.Fuel = data.Fuel; _player.MaxFuel = data.MaxFuel;
            _player.CargoCapacity = data.CargoCapacity;
            _player.OwnedUpgrades = new List<string>(data.OwnedUpgrades);
            _player.CompletedQuests = new List<string>(data.CompletedQuests);
            _player.CurrentSystemId = data.CurrentSystemId;
            _player.Resources = data.Resources.Select(r => new InventoryEntry { Id = r.Id, Quantity = r.Quantity }).ToList();
            _player.QuestItems = data.QuestItems.Select(r => new InventoryEntry { Id = r.Id, Quantity = r.Quantity }).ToList();
            _player.Consumables = data.Consumables.Select(r => new InventoryEntry { Id = r.Id, Quantity = r.Quantity }).ToList();
            _player.UnequippedEquipment = data.UnequippedEquipment.Select(r => new InventoryEntry { Id = r.Id, Quantity = r.Quantity }).ToList();
            _player.Equipment = new Dictionary<string, string>(data.Equipment);
            _player.RecalculateStats(_galaxy.AllEquipment);
            _player.BaseMaxSpeed = data.BaseMaxSpeed; _player.BaseThrust = data.BaseThrust;
            _player.BaseRotationSpeed = data.BaseRotationSpeed;

            foreach (var sd in data.Systems)
            {
                var sys = _galaxy.FindSystemById(sd.Id);
                if (sys == null) continue;
                sys.Hostility = sd.Hostility; sys.Faction = sd.Faction;
                if (sys.Station != null)
                {
                    sys.Station.DefenseLevel = sd.DefenseLevel;
                    sys.Station.Angle = sd.StationAngle;
                }
            }

            _galaxy.ActiveQuests.Clear(); _galaxy.ActiveQuests.AddRange(data.ActiveQuests);
            _galaxy.AvailableQuests.Clear(); _galaxy.AvailableQuests.AddRange(data.AvailableQuests);
            _galaxy.AllQuests.Clear(); _galaxy.AllQuests.AddRange(data.AllQuests);

            _galaxy.CurrentSystem = !string.IsNullOrEmpty(data.CurrentSystemIdRef) ? _galaxy.FindSystemById(data.CurrentSystemIdRef) : null;
            _galaxy.TargetSystem = !string.IsNullOrEmpty(data.TargetSystemId) ? _galaxy.FindSystemById(data.TargetSystemId) : null;

            _routeManager.SetBlockedRoutes(data.BlockedRoutes);
            _routeManager.Difficulty = (AiDifficulty)data.AiDifficulty;

            _galaxy.Economy.SetMarkets(data.Markets.ToDictionary(kv => kv.Key, kv => new SystemMarketState
            {
                Stocks = new Dictionary<string, float>(kv.Value.Stocks),
                Demands = new Dictionary<string, float>(kv.Value.Demands),
                ProductionRates = new Dictionary<string, float>(kv.Value.ProductionRates)
            }));

            _galaxy.NewsService.SetArticles(data.NewsArticles);

            _attackManager.RestoreAttacks(data.ActiveAttacks);

            _galaxyPlayerPos = data.GalaxyPlayerPos; _galaxyPlayerVel = data.GalaxyPlayerVel;
            _aiManager.AiTickTimer = data.AiTickTimer; _aiManager.AiCaptureTimer = data.AiCaptureTimer;
            _aiManager.FederationAiTimer = data.FederationAiTimer; _aiManager.AiDefenseTimer = data.AiDefenseTimer;
            _aiManager.InitialIndependentCount = data.InitialIndependentCount;

            _viewMode = ViewMode.Galaxy;
            _currentMenu = MenuType.None;

            if (data.IsInSystemView && _player.CurrentSystemId != null)
            {
                var sys = _galaxy.FindSystemById(_player.CurrentSystemId);
                if (sys != null)
                {
                    _galaxy.CurrentSystem = sys;
                    _systemScene = new SystemScene(_player);
                    _systemScene.EnterSystem(sys, this);
                    _viewMode = ViewMode.System;
                }
            }

            SetStatus("Game loaded.");
            _statusTimer = 2f;
            return true;
        }
        catch (Exception ex)
        {
            SetStatus($"Load failed: {ex.Message}");
            _statusTimer = 3f;
            return false;
        }
    }

    private static QuestData CloneQuest(QuestData q)
    {
        return new QuestData
        {
            Id = q.Id, Name = q.Name, Description = q.Description,
            QuestType = q.QuestType, NextQuestId = q.NextQuestId,
            ObjectiveType = q.ObjectiveType, TargetSystem = q.TargetSystem,
            TargetItem = q.TargetItem, TargetCount = q.TargetCount,
            RewardCredits = q.RewardCredits, RewardUpgrade = q.RewardUpgrade,
            RewardEquipment = q.RewardEquipment, GiverSystem = q.GiverSystem,
            RewardDefenseSystem = q.RewardDefenseSystem,
            RequiredResources = new Dictionary<string, int>(q.RequiredResources)
        };
    }

    private void NewGame()
    {
        _player.Velocity = Vector2.Zero; _player.Angle = 0;
        _player.Credits = 50; _player.Health = 50; _player.Fuel = _player.MaxFuel;
        _player.OwnedUpgrades.Clear(); _player.CompletedQuests.Clear();
        _player.Resources.Clear(); _player.QuestItems.Clear();
        _player.Consumables.Clear(); _player.UnequippedEquipment.Clear();
        _player.Equipment.Clear(); _player.CurrentSystemId = null;

        _galaxy.LoadData();
        _player.RecalculateStats(_galaxy.AllEquipment);
        _routeManager.SetGalaxy(_galaxy);

        var trigor = _galaxy.FindSystemById("trigor");
        if (trigor?.Station != null) trigor.Station.DefenseLevel = 5;
        var trigorAlpha = _galaxy.FindSystemById("trigor_alpha");
        if (trigorAlpha?.Station != null) trigorAlpha.Station.DefenseLevel = 3;
        var trigorBeta = _galaxy.FindSystemById("trigor_beta");
        if (trigorBeta?.Station != null) trigorBeta.Station.DefenseLevel = 2;
        var trigorGamma = _galaxy.FindSystemById("trigor_gamma");
        if (trigorGamma?.Station != null) trigorGamma.Station.DefenseLevel = 1;
        _aiManager.AiTickTimer = 8f; _aiManager.AiCaptureTimer = 30f;
        _aiManager.FederationAiTimer = 30f; _aiManager.AiDefenseTimer = 60f;
        _aiManager.InitialIndependentCount = _galaxy.Systems.Count(s => s.Hostility < 3 && s.Faction != "Atlas Federation");
        _galaxy.ActiveQuests.Clear(); _galaxy.TargetSystem = null;

        var startSys = _galaxy.FindSystemById("proxima");
        if (startSys != null)
        {
            _player.Position = new Vector2(startSys.X, startSys.Y);
            _player.CurrentSystemId = startSys.Id;
            _galaxy.CurrentSystem = startSys;
        }
        else { _player.Position = Vector2.Zero; }

        _galaxyPlayerPos = _player.Position; _galaxyPlayerVel = Vector2.Zero;
        _selectedConnectionIndex = 0; _isTraveling = false; _travelDestId = null;
        _viewMode = ViewMode.Galaxy; _currentMenu = MenuType.None;
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
        _routeManager.RouteBlocked += OnRouteBlocked;
        _routeManager.RouteUnblocked += OnRouteUnblocked;
        _llmService = new LlmService();
        _attackManager = new AttackManager(_galaxy, _player, _galaxy.NewsService,
            msg => _statusMessage = msg,
            (msg, dur) => { _statusMessage = msg; _statusTimer = dur; });
        _aiManager = new AiManager(_galaxy, _routeManager, _player, _attackManager,
            _llmService, _galaxy.NewsService, _pendingBroadcasts,
            msg => _statusMessage = msg,
            (msg, dur) => { _statusMessage = msg; _statusTimer = dur; });
        _aiManagersInitialized = true;

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

        _pendingBroadcasts.Add(new GalacticBroadcast
        {
            Faction = "Trigor Empire", CommanderName = "Emperor Cyrus III",
            CommanderTitle = "Emperor of the Trigor Empire",
            Message = "I, Cyrus III, Emperor of the Trigor Empire, do hereby declare that the age of Federation complacency has reached its end. The slow rot of bureaucratic indifference that starved billions shall be purged by fire and steel. A new order rises -- swift, absolute, and eternal.",
            Timestamp = 0f
        });
        _pendingBroadcasts.Add(new GalacticBroadcast
        {
            Faction = "Atlas Federation", CommanderName = "Prime Minister Ezara Loban",
            CommanderTitle = "Prime Minister of the Atlas Federation",
            Message = "Th-the Federation stands resolute. We urge calm and measured dialogue. There is always a diplomatic path. Our patrols have been... increased as a precaution. Citizens should go about their daily lives without undue concern.",
            Timestamp = 0f
        });

        base.Initialize();
    }

    protected override void LoadContent()
    {
        _spriteBatch = new SpriteBatch(GraphicsDevice);
        _font = Content.Load<SpriteFont>("Font");
        _titleFont = Content.Load<SpriteFont>("TitleFont");
        _pixel = new Texture2D(GraphicsDevice, 1, 1);
        _pixel.SetData(new[] { Color.White });

        _galaxyRenderer = new GalaxyRenderer(_spriteBatch, _pixel, _font, _titleFont);
        _menuRenderer = new MenuRenderer(_spriteBatch, _pixel, _font, _titleFont);
        _overlayRenderer = new OverlayRenderer(_spriteBatch, _pixel, _font, _titleFont);
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

        _galaxy.Economy?.Tick(dt);
        _galaxy.NewsService?.Tick(dt, (float)_gameTime.TotalGameTime.TotalSeconds);

        var keyboard = Keyboard.GetState();
        var mouse = Mouse.GetState();

        // Overlay toggle
        bool tHit = JustPressed(keyboard, Keys.T);
        bool gHit = JustPressed(keyboard, Keys.G);
        bool iHit = JustPressed(keyboard, Keys.I);
        bool qHit = JustPressed(keyboard, Keys.Q);

        if (tHit)
        {
            _showQuestLog = false;
            if (_overlay == Overlay.SystemMap) _overlay = Overlay.None;
            else if (_galaxy.CurrentSystem != null) _overlay = Overlay.SystemMap;
        }
        if (gHit)
        {
            _showQuestLog = false;
            _overlay = _overlay == Overlay.GalaxyMap ? Overlay.None : Overlay.GalaxyMap;
        }
        if (iHit)
        {
            _showQuestLog = false;
            if (_overlay == Overlay.Inventory) _overlay = Overlay.None;
            else { _overlay = Overlay.Inventory; _inventoryTab = 0; _invScroll = 0; _invSelection = 0; _equipSelectMode = false; }
        }

        bool wasShowingQuestLog = _showQuestLog;
        if (qHit)
        {
            if (_overlay == Overlay.None && _currentMenu == MenuType.None)
                _showQuestLog = !_showQuestLog;
        }

        // Quest dialog
        if (_showQuestDialog)
        {
            HandleQuestDialogInput(keyboard);
            _prevKeyboard = keyboard; _prevMouse = mouse;
            base.Update(gameTime); return;
        }

        // Broadcast dialog
        bool wasShowing = _showBroadcastDialog;
        if (JustPressed(keyboard, Keys.B) && _pendingBroadcasts.Count > 0 && !_showBroadcastDialog)
            _showBroadcastDialog = true;

        if (_showBroadcastDialog)
        {
            HandleBroadcastInput(keyboard, wasShowing);
            _prevKeyboard = keyboard; _prevMouse = mouse;
            base.Update(gameTime); return;
        }

        // Overlay input
        if (_overlay != Overlay.None)
        {
            HandleOverlayInput(keyboard, mouse);
            _prevKeyboard = keyboard; _prevMouse = mouse;
            base.Update(gameTime); return;
        }

        // Quest log overlay
        if (_showQuestLog)
        {
            HandleQuestLogInput(keyboard, wasShowingQuestLog);
        }

        // Galaxy view
        if (_viewMode == ViewMode.Galaxy)
        {
            HandleInput(dt, keyboard, mouse);

            if (_currentMenu == MenuType.None)
            {
                if (_isTraveling)
                {
                    _travelLerp += dt / TravelDuration;
                    if (_travelLerp >= 1f)
                    {
                        _travelLerp = 1f;
                        var destSys = _galaxy.FindSystemById(_travelDestId!);
                        if (destSys != null)
                        {
                            _player.Position = new Vector2(destSys.X, destSys.Y);
                            _player.CurrentSystemId = destSys.Id;
                            _galaxy.CurrentSystem = destSys;
                            _galaxy.CheckQuestProgress(_player);
                            _galaxy.RefreshAvailableQuests(_player);
                            foreach (var q in _galaxy.ActiveQuests.Where(q => q.TargetSystem == destSys.Id))
                                ShowQuestDialogs(q, "on_enter_system");
                            SetStatus($"Arrived at {destSys.Name}");
                            _galaxyPlayerPos = _player.Position;
                            _galaxyPlayerVel = Vector2.Zero;
                        }
                        _isTraveling = false; _travelDestId = null; _selectedConnectionIndex = 0;
                    }
                    else
                    {
                        float t = _travelLerp;
                        _player.Position = new Vector2(
                            _travelStartPos.X + (_travelEndPos.X - _travelStartPos.X) * t,
                            _travelStartPos.Y + (_travelEndPos.Y - _travelStartPos.Y) * t);
                        _player.Velocity = new Vector2(
                            _travelEndPos.X - _travelStartPos.X,
                            _travelEndPos.Y - _travelStartPos.Y).Normalized() * 200f;
                    }
                }
                else if (_player.CurrentSystemId != null)
                {
                    var currentSys = _galaxy.FindSystemById(_player.CurrentSystemId);
                    if (currentSys != null)
                    {
                        var unblockedConns = currentSys.Connections
                            .Where(id => !_routeManager.IsBlocked(_player.CurrentSystemId, id))
                            .Select(id => _galaxy.FindSystemById(id))
                            .Where(s => s != null)
                            .Cast<StarSystemData>()
                            .ToList();

                        var reachableConns = unblockedConns
                            .Where(s =>
                            {
                                float dist = Vector2.Distance(_player.Position, new Vector2(s.X, s.Y));
                                float fuelCost = MathF.Max(25f, dist * 0.015f);
                                return _player.Fuel >= fuelCost && (_player.Fuel - fuelCost) > _player.MaxFuel / 3;
                            })
                            .Select(s => s.Id)
                            .ToList();

                        if (reachableConns.Count > 0)
                        {
                            if (_selectedConnectionIndex >= reachableConns.Count)
                                _selectedConnectionIndex = 0;

                            bool upHit = JustPressed(keyboard, Keys.Up) || JustPressed(keyboard, Keys.W);
                            bool downHit = JustPressed(keyboard, Keys.Down) || JustPressed(keyboard, Keys.S);
                            if (upHit) _selectedConnectionIndex = (_selectedConnectionIndex - 1 + reachableConns.Count) % reachableConns.Count;
                            if (downHit) _selectedConnectionIndex = (_selectedConnectionIndex + 1) % reachableConns.Count;

                            if (JustPressed(keyboard, Keys.Enter))
                            {
                                string targetId = reachableConns[_selectedConnectionIndex];
                                var targetSys = _galaxy.FindSystemById(targetId);
                                if (targetSys != null)
                                {
                                    float dist = Vector2.Distance(_player.Position, new Vector2(targetSys.X, targetSys.Y));
                                    float fuelCost = MathF.Max(25f, dist * 0.015f);
                                    _player.Fuel -= fuelCost;
                                    _travelDestId = targetId;
                                    _travelStartPos = _player.Position;
                                    _travelEndPos = new Vector2(targetSys.X, targetSys.Y);
                                    _travelLerp = 0f; _isTraveling = true;
                                    _player.Velocity = Vector2.Zero;
                                    SetStatus($"Traveling to {targetSys.Name}...");
                                }
                            }
                        }

                        if (_player.Health > _player.MaxHealth / 4 && JustPressed(keyboard, Keys.Y))
                        {
                            _player.Health -= _player.MaxHealth / 4;
                            _player.Fuel += _player.MaxFuel / 4;
                            SetStatus($"Exchanged HP for fuel. Fuel: {_player.Fuel:F0}");
                        }

                        if (JustPressed(keyboard, Keys.E))
                        {
                            _galaxyPlayerPos = _player.Position;
                            _galaxyPlayerVel = Vector2.Zero;
                            _systemScene.EnterSystem(currentSys, this);
                            _viewMode = ViewMode.System;
                        }
                    }
                }

                if (JustClicked(mouse))
                {
                    Vector2 offset = new Vector2(ScreenWidth / 2f, ScreenHeight / 2f) - _player.Position;
                    var worldPos = new Vector2(mouse.X - offset.X, mouse.Y - offset.Y);
                    var clicked = _galaxy.FindSystemAtPosition(worldPos, 80f);
                    if (clicked != null)
                    {
                        _menuSystem = clicked; _currentMenu = MenuType.SystemInfo;
                        _menuSelection = 0; _priceScroll = 0; _systemInfoScroll = 0;
                    }
                }
            }
            _prevKeyboard = keyboard; _prevMouse = mouse;
        }

        // AI commander logic
        if (_aiManagersInitialized && _player.CurrentSystemId != null)
        {
            _aiManager.Tick(dt, _player.CurrentSystemId, (float)_gameTime.TotalGameTime.TotalSeconds);
        }

        // Attack timers
        bool inAttackedSystem = _viewMode == ViewMode.System && _galaxy.CurrentSystem != null &&
            _attackManager.IsUnderAttack(_galaxy.CurrentSystem.Id);
        _attackManager.Tick(dt);

        // System view
        if (_viewMode == ViewMode.System)
        {
            if (_currentMenu == MenuType.Pause)
            {
                HandlePauseInput(keyboard);
            }
            else if (_currentMenu == MenuType.Controls)
            {
                if (JustPressed(keyboard, Keys.Escape))
                { _currentMenu = MenuType.Pause; _menuSelection = 4; }
            }
            else
            {
                if (_currentMenu == MenuType.SystemInfo)
                {
                    if (JustPressed(keyboard, Keys.Tab) && _menuSystem != null)
                    {
                        var quests = _galaxy.AvailableQuests.Where(q => q.GiverSystem == _menuSystem.Id).ToList();
                        if (quests.Count > 0) { _currentMenu = MenuType.QuestBoard; _menuSelection = 0; }
                    }

                    if (JustPressed(keyboard, Keys.Down) || JustPressed(keyboard, Keys.S))
                    {
                        var currentSystem = _galaxy.CurrentSystem;
                        if (_menuSystem != null && currentSystem != null && _menuSystem.Id != currentSystem.Id)
                        {
                            int maxScroll = Math.Max(0, _galaxy.AllResources.Count - 5);
                            if (_priceScroll < maxScroll) _priceScroll++;
                            else if (_systemInfoMaxScroll > 0) _systemInfoScroll = Math.Min(_systemInfoScroll + 22, _systemInfoMaxScroll);
                        }
                        else if (_systemInfoMaxScroll > 0)
                            _systemInfoScroll = Math.Min(_systemInfoScroll + 22, _systemInfoMaxScroll);
                    }
                    if (JustPressed(keyboard, Keys.Up) || JustPressed(keyboard, Keys.W))
                    {
                        if (_systemInfoScroll > 0) _systemInfoScroll = Math.Max(0, _systemInfoScroll - 22);
                        else if (_priceScroll > 0) _priceScroll--;
                    }
                }

                if (JustPressed(keyboard, Keys.Escape))
                {
                    if (_currentMenu != MenuType.None)
                    { _currentMenu = MenuType.None; _menuSelection = 0; }
                    else if (!_systemScene.Docked && !_systemScene.TrainingMode)
                    { _currentMenu = MenuType.Pause; _menuSelection = 0; }
                }

                if (_overlay == Overlay.None && _currentMenu == MenuType.None)
                    _systemScene.Update(dt, keyboard, mouse);
            }
            _prevKeyboard = keyboard; _prevMouse = mouse;
        }

        if (_statusTimer > 0) _statusTimer -= dt;

        if (_statusTimer <= 0f && _pendingBroadcasts.Count > _lastNotifiedBroadcastCount && !_showBroadcastDialog)
        {
            _lastNotifiedBroadcastCount = _pendingBroadcasts.Count;
            _statusMessage = "Galactic Broadcast Received! Press B to view";
            _statusTimer = 6f;
        }

        if (_invMsgTimer > 0f)
        {
            _invMsgTimer -= dt;
            if (_invMsgTimer <= 0f) _invMsgText = "";
        }

        _starfield.Update(dt, _player.Velocity);

        // Global save/load
        if (JustPressed(keyboard, Keys.F5) && _currentMenu == MenuType.None) SaveGame();
        if (JustPressed(keyboard, Keys.F9) && _currentMenu == MenuType.None && File.Exists(SavePath)) LoadGame();

        base.Update(gameTime);
    }

    private void HandleQuestDialogInput(KeyboardState keyboard)
    {
        if (JustPressed(keyboard, Keys.Enter) || JustPressed(keyboard, Keys.Escape) || JustPressed(keyboard, Keys.Space))
        {
            if (_questDialogQueue.Count > 0)
            {
                _currentQuestDialog = _questDialogQueue.Dequeue();
                _questDialogScroll = 0;
            }
            else { _showQuestDialog = false; _currentQuestDialog = null; }
        }
        if (JustPressed(keyboard, Keys.Up)) _questDialogScroll = Math.Max(0, _questDialogScroll - 1);
        if (JustPressed(keyboard, Keys.Down)) _questDialogScroll = Math.Min(_questDialogWrappedLines.Count - 1, _questDialogScroll + 1);
    }

    private void HandleBroadcastInput(KeyboardState keyboard, bool wasShowing)
    {
        if (JustPressed(keyboard, Keys.Escape) || (JustPressed(keyboard, Keys.B) && wasShowing))
        { _showBroadcastDialog = false; _broadcastScroll = 0; }
        if (JustPressed(keyboard, Keys.Up)) _broadcastScroll = Math.Max(0, _broadcastScroll - 22);
        if (JustPressed(keyboard, Keys.Down)) _broadcastScroll += 22;
        if (JustPressed(keyboard, Keys.Tab))
        {
            if (keyboard.IsKeyDown(Keys.LeftShift) || keyboard.IsKeyDown(Keys.RightShift))
                _broadcastTab = _broadcastTab == 0 ? 2 : _broadcastTab - 1;
            else _broadcastTab = (_broadcastTab + 1) % 3;
        }
    }

    private void HandleOverlayInput(KeyboardState keyboard, MouseState mouse)
    {
        bool ovEsc = JustPressed(keyboard, Keys.Escape);
        if (ovEsc)
        {
            if (_inventoryTab == 2 && _equipSelectMode) _equipSelectMode = false;
            else _overlay = Overlay.None;
        }

        if (_overlay == Overlay.Inventory)
        {
            bool invLeft = JustPressed(keyboard, Keys.Left);
            bool invRight = JustPressed(keyboard, Keys.Right) || JustPressed(keyboard, Keys.E);
            bool invDown = JustPressed(keyboard, Keys.Down) || JustPressed(keyboard, Keys.S);
            bool invUp = JustPressed(keyboard, Keys.Up) || JustPressed(keyboard, Keys.W);

            if (invLeft) { _inventoryTab = (_inventoryTab - 1 + 5) % 5; _invSelection = 0; _equipSelectMode = false; }
            if (invRight) { _inventoryTab = (_inventoryTab + 1) % 5; _invSelection = 0; _equipSelectMode = false; }

            if (_inventoryTab == 2)
                HandleEquipmentInput(invUp, invDown, invLeft, invRight, keyboard);
            else if (_inventoryTab == 4)
                HandleConsumableInput(invUp, invDown, keyboard);
            else
            {
                if (invDown) _invScroll++;
                if (invUp) _invScroll--;
                if (_invScroll < 0) _invScroll = 0;
            }
        }
        else if (_overlay == Overlay.GalaxyMap)
        {
            if (JustClicked(mouse))
            {
                float minX = float.MaxValue, maxX = float.MinValue;
                float minY = float.MaxValue, maxY = float.MinValue;
                foreach (var s in _galaxy.Systems)
                {
                    if (s.X < minX) minX = s.X; if (s.X > maxX) maxX = s.X;
                    if (s.Y < minY) minY = s.Y; if (s.Y > maxY) maxY = s.Y;
                }
                float rangeX = maxX - minX + 400; float rangeY = maxY - minY + 400;
                float mapAreaW = ScreenWidth - 80; float mapAreaH = ScreenHeight - 120;
                float scale = MathF.Min(mapAreaW / rangeX, mapAreaH / rangeY) * 0.9f;
                float cx = (minX + maxX) / 2f; float cy = (minY + maxY) / 2f;
                float originX = ScreenWidth / 2f; float originY = ScreenHeight / 2f + 10;
                float worldX = (mouse.X - originX) / scale + cx;
                float worldY = (mouse.Y - originY) / scale + cy;
                var clicked = _galaxy.FindSystemAtPosition(new Vector2(worldX, worldY), 80f);
                if (clicked != null)
                {
                    _menuSystem = clicked; _currentMenu = MenuType.SystemInfo;
                    _menuSelection = 0; _priceScroll = 0; _systemInfoScroll = 0;
                    _overlay = Overlay.None;
                }
            }
        }
    }

    private void HandleEquipmentInput(bool invUp, bool invDown, bool invLeft, bool invRight, KeyboardState keyboard)
    {
        string[] slotKeys = { "weapon1", "weapon2", "shield", "engine", "utility1", "utility2" };
        string[] slotFilters = { "weapon", "weapon", "shield", "engine", "utility", "utility" };
        bool invEnter = JustPressed(keyboard, Keys.Enter);

        if (_equipSelectMode)
        {
            int slotIdx = _equipSelectSlotIdx;
            string filter = slotFilters[slotIdx];
            string key = slotKeys[slotIdx];

            var options = new List<string> { "" };
            foreach (var entry in _player.UnequippedEquipment)
            {
                var def = _galaxy.FindEquipment(entry.Id);
                if (def != null && def.Slot == filter) options.Add(entry.Id);
            }

            if (invDown) _equipSelectCursor++;
            if (invUp) _equipSelectCursor--;
            if (_equipSelectCursor < 0) _equipSelectCursor = 0;
            if (_equipSelectCursor >= options.Count) _equipSelectCursor = options.Count - 1;

            if (invEnter)
            {
                string selected = options[_equipSelectCursor];
                if (selected == "") _player.Equipment.Remove(key);
                else
                {
                    if (_player.Equipment.ContainsKey(key))
                    {
                        var oldId = _player.Equipment[key]; _player.Equipment.Remove(key);
                        var existing = _player.UnequippedEquipment.FirstOrDefault(e => e.Id == oldId);
                        if (existing != null) existing.Quantity++;
                        else _player.UnequippedEquipment.Add(new InventoryEntry { Id = oldId, Quantity = 1 });
                    }
                    var eqEntry = _player.UnequippedEquipment.FirstOrDefault(e => e.Id == selected);
                    if (eqEntry != null)
                    {
                        _player.Equipment[key] = selected; eqEntry.Quantity--;
                        if (eqEntry.Quantity <= 0) _player.UnequippedEquipment.RemoveAll(e => e.Id == selected);
                    }
                }
                _player.RecalculateStats(_galaxy.AllEquipment);
                _equipSelectMode = false;
            }
        }
        else
        {
            if (invDown) _invSelection++;
            if (invUp) _invSelection--;
            int maxSel = slotKeys.Length - 1;
            if (_invSelection < 0) _invSelection = 0;
            if (_invSelection > maxSel) _invSelection = maxSel;

            if (invEnter)
            {
                _equipSelectSlotIdx = _invSelection; _equipSelectCursor = 0;
                string key = slotKeys[_invSelection];
                if (_player.Equipment.ContainsKey(key))
                {
                    string currentId = _player.Equipment[key];
                    string filter = slotFilters[_invSelection];
                    int idx = 0;
                    foreach (var entry in _player.UnequippedEquipment)
                    {
                        var def = _galaxy.FindEquipment(entry.Id);
                        if (def != null && def.Slot == filter) { idx++; if (entry.Id == currentId) { _equipSelectCursor = idx; break; } }
                    }
                }
                _equipSelectMode = true;
            }
        }
    }

    private void HandleConsumableInput(bool invUp, bool invDown, KeyboardState keyboard)
    {
        if (invDown) _invSelection++;
        if (invUp) _invSelection--;
        int maxSel = Math.Max(0, _player.Consumables.Count - 1);
        if (_invSelection < 0) _invSelection = 0;
        if (_invSelection > maxSel) _invSelection = maxSel;

        if (JustPressed(keyboard, Keys.Enter) && _player.Consumables.Count > 0)
        {
            var entry = _player.Consumables[_invSelection];
            if (entry.Id == "energy_canister")
            {
                if (_player.Fuel >= _player.MaxFuel) { _invMsgText = "Fuel Already Full"; _invMsgTimer = 2f; }
                else { _player.UseEnergyCanister(); if (_invSelection >= _player.Consumables.Count) _invSelection = Math.Max(0, _player.Consumables.Count - 1); }
            }
            else if (entry.Id == "fuel_cell")
            {
                if (_player.Fuel >= _player.MaxFuel) { _invMsgText = "Fuel Already Full"; _invMsgTimer = 2f; }
                else { _player.UseFuelCell(); if (_invSelection >= _player.Consumables.Count) _invSelection = Math.Max(0, _player.Consumables.Count - 1); }
            }
        }
    }

    private void HandleQuestLogInput(KeyboardState keyboard, bool wasShowing)
    {
        bool qlDown = JustPressed(keyboard, Keys.Down) || JustPressed(keyboard, Keys.S);
        bool qlUp = JustPressed(keyboard, Keys.Up) || JustPressed(keyboard, Keys.W);
        bool qlQ = JustPressed(keyboard, Keys.Q);
        bool qlEsc = JustPressed(keyboard, Keys.Escape);

        if (qlDown) _questLogSelection++;
        if (qlUp) _questLogSelection--;
        int maxQ = Math.Max(0, _galaxy.ActiveQuests.Count - 1);
        if (_questLogSelection < 0) _questLogSelection = maxQ;
        if (_questLogSelection > maxQ) _questLogSelection = 0;
        if ((wasShowing && qlQ) || qlEsc) _showQuestLog = false;
    }

    private void HandlePauseInput(KeyboardState keyboard)
    {
        if (JustPressed(keyboard, Keys.Escape)) _currentMenu = MenuType.None;
        if (JustPressed(keyboard, Keys.Down) || JustPressed(keyboard, Keys.S)) _menuSelection++;
        if (JustPressed(keyboard, Keys.Up) || JustPressed(keyboard, Keys.W)) _menuSelection--;
        if (JustPressed(keyboard, Keys.Enter))
        {
            if (_menuSelection == 0) NewGame();
            else if (_menuSelection == 1) SaveGame();
            else if (_menuSelection == 2) LoadGame();
            else if (_menuSelection == 3) EnterTraining();
            else if (_menuSelection == 4) { _currentMenu = MenuType.Controls; _controlsScroll = 0; }
            else if (_menuSelection == 5) Exit();
        }
        int maxItem = 5;
        if (_menuSelection < 0) _menuSelection = maxItem;
        if (_menuSelection > maxItem) _menuSelection = 0;
    }

    private void HandleInput(float dt, KeyboardState keyboard, MouseState mouse)
    {
        if (_currentMenu == MenuType.None)
        {
            if (JustPressed(keyboard, Keys.Escape)) { _currentMenu = MenuType.Pause; _menuSelection = 0; return; }
        }
        else if (_currentMenu == MenuType.Pause)
        {
            HandlePauseInput(keyboard);
        }
        else if (_currentMenu == MenuType.Controls)
        {
            if (JustPressed(keyboard, Keys.Escape)) { _currentMenu = MenuType.Pause; _menuSelection = 4; }
            bool ctrlUp = JustPressed(keyboard, Keys.Up) || JustPressed(keyboard, Keys.W);
            bool ctrlDown = JustPressed(keyboard, Keys.Down) || JustPressed(keyboard, Keys.S);
            if (ctrlUp) _controlsScroll--;
            if (ctrlDown) _controlsScroll++;
            if (_controlsScroll < 0) _controlsScroll = 0;
            if (_controlsScroll > 16) _controlsScroll = 16;
        }
        else if (_currentMenu == MenuType.SystemInfo)
        {
            if (JustPressed(keyboard, Keys.Escape))
            { _currentMenu = MenuType.None; _menuSelection = 0; _priceScroll = 0; _systemInfoScroll = 0; }

            if (JustPressed(keyboard, Keys.Tab) && _menuSystem != null)
            {
                var quests = _galaxy.AvailableQuests.Where(q => q.GiverSystem == _menuSystem.Id).ToList();
                if (quests.Count > 0) { _currentMenu = MenuType.QuestBoard; _menuSelection = 0; }
            }

            if (JustPressed(keyboard, Keys.Down) || JustPressed(keyboard, Keys.S))
            {
                var currentSystem = _galaxy.CurrentSystem;
                if (_menuSystem != null && currentSystem != null && _menuSystem.Id != currentSystem.Id)
                {
                    int maxScroll = Math.Max(0, _galaxy.AllResources.Count - 5);
                    if (_priceScroll < maxScroll) _priceScroll++;
                    else if (_systemInfoMaxScroll > 0) _systemInfoScroll = Math.Min(_systemInfoScroll + 22, _systemInfoMaxScroll);
                }
                else if (_systemInfoMaxScroll > 0) _systemInfoScroll = Math.Min(_systemInfoScroll + 22, _systemInfoMaxScroll);
            }
            if (JustPressed(keyboard, Keys.Up) || JustPressed(keyboard, Keys.W))
            {
                if (_systemInfoScroll > 0) _systemInfoScroll = Math.Max(0, _systemInfoScroll - 22);
                else if (_priceScroll > 0) _priceScroll--;
            }
        }
        else if (_currentMenu == MenuType.UpgradeShop)
        {
            if (JustPressed(keyboard, Keys.Down) || JustPressed(keyboard, Keys.S)) _menuSelection++;
            if (JustPressed(keyboard, Keys.Up) || JustPressed(keyboard, Keys.W)) _menuSelection--;

            if (JustPressed(keyboard, Keys.Enter) && _menuSystem != null)
            {
                var upgrades = _galaxy.GetAvailableUpgradesForSystem(_menuSystem.Id, _player);
                if (_menuSelection >= 0 && _menuSelection < upgrades.Count)
                {
                    var upgrade = upgrades[_menuSelection];
                    if (_player.Credits >= upgrade.Cost)
                    { _player.Credits -= upgrade.Cost; _player.OwnedUpgrades.Add(upgrade.Id); SetStatus($"Purchased: {upgrade.Name}"); }
                    else SetStatus("Not enough credits!");
                }
            }

            if (JustPressed(keyboard, Keys.Escape))
            { _currentMenu = MenuType.SystemInfo; _priceScroll = 0; _systemInfoScroll = 0; }
        }
        else if (_currentMenu == MenuType.QuestBoard)
        {
            if (JustPressed(keyboard, Keys.Down) || JustPressed(keyboard, Keys.S)) _menuSelection++;
            if (JustPressed(keyboard, Keys.Up) || JustPressed(keyboard, Keys.W)) _menuSelection--;
            if (JustPressed(keyboard, Keys.Escape))
            { _currentMenu = MenuType.SystemInfo; _priceScroll = 0; _systemInfoScroll = 0; }
        }

        int maxItem = 0;
        if (_currentMenu == MenuType.Pause) maxItem = 5;
        else if (_currentMenu == MenuType.UpgradeShop && _menuSystem != null)
            maxItem = _galaxy.GetAvailableUpgradesForSystem(_menuSystem.Id, _player).Count - 1;
        else if (_currentMenu == MenuType.QuestBoard && _menuSystem != null)
            maxItem = _galaxy.AvailableQuests.Where(q => q.GiverSystem == _menuSystem.Id).Count() - 1;

        if (_menuSelection < 0) _menuSelection = maxItem;
        if (_menuSelection > maxItem) _menuSelection = 0;
    }

    private void SetStatus(string msg)
    {
        _statusMessage = msg;
        _statusTimer = 3f;
    }

    public bool IsSystemUnderAttack(string systemId) =>
        _attackManager.IsUnderAttack(systemId);

    public string? GetAttackAttacker(string systemId) =>
        _attackManager.GetAttackAttacker(systemId);

    public void RepelAttack(string systemId) =>
        _attackManager.RepelAttack(systemId);

    public void SetStatusMessage(string msg, float dur)
    {
        _statusMessage = msg;
        _statusTimer = dur;
    }

    public List<string> WordWrap(SpriteFont font, string text, float maxWidth) =>
        DrawUtils.WordWrap(font, text, maxWidth);

    private bool JustPressed(KeyboardState keyboard, Keys key) =>
        keyboard.IsKeyDown(key) && _prevKeyboard.IsKeyUp(key);

    private bool JustClicked(MouseState mouse) =>
        mouse.LeftButton == ButtonState.Pressed && _prevMouse.LeftButton == ButtonState.Released;

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

            _galaxyRenderer.DrawStarfield(_starfield, gameTime, offset, ScreenWidth, ScreenHeight);
            _galaxyRenderer.DrawConnectionLines(_galaxy, _routeManager, _player, gameTime,
                _attackManager.ActiveAttacks as Dictionary<string, List<AttackState>> ?? new(),
                offset, ScreenWidth, ScreenHeight, _isTraveling, _selectedConnectionIndex);
            _galaxyRenderer.DrawSystems(_galaxy, _player, gameTime,
                _attackManager.ActiveAttacks as Dictionary<string, List<AttackState>> ?? new(),
                offset, ScreenWidth, ScreenHeight);
            _galaxyRenderer.DrawShip(screenCenter, _player, _galaxy, _routeManager,
                _isTraveling, _travelStartPos, _travelEndPos, _selectedConnectionIndex);
            _galaxyRenderer.DrawHUD(_player, _routeManager, _galaxy, gameTime,
                _attackManager.ActiveAttacks as Dictionary<string, List<AttackState>> ?? new(),
                _galaxyPlayerPos, _aiManager?.UseLlm ?? false,
                _isTraveling, _travelDestId, _travelLerp, _selectedConnectionIndex,
                ScreenWidth, ScreenHeight);

            if (_currentMenu != MenuType.None)
            {
                if (_currentMenu == MenuType.Controls)
                    _menuRenderer.DrawControls(_menuSelection, _controlsScroll, ScreenWidth, ScreenHeight);
                else
                    _menuRenderer.DrawMenu(_currentMenu.ToString(), _menuSystem, _menuSelection,
                        _priceScroll, _systemInfoScroll, ref _systemInfoMaxScroll,
                        _player, _galaxy, _routeManager, gameTime,
                        _attackManager.ActiveAttacks as Dictionary<string, List<AttackState>> ?? new(),
                        ScreenWidth, ScreenHeight);
            }
        }
        else if (_viewMode == ViewMode.System)
        {
            if (_systemScene != null)
                _systemScene.Draw(_spriteBatch, _pixel, _font, _titleFont);

            if (_currentMenu != MenuType.None)
            {
                if (_currentMenu == MenuType.Controls)
                    _menuRenderer.DrawControls(_menuSelection, _controlsScroll, ScreenWidth, ScreenHeight);
                else
                    _menuRenderer.DrawMenu(_currentMenu.ToString(), _menuSystem, _menuSelection,
                        _priceScroll, _systemInfoScroll, ref _systemInfoMaxScroll,
                        _player, _galaxy, _routeManager, gameTime,
                        _attackManager?.ActiveAttacks as Dictionary<string, List<AttackState>> ?? new(),
                        ScreenWidth, ScreenHeight);
            }
        }

        _galaxyRenderer.DrawStatusMessage(_statusMessage, _statusTimer, ScreenWidth, ScreenHeight);

        if (_overlay == Overlay.SystemMap)
            _overlayRenderer.DrawSystemMapOverlay(gameTime, _player, _galaxy, _systemScene!, ScreenWidth, ScreenHeight);
        else if (_overlay == Overlay.GalaxyMap)
            _overlayRenderer.DrawGalaxyMapOverlay(gameTime, _player, _galaxy, _routeManager,
                _attackManager?.ActiveAttacks as Dictionary<string, List<AttackState>> ?? new(),
                ScreenWidth, ScreenHeight);
        else if (_overlay == Overlay.Inventory)
            _overlayRenderer.DrawInventoryOverlay(_player, _galaxy, gameTime,
                _inventoryTab, _invScroll, _invSelection, _equipSelectMode, _equipSelectSlotIdx,
                _equipSelectCursor, _invMsgText, _invMsgTimer, ScreenWidth, ScreenHeight);

        if (_showQuestLog)
            _overlayRenderer.DrawQuestLog(_galaxy, _questLogSelection, ScreenWidth, ScreenHeight);

        if (_showBroadcastDialog)
            _overlayRenderer.DrawBroadcastDialog(_pendingBroadcasts, gameTime,
                _broadcastScroll, _broadcastTab, ScreenWidth, ScreenHeight);

        if (_showQuestDialog && _currentQuestDialog != null)
            _overlayRenderer.DrawQuestDialog(_currentQuestDialog, ref _questDialogWrappedLines,
                ref _questDialogScroll, ScreenWidth, ScreenHeight);

        _spriteBatch.End();
        base.Draw(gameTime);
    }

    private void OnRouteBlocked(string a, string b)
    {
        var sysA = _galaxy.FindSystemById(a);
        var sysB = _galaxy.FindSystemById(b);
        string nameA = sysA?.Name ?? a;
        string nameB = sysB?.Name ?? b;
        _galaxy.NewsService?.PostBreakingNews(
            $"Trade Route Blocked: {nameA} - {nameB}",
            $"Imperial blockade has been established between {nameA} and {nameB}. Travel is advised against.",
            "Imperial Herald", "Trigor Empire");
    }

    private void OnRouteUnblocked(string a, string b)
    {
        var sysA = _galaxy.FindSystemById(a);
        var sysB = _galaxy.FindSystemById(b);
        string nameA = sysA?.Name ?? a;
        string nameB = sysB?.Name ?? b;
        _galaxy.NewsService?.PostBreakingNews(
            $"Route Reopened: {nameA} - {nameB}",
            $"Atlas Federation forces have successfully cleared the blockade between {nameA} and {nameB}. Normal travel resumes.",
            "Atlas Federation News Network", "Atlas Federation");
    }
}
