using SpaceGameEditor.Dialogs;
using SpaceGameEditor.Models;

namespace SpaceGameEditor;

public class MainForm : Form
{
    private readonly DataManager _data;
    private readonly TabControl _tabControl;

    private DataGridView _gridSystems = null!;
    private DataGridView _gridQuests = null!;
    private DataGridView _gridEquipment = null!;
    private DataGridView _gridResources = null!;
    private DataGridView _gridUpgrades = null!;
    private DataGridView _gridSpawns = null!;

    private readonly Label _lblStatus;

    public MainForm(DataManager data)
    {
        _data = data;

        Text = "SpaceGame Editor";
        Size = new Size(1100, 700);
        StartPosition = FormStartPosition.CenterScreen;

        var mainPanel = new Panel { Dock = DockStyle.Fill, Padding = new Padding(4) };
        mainPanel.AddTo(this);

        _tabControl = new TabControl { Dock = DockStyle.Fill };
        _tabControl.AddTo(mainPanel);

        _tabControl.TabPages.Add(MakeSystemsTab());
        _tabControl.TabPages.Add(MakeQuestsTab());
        _tabControl.TabPages.Add(MakeEquipmentTab());
        _tabControl.TabPages.Add(MakeResourcesTab());
        _tabControl.TabPages.Add(MakeUpgradesTab());
        _tabControl.TabPages.Add(MakeSpawnsTab());

        _lblStatus = new Label { Dock = DockStyle.Bottom, Height = 24, Text = "Ready" };
        _lblStatus.AddTo(mainPanel);

        RefreshAllGrids();

        KeyPreview = true;
        KeyDown += (_, e) =>
        {
            if (e.Control && e.KeyCode == Keys.S)
            {
                SaveData();
                e.SuppressKeyPress = true;
            }
        };
    }

    private void SaveData()
    {
        if (_data.SaveAll())
            _lblStatus.Text = $"Saved to {_data.DataPath} at {DateTime.Now:HH:mm:ss}";
    }

    private void RefreshAllGrids()
    {
        RefreshGrid(_gridSystems, _data.Systems.Select(s => new SystemRow
        {
            Id = s.Id, Name = s.Name, X = s.X, Y = s.Y, Hostility = s.Hostility, Faction = s.Faction ?? "",
            Connections = string.Join(", ", s.Connections), HasStation = s.Station != null
        }).ToList());

        RefreshGrid(_gridQuests, _data.Quests.Select(q => new
        {
            q.Id, q.Name, q.ObjectiveType, q.TargetSystem, q.GiverSystem, q.RewardCredits
        }).ToList());

        RefreshGrid(_gridEquipment, _data.Equipment.Select(e => new
        {
            e.Id, e.Name, e.Slot, e.Cost, e.Location, e.MinQuests
        }).ToList());

        RefreshGrid(_gridResources, _data.Resources.Select(r => new
        {
            r.Id, r.Name, r.Symbol, r.Category, r.BasePrice
        }).ToList());

        RefreshGrid(_gridUpgrades, _data.Upgrades.Select(u => new
        {
            u.Id, u.Name, u.EffectType, u.Cost, u.Location
        }).ToList());

        RefreshGrid(_gridSpawns, _data.Spawns.Select(s => new
        {
            s.SystemId, ShipCount = s.Ships.Count,
            QuestCond = s.QuestCondition != null ? $"{s.QuestCondition.QuestId} ({s.QuestCondition.Status})" : ""
        }).ToList());

        _lblStatus.Text = $"Loaded {_data.Systems.Count} systems, {_data.Quests.Count} quests, {_data.Equipment.Count} equipment, {_data.Resources.Count} resources, {_data.Upgrades.Count} upgrades, {_data.Spawns.Count} spawn entries";
    }

    private static void RefreshGrid<T>(DataGridView grid, List<T> data)
    {
        grid.DataSource = null;
        grid.DataSource = data;
    }

    private static DataGridView MakeGrid()
    {
        return new DataGridView
        {
            Dock = DockStyle.Fill,
            AllowUserToAddRows = false,
            AllowUserToDeleteRows = false,
            ReadOnly = true,
            SelectionMode = DataGridViewSelectionMode.FullRowSelect,
            MultiSelect = false,
            AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill,
            AllowUserToResizeRows = false,
            RowHeadersVisible = false
        };
    }

    private TabPage MakeTab(string title, DataGridView grid, params (string text, EventHandler handler)[] buttons)
    {
        var page = new TabPage(title);
        var panel = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 2 };
        panel.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        panel.RowStyles.Add(new RowStyle(SizeType.Absolute, 36));

        grid.Dock = DockStyle.Fill;
        grid.DoubleClick += (_, _) =>
        {
            if (grid.SelectedRows.Count > 0)
                EditSelected(title);
        };
        panel.Controls.Add(grid, 0, 0);

