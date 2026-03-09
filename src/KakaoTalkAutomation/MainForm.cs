using System.Data;

namespace KakaoTalkAutomation;

public sealed class MainForm : Form
{
    private readonly TextBox _hostTextBox = new() { PlaceholderText = "127.0.0.1" };
    private readonly NumericUpDown _portInput = new() { Minimum = 1, Maximum = 65535, Value = 5432 };
    private readonly TextBox _databaseTextBox = new();
    private readonly TextBox _usernameTextBox = new();
    private readonly TextBox _passwordTextBox = new() { UseSystemPasswordChar = true };
    private readonly TextBox _searchPathTextBox = new();
    private readonly CheckBox _sslCheckBox = new() { Text = "SSL Require" };
    private readonly TextBox _queryTextBox = new()
    {
        Multiline = true,
        ScrollBars = ScrollBars.Both,
        AcceptsTab = true,
        Height = 90
    };
    private readonly DataGridView _resultGrid = new()
    {
        Dock = DockStyle.Fill,
        ReadOnly = true,
        AllowUserToAddRows = false,
        AllowUserToDeleteRows = false,
        AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.DisplayedCells
    };
    private readonly TextBox _roomNameTextBox = new();
    private readonly TextBox _messageTextBox = new()
    {
        Multiline = true,
        ScrollBars = ScrollBars.Vertical,
        Height = 90
    };
    private readonly Label _statusLabel = new() { Dock = DockStyle.Fill, AutoEllipsis = true, TextAlign = ContentAlignment.MiddleLeft };

    private ClientSettings _settings = new();

    public MainForm()
    {
        Text = "KakaoTalk Automation Client";
        MinimumSize = new Size(1100, 760);
        StartPosition = FormStartPosition.CenterScreen;

        BuildLayout();
        LoadSettingsIntoForm();
    }

