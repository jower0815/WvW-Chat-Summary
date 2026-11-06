using WvWSummaryTool;

internal static class HistoryChecks
{
    public static void Run()
    {
        var directory = Path.Combine(Path.GetTempPath(), "WvwHistoryChecks-" + Guid.NewGuid());
        Directory.CreateDirectory(directory);
        try
        {
            var path = Path.Combine(directory, "history.json");
            var history = FightHistory.Load(path, out var warning);
            if (warning is not null || history.Entries.Count != 0) throw new Exception("Missing history must be empty.");
            var team = new TeamSummary("Red", 5, 1, 2, 1000, true,
                [new DamageDealer("Reaper", 1000)], [new SpecializationDamage("Reaper", 5, 1000)]);
            var fight = new FightSummary("fight.evtc", TimeSpan.FromSeconds(73), [team],
                new FightLocation(38, "EBG", Track: [new(0, 0.3, 0.4), new(1, 0.4, 0.5)], TrackSource: "Aufzeichnender Spieler"), 1);
            var time = DateTimeOffset.UtcNow;
            history.Add(fight, time);
            history.Add(fight with { SourceFile = "FIGHT.EVTC" }, time);
            if (history.Entries.Count != 1) throw new Exception("Repeated logs must not duplicate history.");
            history.Save(path);
            history = FightHistory.Load(path, out warning);
            if (history.Entries[0].Fight.Location?.Track?.Count != 2 || history.Entries[0].Fight.HistoryVersion != 1)
                throw new Exception("History must retain movement tracks and their schema version.");
            if (warning is not null || history.Entries.Count != 1
                || SummaryFormatter.Format(history.Entries[0].Fight) != SummaryFormatter.Format(fight)
                || SummaryFormatter.FormatTopDamage(history.Entries[0].Fight, "Red") != SummaryFormatter.FormatTopDamage(fight, "Red")
                || SummaryFormatter.FormatTopSpecializations(history.Entries[0].Fight, "Red") != SummaryFormatter.FormatTopSpecializations(fight, "Red"))
                throw new Exception("History must retain summary, location and top damage data.");
            for (var i = 1; i <= 105; i++) history.Add(fight with { SourceFile = $"{i}.evtc" }, time.AddMinutes(i));
            history.Save(path);
            history = FightHistory.Load(path, out warning);
            if (history.Entries.Count != 100 || history.Entries[0].Fight.SourceFile != "105.evtc"
                || history.Entries[^1].Fight.SourceFile != "6.evtc") throw new Exception("History must keep the newest 100 logs.");
            history.SetLimit(3);
            if (history.Entries.Count != 3 || history.Entries[^1].Fight.SourceFile != "103.evtc")
                throw new Exception("Lowering the limit must discard the oldest fights.");
            history.Save(path);
            if (FightHistory.Load(path, out warning, limit: 2).Entries.Count != 2 || warning is not null)
                throw new Exception("Loading history must honor the configured limit.");
            foreach (var invalid in new[] { "broken json", "null", "[null]", "[{\"Fight\":null}]" })
            {
                File.WriteAllText(path, invalid);
                if (FightHistory.Load(path, out warning).Entries.Count != 0 || warning is null)
                    throw new Exception("Corrupt history must recover with a warning.");
            }
            Console.WriteLine("PASS: history persistence, deduplication, retained statistics, limit and corrupt-file recovery.");
        }
        finally { Directory.Delete(directory, recursive: true); }
    }
}
