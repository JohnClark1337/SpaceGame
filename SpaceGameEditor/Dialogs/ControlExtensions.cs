namespace SpaceGameEditor.Dialogs;

public static class ControlExtensions
{
    public static void AddTo(this Control control, Control parent)
    {
        parent.Controls.Add(control);
    }
}
