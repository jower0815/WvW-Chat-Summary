using System.Buffers.Binary;
using System.IO.Compression;
using System.Text;

namespace WvWSummaryTool;

internal static class EvtcParser
{
    private const int AgentSize = 96;
    private const int SkillSize = 68;
    private const int EventSize = 64;
    private const byte StateNone = 0;
    private const byte StateEnterCombat = 1;
    private const byte StateExitCombat = 2;
    private const byte StateDead = 4;
    private const byte StateDown = 5;
    private const byte StatePointOfView = 13;
    private const byte StateTeamChange = 22;
    private const byte StateIdToGuid = 46;

    private static readonly Dictionary<string, string> TeamGuids = new(StringComparer.Ordinal)
    {
        ["BC8AEAEF73DC8C43B041CEDFEA4D5020"] = "Green",
        ["5D22513B9498EB48944E94EC7A8DD657"] = "Red",
        ["CF6F7C254FCB184CBCCE4738EADD8388"] = "Blue"
    };

    // Fallback IDs retained by the upstream add-on for logs where the stable
    // team-color GUID records are not present.
    private static readonly Dictionary<uint, string> LegacyTeamIds = new()
    {
        [697] = "Red", [699] = "Red", [705] = "Red", [706] = "Red", [707] = "Red",
        [882] = "Red", [885] = "Red", [886] = "Red", [2520] = "Red",
        [39] = "Green", [2739] = "Green", [2741] = "Green", [2752] = "Green",
        [2763] = "Green", [2767] = "Green",
        [432] = "Blue", [433] = "Blue", [1277] = "Blue", [1281] = "Blue",
        [1282] = "Blue", [1989] = "Blue", [1996] = "Blue"
    };

    public static FightSummary Parse(string path)
    {
        using var data = OpenEvtc(path);
        using var reader = new BinaryReader(data, Encoding.UTF8, leaveOpen: false);
        var header = Encoding.ASCII.GetString(ReadExactly(reader, 12));
        if (!header.StartsWith("EVTC", StringComparison.Ordinal) ||
            !int.TryParse(header.AsSpan(4), out var version) || version < 20240612)
            throw new InvalidDataException("Die Datei ist kein unterstützter EVTC-Log.");

        _ = reader.ReadByte();
        var fightId = reader.ReadUInt16();
        _ = reader.ReadByte();
        if (fightId != 1) throw new InvalidDataException("Der neueste Log ist kein WvW-Log.");

        var agentCount = reader.ReadUInt32();
        var agents = new Dictionary<ulong, Agent>();
        for (var i = 0U; i < agentCount; i++)
        {
            var block = ReadExactly(reader, AgentSize);
            var address = BinaryPrimitives.ReadUInt64LittleEndian(block.AsSpan(0, 8));
            var profession = BinaryPrimitives.ReadUInt32LittleEndian(block.AsSpan(8, 4));
            var strings = Encoding.UTF8.GetString(block, 28, 68).Split('\0', StringSplitOptions.RemoveEmptyEntries);
            var account = strings.Length > 1 ? strings[1] : "";
            var subgroup = strings.Length > 2 && int.TryParse(strings[2], out var group) ? group : -1;
            // ArcDPS anonymizes enemy players in WvW, so only squad members
            // have a normal account name. Profession IDs 1..9 identify all
            // player agents while excluding pets, siege and NPCs.
            if (profession is >= 1 and <= 9)
                agents[address] = new Agent { Address = address, AccountName = account, Subgroup = subgroup };
        }

        var skillCount = reader.ReadUInt32();
        data.Position += checked((long)skillCount * SkillSize);
        var events = new List<CombatEvent>(checked((int)((data.Length - data.Position) / EventSize)));
        while (data.Position + EventSize <= data.Length) events.Add(ReadEvent(ReadExactly(reader, EventSize)));
        return Summarize(path, agents, events);
    }

