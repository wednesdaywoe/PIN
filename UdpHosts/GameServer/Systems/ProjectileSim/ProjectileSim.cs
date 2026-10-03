using System;
using System.Collections.Generic;
using System.Threading;
using System.Numerics;
using AeroMessages.GSS.V66;
using GameServer.Entities.Character;
using GameServer.Physics;
using GameServer.StaticDB;
using GameServer.StaticDB.Records.dbitems;
using GameServer.Systems.Aptitude;
using GameServer.Systems.Combat;
using GameServer.Systems.Hostility;
using Serilog;

namespace GameServer.Systems.ProjectileSim;

public class ProjectileSim
{
    private readonly Shard _shard;
    private readonly ILogger _logger;
    private readonly List<InFlight> _inFlight = [];

    public ProjectileSim(Shard shard)
    {
        _shard = shard;
        _logger = shard.Logger.ForContext(typeof(ProjectileSim));
    }

    public void FireProjectile(CharacterEntity entity, uint trace, Vector3 origin, Vector3 direction, Ammo ammo, WeaponTemplateResult weapon)
    {
        // A shot that struck the world still resolves, because it may still explode. `target` is null in
        // that case and everything below is written to expect it.
        if (!TryResolveHit(entity, origin, direction, trace, out var hit, out var target))
        {
            return;
        }

        // Range decay applies to the weapon's damage per round. Hit location and headshot scale what's left
        var distance = Vector3.Distance(origin, hit.Position);
        var falloff = DamageFalloff.Resolve(entity.GetEffectiveWeaponDamage(weapon), weapon, ammo);
        var decayed = falloff.DamageAt(distance);

        if (falloff.Enabled && distance > falloff.FullDamageRange)
        {
            _logger.Debug(
                "Damage falloff for {Weapon} at {Distance}m: {Base} -> {Decayed} (full to {FullDamageRange}m, {MinDamage} at {MaxRange}m)",
                weapon.DebugName,
                distance,
                falloff.BaseDamage,
                decayed,
                falloff.FullDamageRange,
                falloff.MinDamage,
                falloff.MaxRange);
        }

        var damageType = entity.WeaponDamageTypeOverride ?? ammo.Damagetype;

        if (target != null)
        {
            float damage = decayed * hit.DamageMod;
            if (hit.Headshot && weapon.HeadshotMult > 0)
            {
                damage *= weapon.HeadshotMult;
            }

            target.TakeDamage(new DamageInfo
            {
                Amount = damage,
                Attacker = entity,
                DamageType = damageType,
                Flags = ResolveFlags(hit),
                WeaponId = weapon.WeaponSdbId,
                WeaponName = weapon.DebugName,
            });
        }

        // Splash is deliberately based on `decayed` rather than on what the direct target took: a headshot
        // multiplier and a hit-location modifier belong to the body they were read off, not to everyone
        // standing near it.
        ApplySplash(entity, hit.Position, ammo, decayed, damageType, target, weapon);
    }

    /// <summary>
    ///    Fires a single projectile for an ability (FireProjectile aptitude command).
    ///    Damage comes from the command instead of the equipped weapon, so there's no headshot multiplier
    ///    and no range decay. <see cref="DamageFalloff"/> is anchored on the weapon template's Range, and an
    ///    ability firing on its own has no equivalent.
    ///    <para>
    ///    Ammo with a ProjectileSpeed flies: the arc is traced now, against the world as it stands, and the
    ///    landing is held until the flight time has passed. That window is what DetonateProjectiles bursts
    ///    early. Ammo with no speed lands at once, as every ability projectile did before.
    ///    </para>
    /// </summary>
    public void FireAbilityProjectile(CharacterEntity shooter, Vector3 origin, Vector3 direction, Ammo ammo, float damage)
    {
        // No ammo-radius splash here, unlike the weapon path above. An ability's area damage comes from the
        // ammo's impact ability (below) or InflictDamageCommand's own radius, so reading the ammo radius as well
        // would apply a second blast. DATA-21 is about weapons.
        if (ammo.ProjectileSpeed <= 0)
        {
            var resolved = TryResolveHit(shooter, origin, direction, 0, out var hit, out var target);
            if (resolved)
            {
                Land(shooter, ammo, damage, hit, target);
            }

            return;
        }

        var flight = new InFlight
        {
            Shooter = shooter,
            Ammo = ammo,
            Damage = damage,
            Origin = origin,
            Velocity = direction * ammo.ProjectileSpeed,
            Gravity = ammo.Gravity,
            FiredAt = _shard.CurrentTimeLong,
        };

        var landed = TraceFlight(flight, out var seconds);
        flight.EndsAt = flight.FiredAt + (ulong)(seconds * 1000f);

        // Poison Trail and its kind act while in flight, not on impact
        if (ammo.PeriodAbilityId != 0 && ammo.PeriodAbilityMs > 0)
        {
            RunPeriodAbility(shooter, origin, direction, ammo, landed ? flight.Hit.Position : null);
        }

        lock (_inFlight)
        {
            _inFlight.Add(flight);
        }

        _logger.Debug(
            "Ability projectile {Ammo} in flight for {Seconds:0.00}s, {Outcome}",
            ammo.Name,
            seconds,
            landed ? $"lands at {flight.Hit.Position}" : "lands nowhere");
    }

