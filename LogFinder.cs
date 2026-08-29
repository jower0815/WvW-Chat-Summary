namespace WvWSummaryTool;

internal static class LogFinder
{
    public static string? FindDefaultLogDirectory()
    {
        var home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        var candidates = new[]
        {
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "Guild Wars 2", "addons", "arcdps", "arcdps.cbtlogs"),
            Path.Combine(home, "OneDrive", "Documents", "Guild Wars 2", "addons", "arcdps", "arcdps.cbtlogs"),
            Path.Combine(home, "Documents", "Guild Wars 2", "addons", "arcdps", "arcdps.cbtlogs")
        };
        return candidates.FirstOrDefault(Directory.Exists);
    }

    public static string? FindNewestLog(string directory) => Directory.Exists(directory)
        ? Directory.EnumerateFiles(directory, "*.*", SearchOption.AllDirectories)
            .Where(path => path.EndsWith(".zevtc", StringComparison.OrdinalIgnoreCase) || path.EndsWith(".evtc", StringComparison.OrdinalIgnoreCase))
            .Select(path => new FileInfo(path)).OrderByDescending(file => file.LastWriteTimeUtc).FirstOrDefault()?.FullName
        : null;
}
