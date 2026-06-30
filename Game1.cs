using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using SpaceGame.Models;
using SpaceGame.Services;
using SpaceGame.Systems;
using System.Text.Json;
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
    private float _aiCaptureTimer;
    private float _federationAiTimer;
    private float _aiDefenseTimer;
    private int _initialIndependentCount;
    private float _aiDefenseQuestTimer;
    private LlmService? _llmService;
    private bool _useLlm;
    private bool _llmChecked;
    private readonly Dictionary<string, List<AttackState>> _activeAttacks = new();
    private const float AttackDuration = 60f;
    private readonly List<GalacticBroadcast> _pendingBroadcasts = new();
    private bool _showBroadcastDialog;
    private int _lastNotifiedBroadcastCount;
    private readonly Queue<QuestDialog> _questDialogQueue = new();
    private bool _showQuestDialog;
    private QuestDialog? _currentQuestDialog;
    private List<string> _questDialogWrappedLines = new();
    private int _questDialogScroll;
    private int _broadcastScroll;
    private int _broadcastTab; // 0=All, 1=Empire, 2=Federation

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

    // Training mode player state snapshot
    private struct PlayerSnapshot
    {
        public int Credits;
        public int Health;
        public int MaxHealth;
        public float Fuel;
        public float MaxFuel;
        public int CargoCapacity;
        public string? CurrentSystemId;
        public Vector2 Position;
        public Vector2 Velocity;
        public List<string> OwnedUpgrades;
        public List<string> CompletedQuests;
        public List<InventoryEntry> Resources;
        public List<InventoryEntry> QuestItems;
        public List<InventoryEntry> Consumables;
        public List<InventoryEntry> UnequippedEquipment;
        public Dictionary<string, string> Equipment;
        public float BaseMaxSpeed;
        public float BaseThrust;
        public float BaseRotationSpeed;
        public float Angle;
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

        // Snapshot player state to restore on exit
        _trainingPlayerSnapshot = new PlayerSnapshot
        {
            Credits = _player.Credits,
            Health = _player.Health,
            MaxHealth = _player.MaxHealth,
            Fuel = _player.Fuel,
            MaxFuel = _player.MaxFuel,
            CargoCapacity = _player.CargoCapacity,
            Angle = _player.Angle,
            CurrentSystemId = _player.CurrentSystemId,
            Position = _player.Position,
            Velocity = _player.Velocity,
            OwnedUpgrades = new List<string>(_player.OwnedUpgrades),
            CompletedQuests = new List<string>(_player.CompletedQuests),
            Resources = _player.Resources.Select(r => new InventoryEntry { Id = r.Id, Quantity = r.Quantity }).ToList(),
            QuestItems = _player.QuestItems.Select(r => new InventoryEntry { Id = r.Id, Quantity = r.Quantity }).ToList(),
            Consumables = _player.Consumables.Select(r => new InventoryEntry { Id = r.Id, Quantity = r.Quantity }).ToList(),
            UnequippedEquipment = _player.UnequippedEquipment.Select(r => new InventoryEntry { Id = r.Id, Quantity = r.Quantity }).ToList(),
            Equipment = new Dictionary<string, string>(_player.Equipment),
            BaseMaxSpeed = _player.BaseMaxSpeed,
            BaseThrust = _player.BaseThrust,
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

        // Restore player state from pre-training snapshot
        if (_trainingPlayerSnapshot.HasValue)
        {
            var snap = _trainingPlayerSnapshot.Value;
            _player.Credits = snap.Credits;
            _player.Health = snap.Health;
            _player.MaxHealth = snap.MaxHealth;
            _player.Fuel = snap.Fuel;
            _player.MaxFuel = snap.MaxFuel;
            _player.CargoCapacity = snap.CargoCapacity;
            _player.Angle = snap.Angle;
            _player.CurrentSystemId = snap.CurrentSystemId;
            _player.Position = snap.Position;
            _player.Velocity = snap.Velocity;
            _player.OwnedUpgrades = snap.OwnedUpgrades;
            _player.CompletedQuests = snap.CompletedQuests;
            _player.Resources = snap.Resources;
            _player.QuestItems = snap.QuestItems;
            _player.Consumables = snap.Consumables;
            _player.UnequippedEquipment = snap.UnequippedEquipment;
            _player.Equipment = snap.Equipment;
            _player.BaseMaxSpeed = snap.BaseMaxSpeed;
            _player.BaseThrust = snap.BaseThrust;
            _player.BaseRotationSpeed = snap.BaseRotationSpeed;
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
            PlayerPosition = _player.Position,
            PlayerVelocity = _player.Velocity,
            PlayerAngle = _player.Angle,
            Credits = _player.Credits,
            Health = _player.Health,
            MaxHealth = _player.MaxHealth,
            Fuel = _player.Fuel,
            MaxFuel = _player.MaxFuel,
            CargoCapacity = _player.CargoCapacity,
            OwnedUpgrades = new List<string>(_player.OwnedUpgrades),
            CompletedQuests = new List<string>(_player.CompletedQuests),
            CurrentSystemId = _player.CurrentSystemId,
            Resources = _player.Resources.Select(r => new InventoryEntry { Id = r.Id, Quantity = r.Quantity }).ToList(),
            QuestItems = _player.QuestItems.Select(r => new InventoryEntry { Id = r.Id, Quantity = r.Quantity }).ToList(),
            Consumables = _player.Consumables.Select(r => new InventoryEntry { Id = r.Id, Quantity = r.Quantity }).ToList(),
            UnequippedEquipment = _player.UnequippedEquipment.Select(r => new InventoryEntry { Id = r.Id, Quantity = r.Quantity }).ToList(),
            Equipment = new Dictionary<string, string>(_player.Equipment),
            BaseMaxSpeed = _player.BaseMaxSpeed,
            BaseThrust = _player.BaseThrust,
            BaseRotationSpeed = _player.BaseRotationSpeed,
            Systems = _galaxy.Systems.Select(sys => new SystemSaveData
            {
                Id = sys.Id,
                Hostility = sys.Hostility,
                Faction = sys.Faction,
                DefenseLevel = sys.Station?.DefenseLevel ?? 0,
                StationAngle = sys.Station?.Angle ?? 0f
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
            ActiveAttacks = _activeAttacks.ToDictionary(kv => kv.Key, kv => kv.Value.Select(s => new AttackStateSave
            {
                Timer = s.Timer,
                Attacker = s.Attacker
            }).ToList()),
            GalaxyPlayerPos = _galaxyPlayerPos,
            GalaxyPlayerVel = _galaxyPlayerVel,
            AiTickTimer = _aiTickTimer,
            AiCaptureTimer = _aiCaptureTimer,
            FederationAiTimer = _federationAiTimer,
            AiDefenseTimer = _aiDefenseTimer,
            InitialIndependentCount = _initialIndependentCount,
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

            // Restore player
            _player.Position = data.PlayerPosition;
            _player.Velocity = data.PlayerVelocity;
            _player.Angle = data.PlayerAngle;
            _player.Credits = data.Credits;
            _player.Health = data.Health;
            _player.MaxHealth = data.MaxHealth;
            _player.Fuel = data.Fuel;
            _player.MaxFuel = data.MaxFuel;
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
            _player.BaseMaxSpeed = data.BaseMaxSpeed;
            _player.BaseThrust = data.BaseThrust;
            _player.BaseRotationSpeed = data.BaseRotationSpeed;

            // Restore galaxy systems (mutable fields)
            foreach (var sd in data.Systems)
            {
                var sys = _galaxy.FindSystemById(sd.Id);
                if (sys == null) continue;
                sys.Hostility = sd.Hostility;
                sys.Faction = sd.Faction;
                if (sys.Station != null)
                {
                    sys.Station.DefenseLevel = sd.DefenseLevel;
                    sys.Station.Angle = sd.StationAngle;
                }
            }

            // Restore quests
            _galaxy.ActiveQuests.Clear();
            _galaxy.ActiveQuests.AddRange(data.ActiveQuests);
            _galaxy.AvailableQuests.Clear();
            _galaxy.AvailableQuests.AddRange(data.AvailableQuests);
            _galaxy.AllQuests.Clear();
            _galaxy.AllQuests.AddRange(data.AllQuests);

            // Restore navigation references
            _galaxy.CurrentSystem = !string.IsNullOrEmpty(data.CurrentSystemIdRef) ? _galaxy.FindSystemById(data.CurrentSystemIdRef) : null;
            _galaxy.TargetSystem = !string.IsNullOrEmpty(data.TargetSystemId) ? _galaxy.FindSystemById(data.TargetSystemId) : null;

            // Restore route manager
            _routeManager.SetBlockedRoutes(data.BlockedRoutes);
            _routeManager.Difficulty = (AiDifficulty)data.AiDifficulty;

            // Restore economy
            _galaxy.Economy.SetMarkets(data.Markets.ToDictionary(kv => kv.Key, kv => new SystemMarketState
            {
                Stocks = new Dictionary<string, float>(kv.Value.Stocks),
                Demands = new Dictionary<string, float>(kv.Value.Demands),
                ProductionRates = new Dictionary<string, float>(kv.Value.ProductionRates)
            }));

            // Restore news
            _galaxy.NewsService.SetArticles(data.NewsArticles);

            // Restore attacks and timers
            _activeAttacks.Clear();
            foreach (var kv in data.ActiveAttacks)
                _activeAttacks[kv.Key] = kv.Value.Select(s => new AttackState { Timer = s.Timer, Attacker = s.Attacker }).ToList();

            _galaxyPlayerPos = data.GalaxyPlayerPos;
            _galaxyPlayerVel = data.GalaxyPlayerVel;
            _aiTickTimer = data.AiTickTimer;
            _aiCaptureTimer = data.AiCaptureTimer;
            _federationAiTimer = data.FederationAiTimer;
            _aiDefenseTimer = data.AiDefenseTimer;
            _initialIndependentCount = data.InitialIndependentCount;

            // Return to galaxy view
            _viewMode = ViewMode.Galaxy;
            _currentMenu = MenuType.None;

            // Recreate system view if player was in one
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
            Id = q.Id,
            Name = q.Name,
            Description = q.Description,
            QuestType = q.QuestType,
            NextQuestId = q.NextQuestId,
            ObjectiveType = q.ObjectiveType,
            TargetSystem = q.TargetSystem,
            TargetItem = q.TargetItem,
            TargetCount = q.TargetCount,
            RewardCredits = q.RewardCredits,
            RewardUpgrade = q.RewardUpgrade,
            RewardEquipment = q.RewardEquipment,
            GiverSystem = q.GiverSystem,
            RewardDefenseSystem = q.RewardDefenseSystem,
            RequiredResources = new Dictionary<string, int>(q.RequiredResources)
        };
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
        _player.Consumables.Clear();
        _player.UnequippedEquipment.Clear();
        _player.Equipment.Clear();
        _player.CurrentSystemId = null;

        _galaxy.LoadData();
        _player.RecalculateStats(_galaxy.AllEquipment);
        _routeManager.SetGalaxy(_galaxy);

        // Set initial Empire defense levels
        var trigor = _galaxy.FindSystemById("trigor");
        if (trigor?.Station != null) trigor.Station.DefenseLevel = 5;
        var trigorAlpha = _galaxy.FindSystemById("trigor_alpha");
        if (trigorAlpha?.Station != null) trigorAlpha.Station.DefenseLevel = 3;
        var trigorBeta = _galaxy.FindSystemById("trigor_beta");
        if (trigorBeta?.Station != null) trigorBeta.Station.DefenseLevel = 2;
        var trigorGamma = _galaxy.FindSystemById("trigor_gamma");
        if (trigorGamma?.Station != null) trigorGamma.Station.DefenseLevel = 1;
        _aiTickTimer = 8f;
        _aiCaptureTimer = 30f;
        _federationAiTimer = 30f;
        _aiDefenseTimer = 60f;
        _initialIndependentCount = _galaxy.Systems.Count(s => s.Hostility < 3 && s.Faction != "Atlas Federation");
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
        _selectedConnectionIndex = 0;
        _isTraveling = false;
        _travelDestId = null;
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
        _routeManager.RouteBlocked += OnRouteBlocked;
        _routeManager.RouteUnblocked += OnRouteUnblocked;
        _llmService = new LlmService();

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

        _pendingBroadcasts.Clear();
        _pendingBroadcasts.Add(new GalacticBroadcast
        {
            Faction = "Trigor Empire",
            CommanderName = "Emperor Cyrus III",
            CommanderTitle = "Emperor of the Trigor Empire",
            Message = "I, Cyrus III, Emperor of the Trigor Empire, do hereby declare that the age of Federation complacency has reached its end. The slow rot of bureaucratic indifference that starved billions shall be purged by fire and steel. A new order rises -- swift, absolute, and eternal.",
            Timestamp = 0f
        });
        _pendingBroadcasts.Add(new GalacticBroadcast
        {
            Faction = "Atlas Federation",
            CommanderName = "Prime Minister Ezara Loban",
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

        // Overlay toggle (T = system map, G = galaxy map, I = inventory, Q = quest log)
        bool tHit = keyboard.IsKeyDown(Keys.T) && _prevKeyboard.IsKeyUp(Keys.T);
        bool gHit = keyboard.IsKeyDown(Keys.G) && _prevKeyboard.IsKeyUp(Keys.G);
        bool iHit = keyboard.IsKeyDown(Keys.I) && _prevKeyboard.IsKeyUp(Keys.I);
        bool qHit = keyboard.IsKeyDown(Keys.Q) && _prevKeyboard.IsKeyUp(Keys.Q);

        if (tHit)
        {
            _showQuestLog = false;
            if (_overlay == Overlay.SystemMap)
                _overlay = Overlay.None;
            else if (_galaxy.CurrentSystem != null)
                _overlay = Overlay.SystemMap;
        }
        if (gHit)
        {
            _showQuestLog = false;
            _overlay = _overlay == Overlay.GalaxyMap ? Overlay.None : Overlay.GalaxyMap;
        }

        if (iHit)
        {
            _showQuestLog = false;
            if (_overlay == Overlay.Inventory)
                _overlay = Overlay.None;
            else
            {
                _overlay = Overlay.Inventory;
                _inventoryTab = 0;
                _invScroll = 0;
                _invSelection = 0;
                _equipSelectMode = false;
            }
        }

        bool wasShowingQuestLog = _showQuestLog;
        if (qHit)
        {
            if (_overlay == Overlay.None && _currentMenu == MenuType.None)
                _showQuestLog = !_showQuestLog;
        }

        if (_showQuestDialog)
        {
            if (JustPressed(keyboard, Keys.Enter) || JustPressed(keyboard, Keys.Escape) || JustPressed(keyboard, Keys.Space))
            {
                if (_questDialogQueue.Count > 0)
                {
                    _currentQuestDialog = _questDialogQueue.Dequeue();
                    _questDialogScroll = 0;
                }
                else
                {
                    _showQuestDialog = false;
                    _currentQuestDialog = null;
                }
            }
            if (JustPressed(keyboard, Keys.Up))
                _questDialogScroll = Math.Max(0, _questDialogScroll - 1);
            if (JustPressed(keyboard, Keys.Down))
                _questDialogScroll = Math.Min(_questDialogWrappedLines.Count - 1, _questDialogScroll + 1);
            _prevKeyboard = keyboard;
            _prevMouse = mouse;
            base.Update(gameTime);
            return;
        }

        // B key opens broadcast dialog (any view)
        bool wasShowing = _showBroadcastDialog;
        if (JustPressed(keyboard, Keys.B) && _pendingBroadcasts.Count > 0 && !_showBroadcastDialog)
            _showBroadcastDialog = true;

        if (_showBroadcastDialog)
        {
            if (JustPressed(keyboard, Keys.Escape) || (JustPressed(keyboard, Keys.B) && wasShowing))
            {
                _showBroadcastDialog = false;
                _broadcastScroll = 0;
            }
            if (JustPressed(keyboard, Keys.Up))
                _broadcastScroll = Math.Max(0, _broadcastScroll - 22);
            if (JustPressed(keyboard, Keys.Down))
                _broadcastScroll += 22;
            if (JustPressed(keyboard, Keys.Tab))
            {
                if (keyboard.IsKeyDown(Keys.LeftShift) || keyboard.IsKeyDown(Keys.RightShift))
                    _broadcastTab = _broadcastTab == 0 ? 2 : _broadcastTab - 1;
                else
                    _broadcastTab = (_broadcastTab + 1) % 3;
            }
            _prevKeyboard = keyboard;
            _prevMouse = mouse;
            base.Update(gameTime);
            return;
        }

        if (_overlay != Overlay.None)
        {
            bool ovEsc = keyboard.IsKeyDown(Keys.Escape) && _prevKeyboard.IsKeyUp(Keys.Escape);
                if (ovEsc)
                {
                    if (_inventoryTab == 2 && _equipSelectMode)
                    {
                        _equipSelectMode = false;
                    }
                    else
                    {
                        _overlay = Overlay.None;
                    }
                }

            if (_overlay == Overlay.Inventory)
            {
                bool invLeft = keyboard.IsKeyDown(Keys.Left) && _prevKeyboard.IsKeyUp(Keys.Left);
                bool invRight = (keyboard.IsKeyDown(Keys.Right) && _prevKeyboard.IsKeyUp(Keys.Right)) ||
                                (keyboard.IsKeyDown(Keys.E) && _prevKeyboard.IsKeyUp(Keys.E));
                bool invDown = (keyboard.IsKeyDown(Keys.Down) && _prevKeyboard.IsKeyUp(Keys.Down)) ||
                               (keyboard.IsKeyDown(Keys.S) && _prevKeyboard.IsKeyUp(Keys.S));
                bool invUp = (keyboard.IsKeyDown(Keys.Up) && _prevKeyboard.IsKeyUp(Keys.Up)) ||
                             (keyboard.IsKeyDown(Keys.W) && _prevKeyboard.IsKeyUp(Keys.W));
                if (invLeft)
                {
                    _inventoryTab = (_inventoryTab - 1 + 4) % 4;
                    _invSelection = 0;
                    _equipSelectMode = false;
                }
                if (invRight)
                {
                    _inventoryTab = (_inventoryTab + 1) % 4;
                    _invSelection = 0;
                    _equipSelectMode = false;
                }
                if (_inventoryTab == 2)
                {
                    string[] slotKeys = { "weapon1", "weapon2", "shield", "engine", "utility1", "utility2" };
                    string[] slotFilters = { "weapon", "weapon", "shield", "engine", "utility", "utility" };
                    bool invEnter = (keyboard.IsKeyDown(Keys.Enter) && _prevKeyboard.IsKeyUp(Keys.Enter));

                    if (_equipSelectMode)
                    {
                        int slotIdx = _equipSelectSlotIdx;
                        string filter = slotFilters[slotIdx];
                        string key = slotKeys[slotIdx];

                        // Build options list: "None" + unequipped items matching slot
                        var options = new List<string> { "" }; // "" = None
                        foreach (var entry in _player.UnequippedEquipment)
                        {
                            var def = _galaxy.FindEquipment(entry.Id);
                            if (def != null && def.Slot == filter)
                                options.Add(entry.Id);
                        }

                        if (invDown)
                            _equipSelectCursor++;
                        if (invUp)
                            _equipSelectCursor--;
                        if (_equipSelectCursor < 0) _equipSelectCursor = 0;
                        if (_equipSelectCursor >= options.Count) _equipSelectCursor = options.Count - 1;

                        if (invEnter)
                        {
                            string selected = options[_equipSelectCursor];
                            if (selected == "")
                            {
                                // Unequip
                                _player.Equipment.Remove(key);
                            }
                            else
                            {
                                // Equip selected item
                                // First unequip current if any
                                if (_player.Equipment.ContainsKey(key))
                                {
                                    var oldId = _player.Equipment[key];
                                    _player.Equipment.Remove(key);
                                    var existing = _player.UnequippedEquipment.FirstOrDefault(e => e.Id == oldId);
                                    if (existing != null)
                                        existing.Quantity++;
                                    else
                                        _player.UnequippedEquipment.Add(new InventoryEntry { Id = oldId, Quantity = 1 });
                                }
                                // Remove from unequipped
                                var eqEntry = _player.UnequippedEquipment.FirstOrDefault(e => e.Id == selected);
                                if (eqEntry != null)
                                {
                                    _player.Equipment[key] = selected;
                                    eqEntry.Quantity--;
                                    if (eqEntry.Quantity <= 0)
                                        _player.UnequippedEquipment.RemoveAll(e => e.Id == selected);
                                }
                            }
                            _player.RecalculateStats(_galaxy.AllEquipment);
                            _equipSelectMode = false;
                        }
                    }
                    else
                    {
                        if (invDown)
                            _invSelection++;
                        if (invUp)
                            _invSelection--;
                        int maxSel = slotKeys.Length - 1;
                        if (_invSelection < 0) _invSelection = 0;
                        if (_invSelection > maxSel) _invSelection = maxSel;

                        if (invEnter)
                        {
                            _equipSelectSlotIdx = _invSelection;
                            _equipSelectCursor = 0;

                            string key = slotKeys[_invSelection];
                            // Start cursor at current equipment (if any) + 1 for "None" offset
                            if (_player.Equipment.ContainsKey(key))
                            {
                                string currentId = _player.Equipment[key];
                                string filter = slotFilters[_invSelection];
                                int idx = 0;
                                foreach (var entry in _player.UnequippedEquipment)
                                {
                                    var def = _galaxy.FindEquipment(entry.Id);
                                    if (def != null && def.Slot == filter)
                                    {
                                        idx++;
                                        if (entry.Id == currentId)
                                        {
                                            _equipSelectCursor = idx;
                                            break;
                                        }
                                    }
                                }
                            }
                            _equipSelectMode = true;
                        }
                    }
                }
                else if (_inventoryTab == 3)
                {
                    if (invDown)
                        _invSelection++;
                    if (invUp)
                        _invSelection--;
                    int maxSel = Math.Max(0, _player.Consumables.Count - 1);
                    if (_invSelection < 0) _invSelection = 0;
                    if (_invSelection > maxSel) _invSelection = maxSel;

                    bool invEnter = (keyboard.IsKeyDown(Keys.Enter) && _prevKeyboard.IsKeyUp(Keys.Enter));
                    if (invEnter && _player.Consumables.Count > 0)
                    {
                        var entry = _player.Consumables[_invSelection];
                        if (entry.Id == "energy_canister")
                        {
                            if (_player.Fuel >= _player.MaxFuel)
                            {
                                _invMsgText = "Fuel Already Full";
                                _invMsgTimer = 2f;
                            }
                            else
                            {
                                _player.UseEnergyCanister();
                                if (_invSelection >= _player.Consumables.Count)
                                    _invSelection = Math.Max(0, _player.Consumables.Count - 1);
                            }
                        }
                        else if (entry.Id == "fuel_cell")
                        {
                            if (_player.Fuel >= _player.MaxFuel)
                            {
                                _invMsgText = "Fuel Already Full";
                                _invMsgTimer = 2f;
                            }
                            else
                            {
                                _player.UseFuelCell();
                                if (_invSelection >= _player.Consumables.Count)
                                    _invSelection = Math.Max(0, _player.Consumables.Count - 1);
                            }
                        }
                    }
                }
                else
                {
                    if (invDown)
                        _invScroll++;
                    if (invUp)
                        _invScroll--;
                    if (_invScroll < 0) _invScroll = 0;
                }
            }
            else if (_overlay == Overlay.GalaxyMap)
            {
                if (mouse.LeftButton == ButtonState.Pressed && _prevMouse.LeftButton == ButtonState.Released)
                {
                    float minX = float.MaxValue, maxX = float.MinValue;
                    float minY = float.MaxValue, maxY = float.MinValue;
                    foreach (var s in _galaxy.Systems)
                    {
                        if (s.X < minX) minX = s.X;
                        if (s.X > maxX) maxX = s.X;
                        if (s.Y < minY) minY = s.Y;
                        if (s.Y > maxY) maxY = s.Y;
                    }
                    float rangeX = maxX - minX + 400;
                    float rangeY = maxY - minY + 400;
                    float mapAreaW = ScreenWidth - 80;
                    float mapAreaH = ScreenHeight - 120;
                    float scale = MathF.Min(mapAreaW / rangeX, mapAreaH / rangeY) * 0.9f;
                    float cx = (minX + maxX) / 2f;
                    float cy = (minY + maxY) / 2f;
                    float originX = ScreenWidth / 2f;
                    float originY = ScreenHeight / 2f + 10;
                    float worldX = (mouse.X - originX) / scale + cx;
                    float worldY = (mouse.Y - originY) / scale + cy;
                    var clicked = _galaxy.FindSystemAtPosition(
                        new Vector2(worldX, worldY), 80f);
                    if (clicked != null)
                    {
                        _menuSystem = clicked;
                        _currentMenu = MenuType.SystemInfo;
                        _menuSelection = 0;
                        _priceScroll = 0;
                        _systemInfoScroll = 0;
                        _overlay = Overlay.None;
                    }
                }
            }

            _prevKeyboard = keyboard;
            _prevMouse = mouse;
            base.Update(gameTime);
            return;
        }

        // Quest log overlay (non-pausing)
        if (_showQuestLog)
        {
            bool qlDown = (keyboard.IsKeyDown(Keys.Down) && _prevKeyboard.IsKeyUp(Keys.Down)) ||
                          (keyboard.IsKeyDown(Keys.S) && _prevKeyboard.IsKeyUp(Keys.S));
            bool qlUp = (keyboard.IsKeyDown(Keys.Up) && _prevKeyboard.IsKeyUp(Keys.Up)) ||
                        (keyboard.IsKeyDown(Keys.W) && _prevKeyboard.IsKeyUp(Keys.W));
            bool qlQ = keyboard.IsKeyDown(Keys.Q) && _prevKeyboard.IsKeyUp(Keys.Q);
            bool qlEsc = keyboard.IsKeyDown(Keys.Escape) && _prevKeyboard.IsKeyUp(Keys.Escape);

            if (qlDown)
                _questLogSelection++;
            if (qlUp)
                _questLogSelection--;
            int maxQ = Math.Max(0, _galaxy.ActiveQuests.Count - 1);
            if (_questLogSelection < 0) _questLogSelection = maxQ;
            if (_questLogSelection > maxQ) _questLogSelection = 0;
            if ((wasShowingQuestLog && qlQ) || qlEsc)
                _showQuestLog = false;
        }

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
                            foreach (var q in _galaxy.ActiveQuests)
                                ShowQuestDialogs(q, "on_enter_system");
                            SetStatus($"Arrived at {destSys.Name}");
                            _galaxyPlayerPos = _player.Position;
                            _galaxyPlayerVel = Vector2.Zero;
                        }
                        _isTraveling = false;
                        _travelDestId = null;
                        _selectedConnectionIndex = 0;
                    }
                    else
                    {
                        float t = _travelLerp;
                        _player.Position = new Vector2(
                            _travelStartPos.X + (_travelEndPos.X - _travelStartPos.X) * t,
                            _travelStartPos.Y + (_travelEndPos.Y - _travelStartPos.Y) * t
                        );
                        _player.Velocity = new Vector2(
                            _travelEndPos.X - _travelStartPos.X,
                            _travelEndPos.Y - _travelStartPos.Y
                        ).Normalized() * 200f;
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
                                float dist = Vector2.Distance(
                                    _player.Position, new Vector2(s.X, s.Y));
                                float fuelCost = MathF.Max(25f, dist * 0.015f);
                                return _player.Fuel >= fuelCost &&
                                       (_player.Fuel - fuelCost) > _player.MaxFuel / 3;
                            })
                            .Select(s => s.Id)
                            .ToList();

                        if (reachableConns.Count > 0)
                        {
                            if (_selectedConnectionIndex >= reachableConns.Count)
                                _selectedConnectionIndex = 0;

                            bool upHit = (keyboard.IsKeyDown(Keys.Up) && _prevKeyboard.IsKeyUp(Keys.Up)) ||
                                         (keyboard.IsKeyDown(Keys.W) && _prevKeyboard.IsKeyUp(Keys.W));
                            bool downHit = (keyboard.IsKeyDown(Keys.Down) && _prevKeyboard.IsKeyUp(Keys.Down)) ||
                                           (keyboard.IsKeyDown(Keys.S) && _prevKeyboard.IsKeyUp(Keys.S));
                            if (upHit) _selectedConnectionIndex = (_selectedConnectionIndex - 1 + reachableConns.Count) % reachableConns.Count;
                            if (downHit) _selectedConnectionIndex = (_selectedConnectionIndex + 1) % reachableConns.Count;

                            if (keyboard.IsKeyDown(Keys.Enter) && _prevKeyboard.IsKeyUp(Keys.Enter))
                            {
                                string targetId = reachableConns[_selectedConnectionIndex];
                                var targetSys = _galaxy.FindSystemById(targetId);
                                if (targetSys != null)
                                {
                                    float dist = Vector2.Distance(
                                        _player.Position,
                                        new Vector2(targetSys.X, targetSys.Y));
                                    float fuelCost = MathF.Max(25f, dist * 0.015f);
                                    _player.Fuel -= fuelCost;
                                    _travelDestId = targetId;
                                    _travelStartPos = _player.Position;
                                    _travelEndPos = new Vector2(targetSys.X, targetSys.Y);
                                    _travelLerp = 0f;
                                    _isTraveling = true;
                                    _player.Velocity = Vector2.Zero;
                                    SetStatus($"Traveling to {targetSys.Name}...");
                                }
                            }
                        }

                        // Fuel exchange — available even when no routes are reachable
                        if (_player.Health > _player.MaxHealth / 4 &&
                            keyboard.IsKeyDown(Keys.Y) && _prevKeyboard.IsKeyUp(Keys.Y))
                        {
                            _player.Health -= _player.MaxHealth / 4;
                            _player.Fuel += _player.MaxFuel / 4;
                            SetStatus($"Exchanged HP for fuel. Fuel: {_player.Fuel:F0}");
                        }

                        if (keyboard.IsKeyDown(Keys.E) && _prevKeyboard.IsKeyUp(Keys.E))
                        {
                            _galaxyPlayerPos = _player.Position;
                            _galaxyPlayerVel = Vector2.Zero;
                            _systemScene.EnterSystem(currentSys, this);
                            _viewMode = ViewMode.System;
                        }
                    }
                }

                // Click on system to show info
                if (mouse.LeftButton == ButtonState.Pressed && _prevMouse.LeftButton == ButtonState.Released)
                {
                    Vector2 offset = new Vector2(ScreenWidth / 2f, ScreenHeight / 2f) - _player.Position;
                    var worldPos = new Vector2(mouse.X - offset.X, mouse.Y - offset.Y);
                    var clicked = _galaxy.FindSystemAtPosition(worldPos, 80f);
                    if (clicked != null)
                    {
                        _menuSystem = clicked;
                        _currentMenu = MenuType.SystemInfo;
                        _menuSelection = 0;
                        _priceScroll = 0;
                        _systemInfoScroll = 0;
                    }
                }
            }

            _prevKeyboard = keyboard;
            _prevMouse = mouse;

            // AI commander logic
            if (_player.CurrentSystemId != null)
            {
                if (!_llmChecked)
                {
                    _llmChecked = true;
                    _ = CheckLlmAsync();
                }

                if (_useLlm)
                {
                    _aiTickTimer -= dt;
                    if (_aiTickTimer <= 0f)
                    {
                        _aiTickTimer = 15f;
                        _ = LlmAiTickAsync();
                    }
                }
                else
                {
                    // Rule-based route blocking (enemy-system routes only, filtered by RouteManager)
                    _aiTickTimer -= dt;
                    if (_aiTickTimer <= 0f)
                    {
                        _aiTickTimer = 4f;
                        _routeManager.AiTick(_player.CurrentSystemId);
                    }

                    // Rule-based station capture
                    _aiCaptureTimer -= dt;
                    if (_aiCaptureTimer <= 0f)
                    {
                        _aiCaptureTimer = 30f;
                        AiCaptureTick();
                    }

                    // Rule-based Federation counter-attack
                    _federationAiTimer -= dt;
                    if (_federationAiTimer <= 0f)
                    {
                        _federationAiTimer = 30f;
                        FederationAiTick();
                    }

                    // AI station defense building
                    _aiDefenseTimer -= dt;
                    if (_aiDefenseTimer <= 0f)
                    {
                        _aiDefenseTimer = 60f;
                        AiDefenseTick();
                    }

                    // AI defense fetch quest generation
                    _aiDefenseQuestTimer -= dt;
                    if (_aiDefenseQuestTimer <= 0f)
                    {
                        _aiDefenseQuestTimer = 120f;
                        GenerateDefenseQuest();
                    }
                }
            }

            // Attack timer — pauses while player is inside the attacked system
            bool inAttackedSystem = _viewMode == ViewMode.System && _galaxy.CurrentSystem != null &&
                _activeAttacks.ContainsKey(_galaxy.CurrentSystem.Id);
            if (!inAttackedSystem)
            {
                foreach (var id in _activeAttacks.Keys.ToList())
                {
                    var list = _activeAttacks[id];
                    // Decrement the first attack's timer; all share the same window
                    if (list.Count > 0)
                    {
                        var first = list[0];
                        first.Timer -= dt;
                        list[0] = first;
                        if (first.Timer <= 0f)
                            ResolveAttack(id);
                    }
                }
            }

            if (_invMsgTimer > 0f)
            {
                _invMsgTimer -= dt;
                if (_invMsgTimer <= 0f)
                    _invMsgText = "";
            }

            _starfield.Update(dt, _player.Velocity);
        }

        // Global save/load shortcuts
        bool f5Hit = keyboard.IsKeyDown(Keys.F5) && _prevKeyboard.IsKeyUp(Keys.F5);
        bool f9Hit = keyboard.IsKeyDown(Keys.F9) && _prevKeyboard.IsKeyUp(Keys.F9);
        if (f5Hit && _currentMenu == MenuType.None)
            SaveGame();
        if (f9Hit && _currentMenu == MenuType.None && File.Exists(SavePath))
            LoadGame();

        if (_viewMode == ViewMode.System)
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
                    else if (_menuSelection == 1) // Save Game
                        SaveGame();
                    else if (_menuSelection == 2) // Load Game
                        LoadGame();
                    else if (_menuSelection == 3) // Training Mode
                        EnterTraining();
                    else if (_menuSelection == 4) // Controls
                    {
                        _currentMenu = MenuType.Controls;
                        _controlsScroll = 0;
                    }
                    else if (_menuSelection == 5) // Quit
                        Exit();
                }

                int maxItem = 5;
                if (_menuSelection < 0) _menuSelection = maxItem;
                if (_menuSelection > maxItem) _menuSelection = 0;
            }
            else if (_currentMenu == MenuType.Controls)
            {
                if (JustPressed(keyboard, Keys.Escape))
                {
                    _currentMenu = MenuType.Pause;
                    _menuSelection = 4;
                }
            }
            else
            {
                // View-agnostic menu key handling for menus opened from system view
                if (_currentMenu == MenuType.SystemInfo)
                {
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
                    {
                        _currentMenu = MenuType.None;
                        _menuSelection = 0;
                    }
                    else if (!_systemScene.Docked && !_systemScene.TrainingMode)
                    {
                        _currentMenu = MenuType.Pause;
                        _menuSelection = 0;
                    }
                }

                if (_overlay == Overlay.None && _currentMenu == MenuType.None)
                {
                    _systemScene.Update(dt, keyboard, mouse);
                }
            }
            _prevKeyboard = keyboard;
            _prevMouse = mouse;
        }

        if (_statusTimer > 0) _statusTimer -= dt;

        if (_statusTimer <= 0f && _pendingBroadcasts.Count > _lastNotifiedBroadcastCount && !_showBroadcastDialog)
        {
            _lastNotifiedBroadcastCount = _pendingBroadcasts.Count;
            _statusMessage = "Galactic Broadcast Received! Press B to view";
            _statusTimer = 6f;
        }

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
                else if (_menuSelection == 1) // Save Game
                    SaveGame();
                else if (_menuSelection == 2) // Load Game
                    LoadGame();
                else if (_menuSelection == 3) // Training Mode
                    EnterTraining();
                else if (_menuSelection == 4) // Controls
                {
                    _currentMenu = MenuType.Controls;
                    _controlsScroll = 0;
                }
                else if (_menuSelection == 5) // Quit
                    Exit();
            }
        }
        else if (_currentMenu == MenuType.Controls)
        {
            if (JustPressed(keyboard, Keys.Escape))
            {
                _currentMenu = MenuType.Pause;
                _menuSelection = 4;
            }
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
            {
                _currentMenu = MenuType.None;
                _menuSelection = 0;
                _priceScroll = 0;
                _systemInfoScroll = 0;
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
            {
                _currentMenu = MenuType.SystemInfo;
                _priceScroll = 0;
                _systemInfoScroll = 0;
            }
        }
        else if (_currentMenu == MenuType.QuestBoard)
        {
            if (JustPressed(keyboard, Keys.Down) || JustPressed(keyboard, Keys.S))
                _menuSelection++;
            if (JustPressed(keyboard, Keys.Up) || JustPressed(keyboard, Keys.W))
                _menuSelection--;

            if (JustPressed(keyboard, Keys.Escape))
            {
                _currentMenu = MenuType.SystemInfo;
                _priceScroll = 0;
                _systemInfoScroll = 0;
            }
        }

        int maxItem = 0;
        if (_currentMenu == MenuType.Pause) maxItem = 5;
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
        else if (_viewMode == ViewMode.System)
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

        if (_showQuestLog)
            DrawQuestLog();

        if (_showBroadcastDialog)
        {
            DrawBroadcastDialog();
        }

        if (_showQuestDialog && _currentQuestDialog != null)
            DrawQuestDialog();

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

                bool blocked = _routeManager.IsBlocked(sys.Id, connId);
                if (blocked)
                {
                    float pulse = MathF.Sin((float)_gameTime.TotalGameTime.TotalSeconds * 2f) * 0.2f + 0.6f;
                    DrawLine(x1, y1, x2, y2, new Color(200, 30, 30) * pulse);
                }
                else
                {
                    bool isCurrentRoute = (_player.CurrentSystemId == sys.Id || _player.CurrentSystemId == connId);
                    float lineAlpha = isCurrentRoute ? 0.4f : 0.15f;
                    DrawLine(x1, y1, x2, y2, new Color(60, 80, 120) * lineAlpha);
                }
            }
        }

        // Highlight selected route on map
        if (!_isTraveling && _player.CurrentSystemId != null)
        {
            var currentSys = _galaxy.FindSystemById(_player.CurrentSystemId);
            if (currentSys != null)
            {
                var openConns = currentSys.Connections
                    .Where(id => !_routeManager.IsBlocked(_player.CurrentSystemId, id))
                    .ToList();
                if (_selectedConnectionIndex < openConns.Count)
                {
                    var target = _galaxy.FindSystemById(openConns[_selectedConnectionIndex]);
                    if (target != null)
                    {
                        float x1 = currentSys.X + offset.X;
                        float y1 = currentSys.Y + offset.Y;
                        float x2 = target.X + offset.X;
                        float y2 = target.Y + offset.Y;
                        float pulse = MathF.Sin((float)_gameTime.TotalGameTime.TotalSeconds * 3f) * 0.2f + 0.8f;
                        DrawLine(x1, y1, x2, y2, Color.Cyan * pulse);
                        DrawLine(x1, y1, x2, y2, Color.White * (pulse * 0.3f));
                    }
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

            Color color = GetFactionColor(sys.Faction);
            float t = (float)_gameTime.TotalGameTime.TotalSeconds;
            float pulse = MathF.Sin(t * 1.5f + sys.X * 0.01f) * 0.12f + 1f;
            float drawRadius = sys.Radius * pulse;

            var label = sys.Name;
            var labelSize = _font.MeasureString(label);

            // Outer glow - big and bright
            for (int i = 5; i >= 0; i--)
            {
                float r = drawRadius + i * 10f;
                float alpha = 0.02f + i * 0.06f;
                FillCircle(sx, sy, r, color * MathF.Min(alpha, 0.5f));
            }

            // Core - filled circle
            FillCircle(sx, sy, drawRadius * 0.7f, color * 0.9f);

            // Core edge
            DrawCircle(sx, sy, drawRadius * 0.7f, color);

            // Player's current system highlight
            if (_player.CurrentSystemId == sys.Id)
            {
                float ringAlpha = MathF.Sin((float)_gameTime.TotalGameTime.TotalSeconds * 2f) * 0.2f + 0.6f;
                DrawCircle(sx, sy, drawRadius + 8f, Color.Cyan * ringAlpha);
                DrawCircle(sx, sy, drawRadius + 14f, Color.Cyan * ringAlpha * 0.3f);
            }

            // Quest target indicator
            bool isQuestTarget = false;
            foreach (var q in _galaxy.ActiveQuests)
            {
                if (q.ObjectiveType == "travel" && q.TargetSystem == sys.Id) { isQuestTarget = true; break; }
            }
            if (isQuestTarget && _player.CurrentSystemId != sys.Id)
            {
                float pulse2 = MathF.Sin((float)_gameTime.TotalGameTime.TotalSeconds * 2f) * 0.3f + 0.7f;
                DrawCircle(sx, sy, drawRadius + 18f, Color.Gold * pulse2);
                DrawCircle(sx, sy, drawRadius + 24f, Color.Gold * pulse2 * 0.3f);
            }

            // Under-attack indicator
            if (_activeAttacks.ContainsKey(sys.Id) && _player.CurrentSystemId != sys.Id)
            {
                float pulse3 = MathF.Sin((float)_gameTime.TotalGameTime.TotalSeconds * 3f) * 0.25f + 0.75f;
                Color atkColor = new Color(255, 120, 0) * pulse3;
                DrawCircle(sx, sy, drawRadius + 14f, atkColor);
                DrawCircle(sx, sy, drawRadius + 22f, atkColor * 0.3f);
                var atkSz = _font.MeasureString("UNDER ATTACK");
                DrawSpacedText(_font, "UNDER ATTACK",
                    new Microsoft.Xna.Framework.Vector2(sx - atkSz.X / 2f, sy - drawRadius - atkSz.Y - 10),
                    atkColor);
            }

            // Distance hint for non-current systems
            float dist = Vector2.Distance(_player.Position, new Vector2(sys.X, sys.Y));
            if (dist < 400f && _player.CurrentSystemId != sys.Id)
            {
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
            bool isCurrent = _player.CurrentSystemId == sys.Id;
            byte bg = (byte)(isCurrent ? 60 : 20);
            _spriteBatch.Draw(_pixel,
                new Microsoft.Xna.Framework.Rectangle((int)labelX - 4, (int)labelY - 2,
                    (int)labelSize.X + 8, (int)labelSize.Y + 4),
                new Color(0, 0, 0, (int)bg));
            DrawSpacedText(_font, label,
                new Microsoft.Xna.Framework.Vector2(labelX, labelY),
                isCurrent ? Color.Cyan : Color.White * 0.7f);
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
        float angle;
        if (_isTraveling)
        {
            float dx = _travelEndPos.X - _travelStartPos.X;
            float dy = _travelEndPos.Y - _travelStartPos.Y;
            angle = MathF.Atan2(dy, dx);
        }
        else if (_player.CurrentSystemId != null)
        {
            var currentSys = _galaxy.FindSystemById(_player.CurrentSystemId);
            if (currentSys != null)
            {
                var connections = currentSys.Connections
                    .Where(id => !_routeManager.IsBlocked(_player.CurrentSystemId, id))
                    .ToList();
                if (_selectedConnectionIndex < connections.Count)
                {
                    var target = _galaxy.FindSystemById(connections[_selectedConnectionIndex]);
                    if (target != null)
                    {
                        float dx = target.X - currentSys.X;
                        float dy = target.Y - currentSys.Y;
                        angle = MathF.Atan2(dy, dx);
                        _player.Angle = angle;
                    }
                    else { angle = _player.Angle; }
                }
                else { angle = _player.Angle; }
            }
            else { angle = _player.Angle; }
        }
        else
        {
            angle = _player.Angle;
        }
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
        DrawSpacedText(_font, $"Fuel: {_player.Fuel:F0}/{_player.MaxFuel}  HP: {_player.Health}/{_player.MaxHealth}",
            new Microsoft.Xna.Framework.Vector2(10, 190), Color.Gray * 0.6f);

        // AI status
        string diffStr = _routeManager.Difficulty.ToString();
        int blockedCount = _routeManager.CountBlocked;
        int maxBlocked = _routeManager.MaxBlocked;
        Color aiColor = blockedCount > 0 ? new Color(255, 150, 100) : Color.Gray * 0.6f;
        DrawSpacedText(_font, $"AI [{diffStr}]  Blockades: {blockedCount}/{maxBlocked}",
            new Microsoft.Xna.Framework.Vector2(10, 170), aiColor);

        // LLM commander notification
        if (_useLlm)
        {
            string llmLabel = "LLM Commander Active";
            var llmSz = _font.MeasureString(llmLabel);
            float pulse = MathF.Sin((float)_gameTime.TotalGameTime.TotalSeconds * 2f) * 0.15f + 0.85f;
            DrawSpacedText(_font, llmLabel,
                new Microsoft.Xna.Framework.Vector2((ScreenWidth - llmSz.X) / 2f, 6),
                new Color(100, 200, 255) * pulse);
        }

        if (_isTraveling)
        {
            // Travel in progress
            var destSys = _galaxy.FindSystemById(_travelDestId ?? "");
            string destName = destSys?.Name ?? "unknown";
            float pct = _travelLerp * 100f;
            DrawSpacedText(_font, $"Traveling to {destName}... {pct:F0}%",
                new Microsoft.Xna.Framework.Vector2(10, 30), Color.Cyan);
        }
        else if (_player.CurrentSystemId != null)
        {
            var currentSys = _galaxy.FindSystemById(_player.CurrentSystemId);
            if (currentSys != null)
            {
                string info = $"Docked at: {currentSys.Name} [{currentSys.Faction}]";
                DrawSpacedText(_font, info, new Microsoft.Xna.Framework.Vector2(10, 30), Color.Cyan);
                DrawSpacedText(_font, "[E] Enter System",
                    new Microsoft.Xna.Framework.Vector2(10, 50), Color.Gray * 0.7f);

                // Route selection list
                var connections = currentSys.Connections
                    .Select(id => _galaxy.FindSystemById(id))
                    .Where(s => s != null)
                    .Cast<StarSystemData>()
                    .ToList();

                float routeY = 210;
                DrawSpacedText(_font, "--- Connections ---",
                    new Microsoft.Xna.Framework.Vector2(10, routeY), Color.Gold);
                routeY += 22;

                int selectableIdx = 0;
                foreach (var conn in connections)
                {
                    bool blocked = _routeManager.IsBlocked(currentSys.Id, conn.Id);
                    float dist = Vector2.Distance(
                        new Vector2(currentSys.X, currentSys.Y),
                        new Vector2(conn.X, conn.Y));
                    float fuelCost = MathF.Max(25f, dist * 0.015f);
                    bool inRange = _player.Fuel >= fuelCost &&
                                   (_player.Fuel - fuelCost) > _player.MaxFuel / 3;
                    bool selected = !blocked && inRange && selectableIdx == _selectedConnectionIndex;

                    Color c;
                    string prefix;
                    string suffix = "";
                    bool connUnderAttack = _activeAttacks.ContainsKey(conn.Id);
                    if (blocked)
                    {
                        c = Color.Red * 0.5f;
                        prefix = "  ";
                        suffix = "  BLOCKED";
                    }
                    else if (connUnderAttack)
                    {
                        c = new Color(255, 150, 50) * 0.9f;
                        prefix = "  ";
                        suffix = "  UNDER ATTACK";
                    }
                    else if (!inRange)
                    {
                        c = Color.DimGray * 0.7f;
                        prefix = "  ";
                        suffix = "  OUT OF RANGE";
                    }
                    else if (selected)
                    {
                        c = Color.Yellow;
                        prefix = "> ";
                    }
                    else
                    {
                        c = Color.White * 0.8f;
                        prefix = "  ";
                    }
                    string label = $"{prefix}{conn.Name}  [{(int)dist}u]{suffix}";
                    DrawSpacedText(_font, label,
                        new Microsoft.Xna.Framework.Vector2(10, routeY), c);
                    routeY += 18;

                    if (!blocked && inRange) selectableIdx++;
                }
            }
        }

        // Active quests
        float y = _isTraveling ? 55 : 80;
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
        string controls;
        if (_isTraveling)
            controls = "Traveling...";
        else if (_player.CurrentSystemId != null)
            controls = "Up/Down: Select Route | Enter: Travel | E: Enter System | Q: Quest Log | ESC: Pause";
        else
            controls = "Q: Quest Log | ESC: Pause";
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
            Color c = GetFactionColor(sys.Faction);
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

            int panelH = _currentMenu == MenuType.SystemInfo ? 640 : 500;
        int panelW = 700;
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

            string[] options = { "New Game", "Save Game", "Load Game", "Training Mode", "Controls", "Quit" };
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
            int scrollOffset = -_systemInfoScroll;

            _spriteBatch.End();
            var prevRect = _spriteBatch.GraphicsDevice.ScissorRectangle;
            _spriteBatch.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, SamplerState.PointClamp,
                null, new RasterizerState { ScissorTestEnable = true });
            _spriteBatch.GraphicsDevice.ScissorRectangle = new Microsoft.Xna.Framework.Rectangle(
                px + 1, py + 1, panelW - 2, panelH - 2);

            // System name
            DrawSpacedText(_titleFont, sys.Name, new Microsoft.Xna.Framework.Vector2(textX, textY + scrollOffset), ParseColor(sys.Color));
            textY += 48;

            // Description (left side)
            float descWidth = panelW - 60;
            var descLines = WordWrap(_font, sys.Description, descWidth);
            foreach (var line in descLines)
            {
                DrawSpacedText(_font, line, new Microsoft.Xna.Framework.Vector2(textX, textY + scrollOffset), Color.White * 0.8f);
                textY += 20;
            }
            textY += 8;

            // Faction
            DrawSpacedText(_font, $"Faction: {sys.Faction ?? "None"}", new Microsoft.Xna.Framework.Vector2(textX, textY + scrollOffset), Color.Cyan);
            textY += 20;
            DrawSpacedText(_font, $"Hostility Level: {sys.Hostility}/10", new Microsoft.Xna.Framework.Vector2(textX, textY + scrollOffset),
                sys.Hostility > 3 ? Color.OrangeRed : Color.LimeGreen);
            textY += 20;

            if (_activeAttacks.ContainsKey(sys.Id))
            {
                float pulse = MathF.Sin((float)_gameTime.TotalGameTime.TotalSeconds * 3f) * 0.2f + 0.8f;
                DrawSpacedText(_font, "*** UNDER ATTACK ***", new Microsoft.Xna.Framework.Vector2(textX, textY + scrollOffset),
                    new Color(255, 120, 0) * pulse);
                textY += 22;
            }

            if (sys.Services.Count > 0)
            {
                DrawSpacedText(_font, "Services: " + string.Join(", ", sys.Services), new Microsoft.Xna.Framework.Vector2(textX, textY + scrollOffset), Color.Yellow * 0.9f);
                textY += 26;
            }

            // Mini system map (bottom-right corner of panel) — fixed position, NOT scrolled
            float mapCx = px + panelW - 105;
            float mapCy = py + panelH - 105;
            float mapR = 85;
            DrawRect(mapCx - mapR - 3, mapCy - mapR - 3, mapR * 2 + 6, mapR * 2 + 6, new Color(60, 60, 100));

            float miniSysR = 0f;
            foreach (var p in sys.Planets)
                if (p.OrbitRadius > miniSysR) miniSysR = p.OrbitRadius;
            if (sys.Station != null && sys.Station.OrbitRadius > miniSysR)
                miniSysR = sys.Station.OrbitRadius;
            miniSysR = MathF.Max(MathF.Max(miniSysR, 1f) * 1.1f, sys.StarRadius * 2f);
            float mScale = mapR / miniSysR;

            float mStarR = MathF.Max(sys.StarRadius * mScale, 3f);
            FillCircle(mapCx, mapCy, mStarR, ParseColor(sys.Color) * 0.5f);
            DrawCircle(mapCx, mapCy, mStarR, ParseColor(sys.Color));

            foreach (var p in sys.Planets)
                DrawCircle(mapCx, mapCy, p.OrbitRadius * mScale, new Color(60, 60, 80, 100));
            if (sys.Station != null)
                DrawCircle(mapCx, mapCy, sys.Station.OrbitRadius * mScale, new Color(60, 80, 100, 100));

            float ang = 0;
            foreach (var p in sys.Planets)
            {
                float ppx = mapCx + MathF.Cos(ang) * p.OrbitRadius * mScale;
                float ppy = mapCy + MathF.Sin(ang) * p.OrbitRadius * mScale;
                FillCircle(ppx, ppy, MathF.Max(p.Radius * mScale, 2f), ParseColor(p.Color));
                ang += 1.5f;
            }

            if (sys.Station != null)
            {
                float stAngle = sys.Station.Angle;
                float stx = mapCx + MathF.Cos(stAngle) * sys.Station.OrbitRadius * mScale;
                float sty = mapCy + MathF.Sin(stAngle) * sys.Station.OrbitRadius * mScale;
                Color stCol = sys.Hostility >= 3 ? new Color(200, 60, 60) : Color.LightBlue;
                FillCircle(stx, sty, MathF.Max(sys.Station.Radius * mScale, 2f), stCol);
            }

            string mapLabel = "System";
            var mapSz = _font.MeasureString(mapLabel);
            DrawSpacedText(_font, mapLabel,
                new Microsoft.Xna.Framework.Vector2(mapCx - mapSz.X / 2f, mapCy + mapR + 8), Color.Gray * 0.7f);

            textY += 12;

            // Resource price comparison
            var currentSys = _galaxy.CurrentSystem;
            if (currentSys != null && currentSys.Id != sys.Id)
            {
                float scale = 1.3f;
                int pageSize = 5;

                DrawSpacedText(_font, "--- Market Prices vs Current ---",
                    new Microsoft.Xna.Framework.Vector2(textX, textY + scrollOffset), Color.Gold, scale);
                textY += (int)(26 * scale);

                float col1 = textX;
                float col2 = textX + 155;
                float col3 = textX + 365;
                float col4 = textX + 575;
                float tableW = 620;
                int lineH = (int)(20 * scale);
                int headerSize = (int)(18 * scale);

                // Header row
                DrawSpacedText(_font, "Resource",
                    new Microsoft.Xna.Framework.Vector2(col1, textY + scrollOffset), Color.Gray * 0.7f, scale);
                DrawSpacedText(_font, "Selected (B/S)",
                    new Microsoft.Xna.Framework.Vector2(col2 + 18, textY + scrollOffset), Color.Gray * 0.7f, scale);
                DrawSpacedText(_font, "Current (B/S)",
                    new Microsoft.Xna.Framework.Vector2(col3 + 18, textY + scrollOffset), Color.Gray * 0.7f, scale);
                DrawSpacedText(_font, "Action",
                    new Microsoft.Xna.Framework.Vector2(col4 + 18, textY + scrollOffset), Color.Gray * 0.7f, scale);
                float headerY = textY;
                textY += headerSize;

                // Header separator
                float hdrLine = textY + scrollOffset - 4;
                DrawLine(col1, hdrLine, col1 + tableW, hdrLine, new Color(80, 80, 120) * 0.5f);

                // Column separators
                float sepBottom = textY + pageSize * lineH + 4;
                DrawLine(col2 - 2, headerY + scrollOffset - 2, col2 - 2, sepBottom + scrollOffset, new Color(80, 80, 120) * 0.3f);
                DrawLine(col3 - 2, headerY + scrollOffset - 2, col3 - 2, sepBottom + scrollOffset, new Color(80, 80, 120) * 0.3f);
                DrawLine(col4 - 2, headerY + scrollOffset - 2, col4 - 2, sepBottom + scrollOffset, new Color(80, 80, 120) * 0.3f);

                var allRes = _galaxy.AllResources;
                int total = allRes.Count;
                int maxScroll = Math.Max(0, total - pageSize);
                if (_priceScroll > maxScroll) _priceScroll = maxScroll;

                for (int i = _priceScroll; i < _priceScroll + pageSize && i < total; i++)
                {
                    var res = allRes[i];
                    int hereBuy = _galaxy.Economy.GetBuyPrice(sys.Id, res.Id);
                    int hereSell = _galaxy.Economy.GetSellPrice(sys.Id, res.Id);
                    int curBuy = _galaxy.Economy.GetBuyPrice(currentSys.Id, res.Id);
                    int curSell = _galaxy.Economy.GetSellPrice(currentSys.Id, res.Id);

                    DrawSpacedText(_font, $"[{res.Symbol}] {res.Name}",
                        new Microsoft.Xna.Framework.Vector2(col1, textY + scrollOffset), Color.White * 0.7f, scale);

                    string price = $"{hereBuy}/{hereSell}";
                    float pw = _font.MeasureString(price).X * scale;
                    DrawSpacedText(_font, price,
                        new Microsoft.Xna.Framework.Vector2(col2 + (182 - pw) / 2f, textY + scrollOffset), Color.White * 0.7f, scale);

                    string curPrice = $"{curBuy}/{curSell}";
                    float cw = _font.MeasureString(curPrice).X * scale;
                    DrawSpacedText(_font, curPrice,
                        new Microsoft.Xna.Framework.Vector2(col3 + (182 - cw) / 2f, textY + scrollOffset), Color.White * 0.7f, scale);

                    Color c = Color.White * 0.75f;
                    string hint = "";
                    if (hereBuy < curBuy) { hint = "BUY"; c = Color.LightGreen; }
                    else if (hereSell > curSell) { hint = "SELL"; c = Color.Orange; }

                    if (hint != "")
                    {
                        float hw = _font.MeasureString(hint).X * scale;
                        DrawSpacedText(_font, hint,
                            new Microsoft.Xna.Framework.Vector2(col4 + (40 - hw) / 2f, textY + scrollOffset), c, scale);
                    }

                    // Row separator
                    float rowLine = textY + lineH;
                    DrawLine(col1, rowLine + scrollOffset, col1 + tableW, rowLine + scrollOffset, new Color(60, 60, 90) * 0.3f);

                    textY += lineH;
                }

                // Draw empty rows to fill page
                int drawn = Math.Min(pageSize, total - _priceScroll);
                for (int i = drawn; i < pageSize; i++)
                {
                    float rowLine = textY + lineH;
                    DrawLine(col1, rowLine + scrollOffset, col1 + tableW, rowLine + scrollOffset, new Color(60, 60, 90) * 0.3f);
                    textY += lineH;
                }

                // Scroll bar
                if (maxScroll > 0)
                {
                    float scrollBarX = col1 + tableW + 6;
                    float scrollBarH = pageSize * lineH;
                    float thumbH = scrollBarH * pageSize / total;
                    float thumbY = textY - scrollBarH + _priceScroll * (scrollBarH - thumbH) / maxScroll;

                    DrawLine(scrollBarX, textY - scrollBarH + scrollOffset, scrollBarX, textY + scrollOffset, new Color(60, 60, 100) * 0.5f);
                    DrawRect(scrollBarX - 2, thumbY + scrollOffset, 4, thumbH, new Color(120, 120, 180) * 0.7f);
                }

                textY += 8;
            }
            else if (currentSys != null && currentSys.Id == sys.Id)
            {
                DrawSpacedText(_font, "-- Current system --",
                    new Microsoft.Xna.Framework.Vector2(textX, textY + scrollOffset), Color.Gray * 0.6f);
                textY += 22;
            }

            _spriteBatch.End();
            _spriteBatch.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend);
            _spriteBatch.GraphicsDevice.ScissorRectangle = prevRect;

            // Compute max scroll so shortcuts are always visible
            int shortcutAreaY = py + panelH - 50;
            _systemInfoMaxScroll = Math.Max(0, textY - shortcutAreaY);
            _systemInfoScroll = Math.Min(_systemInfoScroll, _systemInfoMaxScroll);

            // Action items at fixed bottom position
            float sx = px + 20;
            float sy = shortcutAreaY;
            bool hasQuests = _galaxy.AvailableQuests.Any(q => q.GiverSystem == sys.Id);
            if (hasQuests)
            {
                DrawSpacedText(_font, "[Tab] View Quests",
                    new Microsoft.Xna.Framework.Vector2(sx, sy), Color.Gray);
                sy += 22;
            }
            DrawSpacedText(_font, "[ESC] Close",
                new Microsoft.Xna.Framework.Vector2(sx, sy), Color.Gray);
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
            DrawSpacedText(_font, "[ESC] Back",
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
                "  Up / W        Select route up",
                "  Down / S      Select route down",
                "  Enter         Travel along selected route",
                "  E             Enter system",
                "",
                "System View (Flight)",
                "  W / Up        Thrust forward",
                "  S / Down      Thrust backward",
                "  A / Left      Rotate left",
                "  D / Right     Rotate right",
                "  Shift         Boost",
                "  E             Dock / Undock / Interact",
                "  F             Fire primary weapon",
                "  Space         Auto-fire (hold)",
                "  1 Key         Weapon 1 (Cannon)",
                "  2 Key         Weapon 2 (Laser)",
                "  3 Key         Weapon 3 (Missile)",
                "  Tab           Target nearest enemy",
                "  R             Repair",
                "  C             Use Energy Canister",
                "  O             Cycle combat target",
                "  N             Target nearest enemy",
                "",
                "System View (Docked)",
                "  Up / W        Navigate up",
                "  Down / S      Navigate down",
                "  Enter         Buy / Select",
                "  Back          Sell",
                "  U             Upgrade shop",
                "  ESC           Undock",
                "",
                "Training Mode",
                "  F1            Spawn menu",
                "  Up / W        Navigate spawn menu",
                "  Down / S      Navigate spawn menu",
                "  Enter         Spawn selected ship",
                "  Y             Sacrifice health",
                "  ESC           Pause / Resume",
                "",
                "General",
                "  ESC           Pause menu / Back",
                "  T             System map",
                "  G             Galaxy map",
                "  I             Inventory",
                "  Q             Quest log",
                "  F5            Quick save",
                "  F9            Quick load",
            };

            float lx = (ScreenWidth - 500) / 2f;
            float ly = 80 - _controlsScroll * 20;
            foreach (var line in lines)
            {
                bool isHeader = line.Length > 0 && line[0] != ' ';
                Color c = isHeader ? new Color(255, 200, 100) : Color.Gray * 0.9f;
                if (ly + 20 > 20 && ly < ScreenHeight)
                    DrawSpacedText(_font, line,
                        new Microsoft.Xna.Framework.Vector2(lx, ly), c);
                ly += isHeader ? 24 : 20;
            }

            string scrollHint = _controlsScroll > 0 ? " [Up] Scroll up" : "";
            DrawSpacedText(_font, $"[ESC] Back{scrollHint}  [Down] Scroll down",
                new Microsoft.Xna.Framework.Vector2(ScreenWidth - 280, ScreenHeight - 30),
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
            Color stCol = sys.Hostility >= 3 ? new Color(200, 60, 60) : Color.LightBlue;
            FillCircle(stx, sty, stR, stCol);

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

        // Asteroids on system map (small white squares)
        if (_systemScene != null)
        {
            float aSize = 2f;
            foreach (var ast in _systemScene.Asteroids)
            {
                float ax = cx + ast.Position.X * scale;
                float ay = cy + ast.Position.Y * scale;
                _spriteBatch.Draw(_pixel, new Microsoft.Xna.Framework.Rectangle((int)(ax - aSize), (int)(ay - aSize), (int)(aSize * 2), (int)(aSize * 2)), Color.White * 0.5f);
            }
        }

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

            Color color = GetFactionColor(sys.Faction);
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

            // Under-attack indicator on galaxy map overlay
            if (_activeAttacks.ContainsKey(sys.Id))
            {
                float pulse3 = MathF.Sin((float)_gameTime.TotalGameTime.TotalSeconds * 3f) * 0.25f + 0.75f;
                Color atkColor = new Color(255, 120, 0) * pulse3;
                DrawCircle(sx, sy, MathF.Max(sys.Radius * scale * 0.6f, 3f) + 6f, atkColor);
                DrawCircle(sx, sy, MathF.Max(sys.Radius * scale * 0.6f, 3f) + 10f, atkColor * 0.3f);
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

    private void DrawQuestLog()
    {
        _spriteBatch.Draw(_pixel, new Microsoft.Xna.Framework.Rectangle(0, 0, ScreenWidth, ScreenHeight),
            new Color(0, 0, 0, 180));

        int panelW = 640;
        int panelH = ScreenHeight - 80;
        int px = (ScreenWidth - panelW) / 2;
        int py = 40;

        _spriteBatch.Draw(_pixel, new Microsoft.Xna.Framework.Rectangle(px, py, panelW, panelH),
            new Color(10, 10, 30, 230));
        int textX = px + 20;
        int textY = py + 20;

        DrawSpacedText(_titleFont, "Quest Log",
            new Microsoft.Xna.Framework.Vector2(textX, textY), Color.Cyan);
        textY += 40;

        var active = _galaxy.ActiveQuests;
        if (active.Count == 0)
        {
            DrawSpacedText(_font, "No active quests.",
                new Microsoft.Xna.Framework.Vector2(textX, textY), Color.Gray);
        }
        else
        {
            for (int i = 0; i < active.Count; i++)
            {
                var q = active[i];
                bool selected = i == _questLogSelection;
                string prefix = selected ? "> " : "  ";
                Color c = selected ? Color.Yellow : Color.White;

                DrawSpacedText(_font, $"{prefix}{q.Name}",
                    new Microsoft.Xna.Framework.Vector2(textX, textY), c);
                textY += 22;

                DrawSpacedText(_font, $"  {q.Description}",
                    new Microsoft.Xna.Framework.Vector2(textX, textY), c * 0.6f);
                textY += 20;

                string location = q.ObjectiveType == "travel"
                    ? $"Target: {q.TargetSystem}"
                    : $"Search {q.TargetSystem} for {q.TargetItem}";
                bool objectiveMet = _galaxy.IsQuestObjectiveMet(q, _player);
                if (objectiveMet)
                    DrawSpacedText(_font, $"  {location} - Objective Complete!",
                        new Microsoft.Xna.Framework.Vector2(textX, textY), Color.Lime);
                else
                    DrawSpacedText(_font, $"  {location}",
                        new Microsoft.Xna.Framework.Vector2(textX, textY), c * 0.4f);
                textY += 26;
            }
        }

        DrawSpacedText(_font, "[Up/Dn] Scroll  [Q/ESC] Close",
            new Microsoft.Xna.Framework.Vector2(textX, panelH + py - 30), Color.Gray * 0.6f);
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
        string[] tabs = { "Quest Items", "Resources", "Equipment", "Consumables" };
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
        else if (_inventoryTab == 3)
            DrawInventoryConsumables(new Microsoft.Xna.Framework.Vector2(textX, textY), px + panelW - 20);

        // Inventory message overlay (e.g. "Fuel Already Full")
        if (_invMsgTimer > 0f && !string.IsNullOrEmpty(_invMsgText))
        {
            float msgW = 300f;
            float msgH = 40f;
            float msgX = (ScreenWidth - msgW) / 2f;
            float msgY = py + panelH + 30f;
            _spriteBatch.Draw(_pixel, new Microsoft.Xna.Framework.Rectangle((int)msgX, (int)msgY, (int)msgW, (int)msgH),
                new Color(0, 0, 0, 200));
            var msgLabelSz = _font.MeasureString(_invMsgText);
            DrawSpacedText(_font, _invMsgText,
                new Microsoft.Xna.Framework.Vector2(msgX + (msgW - msgLabelSz.X) / 2f, msgY + (msgH - msgLabelSz.Y) / 2f),
                Color.Lime);
        }
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
        string[] slotFilters = { "weapon", "weapon", "shield", "engine", "utility", "utility" };

        if (_equipSelectMode && _equipSelectSlotIdx >= 0 && _equipSelectSlotIdx < slotKeys.Length)
        {
            // Draw selection overlay for the chosen slot
            string slotLabel = slotLabels[_equipSelectSlotIdx];
            DrawSpacedText(_font, $"Select equipment for {slotLabel}:",
                new Microsoft.Xna.Framework.Vector2(pos.X, textY), Color.Cyan);
            textY += 28;

            string filter = slotFilters[_equipSelectSlotIdx];
            string key = slotKeys[_equipSelectSlotIdx];
            string? currentId = _player.Equipment.ContainsKey(key) ? _player.Equipment[key] : null;

            // Build options
            var optLabels = new List<string> { "None" };
            var optIds = new List<string> { "" };
            foreach (var entry in _player.UnequippedEquipment)
            {
                var def = _galaxy.FindEquipment(entry.Id);
                if (def != null && def.Slot == filter)
                {
                    optLabels.Add($"{def.Name} x{entry.Quantity}");
                    optIds.Add(entry.Id);
                }
            }

            // Highlight current equipment
            float cx = pos.X + 30;
            for (int i = 0; i < optLabels.Count; i++)
            {
                bool isSelected = i == _equipSelectCursor;
                bool isCurrent = (!string.IsNullOrEmpty(currentId) && optIds[i] == currentId);
                string prefix = isSelected ? "> " : "  ";
                Color c = isSelected ? Color.Yellow : (isCurrent ? Color.Lime : Color.White);
                DrawSpacedText(_font, $"{prefix}{optLabels[i]}",
                    new Microsoft.Xna.Framework.Vector2(cx, textY), c);
                if (isCurrent && !isSelected)
                {
                    DrawSpacedText(_font, "(equipped)",
                        new Microsoft.Xna.Framework.Vector2(cx + 180, textY), Color.Gray * 0.5f);
                }
                textY += 24;
            }

            textY += 8;
            DrawSpacedText(_font, "[Enter] Select  [ESC] Cancel",
                new Microsoft.Xna.Framework.Vector2(pos.X, textY), Color.Gray * 0.5f);
            return;
        }

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

            bool selected = i == _invSelection;
            string prefix = selected ? "> " : "  ";
            Color slotColor = selected ? Color.Yellow : Color.Gray;
            DrawSpacedText(_font, $"{prefix}{slotLabel}:",
                new Microsoft.Xna.Framework.Vector2(pos.X, textY), slotColor);
            string itemLabel = filled ? equipName : "--- empty ---";
            Color itemColor = filled ? Color.Lime : Color.Gray * 0.5f;
            DrawSpacedText(_font, itemLabel,
                new Microsoft.Xna.Framework.Vector2(pos.X + 130, textY), itemColor);

            if (selected)
            {
                DrawSpacedText(_font, "[Enter] Select",
                    new Microsoft.Xna.Framework.Vector2(rightX - 130, textY), Color.Orange * 0.7f);
            }

            textY += 26;
        }

        // Unequipped equipment section
        textY += 8;
        DrawSpacedText(_font, "Unequipped:",
            new Microsoft.Xna.Framework.Vector2(pos.X, textY), Color.Gray * 0.7f);
        textY += 18;
        if (_player.UnequippedEquipment.Count == 0)
        {
            DrawSpacedText(_font, "  None",
                new Microsoft.Xna.Framework.Vector2(pos.X, textY), Color.Gray * 0.4f);
            textY += 20;
        }
        else
        {
            int maxEq = Math.Min(_player.UnequippedEquipment.Count, 5);
            for (int i = 0; i < maxEq; i++)
            {
                var entry = _player.UnequippedEquipment[i];
                var def = _galaxy.FindEquipment(entry.Id);
                string name = def?.Name ?? entry.Id;
                DrawSpacedText(_font, $"  {name} x{entry.Quantity}",
                    new Microsoft.Xna.Framework.Vector2(pos.X, textY), Color.Gray * 0.6f);
                textY += 18;
            }
        }

        textY += 4;

        // Hint
        DrawSpacedText(_font, "[Q/E] Switch tab  |  [I] or [ESC] Close",
            new Microsoft.Xna.Framework.Vector2(pos.X, textY), Color.Gray * 0.5f);
    }

    private void DrawInventoryConsumables(Microsoft.Xna.Framework.Vector2 pos, float rightX)
    {
        float textY = pos.Y;
        if (_player.Consumables.Count == 0)
        {
            DrawSpacedText(_font, "No consumables.",
                new Microsoft.Xna.Framework.Vector2(pos.X, textY), Color.Gray);
            return;
        }

        for (int i = 0; i < _player.Consumables.Count; i++)
        {
            var entry = _player.Consumables[i];
            var def = _galaxy.FindConsumable(entry.Id);
            string name = def?.Name ?? entry.Id;
            bool selected = i == _invSelection;
            string prefix = selected ? "> " : "  ";
            Color itemColor = selected ? Color.Yellow : Color.White;
            string label = $"{prefix}{name} x{entry.Quantity}";
            DrawSpacedText(_font, label,
                new Microsoft.Xna.Framework.Vector2(pos.X, textY), itemColor);

            if (selected && def != null)
            {
                string hint = "[Enter] Use";
                var hintSz = _font.MeasureString(hint);
                DrawSpacedText(_font, hint,
                    new Microsoft.Xna.Framework.Vector2(rightX - hintSz.X, textY), Color.Lime * 0.7f);
                DrawSpacedText(_font, def.Description,
                    new Microsoft.Xna.Framework.Vector2(pos.X + 20, textY + 22), Color.Gray * 0.7f);
            }

            textY += selected ? 40 : 24;
        }
    }

    private void DrawSpacedText(SpriteFont font, string text, Microsoft.Xna.Framework.Vector2 position, Color color, float scale = 1f)
    {
        if (text.Length == 0) return;

        string[] parts = text.Split(' ');
        if (parts.Length <= 1)
        {
            _spriteBatch.DrawString(font, text, position, color, 0f, Microsoft.Xna.Framework.Vector2.Zero, scale, SpriteEffects.None, 0);
            return;
        }

        float spaceW = 8f * scale;

        float x = position.X;
        float y = position.Y;
        for (int i = 0; i < parts.Length; i++)
        {
            if (parts[i].Length > 0)
            {
                _spriteBatch.DrawString(font, parts[i], new Microsoft.Xna.Framework.Vector2(x, y), color, 0f, Microsoft.Xna.Framework.Vector2.Zero, scale, SpriteEffects.None, 0);
                x += font.MeasureString(parts[i]).X * scale + spaceW;
            }
            else
            {
                x += spaceW;
            }
        }
    }

    internal List<string> WordWrap(SpriteFont font, string text, float maxWidth)
    {
        var lines = new List<string>();
        if (string.IsNullOrEmpty(text)) return lines;

        text = text.Replace("\r\n", "\n");
        var paragraphs = text.Split('\n');
        for (int p = 0; p < paragraphs.Length; p++)
        {
            if (paragraphs[p].Length == 0)
            {
                if (p < paragraphs.Length - 1)
                    lines.Add("");
                continue;
            }

            var words = paragraphs[p].Split(' ');
            var currentLine = new List<string>();
            float lineWidth = 0;

            void FlushLine()
            {
                if (currentLine.Count > 0)
                    lines.Add(string.Join(" ", currentLine));
                currentLine.Clear();
                lineWidth = 0;
            }

            foreach (var word in words)
            {
                float wordWidth = font.MeasureString(word).X;
                if (currentLine.Count > 0)
                    wordWidth += 8f;

                if (lineWidth + wordWidth > maxWidth && currentLine.Count > 0)
                    FlushLine();

                if (currentLine.Count > 0) lineWidth += 8f;
                currentLine.Add(word);
                lineWidth += font.MeasureString(word).X;
            }

            if (currentLine.Count > 0)
                lines.Add(string.Join(" ", currentLine));
        }

        return lines;
    }

    public bool IsSystemUnderAttack(string systemId) => _activeAttacks.ContainsKey(systemId) && _activeAttacks[systemId].Count > 0;

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

    public void SetStatusMessage(string message, float duration = 3f)
    {
        _statusMessage = message;
        _statusTimer = duration;
    }

    public void EmpireStartAttack(string systemId)
    {
        var sys = _galaxy.FindSystemById(systemId);
        if (sys == null || sys.Hostility >= 3) return;
        if (!_activeAttacks.ContainsKey(systemId))
            _activeAttacks[systemId] = new List<AttackState>();
        // Allow multiple attackers on the same system
        _activeAttacks[systemId].Add(new AttackState { Timer = AttackDuration, Attacker = "Trigor Empire" });
        _statusMessage = $"Trigor Empire is attacking {sys.Name}!";
        _statusTimer = 5f;
        _galaxy.NewsService?.PostBreakingNews(
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
        // Allow multiple attackers on the same system
        _activeAttacks[systemId].Add(new AttackState { Timer = AttackDuration, Attacker = "Atlas Federation" });
        _statusMessage = $"Atlas Federation is attacking {sys.Name}!";
        _statusTimer = 5f;
        _galaxy.NewsService?.PostBreakingNews(
            $"Atlas Federation Strikes Back at {sys.Name}",
            $"Atlas Federation naval forces have launched a counter-offensive against {sys.Name}. 'Freedom will prevail,' stated Fleet Command.",
            "Atlas Federation News Network", "Atlas Federation");
    }

    public void RepelAttack(string systemId)
    {
        if (!_activeAttacks.Remove(systemId)) return;
        var sys = _galaxy.FindSystemById(systemId);
        string name = sys?.Name ?? systemId;
        _statusMessage = $"Attack on {name} repelled!";
        _statusTimer = 5f;
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

    private async Task CheckLlmAsync()
    {
        if (_llmService == null) return;
        _useLlm = await _llmService.DetectAsync();
        if (_useLlm)
        {
            _statusMessage = "LLM commanders online (Empire + Federation)";
            _statusTimer = 4f;
        }
    }

    private async Task LlmAiTickAsync()
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

        // Empire news
        if (!string.IsNullOrEmpty(decision.EmpireNewsHeadline) && !string.IsNullOrEmpty(decision.EmpireNewsBody))
        {
            _galaxy.NewsService?.PostFactionNews(
                decision.EmpireNewsHeadline,
                decision.EmpireNewsBody,
                "Imperial Herald", "Trigor Empire");
        }

        // Federation news
        if (!string.IsNullOrEmpty(decision.FederationNewsHeadline) && !string.IsNullOrEmpty(decision.FederationNewsBody))
        {
            _galaxy.NewsService?.PostFactionNews(
                decision.FederationNewsHeadline,
                decision.FederationNewsBody,
                "Atlas Federation News Network", "Atlas Federation");
        }

        // Galactic broadcasts
        if (!string.IsNullOrEmpty(decision.EmpireBroadcast))
        {
            _pendingBroadcasts.Add(new GalacticBroadcast
            {
                Faction = "Trigor Empire",
                CommanderName = "Emperor Cyrus III",
                CommanderTitle = "Emperor of the Trigor Empire",
                Message = decision.EmpireBroadcast,
                Timestamp = (float)_gameTime.TotalGameTime.TotalSeconds
            });
            _statusMessage = "Galactic Broadcast Received! Press B to view";
            _statusTimer = 6f;
        }
        if (!string.IsNullOrEmpty(decision.FederationBroadcast))
        {
            _pendingBroadcasts.Add(new GalacticBroadcast
            {
                Faction = "Atlas Federation",
                CommanderName = "Prime Minister Ezara Loban",
                CommanderTitle = "Prime Minister of the Atlas Federation",
                Message = decision.FederationBroadcast,
                Timestamp = (float)_gameTime.TotalGameTime.TotalSeconds
            });
            _statusMessage = "Galactic Broadcast Received! Press B to view";
            _statusTimer = 6f;
        }

        // Empire actions
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
                    EmpireStartAttack(target.Id);
            }
        }

        // Federation actions (defensive always allowed)
        if (decision.FederationUnblockRoutes != null)
        {
            foreach (var route in decision.FederationUnblockRoutes)
            {
                if (route.Count >= 2)
                    _routeManager.UnblockRoute(route[0], route[1]);
            }
        }

        // Federation offensive only after Empire has captured ~half of independent systems
        int currentIndependent = _galaxy.Systems.Count(s => s.Hostility < 3 && s.Faction != "Atlas Federation");
        float capturedRatio = _initialIndependentCount > 0
            ? 1f - (float)currentIndependent / _initialIndependentCount
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
                    FederationStartAttack(target.Id);
            }
        }
    }

    private void ResolveAttack(string systemId)
    {
        var sys = _galaxy.FindSystemById(systemId);
        if (sys == null) return;

        if (!_activeAttacks.TryGetValue(systemId, out var list) || list.Count == 0) return;

        // If player is currently in this system, don't auto-resolve — let player actions decide
        if (_player.CurrentSystemId == systemId && _viewMode == ViewMode.System)
            return;

        // Random winner among all attackers
        string attacker = list[Random.Shared.Next(list.Count)].Attacker;

        if (attacker == "Atlas Federation")
        {
            sys.Hostility = 0;
            sys.Faction = "Atlas Federation";
            if (sys.Station != null) sys.Station.DefenseLevel = 0;
            _statusMessage = $"Atlas Federation has captured {sys.Name}!";
            _galaxy.NewsService?.PostBreakingNews(
                $"Atlas Federation Liberates {sys.Name}",
                $"The Atlas Federation has successfully captured {sys.Name}, dealing a blow to Trigor Empire aggression in the sector.",
                "Atlas Federation News Network", "Atlas Federation");
        }
        else
        {
            sys.Hostility = 5;
            sys.Faction = "Trigor Empire";
            if (sys.Station != null) sys.Station.DefenseLevel = 0;
            _statusMessage = $"Trigor Empire has captured {sys.Name}!";
            _galaxy.NewsService?.PostBreakingNews(
                $"Trigor Empire Claims {sys.Name}",
                $"Imperial forces have seized control of {sys.Name}. The Empire declares it a 'liberated territory' under Imperial protection.",
                "Imperial Herald", "Trigor Empire");

            if (sys.Connections.Count > 0)
            {
                var blockedConn = sys.Connections[Random.Shared.Next(sys.Connections.Count)];
                _routeManager.BlockRoute(sys.Id, blockedConn);
            }
        }

        _activeAttacks.Remove(systemId);
        _statusTimer = 5f;
    }

    private void AiCaptureTick()
    {
        var targets = _galaxy.Systems
            .Where(s => s.Hostility < 3 && s.Station != null && s.Id != _player.CurrentSystemId)
            .ToList();
        if (targets.Count == 0) return;

        var target = targets[Random.Shared.Next(targets.Count)];

        // Empire can ONLY attack systems directly adjacent to an actual Trigor Empire system (1 hop)
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

        // Start attack (60s timer) instead of instant capture
        EmpireStartAttack(target.Id);
    }

    private void FederationAiTick()
    {
        // Defensive only until Empire has captured ~half of independent systems
        int currentIndependent = _galaxy.Systems.Count(s => s.Hostility < 3 && s.Faction != "Atlas Federation");
        float capturedRatio = _initialIndependentCount > 0
            ? 1f - (float)currentIndependent / _initialIndependentCount
            : 0f;
        bool canOffense = capturedRatio >= 0.5f;

        // Always try to unblock routes near friendly space (defensive)
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

        // Federation counter-attacks Empire systems adjacent to non-Empire systems
        var targets = _galaxy.Systems
            .Where(s => s.Hostility >= 3 && s.Station != null && s.Id != _player.CurrentSystemId)
            .ToList();
        if (targets.Count == 0) return;

        var target = targets[Random.Shared.Next(targets.Count)];

        // Federation can attack Empire systems adjacent to Federation OR Independent systems
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

        FederationStartAttack(target.Id);
    }

    private void AiDefenseTick()
    {
        // Federation systems: auto-upgrade to level 3
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

        // Empire systems: auto-upgrade to level 5, weighted by distance from Trigor
        var empSystems = _galaxy.Systems
            .Where(s => s.Station != null && s.Station.DefenseLevel < 5)
            .Where(s => s.Faction == "Trigor Empire")
            .ToList();
        if (empSystems.Count == 0) return;

        // Weight: closer to "trigor" = higher probability
        var weighted = new List<(StarSystemData sys, int weight)>();
        foreach (var sys in empSystems)
        {
            int dist = GetHopDistance("trigor", sys.Id);
            int weight = Math.Max(1, 5 - dist); // dist 0=5, 1=4, 2=3, etc.
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

        // Pick a giver system (any non-hostile system with a station)
        var givers = _galaxy.Systems
            .Where(s => s.Id != target.Id && s.Hostility < 3 && s.Station != null)
            .ToList();
        if (givers.Count == 0) return;
        var giver = givers[Random.Shared.Next(givers.Count)];

        string resList = string.Join(", ", cost.resources.Select(r =>
        {
            var def = _galaxy.FindResource(r.id);
            return $"{def?.Name ?? r.id} x{r.qty}";
        }));

        var quest = new QuestData
        {
            Id = questId,
            Name = $"Defense Upgrade: {target.Name} (Lv {target.Station.DefenseLevel}->{nextLevel})",
            Description = $"{faction} needs {resList} delivered to {target.Name} Station to upgrade defenses.",
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

    private (int credits, List<(string id, int qty)> resources) GetDefenseCost(int level)
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

    private void DrawQuestDialog()
    {
        if (_currentQuestDialog == null) return;
        int w = ScreenWidth;
        int h = ScreenHeight;
        float dialogW = 600f;
        float dialogH = 300f;
        float dx = (w - dialogW) / 2f;
        float dy = (h - dialogH) / 2f;

        _spriteBatch.Draw(_pixel, new Microsoft.Xna.Framework.Rectangle(0, 0, w, h),
            new Color(0, 0, 0, 200));
        _spriteBatch.Draw(_pixel, new Microsoft.Xna.Framework.Rectangle((int)dx, (int)dy, (int)dialogW, (int)dialogH),
            new Color(10, 10, 30, 240));
        DrawRect(dx, dy, dialogW, dialogH, new Color(80, 80, 140));

        float textX = dx + 30f;
        float textY = dy + 20f;
        float maxTextW = dialogW - 60f;
        float lineH = 22f;

        if (!string.IsNullOrEmpty(_currentQuestDialog.Speaker))
        {
            DrawSpacedText(_titleFont, _currentQuestDialog.Speaker,
                new Microsoft.Xna.Framework.Vector2(textX, textY), Color.Gold);
            textY += 30f;
        }

        float topArea = textY;
        float bottomArea = dy + dialogH - 35f;
        int maxVisible = (int)((bottomArea - topArea) / lineH);

        _questDialogWrappedLines = WordWrap(_font, _currentQuestDialog.Text, maxTextW);

        _questDialogScroll = Math.Clamp(_questDialogScroll, 0,
            Math.Max(0, _questDialogWrappedLines.Count - maxVisible));

        int visibleLines = Math.Min(maxVisible, _questDialogWrappedLines.Count - _questDialogScroll);
        float drawY = topArea;
        for (int i = _questDialogScroll; i < _questDialogScroll + visibleLines; i++)
        {
            DrawSpacedText(_font, _questDialogWrappedLines[i],
                new Microsoft.Xna.Framework.Vector2(textX, drawY), Color.White * 0.9f);
            drawY += lineH;
        }

        if (_questDialogWrappedLines.Count > maxVisible)
        {
            float scrollBarH = (float)maxVisible / _questDialogWrappedLines.Count * (bottomArea - topArea);
            float scrollY = topArea + (float)_questDialogScroll / _questDialogWrappedLines.Count * (bottomArea - topArea);
            float scrollX = dx + dialogW - 8f;
            _spriteBatch.Draw(_pixel, new Microsoft.Xna.Framework.Rectangle(
                (int)scrollX, (int)topArea, 4, (int)(bottomArea - topArea)),
                new Color(40, 40, 60));
            _spriteBatch.Draw(_pixel, new Microsoft.Xna.Framework.Rectangle(
                (int)scrollX, (int)scrollY, 4, (int)scrollBarH),
                new Color(140, 140, 180));
        }

        DrawSpacedText(_font, "[Enter/ESC/Space] Continue" +
            (_questDialogWrappedLines.Count > maxVisible ? " | Up/Down scroll" : ""),
            new Microsoft.Xna.Framework.Vector2(dx + 20, dy + dialogH - 30f), Color.Gray * 0.7f);
    }

    private void DrawBroadcastDialog()
    {
        int w = ScreenWidth;
        int h = ScreenHeight;
        float dialogW = 600f;
        float dialogH = 400f;
        float dx = (w - dialogW) / 2f;
        float dy = (h - dialogH) / 2f;
        float contentTop = dy + 60f;
        float contentBottom = dy + dialogH - 40f;
        float contentH = contentBottom - contentTop;

        // Dim background
        _spriteBatch.Draw(_pixel, new Microsoft.Xna.Framework.Rectangle(0, 0, w, h),
            new Color(0, 0, 0, 180));

        // Dialog box
        _spriteBatch.Draw(_pixel, new Microsoft.Xna.Framework.Rectangle((int)dx, (int)dy, (int)dialogW, (int)dialogH),
            new Color(10, 10, 30, 240));
        DrawRect(dx, dy, dialogW, dialogH, new Color(80, 80, 140));

        // Title
        DrawSpacedText(_titleFont, "GALACTIC BROADCAST",
            new Microsoft.Xna.Framework.Vector2(dx + 20, dy + 15), Color.Gold);

        // Tabs
        string[] tabs = { "All", "Empire", "Federation" };
        float tabW = dialogW / 3f;
        float tabY = dy + 45f;
        for (int i = 0; i < tabs.Length; i++)
        {
            Color tabColor = i == _broadcastTab ? Color.White : Color.Gray * 0.5f;
            float tabX = dx + tabW * i;
            float tw = _font.MeasureString(tabs[i]).X;
            DrawSpacedText(_font, tabs[i], new Microsoft.Xna.Framework.Vector2(
                tabX + (tabW - tw) / 2f, tabY), tabColor);
            if (i < tabs.Length - 1)
            {
                float sepX = tabX + tabW;
                DrawLine(sepX, tabY - 2f, sepX, tabY + 18f, Color.Gray * 0.3f);
            }
        }
        DrawLine(dx, tabY + 20f, dx + dialogW, tabY + 20f, Color.Gray * 0.3f);

        if (_pendingBroadcasts.Count == 0)
        {
            DrawSpacedText(_font, "No broadcasts received.",
                new Microsoft.Xna.Framework.Vector2(dx + 30f, contentTop), Color.Gray);
            // Footer
            DrawSpacedText(_font, "[ESC or B] Close",
                new Microsoft.Xna.Framework.Vector2(dx + 20, dy + dialogH - 30f), Color.Gray * 0.7f);
            return;
        }

        // Build filtered + word-wrapped lines
        var lines = new List<(string text, Color color, bool isHeader)>();
        foreach (var bc in _pendingBroadcasts)
        {
            // Filter
            if (_broadcastTab == 1 && bc.Faction != "Trigor Empire") continue;
            if (_broadcastTab == 2 && bc.Faction != "Atlas Federation") continue;

            Color factionColor = bc.Faction == "Trigor Empire" ? new Color(220, 60, 60) : new Color(60, 140, 220);

            // Faction header
            lines.Add((bc.Faction, factionColor, true));

            // Commander name & title
            if (!string.IsNullOrEmpty(bc.CommanderName))
                lines.Add(($"{bc.CommanderName}, {bc.CommanderTitle}", factionColor * 0.8f, false));

            // Message body with word wrap (spacing matches DrawSpacedText)
            string msg = SanitizeText(bc.Message);
            float wrapW = dialogW - 60f;
            float spaceW = 8f;
            var words = msg.Split(' ');
            string curLine = "";
            float lineW = 0f;
            foreach (var word in words)
            {
                float wordW = _font.MeasureString(word).X;
                bool wordTooWide = wordW > wrapW;

                if (curLine.Length == 0)
                {
                    if (wordTooWide)
                    {
                        // Character-split long word
                        string chunk = "";
                        float chunkW = 0f;
                        foreach (char c in word)
                        {
                            float cw = _font.MeasureString(c.ToString()).X;
                            if (chunkW + cw > wrapW && chunk.Length > 0)
                            {
                                lines.Add((chunk, Color.White * 0.9f, false));
                                chunk = c.ToString();
                                chunkW = cw;
                            }
                            else
                            {
                                chunk += c;
                                chunkW += cw;
                            }
                        }
                        curLine = chunk;
                        lineW = chunkW;
                    }
                    else
                    {
                        curLine = word;
                        lineW = wordW;
                    }
                }
                else if (lineW + spaceW + wordW > wrapW)
                {
                    lines.Add((curLine, Color.White * 0.9f, false));
                    curLine = word;
                    lineW = wordW;
                }
                else
                {
                    curLine += " " + word;
                    lineW += spaceW + wordW;
                }
            }
            if (curLine.Length > 0)
                lines.Add((curLine, Color.White * 0.9f, false));

            lines.Add(("", Color.Transparent, false)); // spacer between broadcasts
        }

        // Remove trailing spacer
        if (lines.Count > 0 && lines[^1].text.Length == 0)
            lines.RemoveAt(lines.Count - 1);

        // Calculate total content height
        float totalH = 0f;
        foreach (var l in lines)
        {
            totalH += l.isHeader ? 36f : (l.text.Length == 0 ? 12f : 24f);
        }

        // Clamp scroll (pixel-based)
        float maxScrollF = Math.Max(0f, totalH - contentH);
        _broadcastScroll = (int)Math.Clamp(_broadcastScroll, 0, (int)maxScrollF);
        if (_broadcastScroll > maxScrollF) _broadcastScroll = (int)maxScrollF;

        // Draw visible lines (end/rebegin batch with scissor rect for clipping)
        _spriteBatch.End();
        var prevScissor = GraphicsDevice.ScissorRectangle;
        var rs = new RasterizerState { ScissorTestEnable = true };
        GraphicsDevice.ScissorRectangle = new Microsoft.Xna.Framework.Rectangle(
            (int)dx, (int)contentTop, (int)dialogW, (int)(contentBottom - contentTop));
        _spriteBatch.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, null, null, rs);

        float sy = contentTop - _broadcastScroll;
        float textX = dx + 30f;

        foreach (var l in lines)
        {
            float lineH = l.isHeader ? 36f : (l.text.Length == 0 ? 12f : 24f);
            if (sy + lineH >= contentTop && sy <= contentBottom && l.text.Length > 0)
            {
                if (l.isHeader)
                    DrawSpacedText(_titleFont, l.text, new Microsoft.Xna.Framework.Vector2(textX, sy), l.color);
                else
                    DrawSpacedText(_font, l.text, new Microsoft.Xna.Framework.Vector2(textX, sy), l.color);
            }
            sy += lineH;
        }

        _spriteBatch.End();
        GraphicsDevice.ScissorRectangle = prevScissor;
        _spriteBatch.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend);

        // Scroll indicators
        if (_broadcastScroll > 0)
            DrawSpacedText(_font, "^", new Microsoft.Xna.Framework.Vector2(dx + dialogW - 25f, contentTop + 5f), Color.Gray * 0.6f);
        if (_broadcastScroll < maxScrollF)
            DrawSpacedText(_font, "v", new Microsoft.Xna.Framework.Vector2(dx + dialogW - 25f, contentBottom - 20f), Color.Gray * 0.6f);

        // Footer
        DrawSpacedText(_font, "[ESC or B] Close     [Tab] Filter",
            new Microsoft.Xna.Framework.Vector2(dx + 20, dy + dialogH - 30f), Color.Gray * 0.7f);
    }

    private static string SanitizeText(string text)
    {
        return text.Replace('\u2014', '-').Replace('\u2013', '-')
            .Replace('\u201C', '"').Replace('\u201D', '"')
            .Replace('\u2018', '\'').Replace('\u2019', '\'')
            .Replace('\u2026', '.').Replace('\u00A0', ' ');
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

    private static Color GetFactionColor(string? faction)
    {
        return faction switch
        {
            "Atlas Federation" => new Color(60, 130, 255),
            "Trigor Empire" => new Color(255, 60, 30),
            "Independent" => new Color(60, 200, 60),
            _ => Color.Gray
        };
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

public class AttackState
{
    public float Timer { get; set; }
    public string Attacker { get; set; } = "Trigor Empire";
}
