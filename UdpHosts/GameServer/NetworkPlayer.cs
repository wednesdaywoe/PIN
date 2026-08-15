using System;
using System.Net;
using System.Numerics;
using System.Threading;
using AeroMessages.GSS.V66;
using AeroMessages.GSS.V66.Character;
using AeroMessages.GSS.V66.Character.Controller;
using AeroMessages.GSS.V66.Character.Event;
using AeroMessages.Matrix.V25;
using GameServer.Data;
using GameServer.Data.Persistence;
using GameServer.GRPC;
using GameServer.StaticDB.Records.customdata;
using GameServer.Test;
using GrpcGameServerAPIClient;
using Serilog;
using CharacterEntity = GameServer.Entities.Character.CharacterEntity;

namespace GameServer;

public class NetworkPlayer : NetworkClient, INetworkPlayer
{
    /// <summary>Playtime this character had banked before the current session started.</summary>
    private uint _timePlayedBeforeSession;

    public NetworkPlayer(IPEndPoint endPoint, uint socketId, ILogger logger)
        : base(endPoint, socketId, logger)
    {
        CharacterEntity = null;
        Status = IPlayer.PlayerStatus.Connecting;
        ConnectedAt = (uint)DateTimeOffset.UtcNow.ToUnixTimeSeconds();
    }

    public ulong PlayerId { get; private set; }
    public ulong CharacterId { get; private set; }
    public CharacterEntity CharacterEntity { get; private set; }
    public IPlayer.PlayerStatus Status { get; private set; }
    public PlayerPreferences Preferences { get; private set; }
    public Zone CurrentZone { get; private set; }
    public uint CurrentOutpostId { get; private set; }
    public uint LastRequestedUpdate { get; set; }
    public uint RequestedClientTime { get; set; }
    public bool FirstUpdateRequested { get; set; }
    public ulong SteamUserId { get; set; }
    public CharacterInventory Inventory { get; set; }
    public uint ConnectedAt { get; }
    public bool CanReceiveGSS => (Status.Equals(IPlayer.PlayerStatus.Playing) || Status.Equals(IPlayer.PlayerStatus.Loading)) && NetClientStatus.Equals(ClientStatus.Connected);

    /// <summary>Everything this character has played, banked plus the session in progress.</summary>
    public uint TimePlayedSecs => _timePlayedBeforeSession + ((uint)DateTimeOffset.UtcNow.ToUnixTimeSeconds() - ConnectedAt);

    public void Init(IShard shard)
    {
        Init(this, shard, shard);
        Preferences = new PlayerPreferences();
    }

    public async void Login(ulong characterId)
    {
        PlayerId = 0x4658281c142e9f00ul;
        var guid = characterId & 0xffffffffffffff00;
        CharacterId = guid;

        // Don't crash if they are already logged in
        AssignedShard.Entities.TryGetValue(CharacterId, out var existing);
        if (existing != null)
        {
            Logger.Warning("Closing login because entity with this id is already zoned in");
            var resp = new AeroMessages.Control.CloseConnection { Unk = [0, 0, 0, 0] };
            NetChannels[ChannelType.Control].SendMessage(resp);
            return;
        }

        // Begin setting up player character
        CharacterEntity = new CharacterEntity(AssignedShard, guid);

        // Try to get remote character data
        CharacterAndBattleframeVisuals remoteData = null;
        try
        {
            remoteData = await GRPCService.GetCharacterAndBattleframeVisualsAsync((long)characterId);
        }
        catch
        {
            Logger.ForContext(typeof(GRPCService)).Warning("Could not get character over GRPC, will use fallback");
        }

        // Load inventory so we get loadouts
        Inventory = new CharacterInventory(AssignedShard, this, CharacterEntity);
        Inventory.LoadHardcodedInventory();

        // Everything above this line is what every character gets handed. Everything after it is this
        // character's own, which is the whole distinction persistence runs on.
        Inventory.MarkSeeded();
        var saved = LoadSavedCharacter();

        // Use remote data or fallback to setup character
        bool useRemoteData = true;
        int loadoutId;
        if (remoteData != null && useRemoteData)
        {
            CharacterEntity.LoadRemote(remoteData);

            // Todo: load inventory from db so we can use those loadouts
            loadoutId = Inventory.GetLoadoutIdForChassis(remoteData.CharacterInfo.CurrentBattleframeSDBId);
        }
        else
        {
            CharacterEntity.Load(HardcodedCharacterData.FallbackData);
            loadoutId = Inventory.GetLoadoutIdForChassis(76331);
        }

        var loadoutRefData = Inventory.GetLoadoutReferenceData(loadoutId);
        var loadout = new CharacterLoadout(loadoutRefData);
        CharacterEntity.ApplyLoadout(loadout);

        // Collision data should have been initialized now, create physics representation
        AssignedShard.Physics.CreateKineticEntity(CharacterEntity);

        CharacterEntity.SetControllingPlayer(this);
        CharacterEntity.SetCharacterState(CharacterStateData.CharacterStatus.Spawning, AssignedShard.CurrentTime);
        Status = IPlayer.PlayerStatus.LoggedIn;

        // WelcomeToTheMatrix
        var wel = new WelcomeToTheMatrix { PlayerID = PlayerId, Unk1 = [], Unk2 = [] };
        NetChannels[ChannelType.Matrix].SendMessage(wel);

        Zone zone;
        uint zoneId;
        uint outpostId;

        if (remoteData != null)
        {
            zoneId = AssignedShard.ZoneId;
            zone = DataUtils.GetZone(zoneId);
            outpostId = remoteData.CharacterInfo.LastZoneId == zoneId ? FindClosestAvailableOutpost(zone, remoteData.CharacterInfo.LastOutpostId) : 0;
        }
        else
        {
            zoneId = (uint)(characterId & 0x000000000000ffff);
            zone = DataUtils.GetZone(zoneId);

            // Come back where you logged out, if the save is for the zone being entered. The zone check
            // isn't ceremony: a character id encodes the zone it enters, so a save carrying a different
            // one means the file was hand-copied between characters, and its outpost id would name an
            // outpost that doesn't exist here.
            outpostId = saved != null && saved.LastZoneId == zoneId
                            ? FindClosestAvailableOutpost(zone, saved.LastOutpostId)
                            : zone.DefaultOutpostId;
        }

        Logger.Information("Zone {zoneId} Outpost {outpostId}", zoneId, outpostId);

        EnterZone(zone, outpostId);
    }