    /// <summary>
    ///     Bursts every projectile of <paramref name="ammoId"/> that <paramref name="shooter"/> still has in the air,
    ///     running the ammo's airburst ability where each one is now. Returns how many it burst.
    /// </summary>
    public int Detonate(CharacterEntity shooter, uint ammoId)
    {
        var now = _shard.CurrentTimeLong;
        List<InFlight> burst;
        lock (_inFlight)
        {
            burst = _inFlight.FindAll(f => ReferenceEquals(f.Shooter, shooter) && f.Ammo.Id == ammoId);
            _inFlight.RemoveAll(burst.Contains);
        }

        foreach (var flight in burst)
        {
            Burst(flight, flight.PositionAt((now - flight.FiredAt) / 1000f), "detonated");
        }

        return burst.Count;
    }

    public void Tick(double deltaTime, ulong currentTime, CancellationToken ct)
    {
        List<InFlight> due;
        lock (_inFlight)
        {
            if (_inFlight.Count == 0)
            {
                return;
            }

            due = _inFlight.FindAll(f => currentTime >= f.EndsAt);
            _inFlight.RemoveAll(due.Contains);
        }

        // Outside the lock: an impact ability can fire another projectile
        foreach (var flight in due)
        {
            // Reading of the data, not confirmed: a projectile that outlives ConstLifetime bursts where it is. Poison
            // Ball puts its cooldown in the burst, so one that simply vanished let a shot into the sky be repeated
            // with no cooldown at all.
            if (flight.Hit == null)
            {
                Burst(flight, flight.PositionAt((flight.EndsAt - flight.FiredAt) / 1000f), "ran out of flight");
                continue;
            }

            // The struck target was chosen at launch. It may have died since; the blast still lands where it was.
            var target = flight.Target is { IsAlive: true } ? flight.Target : null;
            Land(flight.Shooter, flight.Ammo, flight.Damage, flight.Hit, target);
        }
    }

    private void Burst(InFlight flight, Vector3 position, string why)
    {
        var abilityId = flight.Ammo.AirburstAbilityId != 0 ? flight.Ammo.AirburstAbilityId : flight.Ammo.AbilityId;
        _logger.Debug("Ability projectile {Ammo} {Why} in the air at {Position}, running {AbilityId}", flight.Ammo.Name, why, position, abilityId);
        if (abilityId != 0)
        {
            _shard.Abilities.HandleActivateAbility(_shard, flight.Shooter, abilityId, _shard.CurrentTime, new AptitudeTargets(), initPosition: position);
        }
    }

    private void Land(CharacterEntity shooter, Ammo ammo, float damage, ProjectileHitResult hit, IDamageable target)
    {
        if (target != null)
        {
            target.TakeDamage(new DamageInfo
            {
                Amount = damage * hit.DamageMod,
                Attacker = shooter,
                DamageType = ammo.Damagetype,
                Flags = ResolveFlags(hit),
            });
        }

        // Where the area happens. Poison Ball, the chemical grenade and most other thrown or fired area abilities
        // put their whole effect in the ammo's impact ability, which searches around InitPosition (TargetPBAE
        // UseInitPos) for whoever to poison or damage. It runs on a world hit too: a grenade on the ground still goes off.
        if (ammo.AbilityId != 0)
        {
            var targets = target is IAptitudeTarget struck ? new AptitudeTargets(struck) : new AptitudeTargets();
            _logger.Debug("Ability projectile {Ammo} landed at {Position}, running impact ability {AbilityId}", ammo.Name, hit.Position, ammo.AbilityId);
            _shard.Abilities.HandleActivateAbility(_shard, shooter, ammo.AbilityId, _shard.CurrentTime, targets, initPosition: hit.Position);
        }
    }

