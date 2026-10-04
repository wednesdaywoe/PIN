using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Threading;
using AeroMessages.GSS.V66.Character.Command;
using GameServer.Enums;
using GameServer.StaticDB;
using Serilog;

namespace GameServer.Systems.Aptitude;

public class AbilitySystem
{
    private static readonly ILogger _logger = Log.ForContext<AbilitySystem>();
    private readonly Shard _shard;
    private readonly ulong _updateIntervalMs = 20;
    private readonly Dictionary<ulong, VehicleCalldownRequest> _playerVehicleCalldownRequests;
    private readonly Dictionary<ulong, DeployableCalldownRequest> _playerDeployableCalldownRequests;
    private readonly Dictionary<ulong, ResourceNodeBeaconCalldownRequest> _playerThumperCalldownRequests;
    private readonly Lazy<FactionStances> _factions;
    private ulong _lastUpdate;

    // Chains booked to run at a set time while the effect that booked them lasts; see UpdateWaitAndFireOnceCommand
    private readonly List<(uint Due, Context Context, Action Run)> _scheduled = [];

    public AbilitySystem(Shard shard)
    {
        _shard = shard;
        Factory = new Factory(shard);
        _factions = new Lazy<FactionStances>(FactionStances.FromSDB);
        _playerVehicleCalldownRequests = [];
        _playerDeployableCalldownRequests = [];
        _playerThumperCalldownRequests = [];
    }

    /// <summary>
    ///     For tests that supply their own <see cref="Factory" />
    /// </summary>
    internal AbilitySystem(Factory factory, FactionStances factions = null)
    {
        Factory = factory;
        _factions = new Lazy<FactionStances>(() => factions);
        _playerVehicleCalldownRequests = [];
        _playerDeployableCalldownRequests = [];
        _playerThumperCalldownRequests = [];
    }

    public Factory Factory { get; }
    public FactionStances Factions => _factions.Value;

    public static float RegistryOp(float first, float second, Operand op)
    {
        if (float.IsNaN(first))
        {
            return second;
        }

        switch (op)
        {
            case Operand.ASSIGN:
                return second;
            case Operand.ADD:
            case Operand.ADD_ALT:
                return second + first;
            case Operand.MULTIPLY:
            case Operand.MULTIPLY_ALT:
                return second * first;
            case Operand.EXPONENTIATE:
                _logger.Debug("Uncertain RegistryOp {op}. {second} ^ {first} = {result}", op, second, first, (float)Math.Pow(second, first));
                return (float)Math.Pow(second, first);
            case Operand.SUBTRACT:
                return second - first;
            case Operand.DIVIDE:
                return second / first;
            case Operand.MINIMUM:
                return (first <= second) ? first : second;
            case Operand.MAXIMUM:
                return (first >= second) ? first : second;
            default:
                _logger.Warning("Unknown RegistryOp {op}", op);
                return second;
        }
    }

    public void Tick(double deltaTime, ulong currentTime, CancellationToken ct)
    {
        if (currentTime > _lastUpdate + _updateIntervalMs)
        {
            _lastUpdate = currentTime;
            RunScheduled((uint)currentTime);
            foreach (var entity in _shard.Entities.Values)
            {
                if (entity is IAptitudeTarget target)
                {
                    ProcessTarget(target, currentTime);
                }
            }
        }
    }

    /// <summary>
    ///     Runs <paramref name="run" /> at <paramref name="due" /> (shard time), as long as the effect whose context
    ///     this is is still on its target then. Effects are only looked at every UpdateFrequency ms, which can be
    ///     coarser than a wait in their Update chain (Fungal Bloom waits 510 ms in an effect checked every 1000 ms).
    /// </summary>
    public void Schedule(uint due, Context context, Action run)
    {
        _scheduled.Add((due, context, run));
    }

    internal void RunScheduled(uint now)
    {
        if (_scheduled.Count == 0)
        {
            return;
        }

        var due = _scheduled.Where(entry => unchecked((int)(now - entry.Due)) >= 0).ToList();
        foreach (var entry in due)
        {
            _scheduled.Remove(entry);
            if (entry.Context.Self?.GetActiveEffects().Any(state => ReferenceEquals(state?.Context, entry.Context)) == true)
            {
                entry.Run();
            }
        }
    }

