using SpaceGameEditor.Models;

namespace SpaceGameEditor.Dialogs;

public class QuestEditorDialog : Form
{
    private readonly TextBox _txtId, _txtName, _txtDesc;
    private readonly ComboBox _cmbObjType, _cmbTargetSys, _cmbTargetItem, _cmbGiverSys, _cmbDefenseSys;
    private readonly NumericUpDown _numTargetCount;
    private readonly ListBox _lstRewards, _lstReqResources, _lstDialogs;
    private readonly Button _btnAddReward, _btnRemoveReward, _btnAddReq, _btnRemoveReq, _btnAddDialog, _btnRemoveDialog, _btnOk, _btnCancel;
    private readonly Label _lblError;
    private readonly List<string> _systemIds;
    private readonly List<string> _upgradeIds;
    private readonly List<string> _equipmentIds;
    private readonly List<string> _resourceIds;
    private List<(string Type, string Value)> _rewards = new();
    private Dictionary<string, int> _requiredResources = new();
    private List<QuestDialog> _dialogs = new();

    public QuestData? Quest { get; private set; }

    public QuestEditorDialog(List<string> existingIds, List<string> systemIds, List<string> targetItems,
        List<string> upgradeIds, List<string> equipmentIds, List<string> resourceIds, QuestData? existing = null)
    {
        _systemIds = systemIds;
        _upgradeIds = upgradeIds;
        _equipmentIds = equipmentIds;
        _resourceIds = resourceIds;

        Text = existing != null ? "Edit Quest" : "Add Quest";
        StartPosition = FormStartPosition.CenterParent;
        ClientSize = new Size(520, 600);
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        MinimizeBox = false;

        int y = 12, labelW = 120, inputW = 350;

        new Label { Text = "ID:", Location = new(12, y + 3), Size = new(labelW, 20) }.AddTo(this);
        _txtId = new TextBox { Location = new(140, y), Size = new(inputW, 24) }; _txtId.AddTo(this);
        y += 28;

        new Label { Text = "Name:", Location = new(12, y + 3), Size = new(labelW, 20) }.AddTo(this);
        _txtName = new TextBox { Location = new(140, y), Size = new(inputW, 24) }; _txtName.AddTo(this);
        y += 28;

        new Label { Text = "Description:", Location = new(12, y + 3), Size = new(labelW, 20) }.AddTo(this);
        _txtDesc = new TextBox { Location = new(140, y), Size = new(inputW, 50), Multiline = true };
        _txtDesc.AddTo(this);
        y += 54;

        new Label { Text = "Objective Type:", Location = new(12, y + 3), Size = new(labelW, 20) }.AddTo(this);
        _cmbObjType = new ComboBox { Location = new(140, y), Size = new(160, 24), DropDownStyle = ComboBoxStyle.DropDownList };
        _cmbObjType.Items.AddRange(["travel", "collect", "deliver", "destroy"]);
        _cmbObjType.SelectedIndex = 0;
        _cmbObjType.AddTo(this);
        y += 28;

        new Label { Text = "Target System:", Location = new(12, y + 3), Size = new(labelW, 20) }.AddTo(this);
        _cmbTargetSys = new ComboBox { Location = new(140, y), Size = new(inputW, 24), DropDownStyle = ComboBoxStyle.DropDownList };
        _cmbTargetSys.Items.Add("");
        foreach (var sid in systemIds) _cmbTargetSys.Items.Add(sid);
        _cmbTargetSys.SelectedIndex = 0;
        _cmbTargetSys.AddTo(this);
        y += 28;

        new Label { Text = "Target Item:", Location = new(12, y + 3), Size = new(labelW, 20) }.AddTo(this);
        _cmbTargetItem = new ComboBox { Location = new(140, y), Size = new(inputW, 24), DropDownStyle = ComboBoxStyle.DropDownList };
        _cmbTargetItem.Items.Add("");
        foreach (var ti in targetItems) _cmbTargetItem.Items.Add(ti);
        _cmbTargetItem.SelectedIndex = 0;
        _cmbTargetItem.AddTo(this);
        y += 28;

        new Label { Text = "Target Count:", Location = new(12, y + 3), Size = new(labelW, 20) }.AddTo(this);
        _numTargetCount = new NumericUpDown { Location = new(140, y), Size = new(100, 24), Maximum = 999, Minimum = 1, Value = 1 };
        _numTargetCount.AddTo(this);
        y += 28;

        new Label { Text = "Giver System:", Location = new(12, y + 3), Size = new(labelW, 20) }.AddTo(this);
        _cmbGiverSys = new ComboBox { Location = new(140, y), Size = new(inputW, 24), DropDownStyle = ComboBoxStyle.DropDownList };
        _cmbGiverSys.Items.Add("");
        foreach (var sid in systemIds) _cmbGiverSys.Items.Add(sid);
        _cmbGiverSys.SelectedIndex = 0;
        _cmbGiverSys.AddTo(this);
        y += 28;

        // Rewards
        new Label { Text = "Rewards:", Location = new(12, y + 3), Size = new(labelW, 20) }.AddTo(this);
        _lstRewards = new ListBox { Location = new(140, y), Size = new(300, 60) };
        _lstRewards.AddTo(this);
        _btnAddReward = new Button { Text = "Add", Location = new(450, y), Size = new(50, 24) };
        _btnAddReward.AddTo(this);
        _btnRemoveReward = new Button { Text = "Del", Location = new(450, y + 28), Size = new(50, 24) };
        _btnRemoveReward.AddTo(this);
        y += 68;

        new Label { Text = "Defense System:", Location = new(12, y + 3), Size = new(labelW, 20) }.AddTo(this);
        _cmbDefenseSys = new ComboBox { Location = new(140, y), Size = new(inputW, 24), DropDownStyle = ComboBoxStyle.DropDownList };
        _cmbDefenseSys.Items.Add("");
        foreach (var sid in systemIds) _cmbDefenseSys.Items.Add(sid);
        _cmbDefenseSys.SelectedIndex = 0;
        _cmbDefenseSys.AddTo(this);
        y += 28;

        // Required Resources
        new Label { Text = "Required Resources:", Location = new(12, y + 3), Size = new(labelW, 20) }.AddTo(this);
        _lstReqResources = new ListBox { Location = new(140, y), Size = new(240, 60) };
        _lstReqResources.AddTo(this);
        _btnAddReq = new Button { Text = "Add", Location = new(390, y), Size = new(50, 24) };
        _btnAddReq.AddTo(this);
        _btnRemoveReq = new Button { Text = "Del", Location = new(445, y), Size = new(50, 24) };
        _btnRemoveReq.AddTo(this);
        y += 68;

        // Dialogs
        new Label { Text = "Dialogs:", Location = new(12, y + 3), Size = new(labelW, 20) }.AddTo(this);
        _lstDialogs = new ListBox { Location = new(140, y), Size = new(300, 60) };
        _lstDialogs.AddTo(this);
        _btnAddDialog = new Button { Text = "Add", Location = new(450, y), Size = new(50, 24) };
        _btnAddDialog.AddTo(this);
        _btnRemoveDialog = new Button { Text = "Del", Location = new(450, y + 28), Size = new(50, 24) };
        _btnRemoveDialog.AddTo(this);
        y += 68;

        _lblError = new Label { Text = "", ForeColor = Color.Red, Location = new(12, y), Size = new(480, 20) };
        _lblError.AddTo(this);
        y += 24;

        _btnOk = new Button { Text = "OK", Location = new(320, y), Size = new(80, 30) };
        _btnOk.AddTo(this);
        _btnOk.Click += (_, _) => ValidateAndClose();

        _btnCancel = new Button { Text = "Cancel", Location = new(410, y), Size = new(80, 30), DialogResult = DialogResult.Cancel };
        _btnCancel.AddTo(this);

        _btnAddReward.Click += (_, _) => AddReward();
        _btnRemoveReward.Click += (_, _) => RemoveReward();
        _btnAddReq.Click += (_, _) => AddResource();
        _btnRemoveReq.Click += (_, _) => RemoveResource();
        _btnAddDialog.Click += (_, _) => AddDialog();
        _btnRemoveDialog.Click += (_, _) => RemoveDialog();

        if (existing != null)
        {
            _txtId.Text = existing.Id;
            _txtName.Text = existing.Name;
            _txtDesc.Text = existing.Description;
            _cmbObjType.SelectedItem = existing.ObjectiveType;
            if (existing.TargetSystem != null) _cmbTargetSys.SelectedItem = existing.TargetSystem;
            if (existing.TargetItem != null) _cmbTargetItem.SelectedItem = existing.TargetItem;
            _numTargetCount.Value = existing.TargetCount;
            if (!string.IsNullOrEmpty(existing.GiverSystem)) _cmbGiverSys.SelectedItem = existing.GiverSystem;
            if (existing.RewardUpgrade != null) _rewards.Add(("Upgrade", existing.RewardUpgrade));
            if (existing.RewardEquipment != null) _rewards.Add(("Equipment", existing.RewardEquipment));
            if (existing.RewardCredits > 0) _rewards.Add(("Credits", existing.RewardCredits.ToString()));
            if (!string.IsNullOrEmpty(existing.RewardDefenseSystem)) _cmbDefenseSys.SelectedItem = existing.RewardDefenseSystem;
            _requiredResources = new Dictionary<string, int>(existing.RequiredResources);
            _dialogs = existing.Dialogs.Select(d => new QuestDialog
            {
                Id = d.Id,
                Text = d.Text,
                Speaker = d.Speaker,
                Trigger = d.Trigger
            }).ToList();
            RefreshRewardsList();
            RefreshReqList();
            RefreshDialogsList();
        }
    }

