using SpaceGameEditor.Models;

namespace SpaceGameEditor.Dialogs;

public class SystemEditorDialog : Form
{
    private TextBox _txtId = null!, _txtName = null!, _txtDesc = null!, _txtColor = null!;
    private ComboBox _cmbFaction = null!;
    private NumericUpDown _numX = null!, _numY = null!, _numRadius = null!, _numStarRadius = null!, _numHostility = null!;
    private CheckedListBox _lstServices = null!;
    private TextBox _txtStationName = null!;
    private NumericUpDown _numStationOrbitRadius = null!, _numStationOrbitSpeed = null!, _numStationDefense = null!;
    private DataGridView _dgvProduction = null!, _dgvDemand = null!;
    private Label _lblError = null!;
    private readonly List<string> _allSystemIds;
    private readonly List<string> _connectionIds = new();
    private readonly List<PlanetData> _planets = new();

    public StarSystemData? System { get; private set; }

    public SystemEditorDialog(List<string> allSystemIds, StarSystemData? existing = null)
    {
        _allSystemIds = allSystemIds;
        Text = existing != null ? "Edit System" : "Add System";
        StartPosition = FormStartPosition.CenterParent;
        ClientSize = new Size(720, 680);
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        MinimizeBox = false;

        var tabs = new TabControl { Location = new(0, 0), Size = new(720, 640) };
        tabs.AddTo(this);

        var basicTab = new TabPage("Basic") { Size = new(710, 610) };
        tabs.TabPages.Add(basicTab);
        BuildBasicTab(basicTab);

        var econTab = new TabPage("Economy") { Size = new(710, 610) };
        tabs.TabPages.Add(econTab);
        BuildEconomyTab(econTab);

        var stationTab = new TabPage("Station & Planets") { Size = new(710, 610) };
        tabs.TabPages.Add(stationTab);
        BuildStationTab(stationTab);

        int by = 645;
        var btnOk = new Button { Text = "OK", Location = new(520, by), Size = new(80, 30) };
        btnOk.AddTo(this);
        btnOk.Click += (_, _) => ValidateAndClose();

        var btnCancel = new Button { Text = "Cancel", Location = new(610, by), Size = new(80, 30), DialogResult = DialogResult.Cancel };
        btnCancel.AddTo(this);

        if (existing != null) LoadExisting(existing);
    }

    private void BuildBasicTab(TabPage tab)
    {
        int y = 12, labelW = 100;

        AddLabel(tab, "ID:", 12, y, labelW);
        _txtId = AddTextBox(tab, 120, y, 200);
        y += 28;

        AddLabel(tab, "Name:", 12, y, labelW);
        _txtName = AddTextBox(tab, 120, y, 300);
        y += 28;

        AddLabel(tab, "X:", 12, y, labelW);
        _numX = AddNumeric(tab, 120, y, 100, -99999, 99999);
        AddLabel(tab, "Y:", 240, y + 3, 20);
        _numY = AddNumeric(tab, 260, y, 100, -99999, 99999);
        y += 28;

        AddLabel(tab, "Radius:", 12, y, labelW);
        _numRadius = AddNumeric(tab, 120, y, 80, 1, 500, 35);
        AddLabel(tab, "Star Radius:", 220, y + 3, 70);
        _numStarRadius = AddNumeric(tab, 300, y, 100, 1, 9999, 800);
        y += 28;

        AddLabel(tab, "Color (hex):", 12, y, labelW);
        _txtColor = AddTextBox(tab, 120, y, 100);
        y += 28;

        AddLabel(tab, "Hostility:", 12, y, labelW);
        _numHostility = AddNumeric(tab, 120, y, 80, 0, 5);
        y += 28;

        AddLabel(tab, "Faction:", 12, y, labelW);
        _cmbFaction = new ComboBox { Location = new(120, y), Size = new(200, 24), DropDownStyle = ComboBoxStyle.DropDownList };
        _cmbFaction.Items.AddRange(["", "Atlas Federation", "Trigor Empire"]);
        _cmbFaction.AddTo(tab);
        y += 28;

        AddLabel(tab, "Description:", 12, y, labelW);
        _txtDesc = AddTextBox(tab, 120, y, 500, 60, true);
        y += 68;

        AddLabel(tab, "Connections:", 12, y, labelW);
        var connListBox = new ListBox { Location = new(120, y), Size = new(200, 80), SelectionMode = SelectionMode.MultiExtended };
        foreach (var id in _allSystemIds) connListBox.Items.Add(id);
        connListBox.AddTo(tab);

        var btnAddConn = new Button { Text = ">>", Location = new(330, y), Size = new(30, 24) };
        btnAddConn.AddTo(tab);
        btnAddConn.Click += (_, _) =>
        {
            foreach (var item in connListBox.SelectedItems)
            {
                string id = item.ToString()!;
                if (!_connectionIds.Contains(id))
                    _connectionIds.Add(id);
            }
        };

        var connDisplay = new Label { Text = "", Location = new(370, y), Size = new(300, 80), BorderStyle = BorderStyle.FixedSingle };
        connDisplay.AddTo(tab);
        btnAddConn.Click += (_, _) =>
        {
            foreach (var item in connListBox.SelectedItems)
            {
                string id = item.ToString()!;
                if (!_connectionIds.Contains(id))
                    _connectionIds.Add(id);
            }
            connDisplay.Text = string.Join(", ", _connectionIds);
        };

        y += 88;

        AddLabel(tab, "Services:", 12, y, labelW);
        _lstServices = new CheckedListBox { Location = new(120, y), Size = new(200, 80) };
        foreach (var s in new[] { "fuel", "repair", "market", "upgrades" })
            _lstServices.Items.Add(s, false);
        _lstServices.AddTo(tab);

        _lblError = new Label { Text = "", ForeColor = Color.Red, Location = new(12, 560), Size = new(500, 20) };
        _lblError.AddTo(tab);
    }

