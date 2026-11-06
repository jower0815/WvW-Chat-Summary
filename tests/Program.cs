using WvWSummaryTool;
using System.Buffers.Binary;
using System.IO.Compression;
using System.Text;

// Binary fixtures exercise the actual file reader, including metadata after combat.
LocationChecks.Run();
HistoryChecks.Run();
var directory = Path.Combine(Path.GetTempPath(), "WvwParserChecks-" + Guid.NewGuid());
Directory.CreateDirectory(directory);
try
{
    var settingsPath = Path.Combine(directory, "settings.json");
    var defaults = AppSettings.Load(settingsPath, out var warning);
    if (defaults.Hotkey != 0x77 || defaults.Modifiers != 0 || warning is not null)
        throw new Exception("Missing settings must use F8 defaults.");
    var settings = new AppSettings { LogDirectory = Path.Combine(directory, "Logs ä"), Hotkey = 0x78, Modifiers = 3, HistoryLimit = 250 };
    settings.Save(settingsPath);
    if (AppSettings.Load(settingsPath, out warning) != settings || warning is not null)
        throw new Exception("Settings round-trip failed.");
    settings = settings with { Modifiers = 4 };
    settings.Save(settingsPath);
    if (AppSettings.Load(settingsPath, out warning) != settings || warning is not null)
        throw new Exception("Replacing saved settings failed.");
    foreach (var invalid in new[] { "broken json", "null", "{\"Hotkey\":123}", "{\"Modifiers\":8}", "{\"LogDirectory\":null}" })
    {
        File.WriteAllText(settingsPath, invalid);
        if (AppSettings.Load(settingsPath, out warning).Hotkey != 0x77 || warning is null)
            throw new Exception("Invalid settings must recover with a warning.");
    }
    Console.WriteLine("PASS: settings defaults, persistence, replacement and corrupt-file recovery.");
    Check("new-tail", 9001, 9002, 9003, "tail");
    Check("old-start", 9001, 9002, 9003, "start");
    Check("mapping-overrides-legacy", 433, 697, 39, "tail");
    Check("legacy-no-mapping", 697, 433, 39, "none");
    Check("legacy-zero-mapping", 697, 433, 39, "zero");
    Check("with-location", 9001, 9002, 9003, "tail", withLocation: true);
    foreach (var log in Directory.EnumerateFiles(directory).Where(p => p.EndsWith(".evtc") || p.EndsWith(".zevtc")))
        File.SetLastWriteTimeUtc(log, DateTime.UtcNow.AddMinutes(-1));
    var imported = HistoryScanner.Scan(directory, [], CancellationToken.None);
    if (imported.Count != 12) throw new Exception("Scan must import all EVTC and ZEVTC fixtures.");
    if (HistoryScanner.Scan(directory, [], CancellationToken.None, limit: 3).Count != 3)
        throw new Exception("Automatic import must honor the configured history limit.");
    if (HistoryScanner.Scan(directory, imported, CancellationToken.None).Count != 0)
        throw new Exception("Unchanged logs must not be parsed again.");
    var legacyHistory = imported.Select(e => e with { Fight = e.Fight with { HistoryVersion = 0 } }).ToArray();
    if (HistoryScanner.Scan(directory, legacyHistory, CancellationToken.None).Count != 12)
        throw new Exception("Older history entries must be reimported to retain movement tracks.");
    var broken = Path.Combine(directory, "broken.evtc");
    File.WriteAllText(broken, "incomplete");
    File.SetLastWriteTimeUtc(broken, DateTime.UtcNow.AddMinutes(-1));
    if (HistoryScanner.Scan(directory, [], CancellationToken.None).Count != 12)
        throw new Exception("Invalid files must not prevent other imports.");
    var changed = imported[0];
    File.SetLastWriteTimeUtc(changed.Fight.SourceFile, DateTime.UtcNow.AddSeconds(-10));
    if (HistoryScanner.Scan(directory, imported, CancellationToken.None).Count != 1)
        throw new Exception("Changed logs must be reimported.");
    File.SetLastWriteTimeUtc(changed.Fight.SourceFile, DateTime.UtcNow.AddHours(1));
    if (HistoryScanner.Scan(directory, [], CancellationToken.None).Count != 11)
        throw new Exception("Recently modified files must be deferred.");
    Console.WriteLine("PASS: automatic EVTC/ZEVTC import, deduplication, invalid-file recovery, changed-file import and active-file deferral.");
    Console.WriteLine("PASS: 6 binary regression fixtures (EVTC and ZEVTC each).");
}
finally
{
    foreach (var file in Directory.EnumerateFiles(directory)) File.Delete(file);
    Directory.Delete(directory);
}