    private void AddReward()
    {
        using var dlg = new RewardEntryDialog(_upgradeIds, _equipmentIds, _resourceIds, _systemIds,
            _rewards.Select(r => $"{r.Type}:{r.Value}").ToHashSet());
        if (dlg.ShowDialog(this) == DialogResult.OK)
            _rewards.Add((dlg.RewardType, dlg.RewardValue));
        RefreshRewardsList();
    }

    private void RemoveReward()
    {
        if (_lstRewards.SelectedIndex >= 0 && _lstRewards.SelectedIndex < _rewards.Count)
        {
            _rewards.RemoveAt(_lstRewards.SelectedIndex);
            RefreshRewardsList();
        }
    }

    private void RefreshRewardsList()
    {
        _lstRewards.Items.Clear();
        foreach (var (type, value) in _rewards)
            _lstRewards.Items.Add($"{type}: {value}");
    }

    private void AddResource()
    {
        using var dlg = new ResourceKeyDialog(_requiredResources.Keys);
        if (dlg.ShowDialog(this) == DialogResult.OK)
        {
            _requiredResources[dlg.ResourceId] = dlg.Quantity;
            RefreshReqList();
        }
    }

    private void RemoveResource()
    {
        if (_lstReqResources.SelectedItem is string s)
        {
            var parts = s.Split(':');
            if (parts.Length > 0) _requiredResources.Remove(parts[0].Trim());
            RefreshReqList();
        }
    }

