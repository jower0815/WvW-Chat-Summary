using System.Runtime.InteropServices;

namespace WvWSummaryTool;

internal sealed class MainForm : Form
{
    private const int HotkeyId = 0x575657;
    private const int WmHotkey = 0x0312;
    private const uint ModNoRepeat = 0x4000;
    private const uint VkF8 = 0x77;
    private readonly TextBox _logPath = new() { Dock = DockStyle.Fill };
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
        _logPath.Text = LogFinder.FindDefaultLogDirectory() ?? "";
        _copy.Click += async (_, _) => await ParseAndCopyAsync();
        _topRed.Click += async (_, _) => await ParseAndCopyTopDamageAsync("Red");
        _topGreen.Click += async (_, _) => await ParseAndCopyTopDamageAsync("Green");
        _topBlue.Click += async (_, _) => await ParseAndCopyTopDamageAsync("Blue");
        _classesRed.Click += async (_, _) => await ParseAndCopyTopSpecializationsAsync("Red");
        _classesGreen.Click += async (_, _) => await ParseAndCopyTopSpecializationsAsync("Green");
        _classesBlue.Click += async (_, _) => await ParseAndCopyTopSpecializationsAsync("Blue");
        var browse = new Button { Text = "Ordner wählen …", AutoSize = true };
        browse.Click += (_, _) => Browse();
        var pathRow = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2, AutoSize = true };
        pathRow.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        pathRow.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        pathRow.Controls.Add(_logPath, 0, 0);
        pathRow.Controls.Add(browse, 1, 0);
        var topButtons = new FlowLayoutPanel { Dock = DockStyle.Fill, AutoSize = true, WrapContents = false };
        topButtons.Controls.Add(_topRed); topButtons.Controls.Add(_topGreen); topButtons.Controls.Add(_topBlue);
        var classButtons = new FlowLayoutPanel { Dock = DockStyle.Fill, AutoSize = true, WrapContents = false };
        classButtons.Controls.Add(_classesRed); classButtons.Controls.Add(_classesGreen); classButtons.Controls.Add(_classesBlue);
        var layout = new TableLayoutPanel { Dock = DockStyle.Fill, Padding = new Padding(12), RowCount = 9 };
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
        layout.Controls.Add(new Label { Text = "F8 kopiert nur die Fight-Zusammenfassung. Die Top-5-Buttons haben keinen Hotkey.", AutoSize = true }, 0, 8);
        Controls.Add(layout);
    }

    protected override void OnHandleCreated(EventArgs e)
    {
        base.OnHandleCreated(e);
        if (!RegisterHotKey(Handle, HotkeyId, ModNoRepeat, VkF8)) _status.Text = "F8 konnte nicht registriert werden. Der Button funktioniert weiterhin.";
    }

    protected override void OnHandleDestroyed(EventArgs e)
    {
        UnregisterHotKey(Handle, HotkeyId);
        base.OnHandleDestroyed(e);
    }

    protected override void WndProc(ref Message m)
    {
        if (m.Msg == WmHotkey && m.WParam.ToInt32() == HotkeyId) _ = ParseAndCopyAsync();
        base.WndProc(ref m);
    }

    private async Task ParseAndCopyAsync()
    {
        try
        {
            _copy.Enabled = false;
            _status.Text = "Lese neuesten Log …";
            var log = await Task.Run(() => LogFinder.FindNewestLog(_logPath.Text.Trim()))
                      ?? throw new FileNotFoundException("Keine EVTC-/ZEVTC-Datei im gewählten Ordner gefunden.");
            var fight = await Task.Run(() => EvtcParser.Parse(log));
            var text = SummaryFormatter.Format(fight);
            if (text.Length > 199) throw new InvalidOperationException($"Die Chatzeile ist mit {text.Length} Zeichen zu lang.");
            _preview.Text = text;
            Clipboard.SetText(text);
            _status.Text = $"Kopiert ({text.Length}/199 Zeichen): {Path.GetFileName(log)}";
        }
        catch (Exception ex) { _status.Text = "Fehler: " + ex.Message; }
        finally { _copy.Enabled = true; }
    }

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
        try
        {
            SetButtonsEnabled(false);
            _status.Text = "Lese neuesten Log …";
            var log = await Task.Run(() => LogFinder.FindNewestLog(_logPath.Text.Trim()))
                      ?? throw new FileNotFoundException("Keine EVTC-/ZEVTC-Datei im gewählten Ordner gefunden.");
            var fight = await Task.Run(() => EvtcParser.Parse(log));
            var text = format(fight);
            if (text.Length > 199) throw new InvalidOperationException($"Die Chatzeile ist mit {text.Length} Zeichen zu lang.");
            _preview.Text = text;
            Clipboard.SetText(text);
            _status.Text = $"Kopiert ({text.Length}/199 Zeichen): {Path.GetFileName(log)}";
        }
        catch (Exception ex) { _status.Text = "Fehler: " + ex.Message; }
        finally { SetButtonsEnabled(true); }
    }

    private void SetButtonsEnabled(bool enabled)
    {
        _copy.Enabled = enabled;
        _topRed.Enabled = enabled;
        _topGreen.Enabled = enabled;
        _topBlue.Enabled = enabled;
        _classesRed.Enabled = enabled;
        _classesGreen.Enabled = enabled;
        _classesBlue.Enabled = enabled;
    }

    private void Browse()
    {
        using var dialog = new FolderBrowserDialog { InitialDirectory = _logPath.Text, ShowNewFolderButton = false };
        if (dialog.ShowDialog(this) == DialogResult.OK) _logPath.Text = dialog.SelectedPath;
    }

    [DllImport("user32.dll", SetLastError = true)] private static extern bool RegisterHotKey(IntPtr window, int id, uint modifiers, uint virtualKey);
    [DllImport("user32.dll", SetLastError = true)] private static extern bool UnregisterHotKey(IntPtr window, int id);
}
