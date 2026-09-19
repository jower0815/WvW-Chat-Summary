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
        var parts = fight.Teams.Select(team => $"{GermanColors.GetValueOrDefault(team.Color, team.Color)}: {team.Deaths}K/{team.Downs}Down/{FormatDamage(team.Damage)}");
        return $"{duration} | {sizes} | {string.Join(" | ", parts)}";
    }

    public static string FormatTopDamage(FightSummary fight, string color)
    {
        var team = fight.Teams.FirstOrDefault(item => item.Color.Equals(color, StringComparison.OrdinalIgnoreCase))
                   ?? throw new InvalidOperationException($"Im neuesten Log ist Team {GermanColors.GetValueOrDefault(color, color)} nicht enthalten.");
        if (team.TopDamageDealers.Count == 0)
            throw new InvalidOperationException($"Für Team {GermanColors.GetValueOrDefault(color, color)} wurden keine Damage-Daten gefunden.");

        var entries = string.Join(" | ", team.TopDamageDealers.Select((dealer, index) =>
            $"{index + 1} {dealer.Profession} {FormatDamage(dealer.Damage)}"));
        return $"{color.ToLowerInvariant()} top players: {entries}";
    }

    public static string FormatTopSpecializations(FightSummary fight, string color)
    {
        var team = fight.Teams.FirstOrDefault(item => item.Color.Equals(color, StringComparison.OrdinalIgnoreCase))
                   ?? throw new InvalidOperationException($"Im neuesten Log ist Team {GermanColors.GetValueOrDefault(color, color)} nicht enthalten.");
        if (team.TopSpecializations.Count == 0)
            throw new InvalidOperationException($"Für Team {GermanColors.GetValueOrDefault(color, color)} wurden keine Klassendaten gefunden.");

        var entries = string.Join(" | ", team.TopSpecializations.Select(spec =>
            $"{spec.Players} {spec.Profession} {FormatDamage(spec.Damage)}"));
        return $"{color.ToLowerInvariant()} top classes: {entries}";
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
