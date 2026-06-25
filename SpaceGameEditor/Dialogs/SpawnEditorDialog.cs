using SpaceGameEditor.Models;

namespace SpaceGameEditor.Dialogs;

public class SpawnEditorDialog : Form
{
    private readonly ComboBox _cmbSystemId, _cmbQuestId, _cmbQuestStatus;
    private readonly DataGridView _dgvShips;
    private readonly Button _btnAddShip, _btnRemoveShip, _btnOk, _btnCancel;
    private readonly Label _lblError;
    private readonly List<SpawnDef> _allSpawns;
    private readonly List<string> _systemIds;
    private readonly List<QuestData> _quests;

    public SpawnDef? Spawn { get; private set; }

    public SpawnEditorDialog(List<string> systemIds, List<QuestData> quests, List<SpawnDef> allSpawns, SpawnDef? existing = null)
    {
        _systemIds = systemIds;
        _quests = quests;
        _allSpawns = allSpawns;

        Text = existing != null ? "Edit Spawn Entry" : "Add Spawn Entry";
        StartPosition = FormStartPosition.CenterParent;
        ClientSize = new Size(520, 400);
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        MinimizeBox = false;

        int y = 12;

        new Label { Text = "System ID:", Location = new(12, y + 3), Size = new(100, 20) }.AddTo(this);
        _cmbSystemId = new ComboBox { Location = new(120, y), Size = new(200, 24), DropDownStyle = ComboBoxStyle.DropDownList };
        _cmbSystemId.Items.AddRange(systemIds.ToArray());
        _cmbSystemId.AddTo(this);
        y += 28;

        new Label { Text = "Quest Condition (opt):", Location = new(12, y + 3), Size = new(130, 20) }.AddTo(this);
        _cmbQuestId = new ComboBox { Location = new(150, y), Size = new(170, 24), DropDownStyle = ComboBoxStyle.DropDownList };
        _cmbQuestId.Items.Add("");
        foreach (var q in quests) _cmbQuestId.Items.Add(q.Id);
        _cmbQuestId.SelectedIndex = 0;
        _cmbQuestId.AddTo(this);

        _cmbQuestStatus = new ComboBox { Location = new(330, y), Size = new(100, 24), DropDownStyle = ComboBoxStyle.DropDownList };
        _cmbQuestStatus.Items.AddRange(["active", "completed", "inactive"]);
        _cmbQuestStatus.SelectedIndex = 0;
        _cmbQuestStatus.AddTo(this);
        y += 32;

        new Label { Text = "Ships/Objects:", Location = new(12, y + 3), Size = new(100, 20) }.AddTo(this);
        _dgvShips = new DataGridView
        {
            Location = new(12, y + 22),
            Size = new(480, 160),
            AllowUserToAddRows = false,
            AllowUserToDeleteRows = false,
            ReadOnly = true,
            SelectionMode = DataGridViewSelectionMode.FullRowSelect,
            MultiSelect = false,
            ColumnHeadersHeightSizeMode = DataGridViewColumnHeadersHeightSizeMode.AutoSize,
            AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill
        };
        _dgvShips.Columns.Add("Type", "Type");
        _dgvShips.Columns.Add("Count", "Count");
        _dgvShips.Columns.Add("Faction", "Faction");
        _dgvShips.Columns.Add("AiState", "AI State");
        _dgvShips.AddTo(this);
        y += 188;

        _btnAddShip = new Button { Text = "Add Ship/Object", Location = new(12, y), Size = new(110, 26) };
        _btnAddShip.AddTo(this);
        _btnAddShip.Click += (_, _) => AddShip();

        _btnRemoveShip = new Button { Text = "Remove Ship", Location = new(130, y), Size = new(90, 26) };
        _btnRemoveShip.AddTo(this);
        _btnRemoveShip.Click += (_, _) => RemoveShip();
        y += 32;

        _lblError = new Label { Text = "", ForeColor = Color.Red, Location = new(12, y), Size = new(480, 20) };
        _lblError.AddTo(this);
        y += 24;

        _btnOk = new Button { Text = "OK", Location = new(330, y), Size = new(80, 30) };
        _btnOk.AddTo(this);
        _btnOk.Click += (_, _) => ValidateAndClose();

        _btnCancel = new Button { Text = "Cancel", Location = new(420, y), Size = new(80, 30), DialogResult = DialogResult.Cancel };
        _btnCancel.AddTo(this);

        if (existing != null)
        {
            _cmbSystemId.SelectedItem = existing.SystemId;
            if (existing.QuestCondition != null)
            {
                _cmbQuestId.SelectedItem = existing.QuestCondition.QuestId;
                _cmbQuestStatus.SelectedItem = existing.QuestCondition.Status;
            }
            foreach (var ship in existing.Ships)
                _dgvShips.Rows.Add(ship.Type, ship.Count, ship.Faction, ship.AiState);
        }
    }

