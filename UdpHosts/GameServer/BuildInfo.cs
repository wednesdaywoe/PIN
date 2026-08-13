using System;
using System.IO;
using System.Reflection;
using System.Security.Cryptography;

namespace GameServer;

/// <summary>
///     Identity of the assembly that is actually running, logged at startup.
///
///     Deploying is a file copy, so the server can quietly be several commits behind the tree a test
///     entry was written against, and nothing on screen says so. That has cost whole sessions: a
///     result gets recorded against code that was never running. The cure is for the process to
///     state which file it came from, first thing, in the same log the session is read back out of.
///
///     The identity is the short SHA-256 of the assembly file rather than a version string, because
///     a version is whatever the build was told to claim while the hash changes whenever the bytes
///     do — and it can be recomputed from outside with <c>sha256sum</c>. deploy.sh records the same
///     value in BUILD-INFO and start-pin.sh prints it before launching, so the terminal banner, the
///     log line and the file on disk are three independent readings of one number: if they agree,
///     the running build is the deployed build is the built build.
/// </summary>
internal static class BuildInfo
{
    /// <summary>
    ///     Enough hex to never collide in practice, short enough to compare by eye against the
    ///     banner. Must match the width deploy.sh writes into BUILD-INFO.
    /// </summary>
    private const int HashCharacters = 12;

    static BuildInfo()
    {
        FileName = "unknown";
        ShortHash = "unknown";
        Written = DateTime.MinValue;

        try
        {
            var path = Assembly.GetExecutingAssembly().Location;

            // Empty for a single-file publish, where there is no assembly file to hash.
            if (string.IsNullOrEmpty(path) || !File.Exists(path))
            {
                return;
            }

            FileName = Path.GetFileName(path);
            Written = File.GetLastWriteTime(path);

            using var contents = File.OpenRead(path);
            ShortHash = Convert.ToHexString(SHA256.HashData(contents))[..HashCharacters].ToLowerInvariant();
        }
        catch (Exception)
        {
            // Never fail startup over this. "unknown" is the honest answer and still tells the
            // reader that the build could not be identified, which is itself worth seeing.
        }
    }

    public static string FileName { get; }

    public static string ShortHash { get; }

    public static DateTime Written { get; }
}