    private void RefreshReqList()
    {
        _lstReqResources.Items.Clear();
        foreach (var kv in _requiredResources)
            _lstReqResources.Items.Add($"{kv.Key}: {kv.Value}");
    }

    private void AddDialog()
    {
        using var dlg = new DialogEditorDialog();
        if (dlg.ShowDialog(this) == DialogResult.OK && dlg.Dialog != null)
        {
            _dialogs.Add(dlg.Dialog);
            RefreshDialogsList();
        }
    }

    private void RemoveDialog()
    {
        if (_lstDialogs.SelectedIndex >= 0 && _lstDialogs.SelectedIndex < _dialogs.Count)
        {
            _dialogs.RemoveAt(_lstDialogs.SelectedIndex);
            RefreshDialogsList();
        }
    }

    private void RefreshDialogsList()
    {
        _lstDialogs.Items.Clear();
        foreach (var d in _dialogs)
            _lstDialogs.Items.Add($"[{d.Trigger}] {d.Id}{(d.Speaker.Length > 0 ? $" ({d.Speaker})" : "")}");
    }

    private void ValidateAndClose()
    {
        if (string.IsNullOrWhiteSpace(_txtId.Text)) { _lblError.Text = "ID is required."; return; }
        if (string.IsNullOrWhiteSpace(_txtName.Text)) { _lblError.Text = "Name is required."; return; }

        int credits = 0;
        string? upgrade = null, equipment = null;
        foreach (var (type, value) in _rewards)
        {
            if (type == "Credits" && int.TryParse(value, out int c)) credits = c;
            else if (type == "Upgrade") upgrade = value;
            else if (type == "Equipment") equipment = value;
        }

        Quest = new QuestData
        {
            Id = _txtId.Text.Trim(),
            Name = _txtName.Text.Trim(),
            Description = _txtDesc.Text.Trim(),
            ObjectiveType = _cmbObjType.SelectedItem?.ToString() ?? "travel",
            TargetSystem = string.IsNullOrWhiteSpace(_cmbTargetSys.SelectedItem?.ToString()) ? null : _cmbTargetSys.SelectedItem.ToString(),
            TargetItem = string.IsNullOrWhiteSpace(_cmbTargetItem.SelectedItem?.ToString()) ? null : _cmbTargetItem.SelectedItem.ToString(),
            TargetCount = (int)_numTargetCount.Value,
            RewardCredits = credits,
            GiverSystem = _cmbGiverSys.SelectedItem?.ToString() ?? "",
            RewardUpgrade = upgrade,
            RewardEquipment = equipment,
            RewardDefenseSystem = string.IsNullOrWhiteSpace(_cmbDefenseSys.SelectedItem?.ToString()) ? null : _cmbDefenseSys.SelectedItem.ToString(),
            RequiredResources = new Dictionary<string, int>(_requiredResources),
            Dialogs = _dialogs.Select(d => new QuestDialog
            {
                Id = d.Id,
                Text = d.Text,
                Speaker = d.Speaker,
                Trigger = d.Trigger
            }).ToList()
        };
        DialogResult = DialogResult.OK;
        Close();
    }
}

