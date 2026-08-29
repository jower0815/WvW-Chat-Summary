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

    public MainForm()
    {
        Text = "WvW Summary Tool";
        MinimumSize = new Size(720, 250);
        Size = new Size(820, 290);
        StartPosition = FormStartPosition.CenterScreen;
        _logPath.Text = LogFinder.FindDefaultLogDirectory() ?? "";
        _copy.Click += async (_, _) => await ParseAndCopyAsync();
        var browse = new Button { Text = "Ordner wählen …", AutoSize = true };
        browse.Click += (_, _) => Browse();
        var pathRow = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2, AutoSize = true };
        pathRow.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        pathRow.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        pathRow.Controls.Add(_logPath, 0, 0);
        pathRow.Controls.Add(browse, 1, 0);
        var layout = new TableLayoutPanel { Dock = DockStyle.Fill, Padding = new Padding(12), RowCount = 7 };
        layout.RowStyles.Add(new RowStyle(SizeType.AutoSize)); layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        layout.RowStyles.Add(new RowStyle(SizeType.AutoSize)); layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        layout.RowStyles.Add(new RowStyle(SizeType.AutoSize)); layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        layout.Controls.Add(new Label { Text = "ArcDPS-Logordner", AutoSize = true }, 0, 0);
        layout.Controls.Add(pathRow, 0, 1);
        layout.Controls.Add(new Label { Text = "GW2-Chatzeile", AutoSize = true, Margin = new Padding(0, 10, 0, 3) }, 0, 2);
        layout.Controls.Add(_preview, 0, 3); layout.Controls.Add(_copy, 0, 4); layout.Controls.Add(_status, 0, 5);
        layout.Controls.Add(new Label { Text = "F8 kopiert nur in die Zwischenablage – gesendet wird nichts automatisch.", AutoSize = true }, 0, 6);
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

    private void Browse()
    {
        using var dialog = new FolderBrowserDialog { InitialDirectory = _logPath.Text, ShowNewFolderButton = false };
        if (dialog.ShowDialog(this) == DialogResult.OK) _logPath.Text = dialog.SelectedPath;
    }

    [DllImport("user32.dll", SetLastError = true)] private static extern bool RegisterHotKey(IntPtr window, int id, uint modifiers, uint virtualKey);
    [DllImport("user32.dll", SetLastError = true)] private static extern bool UnregisterHotKey(IntPtr window, int id);
}
