using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using Serilog;

namespace GameServer.Data.Persistence;

/// <summary>
///     One JSON file per character, written by the GameServer itself.
///
///     This is the same shape as the runtime writes <c>deposit add</c> and <c>spawngroup add</c> already
///     do, and for the same reason: it needs no second service running, so it behaves identically whether
///     or not RIN is up. RIN has never answered a request in this project, so a persistence layer that
///     only worked when it did would be a persistence layer that never worked.
///
///     The files do not live where <c>deploy.sh</c> installs, which matters more than it sounds. Installing
///     is an rsync out of the build output, and the authored CustomData JSON needed an explicit exclude
///     list to survive it. A save directory the build output never contains has no such trap.
///
///     Not thread safe, and doesn't need to be: all three things that save (the autosave tick, the logout
///     message, and the disconnect that reaches <c>Shard.MigrateOut</c>) run on the shard's own thread,
///     because channel processing dispatches its delegates from inside the network tick. Saving from
///     anywhere else would need this to grow a lock.
/// </summary>
public class CharacterStore
{
    private static readonly JsonSerializerOptions _serializerOptions = new() { WriteIndented = true };

    private readonly string _directory;
    private readonly ILogger _logger;

    /// <summary>
    ///     What was last written for each character, so an autosave that would change nothing doesn't
    ///     touch the disk. Compared as text because that is exactly what would be written.
    /// </summary>
    private readonly Dictionary<ulong, string> _lastWritten = [];

    public CharacterStore(string directory, ILogger logger)
    {
        _directory = directory;
        _logger = logger.ForContext<CharacterStore>();
    }

    public string PathFor(ulong characterId)
    {
        return Path.Combine(_directory, $"character-{characterId:x16}.json");
    }

    /// <summary>
    ///     Reads a character back, or null if there's nothing to read.
    /// </summary>
    /// <remarks>
    ///     Never throws. A save that can't be read must not be able to stop a login, because the failure
    ///     it would cause looks exactly like the server being broken. An unreadable file is moved aside
    ///     rather than left in place to be overwritten by the next save, so whatever went wrong with it
    ///     can still be looked at afterwards.
    /// </remarks>
    public SavedCharacter Load(ulong characterId)
    {
        var path = PathFor(characterId);

        if (!File.Exists(path))
        {
            return null;
        }

        try
        {
            var text = File.ReadAllText(path);
            var saved = JsonSerializer.Deserialize<SavedCharacter>(text, _serializerOptions);

            if (saved == null)
            {
                Quarantine(path, "it deserialized to nothing");
                return null;
            }

            if (saved.Version != SavedCharacter.CurrentVersion)
            {
                // Left where it is rather than quarantined. A version this build doesn't know is a
                // downgrade or a half-finished migration, not corruption, and deleting someone's
                // character because they ran an older binary once is not a recovery strategy.
                _logger.Warning(
                    "Ignoring save for character {CharacterId}: version {Found}, this build writes {Expected}. It has not been touched",
                    characterId,
                    saved.Version,
                    SavedCharacter.CurrentVersion);
                return null;
            }

            _lastWritten[characterId] = text;
            return saved;
        }
        catch (Exception ex)
        {
            Quarantine(path, ex.Message);
            return null;
        }
    }

    /// <summary>
    ///     Writes a character out, and says whether it actually wrote anything.
    /// </summary>
    /// <remarks>
    ///     Writes to a temporary file and renames over the target, so an interrupted save leaves the
    ///     previous one intact. The alternative loses everything on a crash mid-write, and the crash
    ///     most likely to happen is the one on shutdown, which is when this runs.
    /// </remarks>
    public bool Save(SavedCharacter state)
    {
        var path = PathFor(state.CharacterId);
        var text = JsonSerializer.Serialize(state, _serializerOptions);

        if (_lastWritten.TryGetValue(state.CharacterId, out var previous) && previous == text && File.Exists(path))
        {
            return false;
        }

        try
        {
            Directory.CreateDirectory(_directory);

            var temporary = path + ".tmp";
            File.WriteAllText(temporary, text);
            File.Move(temporary, path, overwrite: true);

            _lastWritten[state.CharacterId] = text;
            return true;
        }
        catch (Exception ex)
        {
            // Losing a save is bad; taking the shard down with it during a logout is worse.
            _logger.Error(ex, "Could not save character {CharacterId} to {Path}", state.CharacterId, path);
            return false;
        }
    }

    private void Quarantine(string path, string why)
    {
        var moved = $"{path}.corrupt-{DateTimeOffset.UtcNow:yyyyMMdd-HHmmss}";

        try
        {
            File.Move(path, moved, overwrite: true);
            _logger.Error("Could not read {Path} ({Why}). Moved it to {Moved} and carried on with a fresh character", path, why, moved);
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "Could not read {Path} ({Why}), and could not move it aside either", path, why);
        }
    }
}
