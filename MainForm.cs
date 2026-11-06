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
    private readonly System.Windows.Forms.Timer _scanTimer = new() { Interval = 10000 };
    private readonly CancellationTokenSource _scanCancellation = new();
    private bool _scanning;
    private readonly Label _scanStatus = new() { AutoSize = true };
    private readonly Button _historyButton = new() { Text = "History …", AutoSize = true };
    private readonly Label _hotkeyHint = new() { AutoSize = true };
    private readonly TextBox _logPath = new() { Dock = DockStyle.Fill, ReadOnly = true };
    private readonly TextBox _preview = new() { Dock = DockStyle.Fill, ReadOnly = true };
    private readonly Label _status = new() { Dock = DockStyle.Fill, AutoEllipsis = true };
    private readonly Button _copy = new() { Text = "Neuesten Fight kopieren (F8)", AutoSize = true };
    private readonly Button _topRed = new() { Text = "Top 5 Einzelspieler Rot", AutoSize = true };
    private readonly Button _topGreen = new() { Text = "Top 5 Einzelspieler Grün", AutoSize = true };
    private readonly Button _topBlue = new() { Text = "Top 5 Einzelspieler Blau", AutoSize = true };
    private readonly Button _classesRed = new() { Text = "Top 5 Klassen Rot", AutoSize = true };
    private readonly Button _classesGreen = new() { Text = "Top 5 Klassen Grün", AutoSize = true };
    private readonly Button _classesBlue = new() { Text = "Top 5 Klassen Blau", AutoSize = true };

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
            try { using var dialog = new HistoryForm(_history); dialog.ShowDialog(this); }
            finally { _settingsOpen = false; }
        };
        _copy.Click += async (_, _) => await ParseAndCopyAsync();
        _topRed.Click += async (_, _) => await ParseAndCopyTopDamageAsync("Red");
        _topGreen.Click += async (_, _) => await ParseAndCopyTopDamageAsync("Green");
        _topBlue.Click += async (_, _) => await ParseAndCopyTopDamageAsync("Blue");
        _classesRed.Click += async (_, _) => await ParseAndCopyTopSpecializationsAsync("Red");
        _classesGreen.Click += async (_, _) => await ParseAndCopyTopSpecializationsAsync("Green");
        _classesBlue.Click += async (_, _) => await ParseAndCopyTopSpecializationsAsync("Blue");
        var browse = new Button { Text = "Einstellungen …", AutoSize = true };
        browse.Click += (_, _) => OpenSettings();
        var pathRow = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2, AutoSize = true };
        pathRow.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        pathRow.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        pathRow.Controls.Add(_logPath, 0, 0);
        pathRow.Controls.Add(browse, 1, 0);
        pathRow.ColumnCount = 3;
        pathRow.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        pathRow.Controls.Add(_historyButton, 2, 0);
        var topButtons = new FlowLayoutPanel { Dock = DockStyle.Fill, AutoSize = true, WrapContents = false };
        topButtons.Controls.Add(_topRed); topButtons.Controls.Add(_topGreen); topButtons.Controls.Add(_topBlue);
        var classButtons = new FlowLayoutPanel { Dock = DockStyle.Fill, AutoSize = true, WrapContents = false };
        classButtons.Controls.Add(_classesRed); classButtons.Controls.Add(_classesGreen); classButtons.Controls.Add(_classesBlue);
        var layout = new TableLayoutPanel { Dock = DockStyle.Fill, Padding = new Padding(12), RowCount = 10 };
        layout.RowStyles.Add(new RowStyle(SizeType.AutoSize)); layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        layout.RowStyles.Add(new RowStyle(SizeType.AutoSize)); layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        layout.RowStyles.Add(new RowStyle(SizeType.AutoSize)); layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        layout.RowStyles.Add(new RowStyle(SizeType.AutoSize)); layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        layout.Controls.Add(new Label { Text = "ArcDPS-Logordner", AutoSize = true }, 0, 0);
        layout.Controls.Add(pathRow, 0, 1);
        layout.Controls.Add(new Label { Text = "GW2-Chatzeile", AutoSize = true, Margin = new Padding(0, 10, 0, 3) }, 0, 2);
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
        _scanStatus.Text = "History: Suche nach Logs …";
        try
        {
            var entries = await Task.Run(() => HistoryScanner.Scan(directory, known, _scanCancellation.Token, limit));
            if (IsDisposed || _scanCancellation.IsCancellationRequested || directory != _settings.LogDirectory || limit != _history.Limit) return;
            foreach (var entry in entries)
                _history.Add(entry.Fight, entry.LogTime, entry.SourceLength);
            // Also retry persistence after a previous save failure, even without new files.
            if (_history.Entries.Count > 0) _history.Save(FightHistory.FilePath);
            _scanStatus.Text = Directory.Exists(directory)
                ? $"History: {_history.Entries.Count} Fights gespeichert. Automatischer Scan alle 10 Sekunden."
                : "History: Logordner nicht gefunden. Bitte Einstellungen prüfen.";
        }
        catch (OperationCanceledException) { }
        catch (Exception ex)
        { if (!IsDisposed) _scanStatus.Text = "History-Scan: " + ex.Message; }
        finally { _scanning = false; }
    }

    protected override void OnHandleCreated(EventArgs e)
    {
        base.OnHandleCreated(e);
        _hotkeyRegistered = RegisterHotKey(Handle, _hotkeyId, ModNoRepeat | _settings.Modifiers, _settings.Hotkey);
        if (!_hotkeyRegistered) _status.Text = $"{_settings.HotkeyLabel} konnte nicht registriert werden. Bitte Hotkey in den Einstellungen ändern. Der Button funktioniert weiterhin.";
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

    private async Task RunCopyActionAsync(Func<FightSummary, string> format)
    {
        if (_busy) return;
        _busy = true;
        var directory = _settings.LogDirectory;
        try
        {
            SetButtonsEnabled(false);
            _status.Text = "Lese neuesten Log …";
            var log = await Task.Run(() => LogFinder.FindNewestLog(directory))
                      ?? throw new FileNotFoundException("Keine EVTC-/ZEVTC-Datei im gewählten Ordner gefunden.");
            var fight = await Task.Run(() => EvtcParser.Parse(log));
            var text = format(fight);
            if (text.Length > 199) throw new InvalidOperationException($"Die Chatzeile ist mit {text.Length} Zeichen zu lang.");
            _preview.Text = text;
            Clipboard.SetText(text);
            _status.Text = $"Kopiert ({text.Length}/199 Zeichen): {Path.GetFileName(log)}";
            _history.Add(fight, new DateTimeOffset(File.GetLastWriteTimeUtc(log)));
            try { _history.Save(FightHistory.FilePath); }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            { _status.Text += " – History konnte nicht gespeichert werden: " + ex.Message; }
        }
        catch (Exception ex) { _status.Text = "Fehler: " + ex.Message; }
        finally { _busy = false; SetButtonsEnabled(true); }
    }

    private void SetButtonsEnabled(bool enabled)
    {
        _copy.Enabled = enabled;
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
        _logPath.Text = _settings.LogDirectory;
        _copy.Text = $"Neuesten Fight kopieren ({_settings.HotkeyLabel})";
        _hotkeyHint.Text = $"{_settings.HotkeyLabel} kopiert nur die Fight-Zusammenfassung. Die Top-5-Buttons haben keinen Hotkey.";
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
        var replaceHotkey = !_hotkeyRegistered || next.Hotkey != _settings.Hotkey || next.Modifiers != _settings.Modifiers;
        var nextId = _hotkeyId == 0x575657 ? 0x575658 : 0x575657;
        // Keep the existing registration until both the new shortcut and the saved file are ready.
        if (replaceHotkey && !RegisterHotKey(Handle, nextId, ModNoRepeat | next.Modifiers, next.Hotkey))
            return $"{next.HotkeyLabel} konnte nicht registriert werden (möglicherweise bereits belegt). Bitte einen anderen Hotkey wählen. Die bisherigen Einstellungen bleiben erhalten.";
        try { next.Save(AppSettings.FilePath); }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            if (replaceHotkey) UnregisterHotKey(Handle, nextId);
            return "Einstellungen konnten nicht gespeichert werden. Die bisherigen Einstellungen bleiben erhalten. " + ex.Message;
        }
        if (replaceHotkey)
        {
            if (_hotkeyRegistered) UnregisterHotKey(Handle, _hotkeyId);
            _hotkeyId = nextId;
            _hotkeyRegistered = true;
        }
        _settings = next;
        UpdateSettingsDisplay();
        _status.Text = "Einstellungen gespeichert.";
        if (_history.Limit != next.HistoryLimit)
        {
            var count = _history.Entries.Count;
            _history.SetLimit(next.HistoryLimit);
            if (_history.Entries.Count < count)
            {
                try { _history.Save(FightHistory.FilePath); }
                catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
                { _status.Text += " History konnte noch nicht gespeichert werden: " + ex.Message; }
            }
            _ = ScanHistoryAsync();
        }
        return null;
    }

    [DllImport("user32.dll", SetLastError = true)] private static extern bool RegisterHotKey(IntPtr window, int id, uint modifiers, uint virtualKey);
    [DllImport("user32.dll", SetLastError = true)] private static extern bool UnregisterHotKey(IntPtr window, int id);
}