public class RewardEntryDialog : Form
{
    private readonly ComboBox _cmbType, _cmbValue;
    private readonly NumericUpDown _numCredits;
    private readonly Panel _pnlValue;

    public string RewardType => _cmbType.SelectedItem?.ToString() ?? "Credits";
    public string RewardValue
    {
        get
        {
            if (RewardType == "Credits") return ((int)_numCredits.Value).ToString();
            return _cmbValue.SelectedItem?.ToString() ?? "";
        }
    }

    public RewardEntryDialog(List<string> upgradeIds, List<string> equipmentIds, List<string> resourceIds,
        List<string> systemIds, HashSet<string> existing)
    {
        Text = "Add Reward";
        StartPosition = FormStartPosition.CenterParent;
        ClientSize = new Size(320, 160);
        FormBorderStyle = FormBorderStyle.FixedDialog;

        int y = 12;
        new Label { Text = "Type:", Location = new(12, y + 3), Size = new(60, 20) }.AddTo(this);
        _cmbType = new ComboBox { Location = new(80, y), Size = new(200, 24), DropDownStyle = ComboBoxStyle.DropDownList };
        _cmbType.Items.AddRange(["Credits", "Upgrade", "Equipment"]);
        _cmbType.SelectedIndex = 0;
        _cmbType.AddTo(this);
        y += 28;

        new Label { Text = "Value:", Location = new(12, y + 3), Size = new(60, 20) }.AddTo(this);
        _pnlValue = new Panel { Location = new(80, y), Size = new(200, 24) };
        _pnlValue.AddTo(this);

        _numCredits = new NumericUpDown { Location = new(0, 0), Size = new(120, 24), Maximum = 999999 };
        _numCredits.AddTo(_pnlValue);

        _cmbValue = new ComboBox { Location = new(0, 0), Size = new(200, 24), DropDownStyle = ComboBoxStyle.DropDownList };
        _cmbValue.AddTo(_pnlValue);
        _cmbValue.Visible = false;
        y += 32;

        var btnOk = new Button { Text = "OK", Location = new(120, y), Size = new(80, 30), DialogResult = DialogResult.OK };
        btnOk.AddTo(this);
        btnOk.Click += (_, _) =>
        {
            if (RewardType != "Credits" && string.IsNullOrWhiteSpace(RewardValue))
            {
                MessageBox.Show("Please select a value.");
                DialogResult = DialogResult.None;
                return;
            }
            string key = $"{RewardType}:{RewardValue}";
            if (existing.Contains(key))
            {
                MessageBox.Show("This reward has already been added.");
                DialogResult = DialogResult.None;
                return;
            }
        };

        var btnCancel = new Button { Text = "Cancel", Location = new(205, y), Size = new(80, 30), DialogResult = DialogResult.Cancel };
        btnCancel.AddTo(this);

        _cmbType.SelectedIndexChanged += (_, _) =>
        {
            bool isCredits = RewardType == "Credits";
            _numCredits.Visible = isCredits;
            _cmbValue.Visible = !isCredits;
            _cmbValue.Items.Clear();
            _cmbValue.Items.Add("");
            if (!isCredits)
            {
                var items = RewardType switch
                {
                    "Upgrade" => upgradeIds,
                    "Equipment" => equipmentIds,
                    _ => new List<string>()
                };
                foreach (var item in items) _cmbValue.Items.Add(item);
            }
            _cmbValue.SelectedIndex = 0;
        };
    }
}