    private void BuildLayout()
    {
        var root = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 4,
            Padding = new Padding(12)
        };
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));

        root.Controls.Add(BuildConnectionGroup(), 0, 0);
        root.Controls.Add(BuildQueryGroup(), 0, 1);
        root.Controls.Add(BuildResultGroup(), 0, 2);
        root.Controls.Add(BuildSendGroup(), 0, 3);

        Controls.Add(root);
    }

    private Control BuildConnectionGroup()
    {
        var group = new GroupBox
        {
            Text = "PostgreSQL Settings",
            Dock = DockStyle.Top,
            AutoSize = true
        };

        var layout = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 4,
            AutoSize = true,
            Padding = new Padding(12)
        };

        for (var i = 0; i < 4; i++)
            layout.ColumnStyles.Add(new ColumnStyle(i % 2 == 0 ? SizeType.AutoSize : SizeType.Percent, 50));

        AddLabeledControl(layout, 0, "Host", _hostTextBox);
        AddLabeledControl(layout, 0, "Port", _portInput, 2);
        AddLabeledControl(layout, 1, "Database", _databaseTextBox);
        AddLabeledControl(layout, 1, "Username", _usernameTextBox, 2);
        AddLabeledControl(layout, 2, "Password", _passwordTextBox);
        AddLabeledControl(layout, 2, "Search Path", _searchPathTextBox, 2);

        var buttonPanel = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            AutoSize = true,
            FlowDirection = FlowDirection.LeftToRight,
            Margin = new Padding(3, 10, 3, 0)
        };

        var saveButton = new Button { Text = "Save Settings", AutoSize = true };
        saveButton.Click += (_, _) => SaveSettings();

        var testButton = new Button { Text = "Test Connection", AutoSize = true };
        testButton.Click += async (_, _) => await TestConnectionAsync();

        buttonPanel.Controls.Add(saveButton);
        buttonPanel.Controls.Add(testButton);
        buttonPanel.Controls.Add(_sslCheckBox);

        layout.Controls.Add(buttonPanel, 0, 3);
        layout.SetColumnSpan(buttonPanel, 4);

        group.Controls.Add(layout);
        return group;
    }

    private Control BuildQueryGroup()
    {
        var group = new GroupBox
        {
            Text = "DB Query",
            Dock = DockStyle.Top,
            AutoSize = true
        };

        var panel = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            RowCount = 2,
            ColumnCount = 1,
            AutoSize = true,
            Padding = new Padding(12)
        };

        panel.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        panel.RowStyles.Add(new RowStyle(SizeType.AutoSize));

        panel.Controls.Add(_queryTextBox, 0, 0);

        var buttons = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            AutoSize = true,
            FlowDirection = FlowDirection.LeftToRight
        };

        var runButton = new Button { Text = "Run Query", AutoSize = true };
        runButton.Click += async (_, _) => await RunQueryAsync();

        var nowButton = new Button { Text = "Load Now() Query", AutoSize = true };
        nowButton.Click += (_, _) => _queryTextBox.Text = "select now() as server_time;";

        buttons.Controls.Add(runButton);
        buttons.Controls.Add(nowButton);
        panel.Controls.Add(buttons, 0, 1);

        group.Controls.Add(panel);
        return group;
    }

    private Control BuildResultGroup()
    {
        var group = new GroupBox
        {
            Text = "Query Result",
            Dock = DockStyle.Fill,
            Padding = new Padding(12)
        };

        group.Controls.Add(_resultGrid);
        return group;
    }

    private Control BuildSendGroup()
    {
        var group = new GroupBox
        {
            Text = "KakaoTalk Send",
            Dock = DockStyle.Bottom,
            AutoSize = true
        };

        var layout = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 2,
            RowCount = 4,
            AutoSize = true,
            Padding = new Padding(12)
        };

        layout.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));

        layout.Controls.Add(new Label { Text = "Room Name", AutoSize = true, Anchor = AnchorStyles.Left }, 0, 0);
        layout.Controls.Add(_roomNameTextBox, 1, 0);
        layout.Controls.Add(new Label { Text = "Message", AutoSize = true, Anchor = AnchorStyles.Left }, 0, 1);
        layout.Controls.Add(_messageTextBox, 1, 1);

        var sendButton = new Button { Text = "Send Message", AutoSize = true };
        sendButton.Click += async (_, _) => await SendMessageAsync();

        layout.Controls.Add(sendButton, 1, 2);
        layout.Controls.Add(_statusLabel, 1, 3);

        group.Controls.Add(layout);
        return group;
    }

    private void AddLabeledControl(TableLayoutPanel layout, int row, string label, Control control, int column = 0)
    {
        var labelControl = new Label
        {
            Text = label,
            AutoSize = true,
            Anchor = AnchorStyles.Left,
            Margin = new Padding(3, 8, 6, 3)
        };

        layout.Controls.Add(labelControl, column, row);
        control.Dock = DockStyle.Fill;
        control.Margin = new Padding(3, 3, 12, 3);
        layout.Controls.Add(control, column + 1, row);
    }

    private void LoadSettingsIntoForm()
    {
        _settings = SettingsStore.Load();

        _hostTextBox.Text = _settings.Postgres.Host;
        _portInput.Value = _settings.Postgres.Port is > 0 and <= 65535 ? _settings.Postgres.Port : 5432;
        _databaseTextBox.Text = _settings.Postgres.Database;
        _usernameTextBox.Text = _settings.Postgres.Username;
        _passwordTextBox.Text = _settings.Postgres.Password;
        _searchPathTextBox.Text = _settings.Postgres.SearchPath;
        _sslCheckBox.Checked = _settings.Postgres.SslModeRequire;
        _queryTextBox.Text = string.IsNullOrWhiteSpace(_settings.DefaultQuery)
            ? "select now() as server_time;"
            : _settings.DefaultQuery;

        SetStatus($"Settings loaded from {SettingsStore.SettingsPath}");
    }

    private ClientSettings ReadSettingsFromForm()
    {
        return new ClientSettings
        {
            Postgres = new PostgresSettings
            {
                Host = _hostTextBox.Text.Trim(),
                Port = (int)_portInput.Value,
                Database = _databaseTextBox.Text.Trim(),
                Username = _usernameTextBox.Text.Trim(),
                Password = _passwordTextBox.Text,
                SearchPath = _searchPathTextBox.Text.Trim(),
                SslModeRequire = _sslCheckBox.Checked
            },
            DefaultQuery = _queryTextBox.Text
        };
    }

    private void SaveSettings()
    {
        _settings = ReadSettingsFromForm();
        SettingsStore.Save(_settings);
        SetStatus("Settings saved.");
    }

    private async Task TestConnectionAsync()
    {
        try
        {
            _settings = ReadSettingsFromForm();
            SetStatus("Testing PostgreSQL connection...");
            await PostgresClient.TestConnectionAsync(_settings.Postgres);
            SetStatus("PostgreSQL connection succeeded.");
        }
        catch (Exception ex)
        {
            SetStatus($"Connection failed: {ex.Message}");
        }
    }

    private async Task RunQueryAsync()
    {
        try
        {
            _settings = ReadSettingsFromForm();
            if (string.IsNullOrWhiteSpace(_queryTextBox.Text))
            {
                SetStatus("Query is empty.");
                return;
            }

            SetStatus("Running query...");
            var table = await PostgresClient.ExecuteQueryAsync(_settings.Postgres, _queryTextBox.Text);
            _resultGrid.DataSource = table;
            SetStatus($"Query completed. {table.Rows.Count} row(s) loaded.");
        }
        catch (Exception ex)
        {
            _resultGrid.DataSource = null;
            SetStatus($"Query failed: {ex.Message}");
        }
    }

    private async Task SendMessageAsync()
    {
        var roomName = _roomNameTextBox.Text.Trim();
        var message = _messageTextBox.Text;

        if (string.IsNullOrWhiteSpace(roomName) || string.IsNullOrWhiteSpace(message))
        {
            SetStatus("Room name and message are required.");
            return;
        }

        SetStatus("Sending message to KakaoTalk...");
        var ok = await Task.Run(() => MessageSender.Send(roomName, message));
        SetStatus(ok
            ? "Message sent."
            : "Send failed. Check KakaoTalk main window state and popup settings.");
    }

    private void SetStatus(string message)
    {
        _statusLabel.Text = $"{DateTime.Now:HH:mm:ss}  {message}";
    }
}
