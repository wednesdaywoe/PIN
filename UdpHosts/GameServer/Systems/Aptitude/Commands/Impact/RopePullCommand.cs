using System;
using System.Numerics;
using GameServer.Entities.Character;
using GameServer.StaticDB.Records.aptfs;
using GameServer.Systems.AI;

namespace GameServer.Systems.Aptitude.Commands.Impact;

/// <summary>
///     Pulls Self toward the first target: Gravity Pull's victims toward the Dreadnaught who used it (Range 15, Speed 25).
///     The client only runs this when Self is its own player, as forced movement type 4 (tfRopePullCommand, 0xebaaa0),
///     so a pulled NPC is moved here: thrown in an arc that lands <see cref="StopShort" /> m in front of the puller,
///     travelling Speed m/s across the ground and at most Range m. Pulled players are not handled; only PvP pulls them.
/// </summary>
public class RopePullCommand : Command, ICommand
{
    /// <summary>
    ///     Where a pulled NPC is aimed to land, in metres short of the puller: close enough to hit, not inside them. The
    ///     flight is stepped every 50 ms and lands a step or two late, so at 25 m/s it comes down up to 2.5 m nearer.
    /// </summary>
    public const float StopShort = 5f;

    private RopePullCommandDef Params;

    public RopePullCommand(RopePullCommandDef par)
: base(par)
    {
        Params = par;
    }

    public bool Execute(Context context)
    {
        if (context.Targets.Count == 0 || context.Self is not CharacterEntity victim || !victim.IsAlive)
        {
            return true;
        }

        if (victim.IsPlayerControlled)
        {
            Logger.Debug("{Command} {CommandId}: not pulling player {Victim}, only NPCs are pulled", nameof(RopePullCommand), Params.Id, victim);
            return true;
        }

        if (victim.ImmunePhysics)
        {
            Logger.Debug("{Command} {CommandId} doesn't move {Victim}, which an effect makes immune to physics", nameof(RopePullCommand), Params.Id, victim);
            return true;
        }

        var puller = context.Targets.Peek();
        var velocity = Pull(victim.Position, puller.Position, Params.Speed, Params.Range);
        if (velocity == Vector3.Zero)
        {
            return true;
        }

        Logger.Debug("{Command} {CommandId}: pulling {Victim} toward {Puller}", nameof(RopePullCommand), Params.Id, victim, puller);
        context.Shard.AI.KnockBack(victim, velocity);
        return true;
    }

    /// <summary>
    ///     The launch velocity of an arc from <paramref name="from" /> that lands <see cref="StopShort" /> m before
    ///     <paramref name="to" /> (or <paramref name="range" /> m along the way, if nearer), crossing the ground at
    ///     <paramref name="speed" />. Zero when already that close.
    /// </summary>
    internal static Vector3 Pull(Vector3 from, Vector3 to, float speed, float range)
    {
        var toward = to - from;
        toward.Z = 0f;
        var travel = MathF.Min(toward.Length() - StopShort, range > 0f ? range : float.MaxValue);
        if (travel <= 0.1f || speed <= 0f)
        {
            return Vector3.Zero;
        }

        var seconds = travel / speed;
        var ground = Vector3.Normalize(toward) * speed;
        return new Vector3(ground.X, ground.Y, NpcKnockback.Gravity * seconds / 2f);
    }
}
