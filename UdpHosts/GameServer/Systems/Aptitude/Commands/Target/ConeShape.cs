using System;
using System.Numerics;

namespace GameServer.Systems.Aptitude.Commands.Target;

/// <summary>
///     The volume a TargetConeAE command picks targets from, as the client builds it (apt::TargetConeAECommand, 0x1406050):
///     a cone cut off at both ends, laid along the aim from <see cref="Origin" />. Its radius is <see cref="StartRadius" />
///     at the origin and grows evenly to <see cref="EndRadius" /> at <see cref="Length" />. With both radii set and no
///     angle (229 of 622 defs) it is a tube that widens; with only an angle it is a plain cone.
/// </summary>
internal readonly struct ConeShape
{
    // An angle of 180 or more has no cone: the data has ten at 180 and two at 360. Read as everything within range.
    private const float WholeSphereAngle = 180f;

    public ConeShape(Vector3 origin, Vector3 direction, float length, float angleDegrees, float startRadius, float maxRadius, bool roundEnds)
    {
        Origin = origin;
        Direction = direction.LengthSquared() > 1e-6f ? Vector3.Normalize(direction) : Vector3.UnitY;
        Length = MathF.Max(0f, length);
        Sphere = angleDegrees >= WholeSphereAngle;
        StartRadius = MathF.Max(0f, startRadius);

        // Angles go up to 360, so Angle is the cone's full spread and each side opens by half of it. The client adds
        // the angle's widening to MaxRadius rather than replacing it.
        var halfAngle = Math.Clamp(angleDegrees, 0f, WholeSphereAngle) * 0.5f * MathF.PI / 180f;
        EndRadius = Sphere ? Length : MathF.Max(0f, maxRadius) + Length * MathF.Tan(halfAngle);
        RoundEnds = roundEnds;
    }

    public Vector3 Origin { get; }
    public Vector3 Direction { get; }
    public float Length { get; }
    public float StartRadius { get; }
    public float EndRadius { get; }
    public bool Sphere { get; }

    /// <summary>
    ///     IgnorePastEndpoints=0: something just behind the origin or just past the far end still counts, within that
    ///     end's radius. The use key (ability 187) is one of these, so a thumper at your side is still in reach.
    /// </summary>
    public bool RoundEnds { get; }

    /// <summary>
    ///     How far along the aim the point lies (0 at the origin), or <see cref="float.NaN" /> when it is outside
    /// </summary>
    public float AlongIfInside(Vector3 point, float allowance = 0f)
    {
        var offset = point - Origin;
        if (Sphere)
        {
            return offset.Length() <= Length + allowance ? 0f : float.NaN;
        }

        var along = Vector3.Dot(offset, Direction);
        var across = (offset - (Direction * along)).Length();

        if (along >= 0f && along <= Length)
        {
            var t = Length > 0f ? along / Length : 0f;
            var radius = StartRadius + ((EndRadius - StartRadius) * t);
            return across <= radius + allowance ? along : float.NaN;
        }

        if (!RoundEnds)
        {
            return float.NaN;
        }

        var (end, endRadius) = along < 0f ? (Origin, StartRadius) : (Origin + (Direction * Length), EndRadius);
        return Vector3.Distance(point, end) <= endRadius + allowance ? along : float.NaN;
    }

    /// <summary>
    ///     How far off the aim line the point is, as 1 - cos of the angle: 0 dead ahead, 2 straight behind. The client
    ///     sorts by this when SortByAngle is set.
    /// </summary>
    public float AngleKey(Vector3 point)
    {
        var offset = point - Origin;
        return offset.LengthSquared() > 1e-6f ? 1f - Vector3.Dot(Vector3.Normalize(offset), Direction) : 0f;
    }
}