    public void ProcessTarget(IAptitudeTarget entity, ulong currentTime)
    {
        var activeEffects = entity.GetActiveEffects();
        foreach (var activeEffect in activeEffects)
        {
            if (activeEffect?.Effect.DurationChain != null
                && currentTime > activeEffect.LastUpdateTime + activeEffect.Effect.UpdateFrequency)
            {
                activeEffect.Context.ExecutionHint = ExecutionHint.DurationEffect;
                bool durationResult = activeEffect.Effect.DurationChain.Execute(activeEffect.Context);
                activeEffect.LastUpdateTime = currentTime;

                if (durationResult)
                {
                    if (activeEffect.Effect.UpdateChain != null)
                    {
                        activeEffect.Context.ExecutionHint = ExecutionHint.UpdateEffect;
                        activeEffect.Effect.UpdateChain.Execute(activeEffect.Context);
                    }
                }
                else if (_shard.ProjectileSim.HoldsEffect(activeEffect.Context))
                {
                    _logger.Debug("Effect {EffectId} on {Target} held: its projectile is still in flight", activeEffect.Effect.Id, entity);
                }
                else
                {
                    DoRemoveEffect(activeEffect);
                }
            }
        }
    }

    public void DoApplyEffect(uint effectId, IAptitudeTarget target, Context context)
    {
        if (effectId == 0)
        {
            return;
        }

        var applyContext = Context.CopyContext(context);
        applyContext.Self = target;
        applyContext.ExecutionHint = ExecutionHint.ApplyEffect;

        var effect = Factory.LoadEffect(effectId);

        // TODO: Decouple effect storage from fields so that hidden effects can be added without using a network field
        /*
        if (effect.Data.Hidden == 0)
        {

        }
        */
        var effectState = target.AddEffect(effect, applyContext);

        if (effectState.MaxStacksExceeded)
        {
            // warning until re-application semantics are understood
            _logger.Warning("DoApplyEffect dropped effect {EffectId} on {Target}: already active at max stacks", effectId, target);
            return;
        }

        effect.ApplyChain?.Execute(applyContext);

        using var logContext = Serilog.Context.LogContext.PushProperty("ExecutionId", applyContext.ExecutionId);
        foreach (var pair in applyContext.Actives)
        {
            ICommand activeCommand = pair.Key;
            activeCommand.OnApply(applyContext, pair.Value);
        }
    }

    public void DoRemoveEffect(EffectState activeEffect)
    {
        activeEffect.Context.ExecutionHint = ExecutionHint.RemoveEffect;
        activeEffect.Context.Self.ClearEffect(activeEffect);
        activeEffect.Effect.RemoveChain?.Execute(activeEffect.Context);

        using var logContext = Serilog.Context.LogContext.PushProperty("ExecutionId", activeEffect.Context.ExecutionId);
        foreach (var pair in activeEffect.Context.Actives)
        {
            ICommand activeCommand = pair.Key;
            activeCommand.OnRemove(activeEffect.Context, pair.Value);
        }
    }

    public void DoRemoveEffect(IAptitudeTarget entity, uint effectId)
    {
        var activeEffects = entity.GetActiveEffects();
        foreach (var activeEffect in activeEffects)
        {
            if (activeEffect?.Effect.Id != null)
            {
                if (activeEffect.Effect.Id == effectId)
                {
                    DoRemoveEffect(activeEffect);
                    break;
                }
            }
        }
    }

    public VehicleCalldownRequest TryConsumeVehicleCalldownRequest(ulong entityId)
    {
        return _playerVehicleCalldownRequests.Remove(entityId, out var result) ? result : null;
    }

    public DeployableCalldownRequest TryConsumeDeployableCalldownRequest(ulong entityId)
    {
        return _playerDeployableCalldownRequests.Remove(entityId, out var result) ? result : null;
    }

    public ResourceNodeBeaconCalldownRequest TryConsumeResourceNodeBeaconCalldownRequest(ulong entityId)
    {
        return _playerThumperCalldownRequests.Remove(entityId, out var result) ? result : null;
    }