    private void BuildEconomyTab(TabPage tab)
    {
        int y = 12;
        AddLabel(tab, "Production (resource: rate):", 12, y, 200);
        y += 24;

        _dgvProduction = new DataGridView
        {
            Location = new(12, y), Size = new(320, 200),
            AllowUserToAddRows = true, AllowUserToDeleteRows = true,
            ColumnHeadersHeightSizeMode = DataGridViewColumnHeadersHeightSizeMode.AutoSize,
            AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill
        };
        _dgvProduction.Columns.Add("Resource", "Resource ID");
        _dgvProduction.Columns.Add("Rate", "Daily Rate");
        _dgvProduction.AddTo(tab);

        AddLabel(tab, "Demand (resource: mult):", 350, 12, 200);
        _dgvDemand = new DataGridView
        {
            Location = new(350, y), Size = new(320, 200),
            AllowUserToAddRows = true, AllowUserToDeleteRows = true,
            ColumnHeadersHeightSizeMode = DataGridViewColumnHeadersHeightSizeMode.AutoSize,
            AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill
        };
        _dgvDemand.Columns.Add("Resource", "Resource ID");
        _dgvDemand.Columns.Add("Mult", "Demand Mult");
        _dgvDemand.AddTo(tab);
    }

    private void BuildStationTab(TabPage tab)
    {
        int y = 12;

        AddLabel(tab, "Station Name:", 12, y, 100);
        _txtStationName = AddTextBox(tab, 120, y, 200);
        y += 28;

        AddLabel(tab, "Orbit Radius:", 12, y, 100);
        _numStationOrbitRadius = AddNumeric(tab, 120, y, 100, 0, 99999, 4000);
        y += 28;

        AddLabel(tab, "Orbit Speed:", 12, y, 100);
        _numStationOrbitSpeed = new NumericUpDown { Location = new(120, y), Size = new(100, 24), Maximum = 1, DecimalPlaces = 6, Increment = 0.0001m, Value = 0.001m };
        _numStationOrbitSpeed.AddTo(tab);
        y += 28;

        AddLabel(tab, "Defense Level:", 12, y, 100);
        _numStationDefense = AddNumeric(tab, 120, y, 80, 0, 5);
        y += 40;

        AddLabel(tab, "Planets:", 12, y, 100);
        var lstPlanets = new ListBox { Location = new(12, y + 22), Size = new(300, 120) };
        lstPlanets.AddTo(tab);

        void RefreshPlanetList()
        {
            lstPlanets.Items.Clear();
            foreach (var p in _planets)
                lstPlanets.Items.Add($"{p.Name}  (orbit: {p.OrbitRadius}, r: {p.Radius})");
        }

        var btnAddPlanet = new Button { Text = "Add Planet", Location = new(320, y + 22), Size = new(80, 24) };
        btnAddPlanet.AddTo(tab);
        btnAddPlanet.Click += (_, _) =>
        {
            var dlg = new PlanetEditorDialog();
            if (dlg.ShowDialog(this) == DialogResult.OK && dlg.Planet != null)
            {
                _planets.Add(dlg.Planet);
                RefreshPlanetList();
            }
        };

        var btnEditPlanet = new Button { Text = "Edit", Location = new(320, y + 50), Size = new(80, 24) };
        btnEditPlanet.AddTo(tab);
        btnEditPlanet.Click += (_, _) =>
        {
            if (lstPlanets.SelectedIndex < 0 || lstPlanets.SelectedIndex >= _planets.Count) return;
            var dlg = new PlanetEditorDialog(_planets[lstPlanets.SelectedIndex]);
            if (dlg.ShowDialog(this) == DialogResult.OK && dlg.Planet != null)
            {
                _planets[lstPlanets.SelectedIndex] = dlg.Planet;
                RefreshPlanetList();
            }
        };

        var btnRemovePlanet = new Button { Text = "Remove", Location = new(320, y + 78), Size = new(80, 24) };
        btnRemovePlanet.AddTo(tab);
        btnRemovePlanet.Click += (_, _) =>
        {
            if (lstPlanets.SelectedIndex >= 0 && lstPlanets.SelectedIndex < _planets.Count)
            {
                _planets.RemoveAt(lstPlanets.SelectedIndex);
                RefreshPlanetList();
            }
        };
    }

