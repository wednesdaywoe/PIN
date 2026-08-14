using System;
using System.Collections.Generic;
using AeroMessages.Common;
using AeroMessages.GSS.V66;
using AeroMessages.GSS.V66.Outpost.View;
using GameServer.Data;
using GameServer.StaticDB;
using GameServer.StaticDB.Records.customdata;
using GameServer.Systems.Resources;

namespace GameServer.Entities.Outpost;

public sealed class OutpostEntity : BaseEntity
{
    private static readonly Random _rng = new();

    public OutpostEntity(IShard shard, ulong eid, StaticDB.Records.customdata.Outpost record)
        : base(shard, eid)
    {
        AeroEntityId = new EntityId() { Backing = EntityId, ControllerId = Controller.Outpost };
        Scoping = new ScopingComponent() { Global = true };
        InitFields(record);
        InitViews(record);
        AddSpawnPoints(record);
    }

    public ObserverView Outpost_ObserverView { get; set; }

    public ulong EncounterId { get; set; }
    public ScopeBubbleInfoData ScopeBubbleInfo { get; set; }

    public SpawnPoint RandomSpawnPoint => SpawnPoints[_rng.Next(SpawnPoints.Count)];
    public bool IsCapturedByHostiles => HardcodedCharacterData.HostileFactionIds.Contains(Outpost_ObserverView.FactionIdProp);
    private List<SpawnPoint> SpawnPoints { get; set; } = [];

    private void InitFields(StaticDB.Records.customdata.Outpost record)
    {
        Position = record.Position;
        ScopeBubbleInfo = new ScopeBubbleInfoData()
        {
            Layer = 0,
            Unk2 = 1
        };
    }

    private void InitViews(StaticDB.Records.customdata.Outpost record)
    {
        Outpost_ObserverView = new ObserverView
        {
            OutpostNameProp = record.OutpostName,
            PositionProp = record.Position,
            LevelBandIdProp = record.LevelBandId,
            SinUnlockIndexProp = record.SinUnlockIndex,
            TeleportCostProp = record.TeleportCost,
            ProgressProp = 0f,
            FactionIdProp = record.FactionId,
            TeamProp = 0,
            UnderAttackProp = 0,
            OutpostTypeProp = record.OutpostType,
            PossibleBuffsIdProp = record.PossibleBuffsId,
            PowerLevelProp = 0,
            MWCurrentProp = 0,
            MWMaxProp = 0,
            MapMarkerTypeIdProp = record.MarkerType,
            RadiusProp = record.Radius,
            Dynamic_11Prop = new byte[4],
            EncounterIdProp = new EntityId { Backing = EncounterId },
            ScopeBubbleInfoProp = ScopeBubbleInfo
        };

        SetNearbyResources(Outpost_ObserverView, record);
    }

    /// <summary>
    ///     Fills the sixteen resource slots the world map reads. The 1962 client's resource layer is
    ///     one radar disc per outpost, captioned with that outpost's own resource list — it is not fed
    ///     by any scan message — so this is where a zone's deposits become visible before they are
    ///     thumped. An outpost with no deposit in reach leaves every slot unset and draws an empty
    ///     readout, which is the correct answer rather than a gap.
    /// </summary>
    private void SetNearbyResources(ObserverView view, StaticDB.Records.customdata.Outpost record)
    {
        var items = DepositSampler.ResourcesWithin(
            CustomDBInterface.GetZoneResourceDeposits(record.ZoneId).Values,
            record.Position,
            record.Radius,
            SDBInterface.GetResourceNodeTypeResources);

        uint? Slot(int index) => index < items.Count ? items[index] : null;

        view.NearbyResourceItems_0Prop = Slot(0);
        view.NearbyResourceItems_1Prop = Slot(1);
        view.NearbyResourceItems_2Prop = Slot(2);
        view.NearbyResourceItems_3Prop = Slot(3);
        view.NearbyResourceItems_4Prop = Slot(4);
        view.NearbyResourceItems_5Prop = Slot(5);
        view.NearbyResourceItems_6Prop = Slot(6);
        view.NearbyResourceItems_7Prop = Slot(7);
        view.NearbyResourceItems_8Prop = Slot(8);
        view.NearbyResourceItems_9Prop = Slot(9);
        view.NearbyResourceItems_10Prop = Slot(10);
        view.NearbyResourceItems_11Prop = Slot(11);
        view.NearbyResourceItems_12Prop = Slot(12);
        view.NearbyResourceItems_13Prop = Slot(13);
        view.NearbyResourceItems_14Prop = Slot(14);
        view.NearbyResourceItems_15Prop = Slot(15);
    }

    private void AddSpawnPoints(StaticDB.Records.customdata.Outpost record)
    {
        if (record.SpawnPoints.Count == 0)
        {
            SpawnPoints = [new() { Position = record.Position }];
        }
        else
        {
            SpawnPoints = record.SpawnPoints;
        }
    }
}