namespace GameServer.Systems.AI;

public sealed class AIState
{
    public ThreatTable Threat { get; } = new();
    public ulong? CurrentTargetId { get; set; }
}
