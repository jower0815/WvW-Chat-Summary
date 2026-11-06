namespace WvWSummaryTool;

internal sealed class SettingsForm : Form
{
    public SettingsForm(AppSettings settings, Func<AppSettings, string?> apply)
    {
        Text = "Einstellungen";
        FormBorderStyle = FormBorderStyle.FixedDialog;
        StartPosition = FormStartPosition.CenterParent;
        MaximizeBox = false;
        MinimizeBox = false;
        AutoSize = true;
        AutoSizeMode = AutoSizeMode.GrowAndShrink;
        var path = new TextBox { Text = settings.LogDirectory, Width = 440 };
        var browse = new Button { Text = "Ordner wählen …", AutoSize = true };
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
        var ctrl = new CheckBox { Text = "Strg", AutoSize = true, Checked = (settings.Modifiers & 2) != 0 };
        var alt = new CheckBox { Text = "Alt", AutoSize = true, Checked = (settings.Modifiers & 1) != 0 };
        var shift = new CheckBox { Text = "Umschalt", AutoSize = true, Checked = (settings.Modifiers & 4) != 0 };
        var shortcut = new FlowLayoutPanel { AutoSize = true, Dock = DockStyle.Fill };
        shortcut.Controls.AddRange([ctrl, alt, shift, key]);
        var save = new Button { Text = "Speichern", AutoSize = true };
        var historyLimit = new NumericUpDown { Minimum = 1, Maximum = AppSettings.MaximumHistoryLimit,
            Value = settings.HistoryLimit, Width = 90 };
        var cancel = new Button { Text = "Abbrechen", AutoSize = true, DialogResult = DialogResult.Cancel };
        save.Click += (_, _) =>
        {
            var directory = path.Text.Trim();
            if (directory != settings.LogDirectory && !Directory.Exists(directory))
            {
                MessageBox.Show(this, "Bitte einen vorhandenen Logordner auswählen.", Text, MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }
            var error = apply(new AppSettings
            {
                LogDirectory = directory,
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
        layout.Controls.Add(new Label { Text = "ArcDPS-Logordner", AutoSize = true }, 0, 0);
        layout.Controls.Add(path, 0, 1);
        layout.Controls.Add(browse, 1, 1);
        layout.Controls.Add(new Label { Text = "Globaler Hotkey für die Fight-Zusammenfassung", AutoSize = true, Margin = new Padding(3, 15, 3, 3) }, 0, 2);
        layout.Controls.Add(shortcut, 0, 3);
        layout.Controls.Add(new Label { Text = "F12 ist von Windows reserviert. Standard: F8 ohne Zusatztasten.", AutoSize = true }, 0, 4);
        layout.Controls.Add(new Label { Text = "History-Limit (Anzahl Fights)", AutoSize = true, Margin = new Padding(3, 15, 3, 3) }, 0, 5);
        layout.Controls.Add(historyLimit, 0, 6);
        layout.Controls.Add(new Label { Text = "Beim Senken des Limits werden die ältesten Einträge entfernt.", AutoSize = true }, 0, 7);
        layout.Controls.Add(buttons, 0, 8);
        layout.SetColumnSpan(buttons, 2);
        Controls.Add(layout);
        AcceptButton = save;
        CancelButton = cancel;
    }
}