    public void HandleVehicleCalldownRequest(ulong entityId, VehicleCalldownRequest request)
    {
        if (_playerVehicleCalldownRequests.ContainsKey(entityId))
        {
            _logger.Information("Discarded an unconsumed vehicle calldown request for {entityId}", entityId);
            _playerVehicleCalldownRequests.Remove(entityId);
        }

        _playerVehicleCalldownRequests.Add(entityId, request);
    }

    public void HandleDeployableCalldownRequest(ulong entityId, DeployableCalldownRequest request)
    {
        if (_playerDeployableCalldownRequests.ContainsKey(entityId))
        {
            _logger.Information("Discarded an unconsumed deployable calldown request for {entityId}", entityId);
            _playerDeployableCalldownRequests.Remove(entityId);
        }

        _playerDeployableCalldownRequests.Add(entityId, request);
    }

    public void HandleResourceNodeBeaconCalldownRequest(ulong entityId, ResourceNodeBeaconCalldownRequest request)
    {
        if (_playerThumperCalldownRequests.ContainsKey(entityId))
        {
            _logger.Information("Discarded an unconsumed thumper calldown request for {entityId}", entityId);
            _playerThumperCalldownRequests.Remove(entityId);
        }

        _playerThumperCalldownRequests.Add(entityId, request);
    }

    public void HandleLocalProximityAbilitySuccess(IShard shard, IAptitudeTarget source, uint commandId, uint time, AptitudeTargets targets, Guid? executionId = null)
    {
        var execId = executionId ?? Guid.NewGuid();
        using var logContext = Serilog.Context.LogContext.PushProperty("ExecutionId", execId);
        _logger.Information("HandleLocalProximityAbilitySuccess Source {source}, Command {commandId}, Time {time}, TargetsCount {targetsCount}", source, commandId, time, targets.Count);

        var commandDef = SDBInterface.GetRegisterClientProximityCommandDef(commandId);

        if (commandDef.AbilityId != 0)
        {
            HandleActivateAbility(shard, source, commandDef.AbilityId, time, targets, execId);
        }

        if (commandDef.Chain != 0)
        {
            var chain = Factory.LoadChain(commandDef.Chain);
            chain.Execute(new Context(shard, source)
            {
                ExecutionId = execId,
                ChainId = commandDef.Chain,
                Targets = targets,
                InitTime = time,
                ExecutionHint = ExecutionHint.Proximity
            });
        }
    }

    public bool HandleActivateAbility(IShard shard, IAptitudeTarget initiator, uint abilityId, uint activationTime, AptitudeTargets targets, Guid? executionId = null, Vector3? initPosition = null, bool fromUltimate = false)
    {
        var execId = executionId ?? Guid.NewGuid();
        using var logContext = Serilog.Context.LogContext.PushProperty("ExecutionId", execId);
        var chainId = SDBInterface.GetAbilityData(abilityId).Chain;
        if (chainId == 0)
        {
            return false;
        }

        _logger.Information("HandleActivateAbility: Ability {AbilityId} starting Chain {ChainId}", abilityId, chainId);

        var chain = Factory.LoadChain(chainId);
        var context = new Context(shard, initiator)
        {
            ExecutionId = execId,
            ChainId = chainId,
            AbilityId = abilityId,
            Targets = targets,
            InitTime = activationTime,
            ExecutionHint = ExecutionHint.Ability,
            FromUltimate = fromUltimate,
        };
        if (initPosition is { } position)
        {
            context.InitPosition = position;
        }

        return chain.Execute(context);
    }

    public void HandleActivateAbility(IShard shard, IAptitudeTarget initiator, uint abilityId, bool fromUltimate = false)
    {
        HandleActivateAbility(shard, initiator, abilityId, _shard.CurrentTime, new AptitudeTargets(), fromUltimate: fromUltimate);
    }

    public void HandleTargetAbility()
    {
        throw new NotImplementedException();
    }

    public void HandleDeactivateAbility()
    {
        throw new NotImplementedException();
    }

    public void HandleActivateConsumable()
    {
        throw new NotImplementedException();
    }
}