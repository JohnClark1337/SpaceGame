using SpaceGameEditor.Models;

namespace SpaceGameEditor.Dialogs;

public class ResourceEditorDialog : Form
{
    private readonly TextBox _txtId, _txtName, _txtSymbol, _txtCategory, _txtDescription;
    private readonly NumericUpDown _numPrice, _numVolume;
    private readonly Button _btnOk, _btnCancel;
    private readonly Label _lblError;

    public ResourceDef? Resource { get; private set; }

    public ResourceEditorDialog(ResourceDef? existing = null)
    {
        Text = existing != null ? "Edit Resource" : "Add Resource";
        StartPosition = FormStartPosition.CenterParent;
        ClientSize = new Size(450, 320);
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        MinimizeBox = false;

        int y = 12, labelW = 100, inputW = 300;

        void AddField(string label, out TextBox tb)
        {
            new Label { Text = label, Location = new(12, y + 3), Size = new(labelW, 20) }.AddTo(this);
            tb = new TextBox { Location = new(120, y), Size = new(inputW, 24) }; tb.AddTo(this);
            y += 28;
        }

        AddField("ID:", out _txtId);
        AddField("Name:", out _txtName);
        AddField("Symbol:", out _txtSymbol);
        AddField("Category:", out _txtCategory);
        AddField("Description:", out _txtDescription);

        new Label { Text = "Base Price:", Location = new(12, y + 3), Size = new(labelW, 20) }.AddTo(this);
        _numPrice = new NumericUpDown { Location = new(120, y), Size = new(140, 24), Maximum = 99999 };
        _numPrice.AddTo(this);
        y += 28;

        new Label { Text = "Volume:", Location = new(12, y + 3), Size = new(labelW, 20) }.AddTo(this);
        _numVolume = new NumericUpDown { Location = new(120, y), Size = new(140, 24), Maximum = 99, Minimum = 1, Value = 1 };
        _numVolume.AddTo(this);
        y += 32;

        _lblError = new Label { Text = "", ForeColor = Color.Red, Location = new(12, y), Size = new(400, 20) };
        _lblError.AddTo(this);
        y += 24;

        _btnOk = new Button { Text = "OK", Location = new(250, y), Size = new(80, 30), DialogResult = DialogResult.OK };
        _btnOk.AddTo(this);
        _btnOk.Click += (_, _) => ValidateAndClose();

        _btnCancel = new Button { Text = "Cancel", Location = new(340, y), Size = new(80, 30), DialogResult = DialogResult.Cancel };
        _btnCancel.AddTo(this);

        if (existing != null)
        {
            _txtId.Text = existing.Id;
            _txtName.Text = existing.Name;
            _txtSymbol.Text = existing.Symbol;
            _txtCategory.Text = existing.Category;
            _txtDescription.Text = existing.Description;
            _numPrice.Value = existing.BasePrice;
            _numVolume.Value = existing.Volume;
        }
    }

    private void ValidateAndClose()
    {
        if (string.IsNullOrWhiteSpace(_txtId.Text))
        {
            _lblError.Text = "ID is required.";
            return;
        }
        if (string.IsNullOrWhiteSpace(_txtName.Text))
        {
            _lblError.Text = "Name is required.";
            return;
        }
        Resource = new ResourceDef
        {
            Id = _txtId.Text.Trim(),
            Name = _txtName.Text.Trim(),
            Symbol = _txtSymbol.Text.Trim(),
            Category = _txtCategory.Text.Trim(),
            Description = _txtDescription.Text.Trim(),
            BasePrice = (int)_numPrice.Value,
            Volume = (int)_numVolume.Value
        };
        DialogResult = DialogResult.OK;
        Close();
    }
}
