using System;
using System.Collections;
using System.Collections.Generic;
using Serilog;

namespace GameServer.Systems.Aptitude;

public class AptitudeTargets : IEnumerable<IAptitudeTarget>
{
    private static readonly ILogger _logger = Log.ForContext<AptitudeTargets>();

    private readonly List<IAptitudeTarget> _targets;

    public AptitudeTargets()
    {
        _targets = [];
    }

    public AptitudeTargets(AptitudeTargets initialTargets)
    {
        _targets = [.. initialTargets];
    }

    public AptitudeTargets(params IAptitudeTarget[] initialTargets)
    {
        _targets = new(initialTargets.Length);

        foreach (var target in initialTargets)
        {
            if (target != null)
            {
                _targets.Add(target);
            }
        }

        PrintTargets();
    }

    public int Count => _targets.Count;

    IEnumerator IEnumerable.GetEnumerator()
    {
        return GetEnumerator();
    }

    public IEnumerator<IAptitudeTarget> GetEnumerator()
    {
        return _targets.GetEnumerator();
    }

    public void Push(IAptitudeTarget target)
    {
        if (target == null)
        {
            return;
        }

        PrintTargets();

        _logger.Debug("Pushing new target: {target}", target);

        _targets.Add(target);
    }

    public bool TryPop(out IAptitudeTarget result)
    {
        PrintTargets();

        var ok = _targets.Count != 0;

        if (ok)
        {
            result = _targets[^1];

            _logger.Debug("Popping target: {result}", result);

            _targets.RemoveAt(_targets.Count - 1);

            return true;
        }

        result = null;

        return false;
    }

    public bool TryPeek(out IAptitudeTarget result)
    {
        var ok = _targets.Count != 0;

        if (ok)
        {
            result = _targets[^1];

            _logger.Debug("Peeking at target: {result}", result);

            return true;
        }

        result = null;

        return false;
    }

    public IAptitudeTarget Peek()
    {
        return _targets[^1];
    }

    public void RemoveBottomN(int number)
    {
        PrintTargets();

        _logger.Debug("Removing first {number} targets", number);

        _targets.RemoveRange(0, Math.Min(number, _targets.Count));
    }

    public void PopN(int number)
    {
        PrintTargets();

        _logger.Debug("Popping last {number} targets", number);

        _targets.RemoveRange(_targets.Count - Math.Min(number, _targets.Count), Math.Min(number, _targets.Count));
    }

    public bool Contains(IAptitudeTarget target)
    {
        return _targets.Contains(target);
    }

    public void RemoveAll(Predicate<IAptitudeTarget> match)
    {
        _targets.RemoveAll(match);
    }

    /// <summary>
    ///     Removes matching targets by moving the last target into their place, which changes the order.
    ///     The client's health, range, movestate and effect tag filters remove targets this way.
    /// </summary>
    public void SwapRemoveAll(Predicate<IAptitudeTarget> match)
    {
        for (var i = 0; i < _targets.Count; i++)
        {
            if (match(_targets[i]))
            {
                _targets[i] = _targets[^1];
                _targets.RemoveAt(_targets.Count - 1);
                i--;
            }
        }
    }

    public void AddRange(AptitudeTargets targets)
    {
        _targets.AddRange(targets._targets);
    }

    public void Clear()
    {
        _targets.Clear();
    }

    public IAptitudeTarget[] ToArray()
    {
        return [.. _targets];
    }

    public void PrintTargets()
    {
        var s = string.Empty;

        foreach (var e in _targets)
        {
            s += e + ", ";
        }

        _logger.Debug("Targets ({count}): {targets}", _targets.Count, s.Trim(',', ' '));
    }
}