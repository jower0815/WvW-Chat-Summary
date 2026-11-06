using System.Text.Json;

namespace WvWSummaryTool;

internal static class FightLocationResolver
{
    // ArenaNet API snapshot is embedded so parsing needs neither network access nor an API key.
    private static readonly MapInfo[] Maps = ReadResource<MapInfo[]>("wvw-maps.json");
    private static readonly Objective[] Objectives = ReadResource<Objective[]>("wvw-objectives.json");
    private const double MaximumDistance = 12000; // game units; beyond this, only name the map

    public static FightLocation? Resolve(IReadOnlyList<CombatEvent> events,
        IReadOnlyDictionary<ulong, Agent> agents, ulong povAddress, ulong start, ulong end)
    {
        var mapIds = events.Where(e => e.StateChange == 25 && e.SourceAgent is > 0 and <= uint.MaxValue)
            .Select(e => (uint)e.SourceAgent).Distinct().ToArray();
        if (mapIds.Length != 1) return null;
        var mapId = mapIds[0];
        var code = mapId switch { 38 => "EBG", 94 => "RBL", 95 => "GBL", 96 => "BBL", 1099 => "RBL", 968 => "EotM", _ => $"Map {mapId}" };
        var result = new FightLocation(mapId, code);
        var map = Maps.FirstOrDefault(m => m.id == mapId);
        if (map is null || events.Any(e => e.StateChange == 65 && e.SourceAgent != e.DestinationAgent)) return result;

        var positions = events.Where(e => e.StateChange == 19 && e.Time >= start && e.Time <= end)
            .Select(e => new Position(e.SourceAgent, e.Time,
                BitConverter.Int32BitsToSingle(unchecked((int)e.DestinationAgent)),
                BitConverter.Int32BitsToSingle(unchecked((int)(e.DestinationAgent >> 32)))))
            .Where(p => double.IsFinite(p.X) && double.IsFinite(p.Y)
                && p.X >= map.map_rect[0][0] && p.X <= map.map_rect[1][0]
                && p.Y >= map.map_rect[0][1] && p.Y <= map.map_rect[1][1]).ToArray();
        // Prefer the recording player's track. Squad members are the fallback, never enemy/pet tracks.
        var track = positions.Where(p => p.Agent == povAddress).ToArray();
        if (track.Length == 0)
            track = positions.Where(p => agents.TryGetValue(p.Agent, out var agent) && agent.Subgroup > 0).ToArray();
        var source = track.Any(p => p.Agent == povAddress) ? "Aufzeichnender Spieler" : "Squad-Mittelpunkt";
        // Keep time order and bounded history size; average each player's samples equally.
        var interval = Math.Max(1000UL, (end >= start ? end - start : 0) / 3600 + 1);
        var route = track.GroupBy(p => (p.Time - start) / interval).OrderBy(g => g.Key).Select(group =>
        {
            var players = group.GroupBy(p => p.Agent)
                .Select(g => (X: g.Average(p => p.X), Y: g.Average(p => p.Y))).ToArray();
            return new FightTrackPoint(group.Average(p => (double)(p.Time - start)) / 1000,
                (players.Average(p => p.X) - map.map_rect[0][0]) / (map.map_rect[1][0] - map.map_rect[0][0]),
                (map.map_rect[1][1] - players.Average(p => p.Y)) / (map.map_rect[1][1] - map.map_rect[0][1]));
        }).ToArray();
        if (route.Length > 0) result = result with { Track = route, TrackSource = source };
        if (track.Length < 3) return result;

        var objectives = Objectives.Where(o => o.map_id == mapId).Select(o => new
        {
            o.name,
            X = map.map_rect[0][0] + (o.coord[0] - map.continent_rect[0][0])
                / (map.continent_rect[1][0] - map.continent_rect[0][0]) * (map.map_rect[1][0] - map.map_rect[0][0]),
            // Continent Y grows southward; EVTC/map Y grows northward.
            Y = map.map_rect[1][1] - (o.coord[1] - map.continent_rect[0][1])
                / (map.continent_rect[1][1] - map.continent_rect[0][1]) * (map.map_rect[1][1] - map.map_rect[0][1])
        }).ToArray();
        if (objectives.Length == 0) return result;

        // One vote per second keeps high-frequency updates / large squads from dominating.
        // Average each player's updates first so every squad member has equal weight.
        var votes = track.GroupBy(p => (p.Time - start) / 1000).Select(second =>
        {
            var players = second.GroupBy(p => p.Agent).Select(g => (X: g.Average(p => p.X), Y: g.Average(p => p.Y))).ToArray();
            var x = players.Average(p => p.X);
            var y = players.Average(p => p.Y);
            var nearest = objectives.Select(o => (o.name, Distance: Math.Pow(x - o.X, 2) + Math.Pow(y - o.Y, 2)))
                .MinBy(o => o.Distance);
            return nearest.Distance <= MaximumDistance * MaximumDistance ? nearest.name : null;
        }).ToArray();
        if (votes.Length < 3) return result;
        var dominant = votes.Where(name => name is not null).GroupBy(name => name)
            .OrderByDescending(group => group.Count()).FirstOrDefault();
        // Moving fights spanning several areas should not receive a misleading single place.
        if (dominant is null || dominant.Count() < votes.Length * 0.6) return result;
        return result with { NearbyObjective = ShortName(dominant.Key!, mapId) };
    }

    private static string ShortName(string name, uint mapId)
    {
        if (mapId is 94 or 95 or 96)
        {
            if (name.EndsWith(" Bay", StringComparison.Ordinal)) return "Bay";
            if (name.EndsWith(" Hills", StringComparison.Ordinal)) return "Hills";
            if (name.EndsWith(" Garrison", StringComparison.Ordinal) || name == "Garrison") return "Garrison";
        }
        return name == "Stonemist Castle" ? "SM" : name;
    }

    private static T ReadResource<T>(string suffix)
    {
        var assembly = typeof(FightLocationResolver).Assembly;
        var name = assembly.GetManifestResourceNames().Single(n => n.EndsWith(suffix, StringComparison.Ordinal));
        using var stream = assembly.GetManifestResourceStream(name)!;
        return JsonSerializer.Deserialize<T>(stream)!;
    }

    public static MapDisplay? GetMapDisplay(uint id)
    {
        var map = Maps.FirstOrDefault(m => m.id == id);
        if (map is null) return null;
        var left = map.continent_rect[0][0];
        var top = map.continent_rect[0][1];
        var width = map.continent_rect[1][0] - left;
        var height = map.continent_rect[1][1] - top;
        return new MapDisplay(left, top, width, height, map.default_floor,
            Objectives.Where(o => o.map_id == id).Select(o => new MapMarker(ShortName(o.name, id),
                (o.coord[0] - left) / width, (o.coord[1] - top) / height)).ToArray());
    }

    // Elite Insights' EBG image covers a world rectangle shifted east and north.
    // Preserve the stored API-normalized positions and apply the image offset only when drawing.
    public static (double X, double Y) ToBackgroundCoordinates(uint id, double x, double y) =>
        id == 38 ? (x - 950.0 / 73728, y + 2250.0 / 73728) : (x, y);

    private sealed record Position(ulong Agent, ulong Time, double X, double Y);
    private sealed record MapInfo(uint id, double[][] map_rect, double[][] continent_rect, int default_floor);
    private sealed record Objective(string name, uint map_id, double[] coord);
}

internal sealed record MapMarker(string Name, double X, double Y);
internal sealed record MapDisplay(double Left, double Top, double Width, double Height,
    int Floor, IReadOnlyList<MapMarker> Markers);
