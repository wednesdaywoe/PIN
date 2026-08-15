using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using AeroMessages.GSS.V66.Character.Event;
using AeroMessages.GSS.V66.Character.View;
using AeroMessages.GSS.V66.Events;

namespace CaptureReplay;

/// <summary>
///     Measures which way retail's server said each character was facing, against which way that
///     character was actually travelling. Exists for CLIENT-3: PIN orients every NPC with the same
///     +X-forward convention, a Chosen renders correctly under it and an Aranha stands 90° off, and
///     nothing shipped to the client says which models differ. If retail baked a per-rig offset into
///     the orientation it sent, this reads the offset straight off the wire; if retail's numbers match
///     travel for every rig, the client corrects models itself and PIN's bug is elsewhere.
///
///     Remote-entity poses never appear as standalone messages in a capture. They ride inside
///     <c>RoutedMultipleMessage1</c> envelopes, addressed by the shorthand ids that
///     <c>RoutedMessageIdAssign</c> establishes, so this report is also the first thing in the repo
///     that opens those envelopes. A block's payload is one message id byte followed by the message
///     body; that layout is a hypothesis validated by the parse-exactness counts the report prints.
/// </summary>
public sealed class FacingReport
{
    private const byte CurrentPoseUpdateId = 110;
    private const byte RoutedAssignId = 9;
    private const byte ObserverViewKeyframeId = 3;

    /// <summary>
    ///     A step shorter than this is standing noise, not travel, and says nothing about facing.
    /// </summary>
    private const float MinTravel = 0.5f;

    private readonly Dictionary<ulong, uint> _typeByEntity = new();
    private readonly Dictionary<ushort, ulong> _entityByRef = new();
    private readonly Dictionary<ulong, List<Sample>> _samplesByEntity = new();
    private readonly Dictionary<byte, int> _innerMessageIds = new();

    private int _envelopes;
    private int _blocks;
    private int _blocksUnresolved;
    private int _posesParsed;
    private int _posesRejected;
    private int _posesPartial;

    public void Observe(DecodedMessage message)
    {
        if (message.Direction != Direction.ServerToClient)
        {
            return;
        }

        switch (message.Instance)
        {
            case ObserverView view when message.MessageId == ObserverViewKeyframeId:
                _typeByEntity[message.EntityId] = view.StaticInfoProp.CharacterTypeId;
                break;

            case RoutedMessageIdAssign assign:
                _entityByRef[assign.ReffId] = message.EntityId;
                break;

            case RoutedMultipleMessage1 envelope:
                _envelopes++;
                foreach (var block in envelope.DataBlocks)
                {
                    ObserveBlock(message, block);
                }

                break;
        }
    }

    public void Write()
    {
        Console.WriteLine("\n=== facing: envelope decode ===");
        Console.WriteLine($"  envelopes            {_envelopes}");
        Console.WriteLine($"  blocks               {_blocks}");
        Console.WriteLine($"  unresolved shorthand {_blocksUnresolved}");
        Console.WriteLine($"  ref assignments      {_entityByRef.Count}");
        Console.WriteLine($"  pose bodies exact    {_posesParsed}");
        Console.WriteLine($"  pose bodies rejected {_posesRejected}");
        Console.WriteLine($"  pose delta form      {_posesPartial}  (skipped, no full position+rotation)");

        Console.WriteLine("  inner message ids    "
                          + string.Join(", ", _innerMessageIds.OrderByDescending(kv => kv.Value).Take(8).Select(kv => $"{kv.Key} x{kv.Value}")));

        // One row per character type: how far the sent orientation sits from the direction of travel,
        // averaged over every moving step of every entity of that type. Aim is the same comparison
        // against the aim vector, which travels in the same message and is convention-free.
        var rows = new List<(uint TypeId, int Entities, int Steps, double TravelOffset, double TravelSpread, double AimOffset)>();

        foreach (var group in _samplesByEntity.GroupBy(kv => _typeByEntity.GetValueOrDefault(kv.Key)))
        {
            var travelDeltas = new List<double>();
            var aimDeltas = new List<double>();
            var entities = 0;

            foreach (var (_, samples) in group)
            {
                entities++;
                CollectDeltas(samples, travelDeltas, aimDeltas);
            }

            if (travelDeltas.Count == 0 && aimDeltas.Count == 0)
            {
                continue;
            }

            var (travelMean, travelSpread) = CircularStats(travelDeltas);
            var (aimMean, _) = CircularStats(aimDeltas);
            rows.Add((group.Key, entities, travelDeltas.Count, travelMean, travelSpread, aimMean));
        }

        Console.WriteLine("\n=== facing: sent orientation vs travel, by dbcharacter::Monster id (0 = player or no keyframe seen) ===");
        Console.WriteLine("  type      entities  steps   yaw-travel   spread   yaw-aim");

        foreach (var row in rows.OrderByDescending(r => r.Steps))
        {
            Console.WriteLine(
                $"  {row.TypeId,-8}  {row.Entities,8}  {row.Steps,5}   {row.TravelOffset,8:F1}°  {row.TravelSpread,6:F1}°  {row.AimOffset,7:F1}°");
        }
    }

