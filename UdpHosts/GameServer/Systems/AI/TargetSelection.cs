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
    /// <summary>
    ///     How close something has to be before an NPC starts building threat on it.
    ///
    ///     Was 40m through M2, which is wider than anything retail shipped: the `perceptionDist` values
    ///     parsed out of the behaviour strings top out at 25m and are most often 10 or 15 (DATA-10).
    ///     40m was kept deliberately while the spawn groups were being proven, so a placement failure
    ///     couldn't be confused with a tuning change, and it cost something concrete — zone 448's
    ///     starting shelf is about 45m across with Aero standing on it, so nothing hostile fitted on it
    ///     and the zone had nothing to fight within 120m of where a player logs in.
    ///
    ///     Narrowed to retail's widest rather than its most common. 25m is the smallest change that
    ///     puts a group back on the shelf, and staying at the top of the shipped range keeps NPCs as
    ///     alert as anything retail had while PIN has no other detection cue — no hearing, no gunfire
    ///     response, nothing but distance and line of sight.
    ///
    ///     Still one number for every monster in the game. Reading it per monster would not help zone
    ///     448: 1196, 528 and 2342 are all in the third of `dbcharacter::Monster` that carries only a
    ///     `BehaviorInstanceId`, and the rows behind those went to a server db that didn't ship.
    /// </summary>
    public const float PerceptionRange = 25f;

    /// <summary>
    ///     How far it has to get before the NPC forgets it entirely. Deliberately wider than
    ///     <see cref="PerceptionRange"/>: with one radius doing both jobs an NPC drops and re-acquires
    ///     its target every time you shuffle across the boundary.
    ///
    ///     Without this, engagement was bounded by the *weapon's* reach instead — 180m for a Chosen
    ///     Grunt Rifle against the perception radius — so an NPC that noticed you at the edge kept
    ///     firing until you left draw distance, which is what N4 caught.
    ///
    ///     Held at 1.5x perception when that narrowed, so the gap that stops the acquire/drop flapping
    ///     stays proportional rather than swallowing the whole radius.
    /// </summary>
    public const float LeashRange = 37.5f;

    private const float ThreatGainPerSecond = 20f;
    private const float ThreatDecayPerSecond = 8f;
    private const float EngageThreshold = 10f;

    /// <summary>
    ///     Ceiling on banked threat. Sets how long an NPC stays interested once it loses sight of a
    ///     target without that target leaving the leash: from the cap, (60 - 10) / 8 is 6.25 seconds of
    ///     decay to fall back under <see cref="EngageThreshold"/>.
    /// </summary>
    private const float MaxThreat = 60f;

    public static void Tick(IShard shard, CharacterEntity npc, AIState state, float elapsedSeconds)
    {
        state.Threat.Decay(ThreatDecayPerSecond * elapsedSeconds);

        var gain = ThreatGainPerSecond * elapsedSeconds;
        foreach (var entity in shard.Entities.Values)
        {
            if (entity is not CharacterEntity candidate || candidate == npc)
            {
                continue;
            }

            if (Vector3.DistanceSquared(npc.Position, candidate.Position) > LeashRange * LeashRange)
            {
                state.Threat.Forget(candidate.EntityId);
                continue;
            }

            if (IsVisibleHostile(shard, npc, candidate))
            {
                state.Threat.AddThreat(candidate.EntityId, gain, MaxThreat);
            }
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

        if (!Sightline.TrySolve(npc, candidate, out var shot) || shot.Separation > PerceptionRange)
        {
            return false;
        }

        return Sightline.IsClear(shard, npc, candidate, shot);
    }
}
