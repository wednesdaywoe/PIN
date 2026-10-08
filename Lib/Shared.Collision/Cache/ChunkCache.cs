using System.Diagnostics;
using System.Numerics;
using BepuPhysics;
using BepuUtilities;
using BepuUtilities.Memory;
using Serilog;
using Shared.Collision.ZoneLoading;

namespace Shared.Collision.Cache;

public static class ChunkCache
{
    // 2: convex hulls are welded, recentred and given thickness when flat (DATA-22), so a version-1
    // cache holds the placeholder boxes those shapes used to become.
    // 3: also holds the chunk's movement blockers and its water materials, after the statics.
    private const int _formatVersion = 3;
    private static readonly byte[] _magic = "PCCK"u8.ToArray();
    private static readonly ILogger _logger = Log.ForContext(typeof(ChunkCache));

    public static string GetCachePath(string cacheDir, string chunkName)
    {
        var dir = Path.Combine(cacheDir, "chunks");
        Directory.CreateDirectory(dir);
        return Path.Combine(dir, $"{chunkName}.chunkcache");
    }

    /// <summary>
    ///     Writes a chunk's collision. The statics' shapes are read from <paramref name="world"/> and the
    ///     blockers' from <paramref name="blockers"/>, the simulations each was built in.
    /// </summary>
    public static void Save(Simulation world, Simulation blockers, BufferPool pool, ChunkCollision collision, string path)
    {
        var stopwatch = Stopwatch.StartNew();

        using var fs = new FileStream(path, FileMode.Create, FileAccess.Write, FileShare.None, 65536);
        using var writer = new BinaryWriter(fs);

        writer.Write(_magic);
        writer.Write(_formatVersion);
        WriteStatics(world, pool, writer, collision.Statics);
        WriteStatics(blockers, pool, writer, collision.Blockers);

        writer.Write(collision.WaterMaterialIds.Length);
        foreach (var id in collision.WaterMaterialIds)
        {
            writer.Write(id);
        }

        stopwatch.Stop();
        _logger.Information(
            "ChunkCache: Saved {StaticCount} statics, {BlockerCount} blockers and {WaterCount} water material(s) to {Path} in {Elapsed}ms",
            collision.Statics.Length,
            collision.Blockers.Length,
            collision.WaterMaterialIds.Length,
            path,
            stopwatch.ElapsedMilliseconds);
    }

    public static bool TryLoad(Simulation world, Simulation blockers, BufferPool pool, ThreadDispatcher dispatcher, string path, out ChunkCollision collision)
    {
        collision = ChunkCollision.Empty;

        if (!File.Exists(path))
        {
            return false;
        }

        try
        {
            var stopwatch = Stopwatch.StartNew();

            using var fs = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read, 65536);
            using var reader = new BinaryReader(fs);

            var magic = reader.ReadBytes(4);
            if (magic.Length < 4 || magic[0] != _magic[0] || magic[1] != _magic[1] || magic[2] != _magic[2] || magic[3] != _magic[3])
            {
                _logger.Error("ChunkCache: Invalid magic header.");
                return false;
            }

            var version = reader.ReadInt32();
            if (version != _formatVersion)
            {
                _logger.Information("ChunkCache: Version mismatch (expected {_formatVersion}, got {version}).", _formatVersion, version);
                return false;
            }

            var statics = ReadStatics(world, pool, dispatcher, reader);
            var blockerStatics = ReadStatics(blockers, pool, dispatcher, reader);

            var waterCount = reader.ReadInt32();
            var water = new uint[waterCount];
            for (var i = 0; i < waterCount; i++)
            {
                water[i] = reader.ReadUInt32();
            }

            collision = new ChunkCollision(statics, blockerStatics, water);

            stopwatch.Stop();
            _logger.Information("ChunkCache: Loaded {StaticCount} statics and {BlockerCount} blockers from cache in {Elapsed}ms", statics.Length, blockerStatics.Length, stopwatch.ElapsedMilliseconds);
            return true;
        }
        catch (Exception e)
        {
            _logger.Error("ChunkCache: Failed to load: {Message} ({Type})", e.Message, e.GetType().Name);
            return false;
        }
    }

    private static void WriteStatics(Simulation simulation, BufferPool pool, BinaryWriter writer, StaticDescription[] statics)
    {
        writer.Write(statics.Length);

        foreach (var stat in statics)
        {
            writer.Write(stat.Shape.Type);

            writer.Write(stat.Pose.Position.X);
            writer.Write(stat.Pose.Position.Y);
            writer.Write(stat.Pose.Position.Z);
            writer.Write(stat.Pose.Orientation.X);
            writer.Write(stat.Pose.Orientation.Y);
            writer.Write(stat.Pose.Orientation.Z);
            writer.Write(stat.Pose.Orientation.W);

            ShapeSerializer.WriteShape(simulation, pool, writer, stat.Shape);
        }
    }

    private static StaticDescription[] ReadStatics(Simulation simulation, BufferPool pool, ThreadDispatcher dispatcher, BinaryReader reader)
    {
        var count = reader.ReadInt32();
        var result = new StaticDescription[count];

        for (var i = 0; i < count; i++)
        {
            var shapeTypeId = reader.ReadInt32();

            var pose = new RigidPose
            {
                Position = new Vector3(reader.ReadSingle(), reader.ReadSingle(), reader.ReadSingle()),
                Orientation = new Quaternion(reader.ReadSingle(), reader.ReadSingle(), reader.ReadSingle(), reader.ReadSingle())
            };

            var shapeIndex = ShapeSerializer.ReadShape(simulation, pool, dispatcher, reader, shapeTypeId);
            result[i] = new StaticDescription(pose, shapeIndex);
        }

        return result;
    }
}
