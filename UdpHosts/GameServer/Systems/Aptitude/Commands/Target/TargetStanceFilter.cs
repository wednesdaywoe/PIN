using System;

namespace GameServer.Systems.Aptitude.Commands.Target;

/// <summary>
///     Shared by TargetHostiles and TargetFriendlies: keeps the current targets that match a stance towards Self
/// </summary>
internal static class TargetStanceFilter
{
    public static bool Apply(Context context, byte includeSelf, byte includeInitiator, byte includeOwner, byte failNoTargets, Func<IAptitudeTarget, bool> matchesStance)
    {
        // As the client (apt::TargetHostilesCommand): filters the targets in place and leaves the former targets alone
        if (failNoTargets == 1 && context.Targets.Count == 0)
        {
            return false;
        }

        context.Targets.RemoveAll(target => !Keep(target));

        return failNoTargets == 0 || context.Targets.Count > 0;

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
