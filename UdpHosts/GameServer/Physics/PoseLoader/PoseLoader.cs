#nullable enable
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Serilog;

namespace GameServer.Physics.PoseLoader;

public class PoseLoader
{
    private readonly string _assetRoot;
    private readonly ILogger _logger = Log.ForContext<PoseLoader>();

    private readonly HashSet<string> _availableFolders;
    private readonly ConcurrentDictionary<string, string> _pathCache = new();
    private readonly ConcurrentDictionary<string, PoseData> _dataCache = new();

    /// <summary>
    ///     Asset ids already reported as unresolvable. Only successful loads were cached before, so a
    ///     character whose collision id is 0 asked for pose <c>00000000</c> and got a fresh warning on
    ///     every physics query: one run with thirteen standing NPCs logged 8472 copies of the same line.
    ///     The lookup was always cheap; the log was the cost.
    /// </summary>
    private readonly ConcurrentDictionary<string, byte> _missing = new();

    public PoseLoader(string assetRoot)
    {
        _assetRoot = assetRoot;

        if (!Directory.Exists(_assetRoot))
        {
            _logger.Information("PoseLoader AssetDBPath not found, will not load posefiles");
            _availableFolders = [];
            return;
        }

        _availableFolders = [.. Directory.GetDirectories(_assetRoot)
                     .Select(Path.GetFileName)
                     .Where(name => name != null && name.All(char.IsDigit))
                     .Select(name => name!)];

        _logger.Information("PoseLoader initialized with {FolderCount} folders", _availableFolders.Count);
    }

    public PoseData Load(string assetId)
    {
        if (!TryLoad(assetId, out var result))
        {
            throw new FileNotFoundException($"Pose file not found for ID: {assetId}");
        }

        return result;
    }

    public bool TryLoad(string assetId, out PoseData result)
    {
        result = default!;

        if (_missing.ContainsKey(assetId))
        {
            return false;
        }

        _logger.Debug("Pose file {assetId} Requested", assetId);

        if (string.IsNullOrWhiteSpace(assetId) || assetId.Length != 8 || !assetId.All(char.IsDigit))
        {
            _logger.Warning("Invalid asset ID format: {AssetId}", assetId);
            _missing.TryAdd(assetId ?? string.Empty, 0);
            return false;
        }

        // A character with no collision id resolves to this, which is a question with no answer rather
        // than a missing file. Callers already fall back to a default shape.
        if (assetId == "00000000")
        {
            _logger.Debug("Pose file 00000000 requested, which means the character carries no collision id. Using the fallback shape and not asking again.");
            _missing.TryAdd(assetId, 0);
            return false;
        }

        if (_dataCache.ContainsKey(assetId))
        {
            result = _dataCache[assetId];
            _logger.Debug("Pose file {assetId} {Name} Cached", assetId, result.Name);
            return true;
        }

        var path = ResolvePath(assetId);
        if (path == null)
        {
            _logger.Warning("Pose file {AssetId} file not found", assetId);
            _missing.TryAdd(assetId, 0);
            return false;
        }

        try
        {
            var ini = IniLoader.IniLoader.LoadFromFile(path);
            result = PoseData.LoadFromIni(ini, _logger);
            _dataCache[assetId] = result;
            _logger.Debug("Pose file {AssetId} Loaded pose {Name} from {Path}", assetId, result.Name, path);
            return true;
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "Pose file {AssetId} Failed to load pose from file {Path}", assetId, path);
            _missing.TryAdd(assetId, 0);
            return false;
        }
    }

    private string? ResolvePath(string assetId)
    {
        if (_pathCache.TryGetValue(assetId, out var cached))
        {
            return cached;
        }

        string folderName = (int.Parse(assetId) / 1000 * 1000).ToString("D8");

        if (!_availableFolders.Contains(folderName))
        {
            return null;
        }

        string folderPath = Path.Combine(_assetRoot, folderName);
        if (!Directory.Exists(folderPath))
        {
            return null;
        }

        var file = Directory.EnumerateFiles(folderPath)
                            .FirstOrDefault(f => Path.GetFileNameWithoutExtension(f) == assetId);

        if (file != null)
        {
            _pathCache[assetId] = file;
            return file;
        }

        return null;
    }
}
