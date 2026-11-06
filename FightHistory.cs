using System.Text.Json;

namespace WvWSummaryTool;

internal sealed record FightHistoryEntry(DateTimeOffset LogTime, FightSummary Fight, long? SourceLength = null)
{
    public override string ToString() => $"{LogTime.LocalDateTime:g} | {Fight.Location?.Label ?? "WvW"} | {Fight.Duration:m\\:ss} | {Path.GetFileName(Fight.SourceFile)}";
}

internal sealed class FightHistory
{
    public int Limit { get; private set; } = AppSettings.DefaultHistoryLimit;
    private readonly List<FightHistoryEntry> _entries = [];
    public IReadOnlyList<FightHistoryEntry> Entries => _entries;
    public event EventHandler? Changed;
    public static string FilePath => Path.Combine(Path.GetDirectoryName(AppSettings.FilePath)!, "history.json");

    public static FightHistory Load(string path, out string? warning, int limit = AppSettings.DefaultHistoryLimit)
    {
        var history = new FightHistory { Limit = limit };
        warning = null;
        try
        {
            if (!File.Exists(path)) return history;
            var entries = JsonSerializer.Deserialize<List<FightHistoryEntry>>(File.ReadAllText(path));
            if (entries is null || entries.Any(e => e is null || e.Fight is null
                || string.IsNullOrWhiteSpace(e.Fight.SourceFile) || e.Fight.Teams is null
                || e.Fight.Teams.Any(t => t is null || t.TopDamageDealers is null || t.TopSpecializations is null)))
                throw new InvalidDataException("Ungültiger Verlauf.");
            history._entries.AddRange(entries.OrderByDescending(e => e.LogTime).Take(limit));
        }
        catch (Exception ex) when (ex is IOException or InvalidDataException or UnauthorizedAccessException or JsonException)
        {
            warning = "History konnte nicht geladen werden. " + ex.Message;
        }
        return history;
    }

    public void Add(FightSummary fight, DateTimeOffset logTime, long? sourceLength = null)
    {
        _entries.RemoveAll(e => string.Equals(e.Fight.SourceFile, fight.SourceFile, StringComparison.OrdinalIgnoreCase));
        _entries.Add(new FightHistoryEntry(logTime, fight, sourceLength));
        _entries.Sort((a, b) => b.LogTime.CompareTo(a.LogTime));
        if (_entries.Count > Limit) _entries.RemoveRange(Limit, _entries.Count - Limit);
        Changed?.Invoke(this, EventArgs.Empty);
    }

    public void SetLimit(int limit)
    {
        if (Limit == limit) return;
        Limit = limit;
        if (_entries.Count > Limit) _entries.RemoveRange(Limit, _entries.Count - Limit);
        Changed?.Invoke(this, EventArgs.Empty);
    }

    public void Save(string path)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(path))!);
        var temporary = path + ".tmp";
        try
        {
            File.WriteAllText(temporary, JsonSerializer.Serialize(_entries));
            File.Move(temporary, path, overwrite: true);
        }
        finally { if (File.Exists(temporary)) File.Delete(temporary); }
    }
}
