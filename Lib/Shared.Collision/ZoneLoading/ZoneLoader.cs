using System.Diagnostics;
using BepuPhysics;
using BepuUtilities;
using BepuUtilities.Memory;
using Serilog;
using Shared.Collision.Layers;
using Shared.Collision.Zone;

namespace Shared.Collision.ZoneLoading;

public class ZoneLoader
{
    private static readonly ILogger _logger = Log.Logger.ForContext<ZoneLoader>();

    private readonly Simulation _simulation;
    private readonly Simulation _blockers;
    private readonly BufferPool _pool;
    private readonly ThreadDispatcher _dispatcher;
    private readonly string _mapsPath;
    private readonly string _cachePath;

    /// <param name="simulation">Takes the zone's static geometry.</param>
    /// <param name="blockers">
    ///     Takes the zone's movement blockers, kept apart so that nothing querying the world for shots,
    ///     sight lines or ground can meet one.
    /// </param>
    public ZoneLoader(Simulation simulation, Simulation blockers, BufferPool pool, ThreadDispatcher dispatcher, string mapsPath, string cachePath)
    {
        _simulation = simulation;
        _blockers = blockers;
        _pool = pool;
        _dispatcher = dispatcher;
        _mapsPath = mapsPath;
        _cachePath = cachePath;
    }

    /// <summary>
    ///     The physics materials of every water body in the loaded chunks. Empty until a zone loads, and
    ///     empty for good when map collision is off.
    /// </summary>
    public HashSet<uint> WaterMaterialIds { get; } = [];

    public long? LoadZone(uint zoneId, bool forceReload = false)
    {
        var stopwatch = Stopwatch.StartNew();

        var zoneFilePath = Path.Combine(_mapsPath, $"{zoneId}.zone");

        if (!File.Exists(zoneFilePath))
        {
            _logger.Error("Zone file not found: {Path}", zoneFilePath);
            return null;
        }

        var zone = ZoneFileReader.Read(zoneFilePath);

        if (zone.Root is not ZoneRootLayer rootLayer)
        {
            _logger.Error("Invalid zone root layer for zone {ZoneId}", zoneId);
            return null;
        }

        var chunkRefs = ChunkOriginCalculator.ExtractChunks(rootLayer, zoneId);

        _logger.Information($"Zone {{ZoneId}} ({{ZoneName}}): References {{Count}} {(chunkRefs.Length == 1 ? "chunk" : "chunks")}", zoneId, zone.Name, chunkRefs.Length);

        foreach (var chunkRef in chunkRefs)
        {
            _logger.Information("Loading chunk ({CurrentCount}/{TotalCount}) {ChunkName}", chunkRefs.IndexOf(chunkRef) + 1, chunkRefs.Length, chunkRef.Name);
            var chunkPath = Path.Combine(_mapsPath, "chunks", $"{chunkRef.Name}.gtchunk");

            var collision = ChunkProcessor.ProcessChunk(chunkPath, _cachePath, _simulation, _blockers, _pool, _dispatcher, forceReload);

            AddAt(_simulation, collision.Statics, chunkRef.Origin);
            AddAt(_blockers, collision.Blockers, chunkRef.Origin);
            WaterMaterialIds.UnionWith(collision.WaterMaterialIds);
        }

        stopwatch.Stop();
        _logger.Information(
            "Zone {ZoneId}: Loaded successfully in {Duration}. Total statics: {Count}, movement blockers: {Blockers}, water materials: [{Water}]",
            zoneId,
            stopwatch.Elapsed,
            _simulation.Statics.Count,
            _blockers.Statics.Count,
            string.Join(", ", WaterMaterialIds.Order()));

        return zone.Timestamp;
    }

    private static void AddAt(Simulation simulation, StaticDescription[] statics, System.Numerics.Vector3 origin)
    {
        foreach (var item in statics)
        {
            var adjusted = item;
            adjusted.Pose.Position += origin;
            simulation.Statics.Add(adjusted);
        }
    }
}