    private static void CollectDeltas(List<Sample> samples, List<double> travelDeltas, List<double> aimDeltas)
    {
        samples.Sort((a, b) => a.Timestamp.CompareTo(b.Timestamp));

        for (var i = 1; i < samples.Count; i++)
        {
            var step = samples[i].Position - samples[i - 1].Position;
            step.Z = 0;

            if (step.Length() >= MinTravel)
            {
                travelDeltas.Add(WrapDegrees(Yaw(samples[i].Rotation) - Degrees(MathF.Atan2(step.Y, step.X))));
            }

            var aim = samples[i].Aim;
            if (new Vector2(aim.X, aim.Y).Length() > 0.1f)
            {
                aimDeltas.Add(WrapDegrees(Yaw(samples[i].Rotation) - Degrees(MathF.Atan2(aim.Y, aim.X))));
            }
        }
    }

    private void ObserveBlock(DecodedMessage message, RoutedBlockStorage block)
    {
        _blocks++;

        ulong entityId;
        if (block.ReffId == ushort.MaxValue)
        {
            entityId = block.EntityId.Backing & ~0xFFUL;
        }
        else if (_entityByRef.TryGetValue(block.ReffId, out var mapped))
        {
            entityId = mapped;
        }
        else
        {
            _blocksUnresolved++;
            return;
        }

        if (block.Data is not { Length: > 1 })
        {
            return;
        }

        var innerId = block.Data[0];
        _innerMessageIds[innerId] = _innerMessageIds.GetValueOrDefault(innerId) + 1;

        // The assignments themselves ride inside the envelopes, as inline-entity blocks: "this
        // entity is henceforth shorthand N". Everything after resolves through the map they build.
        if (innerId == RoutedAssignId && block.Data.Length == 3)
        {
            _entityByRef[BitConverter.ToUInt16(block.Data, 1)] = entityId;
            return;
        }

        if (innerId != CurrentPoseUpdateId)
        {
            return;
        }

        var pose = new CurrentPoseUpdate();
        var body = block.Data.AsSpan(1);
        int consumed;
        try
        {
            consumed = ((Aero.Gen.IAero)pose).Unpack(body);
        }
        catch (Exception)
        {
            _posesRejected++;
            return;
        }

        if (consumed != body.Length)
        {
            _posesRejected++;
            return;
        }

        _posesParsed++;

        // Only the self-describing form carries a full position and rotation. The delta forms would
        // need the previous pose reconstructed, and there are enough full poses not to bother.
        if (pose.Data.Flags != 0)
        {
            _posesPartial++;
            return;
        }

        if (!_samplesByEntity.TryGetValue(entityId, out var list))
        {
            _samplesByEntity[entityId] = list = new List<Sample>();
        }

        list.Add(new Sample(
            message.Timestamp,
            pose.Data.Position,
            new Quaternion(
                Dequantise(pose.Data.Rotation.X.Value),
                Dequantise(pose.Data.Rotation.Y.Value),
                Dequantise(pose.Data.Rotation.Z.Value),
                Dequantise(pose.Data.Rotation.W.Value)),
            new Vector3(
                Dequantise(pose.Data.Aim.X.Value),
                Dequantise(pose.Data.Aim.Y.Value),
                Dequantise(pose.Data.Aim.Z.Value))));
    }

    /// <summary>
    ///     AeroMessages' own QuantisedFloat-to-float conversion doesn't invert its float-to-quantised
    ///     one: the encoder puts positives in [0, 32767] untouched and negatives above, but the decoder
    ///     mirrors every value, so every positive component comes back as its complement. The encoder is
    ///     the proven side — PIN sends NPC poses through it and the client renders them right — so this
    ///     reads by the encoder's convention.
    /// </summary>
    private static float Dequantise(ushort value)
        => value <= 32767 ? value / 32767f : 1f - (value / 32767f);

    /// <summary>
    ///     World yaw out of a wire quaternion, in degrees. The wire carries the stored orientation,
    ///     which is the inverse of the world rotation — the same convention PIN confirmed on N1 and
    ///     uses in <c>Facing.Towards</c> — so the world forward is +X taken through the inverse.
    /// </summary>
    private static double Yaw(Quaternion wire)
    {
        var forward = Vector3.Transform(Vector3.UnitX, Quaternion.Inverse(wire));
        return Degrees(MathF.Atan2(forward.Y, forward.X));
    }

    private static double Degrees(float radians) => radians * 180.0 / Math.PI;

    private static double WrapDegrees(double degrees)
    {
        degrees %= 360.0;
        return degrees switch
        {
            > 180.0 => degrees - 360.0,
            < -180.0 => degrees + 360.0,
            _ => degrees,
        };
    }

    /// <summary>
    ///     Mean and spread of a set of angles, done on the circle so +179° and -179° average to 180°
    ///     rather than 0°. Spread is the circular standard deviation.
    /// </summary>
    private static (double Mean, double Spread) CircularStats(List<double> degrees)
    {
        if (degrees.Count == 0)
        {
            return (0, 0);
        }

        double sumSin = 0, sumCos = 0;
        foreach (var d in degrees)
        {
            sumSin += Math.Sin(d * Math.PI / 180.0);
            sumCos += Math.Cos(d * Math.PI / 180.0);
        }

        var mean = Math.Atan2(sumSin / degrees.Count, sumCos / degrees.Count) * 180.0 / Math.PI;
        var resultant = Math.Sqrt((sumSin * sumSin) + (sumCos * sumCos)) / degrees.Count;
        var spread = Math.Sqrt(-2.0 * Math.Log(Math.Max(resultant, 1e-9))) * 180.0 / Math.PI;
        return (mean, spread);
    }

    private readonly record struct Sample(ulong Timestamp, Vector3 Position, Quaternion Rotation, Vector3 Aim);
}