    private void LoadExisting(StarSystemData s)
    {
        _txtId.Text = s.Id;
        _txtName.Text = s.Name;
        _numX.Value = (decimal)s.X;
        _numY.Value = (decimal)s.Y;
        _numRadius.Value = (decimal)s.Radius;
        _numStarRadius.Value = (decimal)s.StarRadius;
        _txtColor.Text = s.Color;
        _numHostility.Value = s.Hostility;
        if (!string.IsNullOrEmpty(s.Faction)) _cmbFaction.SelectedItem = s.Faction;
        _txtDesc.Text = s.Description;

        _connectionIds.Clear();
        _connectionIds.AddRange(s.Connections);

        for (int i = 0; i < _lstServices.Items.Count; i++)
        {
            string item = _lstServices.Items[i].ToString()!;
            _lstServices.SetItemChecked(i, s.Services.Contains(item));
        }

        if (s.Economy != null)
        {
            foreach (var kv in s.Economy.Production)
                _dgvProduction.Rows.Add(kv.Key, kv.Value);
            foreach (var kv in s.Economy.Demand)
                _dgvDemand.Rows.Add(kv.Key, kv.Value);
        }

        if (s.Station != null)
        {
            _txtStationName.Text = s.Station.Name;
            _numStationOrbitRadius.Value = (decimal)s.Station.OrbitRadius;
            _numStationOrbitSpeed.Value = (decimal)s.Station.OrbitSpeed;
            _numStationDefense.Value = s.Station.DefenseLevel;
        }

        _planets.Clear();
        _planets.AddRange(s.Planets);
    }

    private void ValidateAndClose()
    {
        if (string.IsNullOrWhiteSpace(_txtId.Text)) { _lblError.Text = "ID is required."; return; }
        if (string.IsNullOrWhiteSpace(_txtName.Text)) { _lblError.Text = "Name is required."; return; }

        var services = new List<string>();
        foreach (var item in _lstServices.CheckedItems)
            services.Add(item.ToString()!);

        var production = new Dictionary<string, float>();
        foreach (DataGridViewRow row in _dgvProduction.Rows)
        {
            if (row.IsNewRow) continue;
            var id = row.Cells[0].Value?.ToString();
            if (string.IsNullOrEmpty(id)) continue;
            production[id] = float.TryParse(row.Cells[1].Value?.ToString(), out float r) ? r : 0;
        }

        var demand = new Dictionary<string, float>();
        foreach (DataGridViewRow row in _dgvDemand.Rows)
        {
            if (row.IsNewRow) continue;
            var id = row.Cells[0].Value?.ToString();
            if (string.IsNullOrEmpty(id)) continue;
            demand[id] = float.TryParse(row.Cells[1].Value?.ToString(), out float r) ? r : 0;
        }

        System = new StarSystemData
        {
            Id = _txtId.Text.Trim(),
            Name = _txtName.Text.Trim(),
            X = (float)_numX.Value,
            Y = (float)_numY.Value,
            Radius = (float)_numRadius.Value,
            Color = _txtColor.Text.Trim(),
            Description = _txtDesc.Text.Trim(),
            Connections = new List<string>(_connectionIds),
            Services = services,
            Hostility = (int)_numHostility.Value,
            Faction = string.IsNullOrWhiteSpace(_cmbFaction.SelectedItem?.ToString()) ? null : _cmbFaction.SelectedItem.ToString(),
            StarRadius = (float)_numStarRadius.Value,
            Planets = new List<PlanetData>(_planets),
            Station = new StationData
            {
                Name = string.IsNullOrWhiteSpace(_txtStationName.Text) ? _txtId.Text.Trim() + "_station" : _txtStationName.Text.Trim(),
                OrbitRadius = (float)_numStationOrbitRadius.Value,
                OrbitSpeed = (float)_numStationOrbitSpeed.Value,
                DefenseLevel = (int)_numStationDefense.Value
            },
            Economy = (production.Count > 0 || demand.Count > 0) ? new EconomyData
            {
                Production = production,
                Demand = demand
            } : null
        };
        DialogResult = DialogResult.OK;
        Close();
    }

