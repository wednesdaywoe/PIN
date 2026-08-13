using System;
using System.Numerics;
using AeroMessages.Common;
using AeroMessages.GSS.V66;
using AeroMessages.GSS.V66.Melding.View;
using GameServer.Systems.Hazards;

namespace GameServer.Entities.Melding;

public sealed class MeldingEntity : BaseEntity
{
    private Vector2[] _perimeter;

    public MeldingEntity(IShard shard, ulong eid, string perimiterSetName)
        : base(shard, eid)
    {
        AeroEntityId = new EntityId() { Backing = EntityId, ControllerId = Controller.Melding };
        PerimiterSetName = perimiterSetName;
        Scoping = new ScopingComponent() { Global = true };
        InitFields();
        InitViews();
    }

    public ObserverView Melding_ObserverView { get; set; }

    public string PerimiterSetName { get; set; } = string.Empty;
    public ActiveDataStruct ActiveData { get; set; }
    public ScopeBubbleInfoData ScopeBubbleInfo { get; set; }

    /// <summary>
    ///     The wall as a flat polyline, for <see cref="GameServer.Systems.Hazards.MeldingField"/> to
    ///     measure against. Built on demand and thrown away whenever the perimeter moves, which is rare —
    ///     only a repulsor does it, and only while it is pushing.
    /// </summary>
    public Vector2[] Perimeter => _perimeter ??= MeldingField.Tessellate(ActiveData.FromPoints, ActiveData.FromTangents);

    public void SetActiveData(ActiveDataStruct newValue)
    {
        ActiveData = newValue;
        Melding_ObserverView.ActiveDataProp = newValue;
        _perimeter = null;
    }

    private void InitFields()
    {
        ActiveData = new ActiveDataStruct()
        {
            TimestampMicro = 0,
            Unk2 = 0,
            Unk3 = 0,
            FromPoints = [],
            FromTangents = [],
            ToPoints = [],
            ToTangets = [],
        };
        ScopeBubbleInfo = new ScopeBubbleInfoData()
        {
            Layer = 0,
            Unk2 = 1
        };
    }

    private void InitViews()
    {
        Melding_ObserverView = new ObserverView
        {
            PerimiterSetNameProp = PerimiterSetName,
            ActiveDataProp = ActiveData,
            ScopeBubbleInfoProp = ScopeBubbleInfo
        };
    }
}