using System.Runtime.InteropServices;

namespace WvWSummaryTool;

internal sealed class MainForm : Form
{
    private int _hotkeyId = 0x575657;
    private const int WmHotkey = 0x0312;
    private const uint ModNoRepeat = 0x4000;
    private AppSettings _settings;
    private bool _hotkeyRegistered;
    private bool _settingsOpen;
    private bool _busy;
    private readonly FightHistory _history;
    private readonly System.Windows.Forms.Timer _scanTimer = new();
    private readonly CancellationTokenSource _scanCancellation = new();
    private bool _scanning;
    private readonly Label _scanStatus = new() { AutoSize = true };
    private readonly Button _historyButton = new() { Text = "History …", AutoSize = true };
    private readonly Label _hotkeyHint = new() { AutoSize = true };
    private readonly TextBox _logPath = new() { Dock = DockStyle.Fill, ReadOnly = true };
    private readonly TextBox _preview = new() { Dock = DockStyle.Fill, ReadOnly = true };
    private readonly Label _status = new() { Dock = DockStyle.Fill, AutoEllipsis = true };
    private readonly Button _copy = new() { AutoSize = true };
    private readonly Button _cleanse = new() { Text = "Cleanse", AutoSize = true };
    private readonly Button _topRed = new() { AutoSize = true };
    private readonly Button _topGreen = new() { AutoSize = true };
    private readonly Button _topBlue = new() { AutoSize = true };
    private readonly Button _classesRed = new() { AutoSize = true };
    private readonly Button _classesGreen = new() { AutoSize = true };
    private readonly Button _classesBlue = new() { AutoSize = true };

    private readonly Button _settingsButton = new() { AutoSize = true };

    private readonly Label _folderLabel = new() { AutoSize = true };

    private readonly Label _previewLabel = new() { AutoSize = true, Margin = new Padding(0, 10, 0, 3) };