void Check(string name, uint red, uint blue, uint green, string mapping, bool withLocation = false)
{
    var path = Path.Combine(directory, name + ".evtc");
    using (var writer = new BinaryWriter(File.Create(path)))
    {
        writer.Write(Encoding.ASCII.GetBytes(mapping == "start" ? "EVTC20260811" : "EVTC20260915"));
        writer.Write((byte)1); writer.Write((ushort)1); writer.Write((byte)0);
        writer.Write(3U);
        for (var i = 1; i <= 3; i++)
        {
            var agent = new byte[96];
            BinaryPrimitives.WriteUInt64LittleEndian(agent, (ulong)i);
            BinaryPrimitives.WriteUInt32LittleEndian(agent.AsSpan(8), 1);
            Encoding.UTF8.GetBytes($"Player{i}\0:Test{i}.1234\0{(i == 3 ? 1 : 0)}\0").CopyTo(agent, 28);
            writer.Write(agent);
        }
        writer.Write(0U);
        var teams = new byte[64];
        teams[56] = 74;
        // Deliberately distinct shard IDs: these must not be used as team IDs.
        BinaryPrimitives.WriteUInt32LittleEndian(teams.AsSpan(8), 12001);
        BinaryPrimitives.WriteUInt32LittleEndian(teams.AsSpan(12), 12002);
        BinaryPrimitives.WriteUInt32LittleEndian(teams.AsSpan(16), 12003);
        BinaryPrimitives.WriteUInt32LittleEndian(teams.AsSpan(20), red);
        BinaryPrimitives.WriteUInt32LittleEndian(teams.AsSpan(24), blue);
        BinaryPrimitives.WriteUInt32LittleEndian(teams.AsSpan(28), green);
        if (mapping == "start") writer.Write(teams);
        writer.Write(Event(22, 1, 0, (int)red));
        writer.Write(Event(22, 2, 0, (int)blue));
        writer.Write(Event(22, 3, 0, (int)green));
        writer.Write(Event(13, 3));
        writer.Write(Event(1, 3, time: 1000));
        writer.Write(Event(0, 1, 2, 100, time: 1100));
        writer.Write(Event(0, 2, 3, 200, time: 1200));
        writer.Write(Event(0, 3, 1, 300, time: 1300));
        writer.Write(Event(5, 1, time: 1400));
        writer.Write(Event(4, 1, time: 1500));
        writer.Write(Event(2, 3, time: withLocation ? 4000UL : 2000UL));
        if (withLocation)
        {
            writer.Write(Event(25, 96));
            for (ulong time = 1000; time <= 3000; time += 1000)
            {
                var position = Event(19, 3, time: time);
                BinaryPrimitives.WriteSingleLittleEndian(position.AsSpan(16), 28180.8f);
                BinaryPrimitives.WriteSingleLittleEndian(position.AsSpan(20), -5056.8f);
                writer.Write(position);
            }
        }
        if (mapping == "tail") writer.Write(teams);
        // Zero metadata must not erase a valid mapping or classify unassigned agents.
        if (mapping is "tail" or "zero") writer.Write(Event(74, 0));
    }
    Verify(EvtcParser.Parse(path));
    var zipPath = Path.ChangeExtension(path, ".zevtc");
    using (var zip = ZipFile.Open(zipPath, ZipArchiveMode.Create)) zip.CreateEntryFromFile(path, "fight.evtc");
    Verify(EvtcParser.Parse(zipPath));

    void Verify(FightSummary fight)
    {
        if (fight.Duration != TimeSpan.FromSeconds(withLocation ? 3 : 1) || fight.Teams.Count != 3)
            throw new Exception(name + ": invalid duration/team count");
        if (withLocation ? fight.Location?.Label != "BBL ~ Hills" : fight.Location is not null)
            throw new Exception(name + ": incorrect location");
        foreach (var (color, damage) in new[] { ("Red", 100L), ("Blue", 200L), ("Green", 300L) })
        {
            var team = fight.Teams.Single(t => t.Color == color);
            if (team.Players != 1 || team.Damage != damage || team.IsPlayerTeam != (color == "Green") ||
                team.Deaths != (color == "Red" ? 1 : 0) || team.Downs != (color == "Red" ? 1 : 0) ||
                team.TopDamageDealers.Single().Damage != damage || team.TopSpecializations.Single().Damage != damage)
                throw new Exception(name + ": incorrect " + color + " totals");
        }
    }
}

static byte[] Event(byte state, ulong source, ulong destination = 0, int value = 0, ulong time = 0)
{
    var bytes = new byte[64];
    BinaryPrimitives.WriteUInt64LittleEndian(bytes, time);
    BinaryPrimitives.WriteUInt64LittleEndian(bytes.AsSpan(8), source);
    BinaryPrimitives.WriteUInt64LittleEndian(bytes.AsSpan(16), destination);
    BinaryPrimitives.WriteInt32LittleEndian(bytes.AsSpan(24), value);
    BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(40), (ushort)source);
    BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(42), (ushort)destination);
    bytes[56] = state;
    return bytes;
}

foreach (var path in args)
{
    try
    {
        var fight = EvtcParser.Parse(path);
        Console.WriteLine($"{Path.GetFileName(path)}: {System.Text.Json.JsonSerializer.Serialize(fight with { SourceFile = "" })}");
    }
    catch (Exception ex)
    {
        Console.WriteLine($"{Path.GetFileName(path)}: ERROR {ex.Message}");
        Environment.ExitCode = 1;
    }
}
