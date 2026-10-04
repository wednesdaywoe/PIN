using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;

namespace GameServer.Systems.Aptitude;

public class Context
{
    public Context(IShard shard, IAptitudeTarget initiator)
    {
        Shard = shard;
        Initiator = initiator;
        Self = initiator;
        Abilities = shard.Abilities;
        Targets = new AptitudeTargets();
        FormerTargets = new AptitudeTargets();
        InitPosition = initiator.Position;
        ExecutionId = Guid.NewGuid();
    }

    public uint ChainId { get; set; }
    public uint AbilityId { get; set; }
    public bool Success { get; set; }
    public IShard Shard { get; set; }
    public AbilitySystem Abilities { get; set; }
    public IAptitudeTarget Self { get; set; }
    public IAptitudeTarget Initiator { get; set; }
    public AptitudeTargets Targets { get; set; }
    public AptitudeTargets FormerTargets { get; set; }

    /// <summary>
    ///     Saved copies of target lists for PushTargets, PopTargets and PeekTargets, separate from <see cref="FormerTargets" />
    /// </summary>
    public Stack<AptitudeTargets> TargetStack { get; } = new();
    public float Register { get; set; }

    /// <summary>
    ///     Saved registers for PushRegister, PeekRegister and PopRegister. An effect keeps its context, so values pushed
    ///     in its apply chain carry into its update ticks.
    /// </summary>
    public Stack<float> RegisterStack { get; private set; } = new();
    public float FormerRegister { get; set; }
    public int Bonus { get; set; }
    public uint InitTime { get; set; }

    /// <summary>
    ///     Set by the chain's ActiveInitiation, which only initiates once, with the time it did so
    /// </summary>
    public bool Initiated { get; set; }
    public uint ActivationTime { get; set; }
    public Vector3 InitPosition { get; set; }
    public ExecutionHint ExecutionHint { get; set; }

    /// <summary>
    ///     Set once an ultimate passes its RequireSuperCharge, and carried into everything it starts (effects,
    ///     projectile landings, tiny objects, deployables), so its damage earns no ultimate meter back.
    /// </summary>
    public bool FromUltimate { get; set; }
    public Guid ExecutionId { get; set; }

    public Dictionary<ICommand, ICommandActiveContext> Actives { get; set; } = [];

    public static Context CopyContext(Context original)
    {
        return new Context(original.Shard, original.Initiator)
        {
            ChainId = original.ChainId,
            AbilityId = original.AbilityId,
            Success = original.Success,
            Shard = original.Shard,
            Abilities = original.Abilities,
            Self = original.Self,
            Initiator = original.Initiator,
            // Copies, not the same lists: each effect applied from one context runs TargetClear/TargetSelf on its own
            // targets, and sharing them left every effect aimed at whichever target was set up last.
            Targets = new AptitudeTargets(original.Targets),
            FormerTargets = new AptitudeTargets(original.FormerTargets),
            Register = original.Register,
            RegisterStack = new Stack<float>(original.RegisterStack.Reverse()),
            Bonus = original.Bonus,
            InitTime = original.InitTime,
            Initiated = original.Initiated,
            ActivationTime = original.ActivationTime,
            InitPosition = original.InitPosition,
            ExecutionHint = original.ExecutionHint,
            ExecutionId = original.ExecutionId,
            FromUltimate = original.FromUltimate,
        };
    }

    /*
    public uint NamedVar;
    public uint Interaction;
    public uint SourceContext;
    public uint SourceEffect;
    */
}