    /// <summary>
    ///     Walks the flight in short straight segments, falling under the ammo's Gravity, and stops at the first
    ///     thing a segment meets. Flight ends at ConstLifetime, or at the hitscan range when the ammo has none.
    ///     Returns false when nothing was met; <paramref name="seconds"/> is then the whole flight, and Tick bursts it.
    /// </summary>
    private bool TraceFlight(InFlight flight, out float seconds)
    {
        const float Step = 0.05f;
        const float HitscanRange = 500f;

        var lifetime = flight.Ammo.ConstLifetime > 0
            ? flight.Ammo.ConstLifetime / 1000f
            : MathF.Min(HitscanRange / flight.Ammo.ProjectileSpeed, 10f);

        var from = flight.Origin;
        for (var t = Step; ; t += Step)
        {
            t = MathF.Min(t, lifetime);
            var to = flight.PositionAt(t);
            var segment = to - from;
            var length = segment.Length();
            if (length > 0.0001f && TryResolveHit(flight.Shooter, from, segment / length, 0, out var hit, out var target, length))
            {
                flight.Hit = hit;
                flight.Target = target;
                seconds = t - Step + (Step * Vector3.Distance(from, hit.Position) / length);
                return true;
            }

            if (t >= lifetime)
            {
                seconds = lifetime;
                return false;
            }

            from = to;
        }
    }

    /// <summary>
    ///     Runs the ammo's period ability where the projectile would have been every PeriodAbilityMs. Projectiles here
    ///     resolve instantly, so the flight is laid out along the aim line instead: ProjectileSpeed for ConstLifetime,
    ///     cut short where the shot hit something, each point dropped onto the ground under it. Poison Trail's ammo
    ///     (1197) does 20 m/s for 1 s every 100 ms, ten clouds 2 m apart. Gravity is ignored; the trail lies flat.
    /// </summary>
    private void RunPeriodAbility(CharacterEntity shooter, Vector3 origin, Vector3 direction, Ammo ammo, Vector3? hitPosition)
    {
        const float DefaultFlightSeconds = 1f;
        const int MaxPoints = 30;

        var flight = ammo.ProjectileSpeed * (ammo.ConstLifetime > 0 ? ammo.ConstLifetime / 1000f : DefaultFlightSeconds);
        var length = hitPosition is { } end ? MathF.Min(Vector3.Distance(origin, end), flight) : flight;
        var step = ammo.ProjectileSpeed * ammo.PeriodAbilityMs / 1000f;

        var points = 0;
        for (var distance = step; distance <= length + 0.01f && points < MaxPoints; distance += step, points++)
        {
            var point = origin + (direction * distance);
            if (_shard.Physics.TryGetGroundHeight(point, out var groundZ))
            {
                point.Z = groundZ;
            }

            _shard.Abilities.HandleActivateAbility(_shard, shooter, ammo.PeriodAbilityId, _shard.CurrentTime, new AptitudeTargets(), initPosition: point);
        }

        _logger.Debug("Ability projectile {Ammo} ran period ability {AbilityId} at {Points} point(s) over {Length:0.0}m", ammo.Name, ammo.PeriodAbilityId, points, length);
    }

    private static DamageResponseFlags ResolveFlags(ProjectileHitResult hit)
    {
        return hit.Headshot || hit.Crit ? DamageResponseFlags.Critical : 0;
    }