    public void EnterZoneAck()
    {
        AssignedShard.EntityMan.Add(CharacterEntity.EntityId, CharacterEntity);
    }

    public void ExitZoneAck()
    {
        AssignedShard.EntityMan.Remove(CharacterEntity);
    }

    public void Respawn()
    {
        var outpostId = FindClosestAvailableOutpost(CurrentZone, CurrentOutpostId);
        var spawnPoint = outpostId == 0 ?
                             new SpawnPoint { Position = CurrentZone.POIs["spawn"] }
                             : AssignedShard.Outposts[CurrentZone.ID][outpostId].RandomSpawnPoint;

        CharacterEntity.PositionAtSpawnPoint(spawnPoint);
        CharacterEntity.MarkPlacedAt(spawnPoint.Position);
        CharacterEntity.SetSpawnTime(AssignedShard.CurrentTime);
        var forcedMove = new ForcedMovement
        {
            Data = new ForcedMovementData
            {
                Type = 1,
                Unk1 = 0,
                HaveUnk2 = 0,
                Params1 = new ForcedMovementType1Params { Position = spawnPoint.Position, Direction = CharacterEntity.AimDirection, Velocity = Vector3.Zero, Time = AssignedShard.CurrentTime + 1 }
            },
            ShortTime = AssignedShard.CurrentShortTime
        };
        NetChannels[ChannelType.ReliableGss].SendMessage(forcedMove, CharacterEntity.EntityId);

        var respawnMsg = new Respawned { ShortTime = AssignedShard.CurrentShortTime, Unk1 = 0, Unk2 = 0 };
        NetChannels[ChannelType.ReliableGss].SendMessage(respawnMsg, CharacterEntity.EntityId);

        var baseController = CharacterEntity.Character_BaseController;

        // Update 1
        CharacterEntity.SetSpawnTime(AssignedShard.CurrentTime);
        CharacterEntity.SetCharacterState(CharacterStateData.CharacterStatus.Respawning, AssignedShard.CurrentTime);
        CharacterEntity.SetSpawnPose();

        // Takes down the countdown and the give-up prompt a downed player was offered. Harmless on
        // the login path, where there was no offer to withdraw — this is also where the old
        // write-then-null dance on RespawnTimesProp ends up, now that the field carries something.
        CharacterEntity.ClearRespawnOffer();
        baseController.TimedDailyRewardProp = new TimedDailyRewardData { State = TimedDailyRewardData.TimedDailyRewardState.ROLLED, MaxRolls = 1, CountdownToTime = AssignedShard.CurrentTime };
        NetChannels[ChannelType.ReliableGss].SendChanges(baseController, CharacterEntity.EntityId);

        // Update 2
        CharacterEntity.SetCharacterState(CharacterStateData.CharacterStatus.Living, AssignedShard.CurrentTime + 1);
        baseController.GibVisualsIdProp = new GibVisuals { Id = 0, Time = AssignedShard.CurrentTime + 1 };
        CharacterEntity.SetMaxHealth(HardcodedCharacterData.MaxHealth, true); // Also updates the entity fields, not just the controller props
        CharacterEntity.SetMaxShields(HardcodedCharacterData.MaxShields, true); // Was writing the prop straight, which left the entity thinking it still had no shields
        baseController.ZoneUnlocksProp = 0xFFFFFFFFFFFFFFFFUL;
        baseController.RegionUnlocksProp = 0xFFFFFFFFFFFFFFFFUL;
        baseController.PersonalFactionStanceProp = new PersonalFactionStanceData
        {
            Friendly = new PersonalFactionStanceBitfield { NumFactions = 50, Bitfield = [0x09, 0x0e, 0x5d, 0xff, 0x5f, 0x08, 0x00, 0x00] },
            Hostile = new PersonalFactionStanceBitfield { NumFactions = 50, Bitfield = [0xf2, 0x00, 0x20, 0x00, 0x00, 0xf2, 0x00, 0x00] }
        };
        NetChannels[ChannelType.ReliableGss].SendChanges(baseController, CharacterEntity.EntityId);

        // Hack to add in jetpack fx until we hook up item effects
        CharacterEntity.AddEffect(AssignedShard.Abilities.Factory.LoadEffect(986), new Systems.Aptitude.Context(AssignedShard, CharacterEntity)
        {
            InitTime = AssignedShard.CurrentTime,
        });
        CharacterEntity.AddEffect(AssignedShard.Abilities.Factory.LoadEffect(472), new Systems.Aptitude.Context(AssignedShard, CharacterEntity)
        {
            InitTime = AssignedShard.CurrentTime,
        });

        var combatController = new CombatController
        {
            CombatTimer_0Prop = AssignedShard.CurrentTime,
        };
        NetChannels[ChannelType.ReliableGss].SendChanges(combatController, CharacterEntity.EntityId);

        // InventoryUpdate
        Inventory.SendFullInventory();
        Inventory.EnablePartialUpdates = true;

        AssignedShard.Physics.UpdateEntity(CharacterEntity);
        CharacterEntity.Alive = true; // Accept MovementInputs only after Respawn
    }