    private static Label AddLabel(Control parent, string text, int x, int y, int w)
    {
        var lbl = new Label { Text = text, Location = new(x, y + 3), Size = new(w, 20) };
        parent.Controls.Add(lbl);
        return lbl;
    }

    private static TextBox AddTextBox(Control parent, int x, int y, int w, int h = 24, bool multi = false)
    {
        var tb = new TextBox { Location = new(x, y), Size = new(w, h), Multiline = multi };
        parent.Controls.Add(tb);
        return tb;
    }

    private static NumericUpDown AddNumeric(Control parent, int x, int y, int w, decimal min, decimal max, decimal val = 0)
    {
        var n = new NumericUpDown { Location = new(x, y), Size = new(w, 24), Minimum = min, Maximum = max, Value = val };
        parent.Controls.Add(n);
        return n;
    }
}

public class PlanetEditorDialog : Form
{
    private readonly TextBox _txtName = null!;
    private readonly TextBox _txtColor = null!;
    private readonly NumericUpDown _numOrbitRadius = null!;
    private readonly NumericUpDown _numRadius = null!;
    private readonly NumericUpDown _numOrbitSpeed = null!;
    public PlanetData? Planet { get; private set; }

    public PlanetEditorDialog(PlanetData? existing = null)
    {
        Text = existing != null ? "Edit Planet" : "Add Planet";
        StartPosition = FormStartPosition.CenterParent;
        ClientSize = new Size(300, 200);
        FormBorderStyle = FormBorderStyle.FixedDialog;

        int y = 12;
        new Label { Text = "Name:", Location = new(12, y + 3), Size = new(80, 20) }.AddTo(this);
        _txtName = new TextBox { Location = new(100, y), Size = new(180, 24) }; _txtName.AddTo(this);
        y += 28;

        new Label { Text = "Orbit Radius:", Location = new(12, y + 3), Size = new(80, 20) }.AddTo(this);
        _numOrbitRadius = new NumericUpDown { Location = new(100, y), Size = new(100, 24), Maximum = 99999, Value = 6000 };
        _numOrbitRadius.AddTo(this);
        y += 28;

        new Label { Text = "Radius:", Location = new(12, y + 3), Size = new(80, 20) }.AddTo(this);
        _numRadius = new NumericUpDown { Location = new(100, y), Size = new(100, 24), Maximum = 9999, Value = 800 };
        _numRadius.AddTo(this);
        y += 28;

        new Label { Text = "Color (hex):", Location = new(12, y + 3), Size = new(80, 20) }.AddTo(this);
        _txtColor = new TextBox { Location = new(100, y), Size = new(100, 24) }; _txtColor.AddTo(this);
        y += 28;

        new Label { Text = "Orbit Speed:", Location = new(12, y + 3), Size = new(80, 20) }.AddTo(this);
        _numOrbitSpeed = new NumericUpDown { Location = new(100, y), Size = new(100, 24), Maximum = 1, DecimalPlaces = 4, Increment = 0.001m, Value = 0.01m };
        _numOrbitSpeed.AddTo(this);
        y += 32;

        var btnOk = new Button { Text = "OK", Location = new(120, y), Size = new(80, 30) };
        btnOk.AddTo(this);
        btnOk.Click += (_, _) => { Planet = new PlanetData { Name = _txtName.Text.Trim(), OrbitRadius = (float)_numOrbitRadius.Value, Radius = (float)_numRadius.Value, Color = _txtColor.Text.Trim(), OrbitSpeed = (float)_numOrbitSpeed.Value }; DialogResult = DialogResult.OK; Close(); };

        var btnCancel = new Button { Text = "Cancel", Location = new(205, y), Size = new(80, 30), DialogResult = DialogResult.Cancel };
        btnCancel.AddTo(this);

        if (existing != null)
        {
            _txtName.Text = existing.Name;
            _numOrbitRadius.Value = (decimal)existing.OrbitRadius;
            _numRadius.Value = (decimal)existing.Radius;
            _txtColor.Text = existing.Color;
            _numOrbitSpeed.Value = (decimal)existing.OrbitSpeed;
        }
    }
}
