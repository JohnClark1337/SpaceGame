using SpaceGameEditor.Models;

namespace SpaceGameEditor.Dialogs;

public class UpgradeEditorDialog : Form
{
    private readonly TextBox _txtId, _txtName, _txtDesc, _txtEffectType, _txtLocation;
    private readonly NumericUpDown _numEffectValue, _numCost;
    private readonly Label _lblError;

    public UpgradeData? Upgrade { get; private set; }

    public UpgradeEditorDialog(UpgradeData? existing = null)
    {
        Text = existing != null ? "Edit Upgrade" : "Add Upgrade";
        StartPosition = FormStartPosition.CenterParent;
        ClientSize = new Size(450, 310);
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        MinimizeBox = false;

        int y = 12, labelW = 120, inputW = 280;

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

        new Label { Text = "Effect Type:", Location = new(12, y + 3), Size = new(labelW, 20) }.AddTo(this);
        _txtEffectType = new TextBox { Location = new(140, y), Size = new(inputW, 24) }; _txtEffectType.AddTo(this);
        y += 28;

        new Label { Text = "Effect Value:", Location = new(12, y + 3), Size = new(labelW, 20) }.AddTo(this);
        _numEffectValue = new NumericUpDown { Location = new(140, y), Size = new(120, 24), Maximum = 99999, DecimalPlaces = 2 };
        _numEffectValue.AddTo(this);
        y += 28;

        new Label { Text = "Cost:", Location = new(12, y + 3), Size = new(labelW, 20) }.AddTo(this);
        _numCost = new NumericUpDown { Location = new(140, y), Size = new(120, 24), Maximum = 999999 };
        _numCost.AddTo(this);
        y += 28;

        new Label { Text = "Location (system):", Location = new(12, y + 3), Size = new(labelW, 20) }.AddTo(this);
        _txtLocation = new TextBox { Location = new(140, y), Size = new(inputW, 24) }; _txtLocation.AddTo(this);
        y += 32;

        _lblError = new Label { Text = "", ForeColor = Color.Red, Location = new(12, y), Size = new(400, 20) };
        _lblError.AddTo(this);
        y += 24;

        var btnOk = new Button { Text = "OK", Location = new(260, y), Size = new(80, 30) };
        btnOk.AddTo(this);
        btnOk.Click += (_, _) => ValidateAndClose();

        var btnCancel = new Button { Text = "Cancel", Location = new(350, y), Size = new(80, 30), DialogResult = DialogResult.Cancel };
        btnCancel.AddTo(this);

        if (existing != null)
        {
            _txtId.Text = existing.Id;
            _txtName.Text = existing.Name;
            _txtDesc.Text = existing.Description;
            _txtEffectType.Text = existing.EffectType;
            _numEffectValue.Value = (decimal)(double)existing.EffectValue;
            _numCost.Value = existing.Cost;
            _txtLocation.Text = existing.Location;
        }
    }

    private void ValidateAndClose()
    {
        if (string.IsNullOrWhiteSpace(_txtId.Text)) { _lblError.Text = "ID is required."; return; }
        if (string.IsNullOrWhiteSpace(_txtName.Text)) { _lblError.Text = "Name is required."; return; }
        Upgrade = new UpgradeData
        {
            Id = _txtId.Text.Trim(),
            Name = _txtName.Text.Trim(),
            Description = _txtDesc.Text.Trim(),
            EffectType = _txtEffectType.Text.Trim(),
            EffectValue = (float)(double)_numEffectValue.Value,
            Cost = (int)_numCost.Value,
            Location = _txtLocation.Text.Trim()
        };
        DialogResult = DialogResult.OK;
        Close();
    }
}
