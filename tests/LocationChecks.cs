using WvWSummaryTool;

internal static class LocationChecks
{
    public static void Run()
    {
        var agents = new Dictionary<ulong, Agent>
        {
            [1] = new() { Address = 1, Subgroup = 1 },
            [2] = new() { Address = 2, Subgroup = 0 }
        };
        FightLocation? Resolve(List<CombatEvent> events, ulong pov = 1) =>
            FightLocationResolver.Resolve(events, agents, pov, 1000, 9000);
        foreach (var (map, code, x, y, place) in new[]
        {
            (38U, "EBG", 2695.2f, -5911.2f, "SM"),
            (95U, "GBL", 28182f, -5056.8f, "Hills"),
            (96U, "BBL", 28180.8f, -5056.8f, "Hills"),
            (1099U, "RBL", 638.4f, 8966.4f, "Stoic Rampart"),
            (968U, "EotM", -276f, 20982f, "Overgrown Fane")
        })
        {
            var location = Resolve([Map(map), Position(1, 1000, x, y), Position(1, 2000, x, y), Position(1, 3000, x, y)]);
            Require(location?.MapCode == code && location.NearbyObjective == place, "map/coordinate conversion: " + code);
            Require(location?.Track?.Count == 3 && location.Track[0].Seconds == 0
                && location.Track[1].Seconds == 1 && location.TrackSource == "Aufzeichnender Spieler", "stored chronological route");
            Require(location!.Track!.All(p => p.X is >= 0 and <= 1 && p.Y is >= 0 and <= 1), "normalized track coordinates");
        }
        var hills = new List<CombatEvent> { Map(96), Position(1, 1000, 28180.8f, -5056.8f), Position(1, 2000, 28180.8f, -5056.8f), Position(1, 3000, 28180.8f, -5056.8f) };
        Require(Resolve(hills, 99)?.NearbyObjective == "Hills", "squad fallback");
        Require(Resolve(hills, 99)?.TrackSource == "Squad-Mittelpunkt", "squad route label");
        var ordered = Resolve([Map(96), hills[3], hills[1], hills[2]]);
        Require(ordered?.Track?.Select(p => p.Seconds).SequenceEqual(new[] { 0d, 1d, 2d }) == true, "out-of-order position events");
        Require(Resolve([Map(96), Position(1, 1000, float.NaN, 0)])?.Track is null, "invalid positions never persisted");
        var display = FightLocationResolver.GetMapDisplay(96)!;
        var hillsMarker = display.Markers.First(m => m.Name == "Hills");
        var ebgImage = FightLocationResolver.ToBackgroundCoordinates(38, 950.0 / 73728, 1 - 2250.0 / 73728);
        Require(Math.Abs(ebgImage.X) < 0.000001 && Math.Abs(ebgImage.Y - 1) < 0.000001, "EBG image origin alignment");
        Require(FightLocationResolver.ToBackgroundCoordinates(96, hillsMarker.X, hillsMarker.Y) == (hillsMarker.X, hillsMarker.Y),
            "alpine image shares the API rectangle");
        var hillsTrack = Resolve(hills)!.Track![0];
        Require(Math.Abs(hillsMarker.X - hillsTrack.X) < 0.001 && Math.Abs(hillsMarker.Y - hillsTrack.Y) < 0.001,
            "map markers and movement use the same projection with north at top");
        Require(Resolve([Map(96)])?.Label == "BBL", "missing positions");
        Require(Resolve([]) is null, "missing map");
        Require(Resolve([Map(9999)])?.Label == "Map 9999", "unknown map");
        Require(Resolve([Map(94)])?.Label == "RBL", "historical red map without coordinates");
        Require(Resolve([Map(96), Map(95)]) is null, "ambiguous map");
        Require(Resolve([.. hills, new(0, 95, 96, 0, 0, 0, 0, 0, 0, 0, 0, 0, 65)])?.NearbyObjective is null, "map transition");
        Require(Resolve([.. hills, new(0, 95, 96, 0, 0, 0, 0, 0, 0, 0, 0, 0, 65)])?.Track is null, "map transitions exclude route");
        foreach (var point in new[] { (float.NaN, 0f), (float.PositiveInfinity, 0f), (90000f, 0f), (-30720f, 43008f) })
            Require(Resolve([Map(96), Position(1, 1000, point.Item1, point.Item2), Position(1, 2000, point.Item1, point.Item2), Position(1, 3000, point.Item1, point.Item2)])?.NearbyObjective is null, "invalid or remote position");
        Require(Resolve([Map(96), Position(2, 1000, 28180.8f, -5056.8f), Position(2, 2000, 28180.8f, -5056.8f), Position(2, 3000, 28180.8f, -5056.8f)])?.NearbyObjective is null, "enemy track exclusion");
        Require(Resolve([Map(96), Position(1, 0, 28180.8f, -5056.8f), Position(1, 10000, 28180.8f, -5056.8f)])?.NearbyObjective is null, "combat time filter");
        Require(Resolve([Map(96), hills[1], hills[1], hills[1]])?.NearbyObjective is null, "sparse track");
        var moving = new List<CombatEvent>(hills);
        for (ulong t = 4000; t <= 6000; t += 1000) moving.Add(Position(1, t, -25029.6f, -6878.4f)); // Ascension Bay
        Require(Resolve(moving)?.NearbyObjective is null, "moving fight without dominant area");
        for (ulong t = 4000; t <= 6000; t += 1000) hills.Add(Position(2, t, -25029.6f, -6878.4f));
        Require(Resolve(hills)?.NearbyObjective == "Hills", "POV preference");

        var fight = new FightSummary("", TimeSpan.FromSeconds(73), [], new(96, "BBL", "Hills"));
        Require(SummaryFormatter.Format(fight).StartsWith("BBL ~ Hills | 1:13"), "summary location");
        Require(!SummaryFormatter.Format(fight with { Location = null }).Contains("BBL"), "legacy summary");
        var longPlace = fight with { Location = new(96, "BBL", new string('x', 200)) };
        Require(SummaryFormatter.Format(longPlace).StartsWith("BBL |") && SummaryFormatter.Format(longPlace).Length <= 199, "chat-length fallback");
        Console.WriteLine("PASS: location mapping, positions, fallbacks, ambiguity and chat formatting.");
    }

    private static void Require(bool condition, string message)
    {
        if (!condition) throw new Exception("Location check failed: " + message);
    }
    private static CombatEvent Map(uint id) => new(0, id, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 25);
    private static CombatEvent Position(ulong agent, ulong time, float x, float y) =>
        new(time, agent, BitConverter.SingleToUInt32Bits(x) | ((ulong)BitConverter.SingleToUInt32Bits(y) << 32),
            0, 0, 0, 0, 0, 0, 0, 0, 0, 19);
}
