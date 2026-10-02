using System;

namespace GameServer.Systems.Aptitude.Commands.Target;

/// <summary>
///     Shared by TargetHostiles and TargetFriendlies: keeps the current targets that match a stance towards Self
/// </summary>
internal static class TargetStanceFilter
{
    public static bool Apply(Context context, byte includeSelf, byte includeInitiator, byte includeOwner, byte failNoTargets, Func<IAptitudeTarget, bool> matchesStance)
    {
        var previousTargets = context.Targets;
        var newTargets = new AptitudeTargets();

        foreach (var target in previousTargets)
        {
            if (Keep(target))
            {
                newTargets.Push(target);
            }
        }

        context.FormerTargets = previousTargets;
        context.Targets = newTargets;

        return failNoTargets == 0 || newTargets.Count > 0;

        // Self, initiator and owner only make it through when asked for, whatever their stance.
        // The SDB sets IncludeSelf on 222 TargetHostiles, which only means something if it forces self in.
        bool Keep(IAptitudeTarget target)
        {
            if (target == context.Self)
            {
                return includeSelf == 1;
            }

            if (target == context.Initiator)
            {
                return includeInitiator == 1;
            }

            if (context.Self.Owner != null && target == context.Self.Owner)
            {
                return includeOwner == 1;
            }

            return matchesStance(target);
        }
    }
}
