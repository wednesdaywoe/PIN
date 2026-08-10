namespace GameServer.Systems.Combat;

/// <summary>
///     Linear falloff for area damage. A target inside the point blank range takes the whole hit, and from
///     there damage falls in a straight line to nothing at the edge of the radius.
///
///     Like the range decay curve this shape is a guess; the SDB gives a radius and a point blank range but
///     not how the client interpolates between them.
/// </summary>
public static class SplashFalloff
{
    /// <summary>
    ///     The fraction of full damage a target <paramref name="distance"/> metres from the centre takes.
    ///     A <paramref name="pointBlankRange"/> at or past <paramref name="radius"/> leaves nothing to
    ///     interpolate over and means full damage everywhere inside the radius.
    /// </summary>
    public static float Scale(float distance, float radius, float pointBlankRange)
    {
        if (distance <= pointBlankRange || radius <= pointBlankRange)
        {
            return 1f;
        }

        if (distance >= radius)
        {
            return 0f;
        }

        return 1f - ((distance - pointBlankRange) / (radius - pointBlankRange));
    }
}
