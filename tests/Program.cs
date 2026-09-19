using WvWSummaryTool;
using System.Buffers.Binary;
using System.IO.Compression;
using System.Text;

// Binary fixtures exercise the actual file reader, including metadata after combat.
var directory = Path.Combine(Path.GetTempPath(), "WvwParserChecks-" + Guid.NewGuid());
Directory.CreateDirectory(directory);
try
{
    Check("new-tail", 9001, 9002, 9003, "tail");
    Check("old-start", 9001, 9002, 9003, "start");
    Check("mapping-overrides-legacy", 433, 697, 39, "tail");
    Check("legacy-no-mapping", 697, 433, 39, "none");
    Check("legacy-zero-mapping", 697, 433, 39, "zero");
    Console.WriteLine("PASS: 5 binary regression fixtures (EVTC and ZEVTC each).");
}
finally
{
    foreach (var file in Directory.EnumerateFiles(directory)) File.Delete(file);
    Directory.Delete(directory);
}

void Check(string name, uint red, uint blue, uint green, string mapping)
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
        writer.Write(Event(2, 3, time: 2000));
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
        if (fight.Duration != TimeSpan.FromSeconds(1) || fight.Teams.Count != 3)
            throw new Exception(name + ": invalid duration/team count");
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
