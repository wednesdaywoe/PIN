using System.Numerics;
using GameServer.Entities;

namespace GameServer.Systems.Admin.Commands;

/// <summary>
///     Says what the server believes is in front of you, and what is under you.
///
///     Both readings were unobtainable in game until terrain loaded, and their absence cost a whole
///     investigation on 2026-08-17: a tester was shot through a rock formation, and settling whether that
///     rock has collision at all meant rebuilding the zone's geometry offline, because nothing could ask
///     the question standing in front of it. <c>target</c> casts the same ray and throws the answer away
///     when it strikes the world, reporting "Failed to find target" — which reads identically to a ray
///     that met nothing at all, and those are the two answers that had to be told apart.
///
///     So the interesting result here is the boring-looking one. <b>Aiming squarely at something solid
///     and being told the ray found nothing means that object has no collision on the server</b>, which
///     is <c>DATA-22</c> — 252 objects in zone 448 convert to an empty shape at load and are simply
///     absent. Aim at ground or a building and the same command should answer immediately.
/// </summary>
[ServerCommand(
    "Report what your aim ray hits and where the ground under you is",
    "probe [range]",
    "probe",
    "ray")]
public class ProbeServerCommand : ServerCommand
{
    private const float DefaultRange = 500f;

    public override void Execute(string[] parameters, ServerCommandContext context)
    {
        var character = context.SourcePlayer?.CharacterEntity;
        if (character == null)
        {
            SourceFeedback("Cannot probe without a player character", context);
            return;
        }

        var range = DefaultRange;
        if (parameters.Length > 0 && float.TryParse(parameters[0], out var asked) && asked > 0f)
        {
            range = asked;
        }

        var direction = character.AimDirection;
        var origin = character.GetProjectileOrigin(direction);
        var probe = context.Shard.Physics.ProbeRay(origin, direction, character, range);

        SourceFeedback(Describe(probe, range, context), context);

        // The ground reading is the other half of the same instrument, and it is what every placement
        // decision in the zone has had to infer from a walked footing. Reported against the character's
        // own Z so "am I standing on it or in it" is one subtraction rather than two numbers to compare.
        if (context.Shard.Physics.TryGetGroundHeight(character.Position, out var groundZ))
        {
            var above = character.Position.Z - groundZ;
            SourceFeedback($"ground at Z {groundZ:0.00}, you are {above:0.00}m above it", context);
        }
        else
        {
            SourceFeedback("ground: nothing under you — a hole in the collision, or outside the loaded chunks", context);
        }
    }

    private static string Describe(Physics.PhysicsEngine.RayProbe probe, float range, ServerCommandContext context)
    {
        if (!probe.Hit)
        {
            return $"probe: nothing within {range:0}m. If you are aiming at something solid, that object has no collision on the server";
        }

        if (probe.IsWorld)
        {
            return $"probe: world at {probe.Position}, {probe.Distance:0.00}m ahead (static {probe.ShapeHandle})";
        }

        var named = context.Shard.Entities.TryGetValue(probe.EntityId, out IEntity entity)
            ? entity.ToString()
            : $"entity {probe.EntityId}";

        return $"probe: {named} at {probe.Position}, {probe.Distance:0.00}m ahead";
    }
}
