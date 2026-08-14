using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Numerics;
using System.Threading;
using AeroMessages.GSS.V66;
using GameServer.StaticDB;
using GameServer.StaticDB.Records.customdata;

namespace GameServer.Systems.Resources;

/// <summary>A ground reading a character has been given: where it was taken and what it said.</summary>
public record GeoScanReport(uint ScanId, Vector3 Position, uint DepositId, uint NodeTypeId, ResourceCompositionData[] Composition);

/// <summary>
///     Answers "what is under this ground" for one shard: which deposit covers a spot, what a scan
///     reports there, and what a thumper should be paid for working it.
/// </summary>
/// <remarks>
///     The last report handed to each character is kept, for two reasons. <c>MapOpened</c> wants to
///     re-show it, and a thumper completion wants the <c>ScanId</c> the client already knows so
///     <c>ResourceNodeCompletedEvent</c> can close the loop it opened. Reports die with the session —
///     persistence is M6's problem.
/// </remarks>
public class ResourceMapSim
{
    /// <summary>
    ///     "Default, Thumper Sifted Earth - Resource Vein 0", the node type ground gets when no deposit
    ///     covers it — and the literal every thumper used before deposits existed. Its shipped yield is
    ///     a single unit of sifted earth, which is the game's own way of paying almost nothing.
    /// </summary>
    public const uint BarrenNodeType = 20;

    private readonly IShard _shard;
    private readonly ConcurrentDictionary<ulong, GeoScanReport> _lastReportByCharacter = new();
    private uint _lastScanId;

    public ResourceMapSim(IShard shard)
    {
        _shard = shard;
    }

    /// <summary>The deposit under a spot, or null for barren ground.</summary>
    public ResourceDeposit FindDepositAt(Vector3 position)
    {
        return DepositSampler.FindDeposit(CustomDBInterface.GetZoneResourceDeposits(_shard.ZoneId).Values, position);
    }

    /// <summary>The node type a thumper called down at a spot should mine.</summary>
    public uint ResolveNodeType(Vector3 position)
    {
        return FindDepositAt(position)?.NodeTypeId ?? BarrenNodeType;
    }

    /// <summary>Rolls what a spot yields right now, gradient applied. Empty for barren ground.</summary>
    public List<DepositYield> SampleAt(Vector3 position)
    {
        var deposit = FindDepositAt(position);
        if (deposit == null)
        {
            return [];
        }

        var rows = SDBInterface.GetResourceNodeTypeResources(deposit.NodeTypeId);
        return DepositSampler.Sample(rows, DepositSampler.DistanceFraction(deposit, position), Random.Shared);
    }

    /// <summary>
    ///     Takes a ground reading for a character and remembers it as their latest. Null on barren
    ///     ground — the caller sends an invalid report, which the client renders as "empty".
    /// </summary>
    public GeoScanReport TakeReport(ulong characterEntityId, Vector3 position)
    {
        var deposit = FindDepositAt(position);
        if (deposit == null)
        {
            return null;
        }

        var rows = SDBInterface.GetResourceNodeTypeResources(deposit.NodeTypeId);
        var yields = DepositSampler.Sample(rows, DepositSampler.DistanceFraction(deposit, position), Random.Shared);
        var report = new GeoScanReport(
            Interlocked.Increment(ref _lastScanId),
            position,
            deposit.Id,
            deposit.NodeTypeId,
            DepositSampler.ToComposition(yields));

        _lastReportByCharacter[characterEntityId] = report;
        return report;
    }

    /// <summary>The latest reading this character was given, or null if they never took one.</summary>
    public GeoScanReport GetLastReport(ulong characterEntityId)
    {
        return _lastReportByCharacter.GetValueOrDefault(characterEntityId);
    }
}