public class ResourceKeyDialog : Form
{
    private readonly TextBox _txtId;
    private readonly NumericUpDown _numQty;
    public string ResourceId => _txtId.Text.Trim();
    public int Quantity => (int)_numQty.Value;

    public ResourceKeyDialog(IEnumerable<string> existingKeys)
    {
        Text = "Add Required Resource";
        StartPosition = FormStartPosition.CenterParent;
        ClientSize = new Size(300, 140);
        FormBorderStyle = FormBorderStyle.FixedDialog;

        new Label { Text = "Resource ID:", Location = new(12, 15), Size = new(80, 20) }.AddTo(this);
        _txtId = new TextBox { Location = new(100, 12), Size = new(180, 24) }; _txtId.AddTo(this);

        new Label { Text = "Quantity:", Location = new(12, 45), Size = new(80, 20) }.AddTo(this);
        _numQty = new NumericUpDown { Location = new(100, 42), Size = new(100, 24), Maximum = 9999, Minimum = 1, Value = 1 };
        _numQty.AddTo(this);

        var btnOk = new Button { Text = "OK", Location = new(120, 75), Size = new(80, 30), DialogResult = DialogResult.OK };
        btnOk.AddTo(this);
        btnOk.Click += (_, _) => { if (string.IsNullOrWhiteSpace(_txtId.Text)) { MessageBox.Show("ID required."); DialogResult = DialogResult.None; } };

        var btnCancel = new Button { Text = "Cancel", Location = new(205, 75), Size = new(80, 30), DialogResult = DialogResult.Cancel };
        btnCancel.AddTo(this);
    }
}