    /// <summary>
    ///     Damages everything the blast reaches, having already dealt with whatever the round struck
    ///     directly. Does nothing at all for a weapon whose ammo carries no radius, which is most of them.
    /// </summary>
    /// <remarks>
    ///     <para>
    ///     Which ammo explodes and how the blast falls off is <see cref="WeaponSplash"/>'s reading of the
    ///     shipped columns, and the notes on why those columns read the way they do live there. Ability
    ///     splash is a separate path that was never affected — <c>InflictDamageCommand</c> has always had
    ///     its own, off <c>Splashrange</c> — and this deliberately does not touch it. DATA-21.
    ///     </para>
    ///     <para>
    ///     Walking every entity in the shard per shot is the same thing <c>InflictDamageCommand</c> does and
    ///     is affordable for the same reason: the loop is not entered at all unless the ammo explodes, so
    ///     ordinary rifle fire costs one comparison.
    ///     </para>
    /// </remarks>
    private void ApplySplash(
        CharacterEntity shooter,
        Vector3 center,
        Ammo ammo,
        float baseDamage,
        byte damageType,
        IDamageable directTarget,
        WeaponTemplateResult weapon)
    {
        var splash = WeaponSplash.Resolve(ammo);
        if (!splash.Enabled || baseDamage <= 0f)
        {
            return;
        }

        var hits = 0;

        foreach (var pair in _shard.Entities)
        {
            // The direct target already took its own round, at its own hit location. Damaging it again here
            // would charge one shot twice, which is the mistake V7 is written to catch.
            if (pair.Value is not IDamageable splashTarget
                || !splashTarget.IsAlive
                || ReferenceEquals(splashTarget, shooter)
                || ReferenceEquals(splashTarget, directTarget))
            {
                continue;
            }

            var scale = splash.ScaleAt(Vector3.Distance(center, splashTarget.Position));
            if (scale <= 0f || !HostilityRules.CanDamage(shooter, splashTarget))
            {
                continue;
            }

            splashTarget.TakeDamage(new DamageInfo
            {
                Amount = baseDamage * scale,
                Attacker = shooter,
                DamageType = damageType,
                WeaponId = weapon.WeaponSdbId,
                WeaponName = weapon.DebugName,
            });

            hits++;
        }

        if (hits > 0)
        {
            _logger.Debug(
                "Splash from {Weapon} ({Ammo}) at {Radius}m caught {Hits} entities beyond the direct hit",
                weapon?.DebugName,
                ammo.Name,
                splash.Radius,
                hits);
        }
    }

    /// <summary>
    ///     Traces a shot and reports back where it landed, and separately what it landed on if that is
    ///     something the shooter is allowed to hurt. Everything past this point differs between a weapon
    ///     and an ability.
    ///     <para>
    ///     The two answers are separate on purpose. False means the round touched nothing at all and there
    ///     is no impact anywhere to reason about. True with a null <paramref name="target"/> means it
    ///     landed somewhere real that cannot bleed — terrain, a wall, a corpse — which is a miss for a
    ///     rifle and an explosion for a grenade. Reporting those two as the same thing is what made every
    ///     shot into the ground disappear.
    ///     </para>
    /// </summary>
    private bool TryResolveHit(CharacterEntity shooter, Vector3 origin, Vector3 direction, uint trace, out ProjectileHitResult hit, out IDamageable target, float maxRange = 500f)
    {
        target = null;
        hit = _shard.Physics.ProjectileRayCast(origin, direction, shooter, trace, maxRange);

        if (hit == null)
        {
            return false;
        }

        // Plenty of things have collision without being able to bleed. A shot into one of those still
        // happened somewhere, so the impact stands and only the target is left unset.
        if (!_shard.Entities.TryGetValue(hit.HitEntityId, out var hitEntity) || hitEntity is not IDamageable damageable || ReferenceEquals(damageable, shooter))
        {
            return true;
        }

        if (!damageable.IsAlive || !HostilityRules.CanDamage(shooter, damageable))
        {
            return true;
        }

        target = damageable;
        return true;
    }

    private sealed class InFlight
    {
        public CharacterEntity Shooter { get; init; }

        public Ammo Ammo { get; init; }

        public float Damage { get; init; }

        public Vector3 Origin { get; init; }

        public Vector3 Velocity { get; init; }

        public float Gravity { get; init; }

        public ulong FiredAt { get; init; }

        public ulong EndsAt { get; set; }

        public ProjectileHitResult Hit { get; set; }

        public IDamageable Target { get; set; }

        public Vector3 PositionAt(float seconds) => Origin + (Velocity * seconds) - new Vector3(0, 0, 0.5f * Gravity * seconds * seconds);
    }
}
