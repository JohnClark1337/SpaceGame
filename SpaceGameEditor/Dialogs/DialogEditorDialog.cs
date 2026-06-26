using SpaceGameEditor.Models;

namespace SpaceGameEditor.Dialogs;

public class DialogEditorDialog : Form
{
    private readonly TextBox _txtId, _txtSpeaker;
    private readonly ComboBox _cmbTrigger;
    private readonly TextBox _txtText;
    private readonly Label _lblError;

    public QuestDialog? Dialog { get; private set; }

    public DialogEditorDialog(QuestDialog? existing = null)
    {
        Text = existing != null ? "Edit Dialog" : "Add Dialog";
        StartPosition = FormStartPosition.CenterParent;
        ClientSize = new Size(480, 340);
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        MinimizeBox = false;

        int y = 12, labelW = 80;

        new Label { Text = "ID:", Location = new(12, y + 3), Size = new(labelW, 20) }.AddTo(this);
        _txtId = new TextBox { Location = new(100, y), Size = new(340, 24) }; _txtId.AddTo(this);
        y += 28;

        new Label { Text = "Speaker:", Location = new(12, y + 3), Size = new(labelW, 20) }.AddTo(this);
        _txtSpeaker = new TextBox { Location = new(100, y), Size = new(340, 24) }; _txtSpeaker.AddTo(this);
        y += 28;

        new Label { Text = "Trigger:", Location = new(12, y + 3), Size = new(labelW, 20) }.AddTo(this);
        _cmbTrigger = new ComboBox { Location = new(100, y), Size = new(160, 24), DropDownStyle = ComboBoxStyle.DropDownList };
        _cmbTrigger.Items.AddRange(["on_accept", "on_complete", "on_enter_system"]);
        _cmbTrigger.SelectedIndex = 0;
        _cmbTrigger.AddTo(this);
        y += 28;

        new Label { Text = "Text:", Location = new(12, y + 3), Size = new(labelW, 20) }.AddTo(this);
        _txtText = new TextBox { Location = new(12, y + 24), Size = new(440, 180), Multiline = true, AcceptsReturn = true };
        _txtText.AddTo(this);
        y += 210;

        _lblError = new Label { Text = "", ForeColor = Color.Red, Location = new(12, y), Size = new(440, 20) };
        _lblError.AddTo(this);
        y += 24;

        var btnOk = new Button { Text = "OK", Location = new(280, y), Size = new(80, 30) };
        btnOk.AddTo(this);
        btnOk.Click += (_, _) => ValidateAndClose();

        var btnCancel = new Button { Text = "Cancel", Location = new(370, y), Size = new(80, 30), DialogResult = DialogResult.Cancel };
        btnCancel.AddTo(this);

        if (existing != null)
        {
            _txtId.Text = existing.Id;
            _txtSpeaker.Text = existing.Speaker;
            _cmbTrigger.SelectedItem = existing.Trigger;
            _txtText.Text = existing.Text;
        }
    }

    private void ValidateAndClose()
    {
        if (string.IsNullOrWhiteSpace(_txtId.Text)) { _lblError.Text = "ID is required."; return; }
        if (string.IsNullOrWhiteSpace(_txtText.Text)) { _lblError.Text = "Text is required."; return; }
        Dialog = new QuestDialog
        {
            Id = _txtId.Text.Trim(),
            Speaker = _txtSpeaker.Text.Trim(),
            Trigger = _cmbTrigger.SelectedItem?.ToString() ?? "on_accept",
            Text = _txtText.Text.Trim()
        };
        DialogResult = DialogResult.OK;
        Close();
    }
}
