using System.Numerics;

namespace Shared.Collision.Tagfile;

/// <summary>
///     Preparing an <c>hkpConvexVerticesShape</c>'s points for Bepu's hull builder, which is stricter than
///     Havok about what it will accept (DATA-22).
/// </summary>
internal static class ConvexPoints
{
    /// <summary>
    ///     Three well-spread points of the set and the normal of the plane through them, or a zero normal
    ///     when the points are collinear.
    /// </summary>
    public static Vector3 SpreadPlaneNormal(Vector3[] vertices, out Vector3 origin)
    {
        origin = vertices[0];
        var a = origin;
        var b = vertices.MaxBy(v => Vector3.DistanceSquared(v, a));
        var ab = b - a;
        var c = vertices.MaxBy(v => Vector3.Cross(ab, v - a).LengthSquared());
        var normal = Vector3.Cross(ab, c - a);
        return normal.LengthSquared() > 0f ? Vector3.Normalize(normal) : Vector3.Zero;
    }

    public static float Thickness(Vector3[] vertices, Vector3 normal, Vector3 origin)
    {
        return normal == Vector3.Zero ? 0f : vertices.Max(v => MathF.Abs(Vector3.Dot(v - origin, normal)));
    }

    public static Vector3[] ThickenIfFlat(Vector3[] vertices, float radius)
    {
        if (vertices.Length < 3 || radius <= 0f)
        {
            return vertices;
        }

        var normal = SpreadPlaneNormal(vertices, out var origin);
        if (normal == Vector3.Zero || Thickness(vertices, normal, origin) > radius * 0.1f)
        {
            return vertices;
        }

        var slab = new Vector3[vertices.Length * 2];
        for (var i = 0; i < vertices.Length; i++)
        {
            slab[2 * i] = vertices[i] + (normal * radius);
            slab[(2 * i) + 1] = vertices[i] - (normal * radius);
        }

        return slab;
    }

    public static Vector3[] Weld(Vector3[] vertices, float tolerance)
    {
        var kept = new List<Vector3>(vertices.Length);
        var toleranceSquared = tolerance * tolerance;
        foreach (var v in vertices)
        {
            if (!kept.Exists(k => Vector3.DistanceSquared(k, v) <= toleranceSquared))
            {
                kept.Add(v);
            }
        }

        return kept.ToArray();
    }

    public static Vector3 BoundsCentre(Vector3[] vertices)
    {
        if (vertices.Length == 0)
        {
            return Vector3.Zero;
        }

        var min = vertices[0];
        var max = vertices[0];
        foreach (var v in vertices)
        {
            min = Vector3.Min(min, v);
            max = Vector3.Max(max, v);
        }

        return (min + max) / 2;
    }

    /// <summary>
    ///     What a failed point set looks like after recentring and welding, for the warning: how many
    ///     points, the convex radius Havok pads it by, its extents, and how far it is from flat. Thickness
    ///     is the largest distance of any point from the plane through three well-spread points of the set.
    /// </summary>
    public static string Describe(Vector3[] vertices, float radius)
    {
        if (vertices.Length == 0)
        {
            return "No vertices.";
        }

        var min = vertices[0];
        var max = vertices[0];
        foreach (var v in vertices)
        {
            min = Vector3.Min(min, v);
            max = Vector3.Max(max, v);
        }

        var thickness = Thickness(vertices, SpreadPlaneNormal(vertices, out var origin), origin);

        return $"{vertices.Length} vertices, radius {radius}, extents {max - min}, thickness {thickness}";
    }
}
