using System;
using System.Numerics;

namespace GameServer.Systems.Hazards;

/// <summary>
///     Where a point stands relative to a melding wall: how far from it, and which side.
/// </summary>
public readonly record struct MeldingBearing(float Distance, bool Melded)
{
    public static MeldingBearing Clear => new(float.MaxValue, false);
}

/// <summary>
///     The melding wall as geometry rather than as something to draw.
///
///     A perimeter set is a chain of control points with a tangent at each, which the client draws as a
///     smooth curve; the server holds the same numbers in <c>Melding_ObserverView.ActiveDataProp</c> and
///     has never done anything with them. Tessellating the curve the same way the client does is what
///     lets the two agree on where the wall is, including while a
///     <see cref="Systems.Encounters.Encounters.MeldingRepulsor"/> is pushing a control point around.
///
///     Two things here are read off the shape of the data rather than out of it, so they are assumptions
///     until a client session says otherwise:
///
///     <list type="bullet">
///       <item>
///         The tangents are Hermite tangents. Nothing else makes the curve behave: tessellated this way
///         the 16 perimeters in zone 448 wander 16m to 447m off their straight polylines, which is a wall
///         following ground, and none of them swallows an outpost that the polyline missed.
///       </item>
///       <item>
///         Melded ground is to the <em>left</em> of the directed curve. Checked against all 24 outposts in
///         zone 448: 23 come out clear, and the one that doesn't (151665, faction 9, north of Hydro Core)
///         sits behind Hydro_Core_Melding_01, which is plausible rather than obviously wrong.
///       </item>
///     </list>
///
///     Altitude is ignored. The wall runs from the ground to well above anything a glider reaches, so
///     this is a question about the XY plane only.
/// </summary>
public static class MeldingField
{
    /// <summary>
    ///     Points per curve segment. The client's own subdivision is unknown; 24 puts the tessellation
    ///     error well under a stride at the segment lengths zone 448 actually uses (150m to 1600m).
    /// </summary>
    private const int StepsPerSegment = 24;

    /// <summary>
    ///     Flattens a control-point-and-tangent chain into the polyline everything else here works on.
    ///     Returns empty for anything that isn't at least one segment, which reads downstream as "no wall".
    /// </summary>
    public static Vector2[] Tessellate(Vector3[] points, Vector3[] tangents)
    {
        if (points == null || tangents == null || points.Length < 2 || tangents.Length < points.Length)
        {
            return [];
        }

        var curve = new Vector2[((points.Length - 1) * StepsPerSegment) + 1];
        var next = 0;

        for (var i = 0; i < points.Length - 1; i++)
        {
            var p0 = new Vector2(points[i].X, points[i].Y);
            var p1 = new Vector2(points[i + 1].X, points[i + 1].Y);
            var m0 = new Vector2(tangents[i].X, tangents[i].Y);
            var m1 = new Vector2(tangents[i + 1].X, tangents[i + 1].Y);

            for (var step = 0; step < StepsPerSegment; step++)
            {
                curve[next++] = Hermite(p0, p1, m0, m1, step / (float)StepsPerSegment);
            }
        }

        curve[next] = new Vector2(points[^1].X, points[^1].Y);

        return curve;
    }

    /// <summary>
    ///     Nearest approach to the wall, and whether <paramref name="position"/> is on the melded side of
    ///     the segment it is nearest to.
    /// </summary>
    public static MeldingBearing Locate(Vector2[] curve, Vector3 position)
    {
        if (curve == null || curve.Length < 2)
        {
            return MeldingBearing.Clear;
        }

        var point = new Vector2(position.X, position.Y);
        var bearing = MeldingBearing.Clear;

        for (var i = 0; i < curve.Length - 1; i++)
        {
            var from = curve[i];
            var along = curve[i + 1] - from;
            var offset = point - from;

            var lengthSquared = along.LengthSquared();
            var t = lengthSquared == 0f ? 0f : Math.Clamp(Vector2.Dot(offset, along) / lengthSquared, 0f, 1f);
            var distance = Vector2.Distance(point, from + (t * along));

            if (distance >= bearing.Distance)
            {
                continue;
            }

            // Positive cross product is to the left of the direction of travel, which is the melded side.
            bearing = new MeldingBearing(distance, (along.X * offset.Y) - (along.Y * offset.X) > 0f);
        }

        return bearing;
    }

    private static Vector2 Hermite(Vector2 p0, Vector2 p1, Vector2 m0, Vector2 m1, float t)
    {
        var t2 = t * t;
        var t3 = t2 * t;

        return (((2f * t3) - (3f * t2) + 1f) * p0)
             + ((t3 - (2f * t2) + t) * m0)
             + (((-2f * t3) + (3f * t2)) * p1)
             + ((t3 - t2) * m1);
    }
}
