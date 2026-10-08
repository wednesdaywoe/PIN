using BepuPhysics;
using BepuUtilities;
using BepuUtilities.Memory;
using Serilog;
using Shared.Collision.Cache;
using Shared.Collision.Chunk;
using Shared.Collision.Layers;
using Shared.Collision.Layers.Collision;
using Shared.Collision.Tagfile;

namespace Shared.Collision.ZoneLoading;

public static class ChunkProcessor
{
    private static readonly ILogger _logger = Log.ForContext(typeof(ChunkProcessor));

    /// <summary>
    ///     Reads one chunk's collision, from the cache when it holds a current copy. Static geometry is
    ///     built in <paramref name="world"/> and the movement blockers in <paramref name="blockers"/>.
    /// </summary>
    public static ChunkCollision ProcessChunk(
        string chunkPath,
        string cachePath,
        Simulation world,
        Simulation blockers,
        BufferPool pool,
        ThreadDispatcher dispatcher,
        bool forceReload = false)
    {
        var chunkName = Path.GetFileNameWithoutExtension(chunkPath);
        var cacheFile = ChunkCache.GetCachePath(cachePath, chunkName);

        if (!forceReload && ChunkCache.TryLoad(world, blockers, pool, dispatcher, cacheFile, out var cached))
        {
            return cached;
        }

        var chunk = ChunkFileReader.Read(chunkPath);

        var lod3Layers = Lod3Layers(chunk);

        if (!lod3Layers.OfType<ChunkStaticGeometryCollisionLayer>().Any())
        {
            _logger.Warning("Chunk {Name} has no LOD3 collision layers, what?", chunk.Name);
            return ChunkCollision.Empty;
        }

        var statics = Build(new TagfileLoader(world, pool, dispatcher), lod3Layers.OfType<ChunkStaticGeometryCollisionLayer>(), withMeshBlocks: true);
        var blockerStatics = Build(new TagfileLoader(blockers, pool, dispatcher), lod3Layers.OfType<ChunkMovementBlockerCollisionLayer>(), withMeshBlocks: false);
        var water = lod3Layers.OfType<ChunkWaterCollisionLayer>()
            .SelectMany(layer => layer.Enwf.PhysicsMatIds)
            .Where(id => id != 0)
            .Distinct()
            .Order()
            .ToArray();

        var result = new ChunkCollision(statics, blockerStatics, water);

        ChunkCache.Save(world, blockers, pool, result, cacheFile);

        return result;
    }

    private static StaticDescription[] Build(TagfileLoader loader, IEnumerable<EnwfLayer> layers, bool withMeshBlocks)
    {
        List<StaticDescription> result = [];

        foreach (var layer in layers)
        {
            var hkxBytes = layer.Enwf.HavokBinaryTagfile;

            if (hkxBytes.Length == 0)
            {
                continue;
            }

            var statics = withMeshBlocks
                ? loader.ProcessTagfileBytes(
                    hkxBytes,
                    EnwfToBepuConverter.ConvertVertBlocks(layer.Enwf.VertBlocks),
                    EnwfToBepuConverter.ConvertIndiceBlocks(layer.Enwf.IndiceBlocks))
                : loader.ProcessTagfileBytes(hkxBytes);

            result.AddRange(statics);
        }

        return [.. result];
    }

    /// <summary>
    ///     Every layer at detail level 3, shared and per sub-chunk. Level 3 is the only one that carries
    ///     collision of any kind.
    /// </summary>
    private static WorldLayer[] Lod3Layers(ChunkFile chunk)
    {
        return chunk.Lod
            .Where(lod => lod.Level == 3)
            .SelectMany(lod => lod.SharedLayers.Concat(lod.SubChunks.SelectMany(subChunk => subChunk.Layers)))
            .ToArray();
    }
}
