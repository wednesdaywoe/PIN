using System.Collections.Generic;
using System.Threading;
using GameServer.Entities.Character;
using GameServer.Entities.Melding;
using GameServer.StaticDB;
using GameServer.Systems.Combat;
using Serilog;

namespace GameServer.Systems.Hazards;

/// <summary>
///     Damage that comes from where you are standing rather than from anything that shot you.
///
///     Until this landed the world could not hurt anyone: every <see cref="CharacterEntity.TakeDamage"/>
///     call in the server came from a projectile or an <c>InflictDamage</c> aptitude command, which is why
///     a character could wade into deep water or walk through a melding wall — both near-instant deaths in
///     retail — and read as invulnerable.
///
///     Retail drove this through status effects on a 500ms update, and the drowning pair survives intact
///     in <c>clientdb.sd2</c> (787 drowning, 789 dying, both damage type 23), so the rates below are read
///     rather than invented. Applying those effects instead of the damage directly would be the faithful
///     version; it is not what happens here, because their chains run through registers, conditional
///     branches and stat modifiers whose implementation status in PIN's aptitude engine is unknown, and a
///     half-executing chain looks exactly like no damage at all. The numbers are the part that matters and
///     the numbers are the part that is real.
/// </summary>
public class HazardSim
{
    /// <summary>
    ///     Retail's <c>update_frequency</c> for both drowning effects. Matching it is what makes the rates
    ///     below mean what they meant.
    /// </summary>
    private const ulong UpdateIntervalMs = 500;

    /// <summary>
    ///     Effect 787: one register of <c>max_health * 0.01</c>, inflicted flat every tick. 50 seconds from
    ///     full to dead, which is a long time to be told to find the surface.
    /// </summary>
    private const float DrowningFractionPerTick = 0.01f;

    /// <summary>
    ///     Effect 789, which is the same shape as 787 with two differences: two registers a tick instead of
    ///     one, and the register is multiplied by 1.05 before every one of them, so it accelerates. Fully
    ///     under kills in about 25 ticks, or 12.5 seconds.
    /// </summary>
    private const float DyingFractionPerTick = 0.02f;
    private const float DyingRampPerTick = 1.05f;

    /// <summary>
    ///     Invented, and the only invented number here — see DATA-12. No melding-wall effect is findable in
    ///     the db: of the 20 status effects that inflict damage type 29 (Melding), every one that could be
    ///     read is a melded creature's attack, with a PBAE target selector and a radius. 5% of max health a
    ///     tick is about eleven seconds through a full health and shield pool, which is lethal enough to
    ///     respect and slow enough to turn around in.
    /// </summary>
    private const float MeldingFractionPerTick = 0.05f;

    private const byte DrowningDamageType = 23;
    private const byte MeldingDamageType = 29;

    /// <summary>
    ///     Which <c>dbvisualrecords::WaterDesc</c> row the description nibble means. It is an index into
    ///     something the map holds and the server doesn't, so every body of water is read as the standard
    ///     one: drown at 0.735 of your height, die at 1.0. The alternative is worse than a wrong guess —
    ///     row 10003 starts killing at 0.328, so mistaking a lake for it drowns people in the shallows.
    ///     <see cref="_seenDescriptions"/> exists to collect what the nibble actually says in play, which
    ///     is what a real mapping would have to be built from.
    /// </summary>
    private const uint DefaultWaterDescId = 10001;

    private readonly IShard _shard;
    private readonly ILogger _logger;
    private readonly Dictionary<ulong, HazardState> _stateByEntity = new();
    private readonly HashSet<byte> _seenDescriptions = [];
    private ulong _lastUpdate;

    public HazardSim(IShard shard)
    {
        _shard = shard;
        _logger = shard.Logger.ForContext<HazardSim>();
    }

    public void Tick(double deltaTime, ulong currentTime, CancellationToken ct)
    {
        if (currentTime < _lastUpdate + UpdateIntervalMs)
        {
            return;
        }

        _lastUpdate = currentTime;

        // NPCs are left out of both hazards on purpose. They have no client, so nothing ever fills in
        // their water level, and the things that live in the melding would be the first to die to it.
        foreach (var entity in _shard.Entities.Values)
        {
            if (entity is not CharacterEntity { IsPlayerControlled: true } character)
            {
                continue;
            }

            if (!character.IsAlive)
            {
                _stateByEntity.Remove(character.EntityId);
                continue;
            }

            if (!_stateByEntity.TryGetValue(character.EntityId, out var state))
            {
                state = new HazardState();
                _stateByEntity[character.EntityId] = state;
            }

            ApplyWater(character, state);
            ApplyMelding(character, state);
        }

        PruneDeparted();
    }