    private void AddShip()
    {
        var dlg = new ShipEntryDialog();
        if (dlg.ShowDialog(this) == DialogResult.OK)
            _dgvShips.Rows.Add(dlg.Type, dlg.Count, dlg.Faction, dlg.AiState);
    }

    private void RemoveShip()
    {
        if (_dgvShips.SelectedRows.Count > 0 && _dgvShips.SelectedRows[0].Index >= 0)
            _dgvShips.Rows.RemoveAt(_dgvShips.SelectedRows[0].Index);
    }

    private void ValidateAndClose()
    {
        if (_cmbSystemId.SelectedItem == null) { _lblError.Text = "System is required."; return; }
        if (_dgvShips.Rows.Count == 0) { _lblError.Text = "At least one ship entry is required."; return; }

        var ships = new List<ShipSpawnEntry>();
        foreach (DataGridViewRow row in _dgvShips.Rows)
        {
            ships.Add(new ShipSpawnEntry
            {
                Type = row.Cells[0].Value?.ToString() ?? "scout",
                Count = int.Parse(row.Cells[1].Value?.ToString() ?? "1"),
                Faction = row.Cells[2].Value?.ToString() ?? "",
                AiState = row.Cells[3].Value?.ToString() ?? "Orbit"
            });
        }

        string qid = _cmbQuestId.SelectedItem?.ToString() ?? "";
        Spawn = new SpawnDef
        {
            SystemId = _cmbSystemId.SelectedItem.ToString()!,
            Ships = ships,
            QuestCondition = string.IsNullOrEmpty(qid) ? null : new QuestCondition
            {
                QuestId = qid,
                Status = _cmbQuestStatus.SelectedItem?.ToString() ?? "active"
            }
        };
        DialogResult = DialogResult.OK;
        Close();
    }
}

public class ShipEntryDialog : Form
{
    private readonly ComboBox _cmbType, _cmbAiState, _cmbFaction;
    private readonly NumericUpDown _numCount;

    public string Type => _cmbType.SelectedItem?.ToString() ?? "scout";
    public int Count => (int)_numCount.Value;
    public string Faction => _cmbFaction.SelectedItem?.ToString() ?? "";
    public string AiState => _cmbAiState.SelectedItem?.ToString() ?? "Orbit";

    public ShipEntryDialog()
    {
        Text = "Add Ship/Object";
        StartPosition = FormStartPosition.CenterParent;
        ClientSize = new Size(280, 180);
        FormBorderStyle = FormBorderStyle.FixedDialog;

        int y = 12;
        new Label { Text = "Type:", Location = new(12, y + 3), Size = new(60, 20) }.AddTo(this);
        _cmbType = new ComboBox { Location = new(80, y), Size = new(170, 24), DropDownStyle = ComboBoxStyle.DropDownList };
        _cmbType.Items.AddRange(["scout", "fighter", "gunship", "cruiser", "dreadnought", "interceptor", "missile_frigate", "destroyer", "battleship", "escape_pod"]);
        _cmbType.SelectedIndex = 0;
        _cmbType.AddTo(this);
        y += 28;

        new Label { Text = "Count:", Location = new(12, y + 3), Size = new(60, 20) }.AddTo(this);
        _numCount = new NumericUpDown { Location = new(80, y), Size = new(80, 24), Maximum = 50, Minimum = 1, Value = 1 };
        _numCount.AddTo(this);
        y += 28;

        new Label { Text = "Faction:", Location = new(12, y + 3), Size = new(60, 20) }.AddTo(this);
        _cmbFaction = new ComboBox { Location = new(80, y), Size = new(170, 24), DropDownStyle = ComboBoxStyle.DropDownList };
        _cmbFaction.Items.AddRange(["", "Atlas Federation", "Trigor Empire"]);
        _cmbFaction.SelectedIndex = 0;
        _cmbFaction.AddTo(this);
        y += 28;

        new Label { Text = "AI State:", Location = new(12, y + 3), Size = new(60, 20) }.AddTo(this);
        _cmbAiState = new ComboBox { Location = new(80, y), Size = new(120, 24), DropDownStyle = ComboBoxStyle.DropDownList };
        _cmbAiState.Items.AddRange(["Orbit", "Attack", "Idle", "Evade"]);
        _cmbAiState.SelectedIndex = 0;
        _cmbAiState.AddTo(this);
        y += 32;

        var btnOk = new Button { Text = "OK", Location = new(100, y), Size = new(80, 30), DialogResult = DialogResult.OK };
        btnOk.AddTo(this);
        var btnCancel = new Button { Text = "Cancel", Location = new(185, y), Size = new(80, 30), DialogResult = DialogResult.Cancel };
        btnCancel.AddTo(this);
    }
}
