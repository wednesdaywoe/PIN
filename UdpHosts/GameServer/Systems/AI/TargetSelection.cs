using System.Numerics;
using GameServer.Entities.Character;
using GameServer.Systems.Hostility;

namespace GameServer.Systems.AI;

/// <summary>
///     Scores what an NPC can see into its threat table and picks a current target from it.
///     Nothing in dbmonster carries a perception or aggro radius, so the constants below are an
///     invented first pass, not shipped data (tracked as DATA-10).
/// </summary>
public static class TargetSelection
{
    public const float PerceptionRange = 40f;
    private const float ThreatGainPerSecond = 20f;
    private const float ThreatDecayPerSecond = 8f;
    private const float EngageThreshold = 10f;
    private const float TargetAimHeight = 1.5f;

    public static void Tick(IShard shard, CharacterEntity npc, AIState state, double deltaTime)
    {
        state.Threat.Decay((float)(ThreatDecayPerSecond * deltaTime));

        var gain = (float)(ThreatGainPerSecond * deltaTime);
        foreach (var entity in shard.Entities.Values)
        {
            if (entity is not CharacterEntity candidate || candidate == npc || !IsVisibleHostile(shard, npc, candidate))
            {
                continue;
            }

            state.Threat.AddThreat(candidate.EntityId, gain);
        }

        state.CurrentTargetId = PickTarget(shard, npc, state.Threat);
    }

    private static ulong? PickTarget(IShard shard, CharacterEntity npc, ThreatTable threat)
    {
        foreach (var (candidateId, score) in threat.EntriesByThreatDescending())
        {
            if (!shard.Entities.TryGetValue(candidateId, out var entity)
                || entity is not CharacterEntity candidate
                || !candidate.IsAlive
                || !HostilityRules.AreHostile(npc, candidate))
            {
                continue;
            }

            // Highest score first, so if the best live candidate hasn't crossed the threshold yet
            // nothing else in the table has either.
            return score >= EngageThreshold ? candidateId : null;
        }

        return null;
    }

    private static bool IsVisibleHostile(IShard shard, CharacterEntity npc, CharacterEntity candidate)
    {
        if (!candidate.IsAlive || !HostilityRules.AreHostile(npc, candidate))
        {
            return false;
        }

        var toCandidate = candidate.Position - npc.Position;
        var distance = toCandidate.Length();
        if (distance <= 0f || distance > PerceptionRange)
        {
            return false;
        }

        var origin = npc.GetProjectileOrigin(toCandidate / distance);
        var targetPoint = candidate.Position + new Vector3(0f, 0f, TargetAimHeight);
        var toTarget = targetPoint - origin;
        var rayLength = toTarget.Length();
        if (rayLength <= 0f)
        {
            return true;
        }

        var (hit, _, hitEntityId) = shard.Physics.TargetRayCast(origin, toTarget / rayLength, npc, rayLength);
        return !hit || hitEntityId == candidate.EntityId;
    }
}
