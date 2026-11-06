using System.Text.Json;

namespace WvWSummaryTool;

internal sealed record AppSettings
{
    public const int DefaultHistoryLimit = 100;
    public const int MaximumHistoryLimit = 1000;
    public int HistoryLimit { get; init; } = DefaultHistoryLimit;
    public string LogDirectory { get; init; } = "";
    public uint Hotkey { get; init; } = 0x77;
    public uint Modifiers { get; init; }
    public string HotkeyLabel => ((Modifiers & 2) != 0 ? "Strg+" : "")
        + ((Modifiers & 1) != 0 ? "Alt+" : "")
        + ((Modifiers & 4) != 0 ? "Umschalt+" : "") + $"F{Hotkey - 0x6F}";

    public static string FilePath => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "WvWSummaryTool", "settings.json");

    public static AppSettings Load(string path, out string? warning)
    {
        warning = null;
        try
        {
            if (!File.Exists(path)) return Defaults();
            var settings = JsonSerializer.Deserialize<AppSettings>(File.ReadAllText(path));
            if (settings is null || settings.LogDirectory is null || settings.Hotkey < 0x70
                || settings.Hotkey > 0x87 || settings.Hotkey == 0x7B || (settings.Modifiers & ~7U) != 0)
                throw new InvalidDataException("Ungültige Einstellungen.");
            return settings with { HistoryLimit = Math.Clamp(settings.HistoryLimit, 1, MaximumHistoryLimit) };
        }
        catch (Exception ex) when (ex is IOException or InvalidDataException or UnauthorizedAccessException or JsonException)
        {
            warning = "Einstellungen konnten nicht geladen werden; Standardwerte werden verwendet. " + ex.Message;
            return Defaults();
        }
    }

    public void Save(string path)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(path))!);
        var temporary = path + ".tmp";
        try
        {
            File.WriteAllText(temporary, JsonSerializer.Serialize(this, new JsonSerializerOptions { WriteIndented = true }));
            File.Move(temporary, path, overwrite: true);
        }
        finally
        {
            if (File.Exists(temporary)) File.Delete(temporary);
        }
    }

    private static AppSettings Defaults() => new() { LogDirectory = LogFinder.FindDefaultLogDirectory() ?? "" };
}
