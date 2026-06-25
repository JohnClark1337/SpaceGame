using SpaceGameEditor;

string dataPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "..\\..\\..\\..\\Data");
if (!Directory.Exists(dataPath))
    dataPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Data");

var data = new DataManager(dataPath);
if (!data.LoadAll())
{
    MessageBox.Show("Failed to load game data. Make sure Data/ folder is accessible.", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
    return;
}

ApplicationConfiguration.Initialize();
Application.Run(new MainForm(data));
