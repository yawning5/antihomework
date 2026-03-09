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
    private readonly NumericUpDown _pollIntervalInput = new() { Minimum = 500, Maximum = 60000, Increment = 100, Value = 1000 };
    private readonly NumericUpDown _postSendDelayInput = new() { Minimum = 0, Maximum = 10000, Increment = 100, Value = 300 };
    private readonly TextBox _manualRoomNameTextBox = new();
    private readonly TextBox _manualMessageTextBox = new()
    {
        Multiline = true,
        ScrollBars = ScrollBars.Vertical,
        Height = 70
    };
    private readonly DataGridView _previewGrid = new()
    {
        Dock = DockStyle.Fill,
        ReadOnly = true,
        AllowUserToAddRows = false,
        AllowUserToDeleteRows = false,
        AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.DisplayedCells
    };
    private readonly Label _workerStateLabel = new() { AutoSize = true };
    private readonly Label _lastMessageLabel = new() { AutoSize = true };
    private readonly Label _successCountLabel = new() { AutoSize = true };
    private readonly Label _failureCountLabel = new() { AutoSize = true };
    private readonly Label _statusLabel = new() { Dock = DockStyle.Fill, AutoEllipsis = true, TextAlign = ContentAlignment.MiddleLeft };

    private readonly System.Windows.Forms.Timer _pollTimer = new();
    private readonly ChatOutRepository _repository = new();
    private readonly MessageDispatchService _dispatchService = new(new ChatOutRepository(), new SequenceCompletedConfirmationPolicy());

    private ClientSettings _settings = new();
    private CancellationTokenSource? _workerCts;
    private int _dispatchInProgress;
    private bool _workerRunning;
    private int _successCount;
    private int _failureCount;

    public MainForm()
    {
        Text = "KakaoTalk Automation Client";
        MinimumSize = new Size(1200, 760);
        StartPosition = FormStartPosition.CenterScreen;

        _pollTimer.Tick += async (_, _) => await PollOnceAsync();

        BuildLayout();
        LoadSettingsIntoForm();
    }

    private void BuildLayout()
    {
        var root = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 5,
            Padding = new Padding(12)
        };
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));

        root.Controls.Add(BuildConnectionGroup(), 0, 0);
        root.Controls.Add(BuildWorkerGroup(), 0, 1);
        root.Controls.Add(BuildManualTestGroup(), 0, 2);
        root.Controls.Add(BuildPreviewGroup(), 0, 3);
        root.Controls.Add(_statusLabel, 0, 4);

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

    private Control BuildWorkerGroup()
    {
        var group = new GroupBox
        {
            Text = "Dispatch Worker",
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

        AddLabeledControl(layout, 0, "Poll Interval (ms)", _pollIntervalInput);
        AddLabeledControl(layout, 0, "Post Send Delay (ms)", _postSendDelayInput, 2);

        var buttonPanel = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            AutoSize = true,
            FlowDirection = FlowDirection.LeftToRight,
            Margin = new Padding(3, 10, 3, 0)
        };

        var startButton = new Button { Text = "Start Polling", AutoSize = true };
        startButton.Click += async (_, _) => await StartWorkerAsync();

        var stopButton = new Button { Text = "Stop Polling", AutoSize = true };
        stopButton.Click += (_, _) => StopWorker("Worker stopped.");

        var refreshButton = new Button { Text = "Refresh Preview", AutoSize = true };
        refreshButton.Click += async (_, _) => await RefreshPreviewAsync();

        buttonPanel.Controls.Add(startButton);
        buttonPanel.Controls.Add(stopButton);
        buttonPanel.Controls.Add(refreshButton);

        layout.Controls.Add(buttonPanel, 0, 1);
        layout.SetColumnSpan(buttonPanel, 4);

        var statusPanel = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            AutoSize = true,
            FlowDirection = FlowDirection.TopDown,
            WrapContents = false,
            Margin = new Padding(3, 10, 3, 0)
        };

        statusPanel.Controls.Add(_workerStateLabel);
        statusPanel.Controls.Add(_lastMessageLabel);
        statusPanel.Controls.Add(_successCountLabel);
        statusPanel.Controls.Add(_failureCountLabel);

        layout.Controls.Add(statusPanel, 0, 2);
        layout.SetColumnSpan(statusPanel, 4);

        group.Controls.Add(layout);
        return group;
    }

    private Control BuildPreviewGroup()
    {
        var group = new GroupBox
        {
            Text = "chat_out Preview",
            Dock = DockStyle.Fill,
            Padding = new Padding(12)
        };

        group.Controls.Add(_previewGrid);
        return group;
    }

    private Control BuildManualTestGroup()
    {
        var group = new GroupBox
        {
            Text = "Manual Test",
            Dock = DockStyle.Top,
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
        layout.Controls.Add(_manualRoomNameTextBox, 1, 0);
        layout.Controls.Add(new Label { Text = "Message", AutoSize = true, Anchor = AnchorStyles.Left }, 0, 1);
        layout.Controls.Add(_manualMessageTextBox, 1, 1);

        var buttons = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            AutoSize = true,
            FlowDirection = FlowDirection.LeftToRight
        };

        var ctrlFButton = new Button { Text = "Test Ctrl+F", AutoSize = true };
        ctrlFButton.Click += async (_, _) => await TestCtrlFAsync();

        var sendButton = new Button { Text = "Send Manual Message", AutoSize = true };
        sendButton.Click += async (_, _) => await SendManualMessageAsync();

        buttons.Controls.Add(ctrlFButton);
        buttons.Controls.Add(sendButton);
        layout.Controls.Add(buttons, 1, 2);

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
        _pollIntervalInput.Value = _settings.PollIntervalMs is >= 500 and <= 60000 ? _settings.PollIntervalMs : 1000;
        _postSendDelayInput.Value = _settings.PostSendDelayMs is >= 0 and <= 10000 ? _settings.PostSendDelayMs : 300;

        _workerStateLabel.Text = "Worker State: Stopped";
        _lastMessageLabel.Text = "Last Message: -";
        _successCountLabel.Text = "Success Count: 0";
        _failureCountLabel.Text = "Failure Count: 0";
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
            PollIntervalMs = (int)_pollIntervalInput.Value,
            PostSendDelayMs = (int)_postSendDelayInput.Value
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

    private async Task StartWorkerAsync()
    {
        try
        {
            _settings = ReadSettingsFromForm();
            await PostgresClient.TestConnectionAsync(_settings.Postgres);

            SaveSettings();
            _workerCts?.Cancel();
            _workerCts = new CancellationTokenSource();
            _workerRunning = true;
            _pollTimer.Interval = Math.Max(500, _settings.PollIntervalMs);
            _workerStateLabel.Text = "Worker State: Running";
            SetStatus("Worker started.");
            await RefreshPreviewAsync();
            _pollTimer.Start();
            await PollOnceAsync();
        }
        catch (Exception ex)
        {
            _workerRunning = false;
            _pollTimer.Stop();
            _workerStateLabel.Text = "Worker State: Stopped";
            SetStatus($"Worker start failed: {ex.Message}");
        }
    }

    private void StopWorker(string reason)
    {
        _pollTimer.Stop();
        _workerRunning = false;
        _workerCts?.Cancel();
        _workerCts = null;
        _workerStateLabel.Text = "Worker State: Stopped";
        SetStatus(reason);
    }

    private async Task PollOnceAsync()
    {
        if (!_workerRunning)
            return;

        if (Interlocked.Exchange(ref _dispatchInProgress, 1) == 1)
            return;

        try
        {
            var settings = ReadSettingsFromForm();
            var result = await _dispatchService.DispatchNextAsync(
                settings.Postgres,
                settings.PostSendDelayMs,
                _workerCts?.Token ?? CancellationToken.None);

            switch (result.Outcome)
            {
                case DispatchOutcome.NoWork:
                    SetStatus("No pending message.");
                    break;
                case DispatchOutcome.SentAndDeleted:
                    _successCount++;
                    _successCountLabel.Text = $"Success Count: {_successCount}";
                    _lastMessageLabel.Text = FormatLastMessage(result.Message);
                    SetStatus(result.Detail);
                    break;
                case DispatchOutcome.SendFailed:
                    _failureCount++;
                    _failureCountLabel.Text = $"Failure Count: {_failureCount}";
                    _lastMessageLabel.Text = FormatLastMessage(result.Message);
                    StopWorker(result.Detail);
                    break;
                case DispatchOutcome.DeleteFailed:
                    _failureCount++;
                    _failureCountLabel.Text = $"Failure Count: {_failureCount}";
                    _lastMessageLabel.Text = FormatLastMessage(result.Message);
                    StopWorker(result.Detail);
                    break;
            }

            await RefreshPreviewAsync();
        }
        catch (OperationCanceledException)
        {
            SetStatus("Worker cancelled.");
        }
        catch (Exception ex)
        {
            _failureCount++;
            _failureCountLabel.Text = $"Failure Count: {_failureCount}";
            StopWorker($"Worker error: {ex.Message}");
        }
        finally
        {
            Interlocked.Exchange(ref _dispatchInProgress, 0);
        }
    }

    private async Task RefreshPreviewAsync()
    {
        try
        {
            var settings = ReadSettingsFromForm();
            var table = await _repository.GetPreviewAsync(settings.Postgres, 20);
            _previewGrid.DataSource = table;
        }
        catch (Exception ex)
        {
            _previewGrid.DataSource = null;
            SetStatus($"Preview refresh failed: {ex.Message}");
        }
    }

    private async Task TestCtrlFAsync()
    {
        SetStatus("Running Ctrl+F test...");
        var ok = await Task.Run(MessageSender.TestOpenSearch);
        SetStatus(ok
            ? "Ctrl+F test completed. Verify KakaoTalk search UI visually."
            : "Ctrl+F test failed. Check KakaoTalk main window state.");
    }

    private async Task SendManualMessageAsync()
    {
        var roomName = _manualRoomNameTextBox.Text.Trim();
        var message = _manualMessageTextBox.Text;

        if (string.IsNullOrWhiteSpace(roomName) || string.IsNullOrWhiteSpace(message))
        {
            SetStatus("Manual test requires room name and message.");
            return;
        }

        SetStatus("Running manual send test...");
        var ok = await Task.Run(() => MessageSender.Send(roomName, message));
        SetStatus(ok
            ? "Manual send test completed."
            : "Manual send test failed.");
    }

    private static string FormatLastMessage(ChatOutMessage? message)
    {
        return message is null
            ? "Last Message: -"
            : $"Last Message: msg_id={message.MsgId}, room={message.RoomName}";
    }

    private void SetStatus(string message)
    {
        _statusLabel.Text = $"{DateTime.Now:HH:mm:ss}  {message}";
    }

    protected override void OnFormClosing(FormClosingEventArgs e)
    {
        StopWorker("Application closing.");
        base.OnFormClosing(e);
    }
}