    /// <summary>
    ///     What is currently hurting a character, for the <c>hazard</c> admin command to print. Runs the
    ///     same two reads the tick does without applying anything.
    /// </summary>
    public (WaterHazard Water, MeldingEntity Melding, MeldingBearing Bearing, Submersion Submersion) Describe(CharacterEntity character)
    {
        var submersion = Submersion.Read(character.WaterLevelAndDesc);
        var (melding, bearing) = NearestMelding(character);

        return (submersion.Against(SDBInterface.GetWaterDesc(DefaultWaterDescId)), melding, bearing, submersion);
    }

    private void ApplyWater(CharacterEntity character, HazardState state)
    {
        var submersion = Submersion.Read(character.WaterLevelAndDesc);

        if (submersion.InWater && _seenDescriptions.Add(submersion.DescIndex))
        {
            _logger.Debug(
                "Water description nibble {Description} seen for the first time; every nibble is read as WaterDesc {Default}",
                submersion.DescIndex,
                DefaultWaterDescId);
        }

        var hazard = submersion.Against(SDBInterface.GetWaterDesc(DefaultWaterDescId));

        if (hazard != state.Water)
        {
            _logger.Debug(
                "{Character} water hazard {Previous} -> {Current} at depth {Depth:0.00} (level {Level}/{MaxLevel})",
                character,
                state.Water,
                hazard,
                submersion.Depth,
                submersion.Level,
                Submersion.MaxLevel);

            state.Water = hazard;
        }

        switch (hazard)
        {
            case WaterHazard.Drowning:
                state.DyingRamp = 1f;
                Hurt(character, DrowningFractionPerTick, DrowningDamageType);
                break;
            case WaterHazard.Dying:
                Hurt(character, DyingFractionPerTick * state.DyingRamp, DrowningDamageType);
                state.DyingRamp *= DyingRampPerTick;
                break;
            default:
                state.DyingRamp = 1f;
                break;
        }
    }

    private void ApplyMelding(CharacterEntity character, HazardState state)
    {
        var (melding, bearing) = NearestMelding(character);
        var inside = melding != null && bearing.Melded;

        if (inside != state.InMelding)
        {
            _logger.Debug(
                "{Character} {Transition} the melding at {Perimeter}, {Distance:0}m from the wall",
                character,
                inside ? "entered" : "left",
                melding?.PerimiterSetName ?? "(none)",
                bearing.Distance);

            state.InMelding = inside;
        }

        if (inside)
        {
            Hurt(character, MeldingFractionPerTick, MeldingDamageType);
        }
    }

    /// <summary>
    ///     The wall this character is closest to, and which side of it they are on. Every perimeter in the
    ///     zone is live at once, which is what the client is drawing, so any of them can be the one.
    /// </summary>
    private (MeldingEntity Melding, MeldingBearing Bearing) NearestMelding(CharacterEntity character)
    {
        MeldingEntity nearest = null;
        var bearing = MeldingBearing.Clear;

        foreach (var entity in _shard.Entities.Values)
        {
            if (entity is not MeldingEntity melding)
            {
                continue;
            }

            var candidate = MeldingField.Locate(melding.Perimeter, character.Position);

            // A wall you are behind beats a nearer wall you are merely standing next to.
            var better = candidate.Melded == bearing.Melded
                ? candidate.Distance < bearing.Distance
                : candidate.Melded;

            if (better)
            {
                nearest = melding;
                bearing = candidate;
            }
        }

        return (nearest, bearing);
    }

    private void Hurt(CharacterEntity character, float fractionOfMaxHealth, byte damageType)
    {
        character.TakeDamage(new DamageInfo
        {
            Amount = character.MaxHealth.Value * fractionOfMaxHealth,
            Attacker = null,
            DamageType = damageType,
            Flags = 0,
        });
    }

    private void PruneDeparted()
    {
        if (_stateByEntity.Count == 0)
        {
            return;
        }

        List<ulong> stale = null;
        foreach (var id in _stateByEntity.Keys)
        {
            if (!_shard.Entities.ContainsKey(id))
            {
                (stale ??= []).Add(id);
            }
        }

        if (stale == null)
        {
            return;
        }

        foreach (var id in stale)
        {
            _stateByEntity.Remove(id);
        }
    }

    private sealed class HazardState
    {
        public WaterHazard Water { get; set; }
        public bool InMelding { get; set; }

        /// <summary>Compounding multiplier behind <see cref="WaterHazard.Dying"/>, reset the moment you surface.</summary>
        public float DyingRamp { get; set; } = 1f;
    }
}
