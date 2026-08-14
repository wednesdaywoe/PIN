using System.Collections.Generic;

namespace GameServer.Packets;

/// <summary>
///     The sequence numbers a channel has already handed upwards, so a resend of one can be
///     recognised as the duplicate it is instead of being processed a second time.
/// </summary>
/// <remarks>
///     Bounded, because a session delivers far more than it could ever need to remember. A resend
///     follows its original by about half a second, and the default holds several seconds of the
///     busiest reliable traffic the 2016 session ever produced.
/// </remarks>
public sealed class DeliveredSequences
{
    private readonly int _capacity;
    private readonly HashSet<ushort> _seen;
    private readonly Queue<ushort> _order;

    public DeliveredSequences(int capacity = 1024)
    {
        _capacity = capacity;
        _seen = new HashSet<ushort>(capacity);
        _order = new Queue<ushort>(capacity);
    }

    public int Count => _seen.Count;

    public bool Contains(ushort sequenceNumber) => _seen.Contains(sequenceNumber);

    public void Remember(ushort sequenceNumber)
    {
        if (!_seen.Add(sequenceNumber))
        {
            return;
        }

        _order.Enqueue(sequenceNumber);

        if (_order.Count > _capacity)
        {
            _seen.Remove(_order.Dequeue());
        }
    }
}
