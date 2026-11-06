namespace WvWSummaryTool;

internal static class HistoryScanner
{
    // Parse on the worker thread; the caller owns all history mutations and persistence.
    public static IReadOnlyList<FightHistoryEntry> Scan(string directory,
        IReadOnlyList<FightHistoryEntry> known, CancellationToken cancellationToken,
        int limit = AppSettings.DefaultHistoryLimit, Func<string, FightSummary>? parse = null)
    {
        parse ??= EvtcParser.Parse;
        if (!Directory.Exists(directory)) return [];
        var files = Directory.EnumerateFiles(directory, "*", new EnumerationOptions
        {
            RecurseSubdirectories = true, IgnoreInaccessible = true,
            AttributesToSkip = FileAttributes.ReparsePoint
        }).Where(p => p.EndsWith(".evtc", StringComparison.OrdinalIgnoreCase)
            || p.EndsWith(".zevtc", StringComparison.OrdinalIgnoreCase))
            .Select(p => new FileInfo(p)).Where(f => f.Exists)
            .OrderByDescending(f => f.LastWriteTimeUtc);
        var results = new List<FightHistoryEntry>();
        var retained = known.ToDictionary(e => e.Fight.SourceFile, StringComparer.OrdinalIgnoreCase);
        var successful = 0;
        foreach (var file in files)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (successful >= limit) break;
            try
            {
                var time = new DateTimeOffset(file.LastWriteTimeUtc);
                var length = file.Length;
                if (DateTimeOffset.UtcNow - time < TimeSpan.FromSeconds(3)) continue;
                if (retained.TryGetValue(file.FullName, out var entry)
                    && entry.LogTime == time && entry.SourceLength == length && entry.Fight.HistoryVersion >= 1)
                { successful++; continue; }
                // A writer holding the file open, or a file changing during parsing, is retried later.
                using var readLock = new FileStream(file.FullName, FileMode.Open, FileAccess.Read, FileShare.Read);
                var fight = parse(file.FullName);
                file.Refresh();
                if (!file.Exists || file.Length != length || file.LastWriteTimeUtc != time.UtcDateTime) continue;
                results.Add(new FightHistoryEntry(time, fight, length));
                successful++;
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException
                or InvalidDataException or ArgumentException or OverflowException)
            { /* Incomplete or unsupported logs must not prevent other fights from being imported. */ }
        }
        return results;
    }
}