    public void Ready()
    {
        Status = IPlayer.PlayerStatus.Playing;
    }

    public void Jump()
    {
        CharacterEntity.TimeSinceLastJump = -1;
    }

    public void Tick(double deltaTime, ulong currentTime, CancellationToken ct)
    {
        switch (Status)
        {
            // TODO: Implement FSM here to move player thru log in process to connecting to a shard to playing
            case IPlayer.PlayerStatus.Connected:
                Status = IPlayer.PlayerStatus.LoggingIn;
                break;
            case IPlayer.PlayerStatus.Loading:
                break;
            case IPlayer.PlayerStatus.Playing:
                {
                    break;
                }
        }
    }

    /// <summary>
    ///     Everything about this character that outlives the session, as of now.
    /// </summary>
    public SavedCharacter Snapshot(DateTimeOffset savedAt)
    {
        return SavedCharacter.Snapshot(
            CharacterId,
            CurrentZone.ID,
            ClosestOutpostToPosition(),
            TimePlayedSecs,
            Inventory.GetResources(),
            Inventory.GetPersistableItems(),
            savedAt);
    }

    /// <summary>
    ///     The outpost the player is standing nearest, which is where they'll come back.
    /// </summary>
    /// <remarks>
    ///     Not the same question as <see cref="FindClosestAvailableOutpost"/>, which measures from another
    ///     outpost and exists to answer "this one is captured, where instead". The spawn point seeds the
    ///     comparison, so standing further from every outpost than the zone's own spawn is means the
    ///     default, rather than means whichever outpost happens to be least far away.
    /// </remarks>
    public uint ClosestOutpostToPosition()
    {
        var zone = CurrentZone;

        if (zone == null || !zone.IsOpenWorld || !zone.POIs.TryGetValue("spawn", out var spawn))
        {
            return CurrentOutpostId;
        }

        var position = CharacterEntity.Position;
        var minDistance = Vector3.DistanceSquared(position, spawn);
        var closest = zone.DefaultOutpostId;

        if (AssignedShard.Outposts.TryGetValue(zone.ID, out var outposts))
        {
            foreach (var outpost in outposts)
            {
                var distance = Vector3.DistanceSquared(position, outpost.Value.Outpost_ObserverView.PositionProp);

                if (distance < minDistance)
                {
                    minDistance = distance;
                    closest = outpost.Key;
                }
            }
        }

        return closest;
    }

