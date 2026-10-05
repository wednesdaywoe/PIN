using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using BepuUtilities;
using GameServer.Entities;
using GameServer.Entities.Character;
using GameServer.Entities.Deployable;
using GameServer.Entities.Vehicle;
using GameServer.Enums;
using GameServer.StaticDB.Records.apt;

namespace GameServer.Systems.Aptitude.Commands.Target;

/// <summary>
///     Picks everyone inside a cone along the aim: melee, the use key, Healing Wave, Thermal Wave and about 350 more.
///     Read from the client's apt::TargetConeAECommand (0x1406050); see <see cref="ConeShape" /> for the volume.
/// </summary>
public class TargetConeAECommand : Command, ICommand
{
    // The client queries hitboxes. The server only knows where a target stands, so it tests three points up its body
    // and allows half a metre for its width.
    private static readonly float[] BodyHeights = [0f, 0.9f, 1.8f];
    private const float BodyAllowance = 0.5f;

    private TargetConeAECommandDef Params;

    public TargetConeAECommand(TargetConeAECommandDef par)
: base(par)
    {
        Params = par;
    }

    public bool Execute(Context context)
    {
        // Unread: IncludeInteractives (TargetPBAE doesn't separate them either), ScaleOffset and ScaleQuerySize (no
        // entity scale on the server), AimRadiusBias (0 in every def) and AimDirOffset/AimPosOffset (zero in every def).
        var shape = BuildShape(context);
        var candidates = context.Shard.Entities.Values.OfType<IAptitudeTarget>();
        Func<IAptitudeTarget, bool> inSight = null;
        if (Params.IgnoreWalls == 0)
        {
            var physics = context.Shard.Physics;
            inSight = target =>
            {
                if (WallCheck.InSight(shape.Origin, target.Position, physics.TryHitWorld))
                {
                    return true;
                }

                Logger.Debug("{Command} {CommandId} {Target} is behind a wall from {Origin}", nameof(TargetConeAECommand), Params.Id, target.EntityId, shape.Origin);
                return false;
            };
        }

        var hits = Pick(shape, candidates, context.Self, Params.SortByAngle == 1, Params.MaxTargets, inSight);

        Logger.Debug("{Command} {CommandId} cone {Length:0.#}m, radius {Start:0.#} to {End:0.#}m from {Origin} along {Direction}: {Hits} hits",
            nameof(TargetConeAECommand), Params.Id, shape.Length, shape.StartRadius, shape.EndRadius, shape.Origin, shape.Direction, hits.Count);

        if (hits.Count < Params.MinTargets)
        {
            return false;
        }

        if (Params.Filter == 1)
        {
            // As the client: keep only the targets already picked that are also in the cone
            context.Targets.RemoveAll(target => !hits.Contains(target));
            return true;
        }

        foreach (var hit in hits)
        {
            if (!context.Targets.Contains(hit))
            {
                context.Targets.Push(hit);
            }
        }

        return true;
    }

    internal static List<IAptitudeTarget> Pick(ConeShape shape, IEnumerable<IAptitudeTarget> candidates, IAptitudeTarget self, bool sortByAngle, int maxTargets, Func<IAptitudeTarget, bool> inSight = null)
    {
        var hits = new List<(IAptitudeTarget Target, float Key)>();
        foreach (var candidate in candidates)
        {
            // The cone starts inside whoever casts it
            if (candidate == null || candidate == self)
            {
                continue;
            }

            foreach (var height in BodyHeights)
            {
                var point = candidate.Position + new Vector3(0f, 0f, height);
                if (!float.IsNaN(shape.AlongIfInside(point, BodyAllowance)))
                {
                    // Behind a wall is checked last, and before MaxTargets, as the client's query drops it outright
                    if (inSight != null && !inSight(candidate))
                    {
                        break;
                    }

                    var key = sortByAngle ? shape.AngleKey(point) : Vector3.Distance(shape.Origin, point);
                    hits.Add((candidate, key));
                    break;
                }
            }
        }

        // Nearest first, or closest to the aim line first; MaxTargets 0 means no limit
        IEnumerable<IAptitudeTarget> ordered = hits.OrderBy(hit => hit.Key).Select(hit => hit.Target);
        if (maxTargets > 0)
        {
            ordered = ordered.Take(maxTargets);
        }

        return ordered.ToList();
    }

    private ConeShape BuildShape(Context context)
    {
        var self = context.Self;
        var length = AbilitySystem.RegistryOp(context.Register, Params.Range, (Operand)Params.RangeRegop);
        var startRadius = AbilitySystem.RegistryOp(context.Register, Params.MinRadius, (Operand)Params.RadiusRegop);
        var maxRadius = AbilitySystem.RegistryOp(context.Register, Params.MaxRadius, (Operand)Params.RadiusRegop);

        var direction = Params.UseBodyOrient == 1 ? BodyForward(self) : AimOf(self) ?? BodyForward(self);

        Vector3 origin;
        if (Params.UseInitPos == 1)
        {
            origin = context.InitPosition;
        }
        else if (Params.UseBodyPosition == 0 && self is CharacterEntity character)
        {
            // Where a shot would leave from, so a cone and a gun aimed the same way agree
            origin = character.GetProjectileOrigin(direction);
        }
        else
        {
            // UseBodyPosition: the client lifts the body position by a fixed height
            origin = self.Position + new Vector3(0f, 0f, 1f);
        }

        return new ConeShape(origin, direction, length, Params.Angle, startRadius, maxRadius, Params.IgnorePastEndpoints == 0);
    }

    private static Vector3? AimOf(IAptitudeTarget self) => self switch
    {
        CharacterEntity character => character.AimDirection,
        VehicleEntity vehicle => vehicle.AimDirection,
        DeployableEntity deployable => deployable.AimDirection,
        _ => null,
    };

    /// <summary>
    ///     The way the body faces, flat on the ground, by the same rotation <see cref="CharacterEntity.GetProjectileOrigin(Vector3)" /> uses
    /// </summary>
    private static Vector3 BodyForward(IAptitudeTarget self)
    {
        if (self is not BaseEntity entity)
        {
            return Vector3.UnitY;
        }

        var forward = QuaternionEx.Transform(Vector3.UnitY, QuaternionEx.Inverse(entity.Orientation));
        forward.Z = 0f;
        return forward.LengthSquared() > 1e-6f ? Vector3.Normalize(forward) : Vector3.UnitY;
    }
}
