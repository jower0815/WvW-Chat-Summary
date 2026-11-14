namespace WvWSummaryTool;

internal sealed class SettingsForm : Form
{
    public SettingsForm(AppSettings settings, Func<AppSettings, string?> apply)
    {
        Text = settings.T("Einstellungen", "Settings");
        FormBorderStyle = FormBorderStyle.FixedDialog;
        StartPosition = FormStartPosition.CenterParent;
        MaximizeBox = false;
        MinimizeBox = false;
        AutoSize = true;
        AutoSizeMode = AutoSizeMode.GrowAndShrink;
        var path = new TextBox { Text = settings.LogDirectory, Width = 440 };
        var browse = new Button { Text = settings.T("Ordner wählen …", "Choose folder …"), AutoSize = true };
        browse.Click += (_, _) =>
        {
            using var picker = new FolderBrowserDialog { InitialDirectory = path.Text, ShowNewFolderButton = false };
            if (picker.ShowDialog(this) == DialogResult.OK) path.Text = picker.SelectedPath;
        };
        var key = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList, Width = 80 };
        // Windows reserves F12 for the debugger, even when no debugger is running.
        var keys = Enumerable.Range(1, 24).Where(n => n != 12).ToArray();
        key.Items.AddRange(keys.Select(n => (object)$"F{n}").ToArray());
        key.SelectedIndex = Array.IndexOf(keys, (int)settings.Hotkey - 0x6F);
        var ctrl = new CheckBox { Text = settings.T("Strg", "Ctrl"), AutoSize = true, Checked = (settings.Modifiers & 2) != 0 };
        var alt = new CheckBox { Text = "Alt", AutoSize = true, Checked = (settings.Modifiers & 1) != 0 };
        var shift = new CheckBox { Text = settings.T("Umschalt", "Shift"), AutoSize = true, Checked = (settings.Modifiers & 4) != 0 };
        var shortcut = new FlowLayoutPanel { AutoSize = true, Dock = DockStyle.Fill };
        shortcut.Controls.AddRange([ctrl, alt, shift, key]);
        var language = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList, Width = 150 };
        language.Items.AddRange(["Deutsch", "English"]);
        language.SelectedIndex = settings.Language == "en" ? 1 : 0;
        var save = new Button { Text = settings.T("Speichern", "Save"), AutoSize = true };
        var historyLimit = new NumericUpDown { Minimum = 1, Maximum = AppSettings.MaximumHistoryLimit,
            Value = settings.HistoryLimit, Width = 90 };
        var scanInterval = new NumericUpDown { Minimum = AppSettings.MinimumScanIntervalSeconds,
            Maximum = AppSettings.MaximumScanIntervalSeconds, Value = settings.ScanIntervalSeconds, Width = 90 };
        var cancel = new Button { Text = settings.T("Abbrechen", "Cancel"), AutoSize = true, DialogResult = DialogResult.Cancel };
        save.Click += (_, _) =>
        {
            var directory = path.Text.Trim();
            if (directory != settings.LogDirectory && !Directory.Exists(directory))
            {
                MessageBox.Show(this, settings.T("Bitte einen vorhandenen Logordner auswählen.", "Select an existing log folder."), Text, MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }
            var error = apply(settings with
            {
                Language = language.SelectedIndex == 1 ? "en" : "de",
                LogDirectory = directory,
                ScanIntervalSeconds = (int)scanInterval.Value,
                HistoryLimit = (int)historyLimit.Value,
                Hotkey = (uint)(keys[key.SelectedIndex] + 0x6F),
                Modifiers = (ctrl.Checked ? 2U : 0) | (alt.Checked ? 1U : 0) | (shift.Checked ? 4U : 0)
            });
            if (error is not null) MessageBox.Show(this, error, Text, MessageBoxButtons.OK, MessageBoxIcon.Warning);
            else DialogResult = DialogResult.OK;
        };
        var buttons = new FlowLayoutPanel { AutoSize = true, Dock = DockStyle.Fill, FlowDirection = FlowDirection.RightToLeft };
        buttons.Controls.AddRange([cancel, save]);
        var layout = new TableLayoutPanel { AutoSize = true, Padding = new Padding(12), ColumnCount = 2 };
        layout.Controls.Add(new Label { Text = settings.T("ArcDPS-Logordner", "ArcDPS log folder"), AutoSize = true }, 0, 0);
        layout.Controls.Add(path, 0, 1);
        layout.Controls.Add(browse, 1, 1);
        layout.Controls.Add(new Label { Text = settings.T("Globaler Hotkey für die Fight-Zusammenfassung", "Global hotkey for the fight summary"), AutoSize = true, Margin = new Padding(3, 15, 3, 3) }, 0, 2);
        layout.Controls.Add(shortcut, 0, 3);
        layout.Controls.Add(new Label { Text = settings.T("F12 ist von Windows reserviert. Standard: F8 ohne Zusatztasten.", "Windows reserves F12. Default: F8 without modifiers."), AutoSize = true }, 0, 4);
        layout.Controls.Add(new Label { Text = settings.T("History-Limit (Anzahl Fights)", "History limit (number of fights)"), AutoSize = true, Margin = new Padding(3, 15, 3, 3) }, 0, 5);
        layout.Controls.Add(historyLimit, 0, 6);
        layout.Controls.Add(new Label { Text = settings.T("Beim Senken des Limits werden die ältesten Einträge entfernt.", "Reducing the limit removes the oldest entries."), AutoSize = true }, 0, 7);
        layout.Controls.Add(new Label { Text = settings.T("Sprache", "Language"), AutoSize = true, Margin = new Padding(3, 15, 3, 3) }, 0, 8);
        layout.Controls.Add(language, 0, 9);
        layout.Controls.Add(new Label { Text = settings.T("Scan-Intervall (Sekunden)", "Scan interval (seconds)"),
            AutoSize = true, Margin = new Padding(3, 15, 3, 3) }, 0, 10);
        layout.Controls.Add(scanInterval, 0, 11);
        layout.Controls.Add(buttons, 0, 12);
        layout.SetColumnSpan(buttons, 2);
        Controls.Add(layout);
        AcceptButton = save;
        CancelButton = cancel;
    }
}
