using System.Globalization;

namespace WvWSummaryTool;

internal static class SummaryFormatter
{
    private static readonly Dictionary<string, string> GermanColors = new(StringComparer.OrdinalIgnoreCase)
    { ["Red"] = "Rot", ["Blue"] = "Blau", ["Green"] = "Grün" };

    public static string Format(FightSummary fight)
    {
        var duration = $"{(int)fight.Duration.TotalMinutes}:{fight.Duration.Seconds:00}";
        var sizes = string.Join(" v ", fight.Teams.Select(team => $"{team.Players}{TeamCode(team.Color)}"));
        var parts = fight.Teams.Select(team => $"{GermanColors.GetValueOrDefault(team.Color, team.Color)}: {team.Deaths}D/{team.Downs}Down/{FormatDamage(team.Damage)}");
        return $"{duration} | {sizes} | {string.Join(" | ", parts)}";
    }

    private static string FormatDamage(long damage) => damage >= 1_000_000
        ? (damage / 1_000_000d).ToString("0.##", CultureInfo.InvariantCulture) + "M"
        : damage >= 1_000 ? (damage / 1_000d).ToString("0.#", CultureInfo.InvariantCulture) + "k"
        : damage.ToString(CultureInfo.InvariantCulture);

    private static string TeamCode(string color) => color.ToLowerInvariant() switch
    {
        "red" => "r",
        "blue" => "b",
        "green" => "g",
        _ => "?"
    };
}