    public MainForm()
    {
        Text = "WvW Summary Tool";
        MinimumSize = new Size(720, 300);
        Size = new Size(860, 340);
        StartPosition = FormStartPosition.CenterScreen;
        _settings = AppSettings.Load(AppSettings.FilePath, out var warning);
        _history = FightHistory.Load(FightHistory.FilePath, out var historyWarning, _settings.HistoryLimit);
        UpdateSettingsDisplay();
        if (warning is not null) _status.Text = warning;
        if (historyWarning is not null) _status.Text += " " + historyWarning;
        _historyButton.Click += (_, _) =>
        {
            _settingsOpen = true;
            try { using var dialog = new HistoryForm(_history, _settings); dialog.ShowDialog(this); }
            finally { _settingsOpen = false; }
        };
        _copy.Click += async (_, _) => await ParseAndCopyAsync();
        _topRed.Click += async (_, _) => await ParseAndCopyTopDamageAsync("Red");
        _topGreen.Click += async (_, _) => await ParseAndCopyTopDamageAsync("Green");
        _topBlue.Click += async (_, _) => await ParseAndCopyTopDamageAsync("Blue");
        _classesRed.Click += async (_, _) => await ParseAndCopyTopSpecializationsAsync("Red");
        _classesGreen.Click += async (_, _) => await ParseAndCopyTopSpecializationsAsync("Green");
        _classesBlue.Click += async (_, _) => await ParseAndCopyTopSpecializationsAsync("Blue");
        _settingsButton.Click += (_, _) => OpenSettings();
        var pathRow = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2, AutoSize = true };
        pathRow.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        pathRow.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        pathRow.Controls.Add(_logPath, 0, 0);
        pathRow.Controls.Add(_settingsButton, 1, 0);
        pathRow.ColumnCount = 3;
        pathRow.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        pathRow.Controls.Add(_historyButton, 2, 0);
        var topButtons = new FlowLayoutPanel { Dock = DockStyle.Fill, AutoSize = true, WrapContents = false };
        topButtons.Controls.Add(_topRed); topButtons.Controls.Add(_topGreen); topButtons.Controls.Add(_topBlue);
        topButtons.Controls.Add(_cleanse);
        _cleanse.Click += async (_, _) => await RunCopyActionAsync(FormatCleanses);
        var classButtons = new FlowLayoutPanel { Dock = DockStyle.Fill, AutoSize = true, WrapContents = false };
        classButtons.Controls.Add(_classesRed); classButtons.Controls.Add(_classesGreen); classButtons.Controls.Add(_classesBlue);
        var layout = new TableLayoutPanel { Dock = DockStyle.Fill, Padding = new Padding(12), RowCount = 10 };
        layout.RowStyles.Add(new RowStyle(SizeType.AutoSize)); layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        layout.RowStyles.Add(new RowStyle(SizeType.AutoSize)); layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        layout.RowStyles.Add(new RowStyle(SizeType.AutoSize)); layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        layout.RowStyles.Add(new RowStyle(SizeType.AutoSize)); layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        layout.Controls.Add(_folderLabel, 0, 0);
        layout.Controls.Add(pathRow, 0, 1);
        layout.Controls.Add(_previewLabel, 0, 2);
        layout.Controls.Add(_preview, 0, 3); layout.Controls.Add(_copy, 0, 4); layout.Controls.Add(classButtons, 0, 5);
        layout.Controls.Add(topButtons, 0, 6); layout.Controls.Add(_status, 0, 7);
        layout.Controls.Add(_hotkeyHint, 0, 8);
        layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        layout.Controls.Add(_scanStatus, 0, 9);
        Controls.Add(layout);
        _scanTimer.Tick += async (_, _) => await ScanHistoryAsync();
        Shown += async (_, _) => { _scanTimer.Start(); await ScanHistoryAsync(); };
        FormClosed += (_, _) => { _scanTimer.Dispose(); _scanCancellation.Cancel(); };
    }

    private async Task ScanHistoryAsync()
    {
        if (_scanning || IsDisposed) return;
        _scanning = true;
        var directory = _settings.LogDirectory;
        var known = _history.Entries.ToArray();
        var limit = _history.Limit;
        _scanStatus.Text = _settings.T("History: Suche nach Logs …", "History: Searching for logs …");
        try
        {
            var entries = await Task.Run(() => HistoryScanner.Scan(directory, known, _scanCancellation.Token, limit));
            if (IsDisposed || _scanCancellation.IsCancellationRequested || directory != _settings.LogDirectory || limit != _history.Limit) return;
            foreach (var entry in entries)
                _history.Add(entry.Fight, entry.LogTime, entry.SourceLength);
            // Also retry persistence after a previous save failure, even without new files.
            if (_history.Entries.Count > 0) _history.Save(FightHistory.FilePath);
            _scanStatus.Text = Directory.Exists(directory)
                ? _settings.T($"History: {_history.Entries.Count} Fights gespeichert. Automatischer Scan alle {_settings.ScanIntervalSeconds} Sekunden.", $"History: {_history.Entries.Count} fights saved. Scans run every {_settings.ScanIntervalSeconds} seconds.")
                : _settings.T("History: Logordner nicht gefunden. Bitte Einstellungen prüfen.", "History: Log folder missing. Check your settings.");
        }
        catch (OperationCanceledException) { }
        catch (Exception ex)
        { if (!IsDisposed) _scanStatus.Text = _settings.T("History-Scan: ", "History scan: ") + ex.Message; }
        finally { _scanning = false; }
    }

    protected override void OnHandleCreated(EventArgs e)
    {
        base.OnHandleCreated(e);
        _hotkeyRegistered = RegisterHotKey(Handle, _hotkeyId, ModNoRepeat | _settings.Modifiers, _settings.Hotkey);
        if (!_hotkeyRegistered) _status.Text = _settings.T($"{_settings.HotkeyLabel} konnte nicht registriert werden. Bitte Hotkey in den Einstellungen ändern. Der Button funktioniert weiterhin.", $"Could not register {_settings.HotkeyLabel}. Change the hotkey in Settings. The button still works.");
    }

    protected override void OnHandleDestroyed(EventArgs e)
    {
        if (_hotkeyRegistered) UnregisterHotKey(Handle, _hotkeyId);
        _hotkeyRegistered = false;
        base.OnHandleDestroyed(e);
    }

    protected override void WndProc(ref Message m)
    {
        if (m.Msg == WmHotkey && m.WParam.ToInt32() == _hotkeyId && !_settingsOpen && !_busy) _ = ParseAndCopyAsync();
        base.WndProc(ref m);
    }

    private Task ParseAndCopyAsync() => RunCopyActionAsync(SummaryFormatter.Format);

    private async Task ParseAndCopyTopDamageAsync(string color)
    {
        await RunCopyActionAsync(fight => SummaryFormatter.FormatTopDamage(fight, color));
    }

    private async Task ParseAndCopyTopSpecializationsAsync(string color)
    {
        await RunCopyActionAsync(fight => SummaryFormatter.FormatTopSpecializations(fight, color));
    }

    private string FormatCleanses(FightSummary fight)
    {
        var cleanses = fight.Cleanses;
        if (cleanses is null)
            throw new InvalidOperationException(_settings.T("Das eigene Team konnte im Log nicht erkannt werden.", "Your team could not be identified in the log."));

        var total = _settings.T($"Cleanses eigenes Team: {cleanses.Total}", $"Your team's cleanses: {cleanses.Total}");
        var duration = $"{(int)fight.Duration.TotalMinutes}:{fight.Duration.Seconds:00}";
        var map = fight.Location?.MapCode ?? _settings.T("Karte unbekannt", "Unknown map");
        var heading = $"{map} | {duration} | {total}";
        var entries = cleanses.TopPlayers.Select((player, index) => $"{index + 1}. {player.Name}: {player.Cleanses}");
        var text = string.Join(" | ", entries.Prepend(heading));
        if (text.Length <= 199) return text;

        var namesBudget = 199 - heading.Length - cleanses.TopPlayers.Sum(player => $" | 1. : {player.Cleanses}".Length);
        var nameLimit = namesBudget / cleanses.TopPlayers.Count;
        entries = cleanses.TopPlayers.Select((player, index) =>
        {
            var name = player.Name;
            if (name.Length > nameLimit)
            {
                var length = nameLimit - 1;
                if (char.IsHighSurrogate(name[length - 1])) length--;
                name = name[..length] + "…";
            }
            return $"{index + 1}. {name}: {player.Cleanses}";
        });
        return string.Join(" | ", entries.Prepend(heading));
    }

    private async Task RunCopyActionAsync(Func<FightSummary, string> format)
    {
        if (_busy) return;
        _busy = true;
        var directory = _settings.LogDirectory;
        try
        {
            SetButtonsEnabled(false);
            _status.Text = _settings.T("Lese neuesten Log …", "Reading the latest log …");
            var log = await Task.Run(() => LogFinder.FindNewestLog(directory))
                      ?? throw new FileNotFoundException(_settings.T("Keine EVTC-/ZEVTC-Datei im gewählten Ordner gefunden.", "No EVTC or ZEVTC file found in the selected folder."));
            var fight = await Task.Run(() => EvtcParser.Parse(log));
            var text = format(fight);
            if (text.Length > 199) throw new InvalidOperationException(_settings.T($"Die Chatzeile ist mit {text.Length} Zeichen zu lang.", $"The chat line is too long ({text.Length} characters)."));
            _preview.Text = text;
            Clipboard.SetText(text);
            _status.Text = _settings.T($"Kopiert ({text.Length}/199 Zeichen): {Path.GetFileName(log)}", $"Copied ({text.Length}/199 characters): {Path.GetFileName(log)}");
            _history.Add(fight, new DateTimeOffset(File.GetLastWriteTimeUtc(log)));
            try { _history.Save(FightHistory.FilePath); }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            { _status.Text += _settings.T(" – History konnte nicht gespeichert werden: ", " – Could not save fight history: ") + ex.Message; }
        }
        catch (Exception ex) { _status.Text = _settings.T("Fehler: ", "Error: ") + ex.Message; }
        finally { _busy = false; SetButtonsEnabled(true); }
    }

    private void SetButtonsEnabled(bool enabled)
    {
        _copy.Enabled = enabled;
        _cleanse.Enabled = enabled;
        _historyButton.Enabled = enabled;
        _topRed.Enabled = enabled;
        _topGreen.Enabled = enabled;
        _topBlue.Enabled = enabled;
        _classesRed.Enabled = enabled;
        _classesGreen.Enabled = enabled;
        _classesBlue.Enabled = enabled;
    }

    private void UpdateSettingsDisplay()
    {
        _topRed.Text = _settings.T("Top 5 Einzelspieler Rot", "Top 5 players (Red)");
        _topGreen.Text = _settings.T("Top 5 Einzelspieler Grün", "Top 5 players (Green)");
        _topBlue.Text = _settings.T("Top 5 Einzelspieler Blau", "Top 5 players (Blue)");
        _classesRed.Text = _settings.T("Top 5 Klassen Rot", "Top 5 classes (Red)");
        _classesGreen.Text = _settings.T("Top 5 Klassen Grün", "Top 5 classes (Green)");
        _classesBlue.Text = _settings.T("Top 5 Klassen Blau", "Top 5 classes (Blue)");
        _settingsButton.Text = _settings.T("Einstellungen …", "Settings …");
        _folderLabel.Text = _settings.T("ArcDPS-Logordner", "ArcDPS log folder");
        _previewLabel.Text = _settings.T("GW2-Chatzeile", "GW2 chat line");
        _scanTimer.Interval = _settings.ScanIntervalSeconds * 1000;
        _logPath.Text = _settings.LogDirectory;
        _copy.Text = _settings.T($"Neuesten Fight kopieren ({_settings.HotkeyLabel})", $"Copy latest fight ({_settings.HotkeyLabel})");
        _hotkeyHint.Text = _settings.T($"{_settings.HotkeyLabel} kopiert nur die Fight-Zusammenfassung. Die Top-5-Buttons haben keinen Hotkey.", $"{_settings.HotkeyLabel} copies the fight summary only. The top 5 buttons have no hotkey.");
    }

    private void OpenSettings()
    {
        _settingsOpen = true;
        try
        {
            using var dialog = new SettingsForm(_settings, ApplySettings);
            dialog.ShowDialog(this);
        }
        finally { _settingsOpen = false; }
    }

    private string? ApplySettings(AppSettings next)
    {
        var languageChanged = _settings.Language != next.Language;
        var replaceHotkey = next.Hotkey != _settings.Hotkey || next.Modifiers != _settings.Modifiers
            || (!_hotkeyRegistered && !languageChanged);
        var nextId = _hotkeyId == 0x575657 ? 0x575658 : 0x575657;
        // Keep the existing registration until both the new shortcut and the saved file are ready.
        if (replaceHotkey && !RegisterHotKey(Handle, nextId, ModNoRepeat | next.Modifiers, next.Hotkey))
            return _settings.T($"{next.HotkeyLabel} konnte nicht registriert werden (möglicherweise bereits belegt). Bitte einen anderen Hotkey wählen. Die bisherigen Einstellungen bleiben erhalten.", $"Could not register the selected hotkey. It may already be in use. Choose a different hotkey.");
        try { next.Save(AppSettings.FilePath); }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            if (replaceHotkey) UnregisterHotKey(Handle, nextId);
            return _settings.T("Einstellungen konnten nicht gespeichert werden. Die bisherigen Einstellungen bleiben erhalten. ", "Could not save settings. ") + ex.Message;
        }
        if (replaceHotkey)
        {
            if (_hotkeyRegistered) UnregisterHotKey(Handle, _hotkeyId);
            _hotkeyId = nextId;
            _hotkeyRegistered = true;
        }
        if (languageChanged) _scanStatus.Text = "";
        _settings = next;
        UpdateSettingsDisplay();
        _status.Text = _settings.T("Einstellungen gespeichert.", "Settings saved.");
        if (_history.Limit != next.HistoryLimit)
        {
            var count = _history.Entries.Count;
            _history.SetLimit(next.HistoryLimit);
            if (_history.Entries.Count < count)
            {
                try { _history.Save(FightHistory.FilePath); }
                catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
                { _status.Text += _settings.T(" History konnte noch nicht gespeichert werden: ", " Could not save fight history: ") + ex.Message; }
            }
        }
        _ = ScanHistoryAsync();
        return null;
    }

    [DllImport("user32.dll", SetLastError = true)] private static extern bool RegisterHotKey(IntPtr window, int id, uint modifiers, uint virtualKey);
    [DllImport("user32.dll", SetLastError = true)] private static extern bool UnregisterHotKey(IntPtr window, int id);
}
