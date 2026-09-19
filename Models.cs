namespace WvWSummaryTool;

internal sealed record DamageDealer(string Profession, long Damage);
internal sealed record SpecializationDamage(string Profession, int Players, long Damage);
internal sealed record TeamSummary(string Color, int Players, int Deaths, int Downs, long Damage,
    bool IsPlayerTeam, IReadOnlyList<DamageDealer> TopDamageDealers,
    IReadOnlyList<SpecializationDamage> TopSpecializations);
internal sealed record FightSummary(string SourceFile, TimeSpan Duration, IReadOnlyList<TeamSummary> Teams);

internal sealed class Agent
{
    public ulong Address { get; init; }
    public string AccountName { get; init; } = "";
    public int Subgroup { get; init; } = -1;
    public string Profession { get; init; } = "Unknown";
    public ushort InstanceId { get; set; }
    public string Team { get; set; } = "Unknown";
    public long Damage { get; set; }
}

internal sealed class MutableTeamSummary
{
    public int Players;
    public int Deaths;
    public int Downs;
    public long Damage;
    public bool IsPlayerTeam;
}

internal readonly record struct CombatEvent(ulong Time, ulong SourceAgent, ulong DestinationAgent,
    int Value, int BuffDamage, uint SkillId, ushort SourceInstanceId, ushort DestinationInstanceId,
    byte Buff, byte Result, byte Activation, byte BuffRemove, byte StateChange);