    private static FightSummary Summarize(string path, Dictionary<ulong, Agent> agents, List<CombatEvent> events)
    {
        var teamIds = new Dictionary<uint, string>(LegacyTeamIds);
        foreach (var evt in events.Where(e => e.StateChange == StateIdToGuid && e.SkillId != 0))
            if (TeamGuids.TryGetValue(GuidHex(evt.SourceAgent, evt.DestinationAgent), out var color)) teamIds[evt.SkillId] = color;

        ulong povAddress = 0;
        ulong start = ulong.MaxValue;
        ulong end = 0;
        var activeAddresses = new HashSet<ulong>();
        var instanceAgents = new Dictionary<ushort, Agent>();
        foreach (var evt in events)
        {
            if (evt.StateChange == StateEnterCombat) start = Math.Min(start, evt.Time);
            if (evt.StateChange == StateExitCombat) end = Math.Max(end, evt.Time);
            if (evt.StateChange == StatePointOfView) povAddress = evt.SourceAgent;
            if (evt.SourceAgent != 0 && evt.SourceInstanceId != 0 && agents.TryGetValue(evt.SourceAgent, out var source))
            {
                source.InstanceId = evt.SourceInstanceId;
                instanceAgents[evt.SourceInstanceId] = source;
                if (evt.StateChange == StateNone) activeAddresses.Add(source.Address);
            }
            if (evt.DestinationAgent != 0 && evt.DestinationInstanceId != 0 && agents.TryGetValue(evt.DestinationAgent, out var destination))
            {
                destination.InstanceId = evt.DestinationInstanceId;
                instanceAgents[evt.DestinationInstanceId] = destination;
            }
            if (evt.StateChange == StateTeamChange && agents.TryGetValue(evt.SourceAgent, out var changed) &&
                teamIds.TryGetValue(unchecked((uint)evt.Value), out var changedColor)) changed.Team = changedColor;
        }

        if (start == ulong.MaxValue) start = events.Count == 0 ? 0 : events.Min(e => e.Time);
        if (end == 0) end = events.Count == 0 ? start : events.Max(e => e.Time);
        var totals = new Dictionary<string, MutableTeamSummary>(StringComparer.OrdinalIgnoreCase);
        MutableTeamSummary Team(string color)
        {
            if (!totals.TryGetValue(color, out var value)) totals[color] = value = new MutableTeamSummary();
            return value;
        }
        if (agents.TryGetValue(povAddress, out var pov) && pov.Team != "Unknown") Team(pov.Team).IsPlayerTeam = true;

        var countedAccounts = new Dictionary<string, HashSet<string>>(StringComparer.OrdinalIgnoreCase);
        foreach (var agent in agents.Values.Where(a => a.Team != "Unknown" &&
                     (a.Subgroup > 0 ? activeAddresses.Contains(a.Address) : a.InstanceId != 0)))
        {
            if (!countedAccounts.TryGetValue(agent.Team, out var accounts))
                countedAccounts[agent.Team] = accounts = new HashSet<string>(StringComparer.Ordinal);
            var identity = agent.AccountName.StartsWith(':')
                ? agent.AccountName
                : agent.InstanceId.ToString("X4");
            if (accounts.Add(identity)) Team(agent.Team).Players++;
        }

        foreach (var evt in events)
        {
            if ((evt.StateChange == StateDead || evt.StateChange == StateDown) &&
                instanceAgents.TryGetValue(evt.SourceInstanceId, out var victim) && victim.Team != "Unknown")
            {
                if (evt.StateChange == StateDead) Team(victim.Team).Deaths++;
                else Team(victim.Team).Downs++;
            }
            if (evt.StateChange != StateNone || evt.Activation != 0 || evt.BuffRemove != 0 || evt.Result is not (0 or 1 or 2 or 8)) continue;
            var damage = evt.Buff == 0 ? evt.Value : evt.Buff == 1 ? evt.BuffDamage : 0;
            if (damage <= 0 || !instanceAgents.TryGetValue(evt.SourceInstanceId, out var attacker) || attacker.Team == "Unknown") continue;
            if (instanceAgents.TryGetValue(evt.DestinationInstanceId, out var target) && target.Team != "Unknown") Team(attacker.Team).Damage += damage;
        }

        var order = new[] { "Red", "Blue", "Green" };
        var teams = totals.Where(pair => pair.Value.Players > 0).OrderBy(pair => Array.IndexOf(order, pair.Key))
            .Select(pair => new TeamSummary(pair.Key, pair.Value.Players, pair.Value.Deaths, pair.Value.Downs, pair.Value.Damage, pair.Value.IsPlayerTeam)).ToArray();
        if (teams.Length < 2) throw new InvalidDataException("Im Log konnten nicht mindestens zwei WvW-Teams erkannt werden.");
        return new FightSummary(path, TimeSpan.FromMilliseconds(end >= start ? end - start : 0), teams);
    }

    private static CombatEvent ReadEvent(byte[] bytes) => new(
        BinaryPrimitives.ReadUInt64LittleEndian(bytes.AsSpan(0, 8)), BinaryPrimitives.ReadUInt64LittleEndian(bytes.AsSpan(8, 8)),
        BinaryPrimitives.ReadUInt64LittleEndian(bytes.AsSpan(16, 8)), BinaryPrimitives.ReadInt32LittleEndian(bytes.AsSpan(24, 4)),
        BinaryPrimitives.ReadInt32LittleEndian(bytes.AsSpan(28, 4)), BinaryPrimitives.ReadUInt32LittleEndian(bytes.AsSpan(36, 4)),
        BinaryPrimitives.ReadUInt16LittleEndian(bytes.AsSpan(40, 2)), BinaryPrimitives.ReadUInt16LittleEndian(bytes.AsSpan(42, 2)),
        bytes[49], bytes[50], bytes[51], bytes[52], bytes[56]);

    private static Stream OpenEvtc(string path)
    {
        if (!path.EndsWith(".zevtc", StringComparison.OrdinalIgnoreCase)) return new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
        using var archive = ZipFile.OpenRead(path);
        var entry = archive.Entries.FirstOrDefault(e => e.Name.EndsWith(".evtc", StringComparison.OrdinalIgnoreCase)) ?? archive.Entries.FirstOrDefault()
            ?? throw new InvalidDataException("Der ZIP-Log ist leer.");
        var memory = new MemoryStream(checked((int)entry.Length));
        using (var input = entry.Open()) input.CopyTo(memory);
        memory.Position = 0;
        return memory;
    }

    private static byte[] ReadExactly(BinaryReader reader, int length)
    {
        var bytes = reader.ReadBytes(length);
        if (bytes.Length != length) throw new EndOfStreamException("Der EVTC-Log ist unvollständig.");
        return bytes;
    }

    private static string GuidHex(ulong first, ulong second)
    {
        Span<byte> bytes = stackalloc byte[16];
        BinaryPrimitives.WriteUInt64LittleEndian(bytes[..8], first);
        BinaryPrimitives.WriteUInt64LittleEndian(bytes[8..], second);
        return Convert.ToHexString(bytes);
    }
}