    public uint FindClosestAvailableOutpost(Zone zone, uint targetOutpostId = 0)
    {
        bool haveOutposts = AssignedShard.Outposts.TryGetValue(zone.ID, out var outposts);
        if (!haveOutposts)
        {
            return 0;
        }
        else if (targetOutpostId == 0)
        {
            return zone.DefaultOutpostId;
        }

        var targetOutpost = outposts[targetOutpostId];

        if (!targetOutpost.IsCapturedByHostiles)
        {
            return targetOutpostId;
        }

        Vector3 sourcePosition = targetOutpost.Outpost_ObserverView.PositionProp;

        var minDistance = Vector3.DistanceSquared(sourcePosition, zone.POIs["spawn"]);
        var closestOutpostId = zone.DefaultOutpostId;

        foreach (var outpost in outposts)
        {
            if (outpost.Value.IsCapturedByHostiles)
            {
                continue;
            }

            var distance = Vector3.DistanceSquared(sourcePosition, outpost.Value.Outpost_ObserverView.PositionProp);
            if (distance < minDistance)
            {
                minDistance = distance;
                closestOutpostId = outpost.Key;
            }
        }

        return closestOutpostId;
    }

    public void HandleFireWeaponProjectile(uint time, Vector3 aim)
    {
        AssignedShard.WeaponSim.OnFireWeaponProjectile(CharacterEntity, time, aim);
    }

    public void EnterZone(Zone z, uint outpostId = 0)
    {
        var spawnPoint = outpostId == 0
                             ? new SpawnPoint { Position = z.POIs["spawn"] }
                             : AssignedShard.Outposts[z.ID][outpostId].RandomSpawnPoint;

        // Ensure character entity is placed at the respawn point
        CharacterEntity.PositionAtSpawnPoint(spawnPoint);
        CharacterEntity.SetSpawnPose();

        CurrentZone = z;
        CurrentOutpostId = outpostId;

        var msg = new EnterZone
        {
            InstanceId = AssignedShard.InstanceId,
            ZoneId = CurrentZone.ID,
            ZoneTimestamp = (long)CurrentZone.Timestamp,
            ZoneFlags = 0,
            ZoneOwner = "r5_exec",
            StreamingProtocol = 0x4c5f,
            SvnRevision = 0x0c9f5,
            HotfixLevel = 0,
            MatchId = 0,
            Unk2 = 0,
            SimulationSeedMs = 0x63e2db5e,
            ZoneName = CurrentZone.Name,
            HaveDevZoneInfo = 0,
            ZoneTimeSyncInfo = new ZoneTimeSyncData { FictionDateTimeOffsetMicros = 0, DayLengthFactor = 12.0F, DayPhaseOffset = 0.896445870399F },
            GameClockInfo = new GameClockInfoData
            {
                MicroUnix_1 = 1478970208392232,
                MicroUnix_2 = 1478774752697322,
                Timescale = 1.0,
                Unk3 = 0,
                Unk4 = 0,
                Paused = 0
            },
            SpectatorModeFlag = 0
        };

        NetChannels[ChannelType.Matrix].SendMessage(msg);

        Status = IPlayer.PlayerStatus.Loading;
    }

    /// <summary>
    ///     Puts a previous session's resources and items back into a freshly seeded inventory.
    /// </summary>
    /// <remarks>
    ///     Runs before <c>EnablePartialUpdates</c>, so nothing here sends anything: the client learns the
    ///     lot in the full inventory that <c>Respawn</c> sends. Returns the save so the caller can read
    ///     the outpost off it, or null when this character has never been saved.
    /// </remarks>
    private SavedCharacter LoadSavedCharacter()
    {
        var saved = AssignedShard.CharacterStore.Load(CharacterId);

        if (saved == null)
        {
            return null;
        }

        foreach (var resource in saved.Resources)
        {
            Inventory.AddResource(resource.SdbId, resource.Quantity);
        }

        foreach (var item in saved.Items)
        {
            Inventory.RestoreItem(item);
        }

        _timePlayedBeforeSession = saved.TimePlayedSecs;

        Logger.Information(
            "Restored character {CharacterId} from {SavedAt}: {ResourceCount} resource kind(s), {ItemCount} item(s), outpost {OutpostId}, {TimePlayed}s played",
            CharacterId,
            saved.SavedAt,
            saved.Resources.Count,
            saved.Items.Count,
            saved.LastOutpostId,
            saved.TimePlayedSecs);

        return saved;
    }
}
