using SpaceGameEditor.Models;

namespace SpaceGameEditor.Dialogs;

public class QuestEditorDialog : Form
{
    private readonly TextBox _txtId, _txtName, _txtDesc, _txtTargetSys, _txtTargetItem, _txtGiver, _txtDefenseSys;
    private readonly ComboBox _cmbObjType;
    private readonly NumericUpDown _numTargetCount, _numRewardCredits;
    private readonly TextBox _txtRewardUpgrade, _txtRewardEquip;
    private readonly ListBox _lstReqResources;
    private readonly Button _btnAddReq, _btnRemoveReq, _btnOk, _btnCancel;
    private readonly Label _lblError;
    private Dictionary<string, int> _requiredResources = new();

    public QuestData? Quest { get; private set; }

    public QuestEditorDialog(List<string> existingIds, QuestData? existing = null)
    {
        Text = existing != null ? "Edit Quest" : "Add Quest";
        StartPosition = FormStartPosition.CenterParent;
        ClientSize = new Size(520, 520);
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
        _txtTargetSys = new TextBox { Location = new(140, y), Size = new(inputW, 24) }; _txtTargetSys.AddTo(this);
        y += 28;

        new Label { Text = "Target Item:", Location = new(12, y + 3), Size = new(labelW, 20) }.AddTo(this);
        _txtTargetItem = new TextBox { Location = new(140, y), Size = new(inputW, 24) }; _txtTargetItem.AddTo(this);
        y += 28;

        new Label { Text = "Target Count:", Location = new(12, y + 3), Size = new(labelW, 20) }.AddTo(this);
        _numTargetCount = new NumericUpDown { Location = new(140, y), Size = new(100, 24), Maximum = 999, Minimum = 1, Value = 1 };
        _numTargetCount.AddTo(this);
        y += 28;

        new Label { Text = "Reward Credits:", Location = new(12, y + 3), Size = new(labelW, 20) }.AddTo(this);
        _numRewardCredits = new NumericUpDown { Location = new(140, y), Size = new(100, 24), Maximum = 999999 };
        _numRewardCredits.AddTo(this);
        y += 28;

        new Label { Text = "Giver System:", Location = new(12, y + 3), Size = new(labelW, 20) }.AddTo(this);
        _txtGiver = new TextBox { Location = new(140, y), Size = new(inputW, 24) }; _txtGiver.AddTo(this);
        y += 28;

        new Label { Text = "Reward Upgrade:", Location = new(12, y + 3), Size = new(labelW, 20) }.AddTo(this);
        _txtRewardUpgrade = new TextBox { Location = new(140, y), Size = new(inputW, 24) }; _txtRewardUpgrade.AddTo(this);
        y += 28;

        new Label { Text = "Reward Equipment:", Location = new(12, y + 3), Size = new(labelW, 20) }.AddTo(this);
        _txtRewardEquip = new TextBox { Location = new(140, y), Size = new(inputW, 24) }; _txtRewardEquip.AddTo(this);
        y += 28;

        new Label { Text = "Defense System:", Location = new(12, y + 3), Size = new(labelW, 20) }.AddTo(this);
        _txtDefenseSys = new TextBox { Location = new(140, y), Size = new(inputW, 24) }; _txtDefenseSys.AddTo(this);
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

        _lblError = new Label { Text = "", ForeColor = Color.Red, Location = new(12, y), Size = new(480, 20) };
        _lblError.AddTo(this);
        y += 24;

        _btnOk = new Button { Text = "OK", Location = new(320, y), Size = new(80, 30), DialogResult = DialogResult.OK };
        _btnOk.AddTo(this);
        _btnOk.Click += (_, _) => ValidateAndClose();

        _btnCancel = new Button { Text = "Cancel", Location = new(410, y), Size = new(80, 30), DialogResult = DialogResult.Cancel };
        _btnCancel.AddTo(this);

        _btnAddReq.Click += (_, _) => AddResource();
        _btnRemoveReq.Click += (_, _) => RemoveResource();

        if (existing != null)
        {
            _txtId.Text = existing.Id;
            _txtName.Text = existing.Name;
            _txtDesc.Text = existing.Description;
            _cmbObjType.SelectedItem = existing.ObjectiveType;
            _txtTargetSys.Text = existing.TargetSystem ?? "";
            _txtTargetItem.Text = existing.TargetItem ?? "";
            _numTargetCount.Value = existing.TargetCount;
            _numRewardCredits.Value = existing.RewardCredits;
            _txtGiver.Text = existing.GiverSystem;
            _txtRewardUpgrade.Text = existing.RewardUpgrade ?? "";
            _txtRewardEquip.Text = existing.RewardEquipment ?? "";
            _txtDefenseSys.Text = existing.RewardDefenseSystem ?? "";
            _requiredResources = new Dictionary<string, int>(existing.RequiredResources);
            RefreshReqList();
        }
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

    private void ValidateAndClose()
    {
        if (string.IsNullOrWhiteSpace(_txtId.Text)) { _lblError.Text = "ID is required."; return; }
        if (string.IsNullOrWhiteSpace(_txtName.Text)) { _lblError.Text = "Name is required."; return; }
        Quest = new QuestData
        {
            Id = _txtId.Text.Trim(),
            Name = _txtName.Text.Trim(),
            Description = _txtDesc.Text.Trim(),
            ObjectiveType = _cmbObjType.SelectedItem?.ToString() ?? "travel",
            TargetSystem = string.IsNullOrWhiteSpace(_txtTargetSys.Text) ? null : _txtTargetSys.Text.Trim(),
            TargetItem = string.IsNullOrWhiteSpace(_txtTargetItem.Text) ? null : _txtTargetItem.Text.Trim(),
            TargetCount = (int)_numTargetCount.Value,
            RewardCredits = (int)_numRewardCredits.Value,
            GiverSystem = _txtGiver.Text.Trim(),
            RewardUpgrade = string.IsNullOrWhiteSpace(_txtRewardUpgrade.Text) ? null : _txtRewardUpgrade.Text.Trim(),
            RewardEquipment = string.IsNullOrWhiteSpace(_txtRewardEquip.Text) ? null : _txtRewardEquip.Text.Trim(),
            RewardDefenseSystem = string.IsNullOrWhiteSpace(_txtDefenseSys.Text) ? null : _txtDefenseSys.Text.Trim(),
            RequiredResources = new Dictionary<string, int>(_requiredResources)
        };
        DialogResult = DialogResult.OK;
        Close();
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
