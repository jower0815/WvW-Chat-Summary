namespace WvWSummaryTool;

internal sealed class HistoryForm : Form
{
    private readonly ListBox _list = new() { Dock = DockStyle.Fill, HorizontalScrollbar = true };
    private readonly TextBox _preview = new() { Dock = DockStyle.Fill, ReadOnly = true };
    private readonly Label _status = new() { AutoSize = true };
    private readonly FightMapControl _map;
    private readonly TrackBar _timeline = new() { Dock = DockStyle.Fill, Minimum = 0, Maximum = 1, TickStyle = TickStyle.None, Enabled = false };
    private readonly Label _time = new() { AutoSize = true };
    private FightSummary? _displayedFight;

    public HistoryForm(FightHistory history, AppSettings settings)
    {
        _map = new FightMapControl(settings);
        _time.Text = settings.T("Gesamter Verlauf", "Full track");
        Text = settings.T("Fight-History", "Fight history");
        Size = new Size(1180, 760);
        MinimumSize = new Size(980, 620);
        StartPosition = FormStartPosition.CenterParent;
        var layout = new TableLayoutPanel { Dock = DockStyle.Fill, Padding = new Padding(12), ColumnCount = 1, RowCount = 5 };
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        for (var i = 0; i < 3; i++) layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        var heading = new Label { AutoSize = true };
        layout.Controls.Add(heading, 0, 0);
        var split = new SplitContainer { Dock = DockStyle.Fill, Size = new Size(1100, 480), SplitterDistance = 330,
            Panel1MinSize = 200, Panel2MinSize = 400 };
        split.Panel1.Controls.Add(_list);
        var mapLayout = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 3 };
        mapLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        mapLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        mapLayout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        mapLayout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        mapLayout.Controls.Add(_map, 0, 0);
        mapLayout.Controls.Add(_timeline, 0, 1);
        mapLayout.Controls.Add(_time, 0, 2);
        split.Panel2.Controls.Add(mapLayout);
        layout.Controls.Add(split, 0, 1);
        _timeline.ValueChanged += (_, _) =>
        {
            _map.SetTime(_timeline.Value);
            _time.Text = $"{TimeSpan.FromSeconds(_timeline.Value):h\\:mm\\:ss} / {TimeSpan.FromSeconds(_timeline.Maximum):h\\:mm\\:ss}";
        };
        _list.SelectedIndexChanged += (_, _) =>
        {
            if (_list.SelectedItem is not FightHistoryEntry entry || ReferenceEquals(_displayedFight, entry.Fight)) return;
            _displayedFight = entry.Fight;
            _preview.Text = SummaryFormatter.Format(entry.Fight);
            var last = entry.Fight.Location?.Track?.LastOrDefault()?.Seconds ?? 0;
            _timeline.Maximum = (int)Math.Clamp(Math.Ceiling(last), 1, int.MaxValue);
            _timeline.Enabled = last > 0;
            _timeline.Value = _timeline.Maximum;
            _time.Text = last > 0 ? settings.T($"Gesamter Verlauf · {_timeline.Maximum} Sekunden · Regler zum Zurückspulen", $"Full track · {_timeline.Maximum} seconds · Drag the slider to rewind")
                : settings.T("Keine Bewegung aufgezeichnet", "No movement recorded");
            _map.ShowFight(entry.Fight);
        };
        layout.Controls.Add(_preview, 0, 2);
        var actions = new FlowLayoutPanel { Dock = DockStyle.Fill, AutoSize = true };
        AddButton(settings.T("Zusammenfassung kopieren", "Copy summary"), SummaryFormatter.Format);
        foreach (var (color, label) in new[] { ("Red", "Rot"), ("Green", "Grün"), ("Blue", "Blau") })
        {
            AddButton(settings.T($"Top 5 Klassen {label}", $"Top 5 classes ({color})"), f => SummaryFormatter.FormatTopSpecializations(f, color));
            AddButton(settings.T($"Top 5 Einzelspieler {label}", $"Top 5 players ({color})"), f => SummaryFormatter.FormatTopDamage(f, color));
        }
        layout.Controls.Add(actions, 0, 3);
        layout.Controls.Add(_status, 0, 4);
        Controls.Add(layout);
        void RefreshHistory(object? sender, EventArgs e)
        {
            heading.Text = settings.T($"Letzte {history.Limit} eingelesene Fights – neueste zuerst", $"Last {history.Limit} imported fights – newest first");
            var selected = (_list.SelectedItem as FightHistoryEntry)?.Fight.SourceFile;
            _list.BeginUpdate();
            _list.Items.Clear();
            foreach (var entry in history.Entries) _list.Items.Add(entry);
            var index = history.Entries.ToList().FindIndex(e => e.Fight.SourceFile == selected);
            if (_list.Items.Count > 0) _list.SelectedIndex = Math.Max(0, index);
            _list.EndUpdate();
            foreach (Control button in actions.Controls) button.Enabled = _list.Items.Count > 0;
            if (_list.Items.Count > 0) _status.Text = settings.T($"{_list.Items.Count} Fights im Verlauf.", $"{_list.Items.Count} fights in history.");
        }
        RefreshHistory(null, EventArgs.Empty);
        history.Changed += RefreshHistory;
        FormClosed += (_, _) => history.Changed -= RefreshHistory;
        if (_list.Items.Count > 0) _list.SelectedIndex = 0;
        else _status.Text = settings.T("Noch keine Fights gespeichert. Der Logordner wird automatisch durchsucht.", "No fights saved yet. The log folder is scanned automatically.");

        void AddButton(string label, Func<FightSummary, string> format)
        {
            var button = new Button { Text = label, AutoSize = true, Enabled = history.Entries.Count > 0 };
            button.Click += (_, _) =>
            {
                if (_list.SelectedItem is not FightHistoryEntry entry) return;
                try
                {
                    var text = format(entry.Fight);
                    if (text.Length > 199) throw new InvalidOperationException(settings.T("Die Chatzeile ist zu lang.", "The chat line is too long."));
                    Clipboard.SetText(text);
                    _preview.Text = text;
                    _status.Text = settings.T($"Kopiert ({text.Length}/199 Zeichen).", $"Copied ({text.Length}/199 characters).");
                }
                catch (Exception ex) { _status.Text = settings.T("Fehler: ", "Error: ") + ex.Message; }
            };
            actions.Controls.Add(button);
        }
    }
}