        var btnPanel = new FlowLayoutPanel { Dock = DockStyle.Fill, FlowDirection = FlowDirection.LeftToRight, Height = 36, Padding = new Padding(4) };
        foreach (var (text, handler) in buttons)
        {
            var btn = new Button { Text = text, Height = 28, AutoSize = true };
            btn.Click += handler;
            btnPanel.Controls.Add(btn);
        }
        panel.Controls.Add(btnPanel, 0, 1);
        page.Controls.Add(panel);
        return page;
    }

    private TabPage MakeSystemsTab()
    {
        _gridSystems = MakeGrid();
        return MakeTab("Systems", _gridSystems,
            ("Add", (_, _) => AddSystem()),
            ("Edit", (_, _) => EditSystem()),
            ("Delete", (_, _) => DeleteSystem()));
    }

    private TabPage MakeQuestsTab()
    {
        _gridQuests = MakeGrid();
        return MakeTab("Quests", _gridQuests,
            ("Add", (_, _) => AddQuest()),
            ("Edit", (_, _) => EditQuest()),
            ("Delete", (_, _) => DeleteQuest()));
    }

    private TabPage MakeEquipmentTab()
    {
        _gridEquipment = MakeGrid();
        return MakeTab("Equipment", _gridEquipment,
            ("Add", (_, _) => AddEquipment()),
            ("Edit", (_, _) => EditEquipment()),
            ("Delete", (_, _) => DeleteEquipment()));
    }

    private TabPage MakeResourcesTab()
    {
        _gridResources = MakeGrid();
        return MakeTab("Resources", _gridResources,
            ("Add", (_, _) => AddResource()),
            ("Edit", (_, _) => EditResource()),
            ("Delete", (_, _) => DeleteResource()));
    }

    private TabPage MakeUpgradesTab()
    {
        _gridUpgrades = MakeGrid();
        return MakeTab("Upgrades", _gridUpgrades,
            ("Add", (_, _) => AddUpgrade()),
            ("Edit", (_, _) => EditUpgrade()),
            ("Delete", (_, _) => DeleteUpgrade()));
    }

    private TabPage MakeSpawnsTab()
    {
        _gridSpawns = MakeGrid();
        return MakeTab("Spawns", _gridSpawns,
            ("Add", (_, _) => AddSpawn()),
            ("Edit", (_, _) => EditSpawn()),
            ("Delete", (_, _) => DeleteSpawn()));
    }

    private void AddSystem()
    {
        var ids = _data.Systems.Select(s => s.Id).ToList();
        var dlg = new SystemEditorDialog(ids);
        if (dlg.ShowDialog(this) == DialogResult.OK && dlg.System != null)
        {
            _data.Systems.Add(dlg.System);
            RefreshAllGrids();
        }
    }

    private void EditSystem()
    {
        if (GetSelectedIndex(_gridSystems, out int idx))
        {
            var ids = _data.Systems.Select(s => s.Id).ToList();
            var dlg = new SystemEditorDialog(ids, _data.Systems[idx]);
            if (dlg.ShowDialog(this) == DialogResult.OK && dlg.System != null)
            {
                _data.Systems[idx] = dlg.System;
                RefreshAllGrids();
            }
        }
    }

    private void DeleteSystem()
    {
        if (GetSelectedIndex(_gridSystems, out int idx))
        {
            if (ConfirmDelete(_data.Systems[idx].Name))
            {
                _data.Systems.RemoveAt(idx);
                RefreshAllGrids();
            }
        }
    }

    private void AddQuest()
    {
        var ids = _data.Quests.Select(q => q.Id).ToList();
        var dlg = new QuestEditorDialog(ids);
        if (dlg.ShowDialog(this) == DialogResult.OK && dlg.Quest != null)
        {
            _data.Quests.Add(dlg.Quest);
            RefreshAllGrids();
        }
    }

    private void EditQuest()
    {
        if (GetSelectedIndex(_gridQuests, out int idx))
        {
            var ids = _data.Quests.Where(q => q.Id != _data.Quests[idx].Id).Select(q => q.Id).ToList();
            var dlg = new QuestEditorDialog(ids, _data.Quests[idx]);
            if (dlg.ShowDialog(this) == DialogResult.OK && dlg.Quest != null)
            {
                _data.Quests[idx] = dlg.Quest;
                RefreshAllGrids();
            }
        }
    }

    private void DeleteQuest()
    {
        if (GetSelectedIndex(_gridQuests, out int idx))
        {
            if (ConfirmDelete(_data.Quests[idx].Name))
            {
                _data.Quests.RemoveAt(idx);
                RefreshAllGrids();
            }
        }
    }

    private void AddEquipment()
    {
        var dlg = new EquipmentEditorDialog();
        if (dlg.ShowDialog(this) == DialogResult.OK && dlg.Equipment != null)
        {
            _data.Equipment.Add(dlg.Equipment);
            RefreshAllGrids();
        }
    }

    private void EditEquipment()
    {
        if (GetSelectedIndex(_gridEquipment, out int idx))
        {
            var dlg = new EquipmentEditorDialog(_data.Equipment[idx]);
            if (dlg.ShowDialog(this) == DialogResult.OK && dlg.Equipment != null)
            {
                _data.Equipment[idx] = dlg.Equipment;
                RefreshAllGrids();
            }
        }
    }

    private void DeleteEquipment()
    {
        if (GetSelectedIndex(_gridEquipment, out int idx))
        {
            if (ConfirmDelete(_data.Equipment[idx].Name))
            {
                _data.Equipment.RemoveAt(idx);
                RefreshAllGrids();
            }
        }
    }

    private void AddResource()
    {
        var dlg = new ResourceEditorDialog();
        if (dlg.ShowDialog(this) == DialogResult.OK && dlg.Resource != null)
        {
            _data.Resources.Add(dlg.Resource);
            RefreshAllGrids();
        }
    }

    private void EditResource()
    {
        if (GetSelectedIndex(_gridResources, out int idx))
        {
            var dlg = new ResourceEditorDialog(_data.Resources[idx]);
            if (dlg.ShowDialog(this) == DialogResult.OK && dlg.Resource != null)
            {
                _data.Resources[idx] = dlg.Resource;
                RefreshAllGrids();
            }
        }
    }

    private void DeleteResource()
    {
        if (GetSelectedIndex(_gridResources, out int idx))
        {
            if (ConfirmDelete(_data.Resources[idx].Name))
            {
                _data.Resources.RemoveAt(idx);
                RefreshAllGrids();
            }
        }
    }

    private void AddUpgrade()
    {
        var dlg = new UpgradeEditorDialog();
        if (dlg.ShowDialog(this) == DialogResult.OK && dlg.Upgrade != null)
        {
            _data.Upgrades.Add(dlg.Upgrade);
            RefreshAllGrids();
        }
    }

    private void EditUpgrade()
    {
        if (GetSelectedIndex(_gridUpgrades, out int idx))
        {
            var dlg = new UpgradeEditorDialog(_data.Upgrades[idx]);
            if (dlg.ShowDialog(this) == DialogResult.OK && dlg.Upgrade != null)
            {
                _data.Upgrades[idx] = dlg.Upgrade;
                RefreshAllGrids();
            }
        }
    }

    private void DeleteUpgrade()
    {
        if (GetSelectedIndex(_gridUpgrades, out int idx))
        {
            if (ConfirmDelete(_data.Upgrades[idx].Name))
            {
                _data.Upgrades.RemoveAt(idx);
                RefreshAllGrids();
            }
        }
    }

    private void AddSpawn()
    {
        var sysIds = _data.Systems.Select(s => s.Id).ToList();
        var dlg = new SpawnEditorDialog(sysIds, _data.Quests, _data.Spawns);
        if (dlg.ShowDialog(this) == DialogResult.OK && dlg.Spawn != null)
        {
            _data.Spawns.Add(dlg.Spawn);
            RefreshAllGrids();
        }
    }

    private void EditSpawn()
    {
        if (GetSelectedIndex(_gridSpawns, out int idx))
        {
            var sysIds = _data.Systems.Select(s => s.Id).ToList();
            var dlg = new SpawnEditorDialog(sysIds, _data.Quests, _data.Spawns, _data.Spawns[idx]);
            if (dlg.ShowDialog(this) == DialogResult.OK && dlg.Spawn != null)
            {
                _data.Spawns[idx] = dlg.Spawn;
                RefreshAllGrids();
            }
        }
    }

    private void DeleteSpawn()
    {
        if (GetSelectedIndex(_gridSpawns, out int idx))
        {
            if (ConfirmDelete($"spawn entry for {_data.Spawns[idx].SystemId}"))
            {
                _data.Spawns.RemoveAt(idx);
                RefreshAllGrids();
            }
        }
    }

    private void EditSelected(string tabName)
    {
        switch (tabName)
        {
            case "Systems": EditSystem(); break;
            case "Quests": EditQuest(); break;
            case "Equipment": EditEquipment(); break;
            case "Resources": EditResource(); break;
            case "Upgrades": EditUpgrade(); break;
            case "Spawns": EditSpawn(); break;
        }
    }

    private static bool GetSelectedIndex(DataGridView grid, out int index)
    {
        index = -1;
        if (grid.SelectedRows.Count > 0)
        {
            index = grid.SelectedRows[0].Index;
            return index >= 0 && index < grid.Rows.Count;
        }
        return false;
    }

    private static bool ConfirmDelete(string name)
    {
        return MessageBox.Show($"Delete '{name}'?", "Confirm Delete", MessageBoxButtons.YesNo, MessageBoxIcon.Warning) == DialogResult.Yes;
    }

    private class SystemRow
    {
        public string Id { get; set; } = "";
        public string Name { get; set; } = "";
        public float X { get; set; }
        public float Y { get; set; }
        public int Hostility { get; set; }
        public string Faction { get; set; } = "";
        public string Connections { get; set; } = "";
        public bool HasStation { get; set; }
    }
}
