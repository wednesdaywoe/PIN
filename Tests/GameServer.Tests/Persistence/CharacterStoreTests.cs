using System;
using System.IO;
using System.Linq;
using GameServer.Data.Persistence;
using Serilog;
using Xunit;

namespace GameServer.Tests.Persistence;

/// <summary>
///     The file half of persistence: what happens on disk, and what happens when what's on disk is wrong.
///     The failure modes matter more than the happy path here, because every one of them happens while a
///     player is trying to log in, and a save that can't be read must not be able to stop them.
/// </summary>
public class CharacterStoreTests : IDisposable
{
    private const ulong CharacterId = 0x99aabbccddee01c0;

    private readonly string _directory;
    private readonly CharacterStore _store;

    public CharacterStoreTests()
    {
        _directory = Path.Combine(Path.GetTempPath(), $"pin-store-tests-{Guid.NewGuid():N}");
        _store = new CharacterStore(_directory, new LoggerConfiguration().CreateLogger());
    }

    public void Dispose()
    {
        if (Directory.Exists(_directory))
        {
            Directory.Delete(_directory, recursive: true);
        }

        GC.SuppressFinalize(this);
    }

    [Fact]
    public void ANeverSavedCharacterReadsBackAsNothing()
    {
        Assert.Null(_store.Load(CharacterId));
    }

    [Fact]
    public void WhatWentInComesBackOut()
    {
        var state = State();
        state.Resources = [new SavedResource { SdbId = 10, Quantity = 89 }];
        state.Items = [new SavedItem { SdbId = 56826, SubInventory = 2, Durability = 1000, TimestampEpoch = 1755000000, Modules = [7] }];

        Assert.True(_store.Save(state));

        var loaded = Fresh().Load(CharacterId);

        Assert.NotNull(loaded);
        Assert.Equal(CharacterId, loaded.CharacterId);
        Assert.Equal(448u, loaded.LastZoneId);
        Assert.Equal(17u, loaded.LastOutpostId);
        Assert.Equal(3600u, loaded.TimePlayedSecs);
        Assert.Equal(89u, loaded.Resources.Single().Quantity);
        Assert.Equal(56826u, loaded.Items.Single().SdbId);
        Assert.Equal([7u], loaded.Items.Single().Modules);
    }

    /// <summary>
    ///     An idle player is snapshotted every minute for as long as they stay logged in. None of those
    ///     should reach the disk.
    /// </summary>
    [Fact]
    public void SavingTheSameThingTwiceOnlyWritesOnce()
    {
        Assert.True(_store.Save(State()));
        Assert.False(_store.Save(State()));
    }

    [Fact]
    public void SavingSomethingDifferentWritesAgain()
    {
        Assert.True(_store.Save(State()));

        var moved = State();
        moved.Resources = [new SavedResource { SdbId = 10, Quantity = 4 }];

        Assert.True(_store.Save(moved));
        Assert.Equal(4u, Fresh().Load(CharacterId).Resources.Single().Quantity);
    }

    /// <summary>
    ///     The skip is an optimisation, and an optimisation that can lose a save isn't one. If the file
    ///     went away underneath the cache, the next save has to put it back.
    /// </summary>
    [Fact]
    public void SavingWritesAgainIfTheFileVanished()
    {
        Assert.True(_store.Save(State()));
        File.Delete(_store.PathFor(CharacterId));

        Assert.True(_store.Save(State()));
        Assert.True(File.Exists(_store.PathFor(CharacterId)));
    }

    [Fact]
    public void SavingLeavesNoTemporaryFileBehind()
    {
        _store.Save(State());

        Assert.Empty(Directory.GetFiles(_directory, "*.tmp"));
    }

    /// <summary>
    ///     A truncated or hand-edited save reads as a character who has never been saved, and the bad
    ///     file is kept rather than silently overwritten by the save at the end of the session it just
    ///     ruined.
    /// </summary>
    [Fact]
    public void AnUnreadableSaveIsMovedAsideAndTheLoginCarriesOn()
    {
        Directory.CreateDirectory(_directory);
        File.WriteAllText(_store.PathFor(CharacterId), "{ \"CharacterId\": 12345, \"Resources\": [ ");

        Assert.Null(_store.Load(CharacterId));
        Assert.False(File.Exists(_store.PathFor(CharacterId)));
        Assert.Single(Directory.GetFiles(_directory, "*.corrupt-*"));
    }

    /// <summary>
    ///     A save from a build that wrote a different schema is somebody's character, not corruption.
    ///     Ignoring it costs them a session; deleting it costs them the character.
    /// </summary>
    [Fact]
    public void ASaveFromAnUnknownVersionIsIgnoredAndLeftWhereItIs()
    {
        var future = State();
        future.Version = SavedCharacter.CurrentVersion + 1;
        _store.Save(future);

        Assert.Null(Fresh().Load(CharacterId));
        Assert.True(File.Exists(_store.PathFor(CharacterId)));
        Assert.Empty(Directory.GetFiles(_directory, "*.corrupt-*"));
    }

    [Fact]
    public void TwoCharactersDoNotShareAFile()
    {
        var first = State();
        var second = State();
        second.CharacterId = CharacterId + 0x100;
        second.LastOutpostId = 3;

        _store.Save(first);
        _store.Save(second);

        Assert.Equal(17u, Fresh().Load(CharacterId).LastOutpostId);
        Assert.Equal(3u, Fresh().Load(second.CharacterId).LastOutpostId);
    }

    private static SavedCharacter State()
    {
        return new SavedCharacter
        {
            CharacterId = CharacterId,
            SavedAt = "2026-08-14 18:00:00Z",
            LastZoneId = 448,
            LastOutpostId = 17,
            TimePlayedSecs = 3600
        };
    }

    /// <summary>A store that has never seen the file, to prove a load really came off the disk.</summary>
    private CharacterStore Fresh()
    {
        return new CharacterStore(_directory, new LoggerConfiguration().CreateLogger());
    }
}
