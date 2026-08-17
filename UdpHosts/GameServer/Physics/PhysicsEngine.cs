#nullable enable
using System;
using System.Collections.Generic;
using System.Numerics;
using System.Runtime.CompilerServices;
using System.Threading;
using BepuPhysics;
using BepuPhysics.Collidables;
using BepuPhysics.Trees;
using BepuUtilities;
using BepuUtilities.Memory;
using DebugPipeProto;
using GameServer.Entities;
using GameServer.Entities.Character;
using GameServer.StaticDB;
using GameServer.Systems.SystemEvents;
using Serilog;
using Shared.Collision;
using Shared.Collision.ZoneLoading;

namespace GameServer.Physics;

/// <summary>
///    Runs physics simulations (primarily hit detection)
/// </summary>
public partial class PhysicsEngine
{
    public const float TargetTimestepDuration = 50; // (1/20f)
    public const float TargetDebugTickDuration = 200;

    /// <summary>
    ///     How high above the asked-for point the ground probe starts. A position handed to
    ///     <see cref="TryGetGroundHeight"/> is usually a guess, and a guess that landed a couple of metres
    ///     inside a hillside would find nothing at all if the ray started at it — the surface would be
    ///     behind the ray rather than in front of it. Starting above and looking down finds the surface
    ///     whether the guess was over it or under it, as long as it was not buried deeper than this.
    /// </summary>
    private const float GroundProbeHeadroom = 30f;

    /// <summary>
    ///     How far down the probe looks, measured from the headroom above. Long enough to cross the 12m
    ///     of vertical the zone 448 valley moves inside 9m horizontal, and short enough that a point over
    ///     a hole in the collision reports no ground instead of finding the far side of the world.
    /// </summary>
    private const float GroundProbeReach = 200f;

    private readonly ILogger _logger;
    private readonly EventBus _eventBus;
    private readonly ZoneLoader _zoneLoader;
    private readonly RigidBodyLoader _rigidBodyLoader;
    private readonly Dictionary<BodyHandle, ulong> _bodyToEntityId = [];
    private readonly Dictionary<ulong, BodyHandle> _entityIdToBody = [];
    private readonly Dictionary<ulong, AssetCompoundKey> _entityIdToAssetKey = [];
    private readonly string _mapsPath = string.Empty;
    private readonly string _cachePath = string.Empty;
    private readonly bool _forceReload;
    private readonly bool _isDebugPipeClient;

    private TypedIndex _fallbackShape;
    private int _debugEntityIndex = -1;
    private double _debugTimeAccumulator;

    public PhysicsEngine(EventBus eventBus, uint zoneId, string mapsPath = "", string assetDBPath = "", bool loadMapsCollision = false, DebugProjectileHitCallbacks? debugProjectileHitCallbacks = null, bool isDebugPipeClient = false, string cachePath = "", bool forceReload = false)
    {
        _eventBus = eventBus;
        _logger = Log.Logger.ForContext<PhysicsEngine>();
        _mapsPath = mapsPath;
        _cachePath = cachePath;
        _forceReload = forceReload;
        DebugProjectileHitCallbacks = debugProjectileHitCallbacks;

        var targetThreadCount = int.Max(1, Environment.ProcessorCount > 4 ? Environment.ProcessorCount - 2 : Environment.ProcessorCount - 1);

        BufferPool = new BufferPool();
        ThreadDispatcher = new ThreadDispatcher(targetThreadCount);
        Simulation = Simulation.Create(BufferPool, new NarrowPhaseCallbacks(), new PoseIntegratorCallbacks(new Vector3(0, 0, -8)), new SolveDescription(8, 1));

        _fallbackShape = Simulation.Shapes.Add(new Sphere(0.9f));

        _zoneLoader = new ZoneLoader(Simulation, BufferPool, ThreadDispatcher, mapsPath, cachePath);
        _rigidBodyLoader = new RigidBodyLoader(Simulation, BufferPool, ThreadDispatcher, assetDBPath, cachePath);
        PoseLoader = new PoseLoader.PoseLoader(assetDBPath);

        _isDebugPipeClient = isDebugPipeClient;
        DebugInitialize(isDebugPipeClient, zoneId);

        if (loadMapsCollision)
        {
            LoadZone(zoneId);
        }
    }

    public long? ZoneFileTimestamp { get; private set; }

    public Simulation Simulation { get; protected set; }
    public BufferPool BufferPool { get; private set; }
    public ThreadDispatcher ThreadDispatcher { get; private set; }
    public double TimeAccumulator { get; protected set; }
    public PoseLoader.PoseLoader PoseLoader { get; private set; }
    private DebugProjectileHitCallbacks? DebugProjectileHitCallbacks { get; set; }

    public void LoadZone(uint zoneId)
    {
        var ts = _zoneLoader.LoadZone(zoneId, _forceReload);
        if (ts.HasValue)
        {
            ZoneFileTimestamp = ts.Value;
        }
    }

    public StaticDescription[] LoadRigidBody(string assetId)
    {
        return _rigidBodyLoader.Load(assetId);
    }

    public void Tick(double deltaTime, ulong currentTime, CancellationToken ct)
    {
        TimeAccumulator += deltaTime;
        while (!ct.IsCancellationRequested && TimeAccumulator >= TargetTimestepDuration)
        {
            DebugProcessMessages();
            Simulation.Timestep(TargetTimestepDuration, ThreadDispatcher);
            TimeAccumulator -= TargetTimestepDuration;
        }

        if (!ct.IsCancellationRequested && !_isDebugPipeClient)
        {
            _debugTimeAccumulator += deltaTime;
            if (_debugTimeAccumulator >= TargetDebugTickDuration)
            {
                DebugSendTickUpdate();
                _debugTimeAccumulator = 0;
            }
        }
    }

    public BodyHandle CreateKineticEntity(CharacterEntity entity)
    {
        _logger.Debug("CreateKineticEntity Character {entityId}", entity.EntityId);
        var pose = new RigidPose { Position = entity.Position, Orientation = Quaternion.Inverse(entity.Orientation) };
        AssetCompoundKey key = GetCharacterPoseAsset(entity);
        var shape = GetAssetShape(key);
        var body = Simulation.Bodies.Add(BodyDescription.CreateKinematic(pose, shape, -1));
        _bodyToEntityId[body] = entity.EntityId;
        _entityIdToBody[entity.EntityId] = body;
        _entityIdToAssetKey[entity.EntityId] = key;

        _ = DebugPipe?.SendAsync(new PipeMessage
        {
            CreateKineticEntity = new CreateKineticEntity
            {
                EntityId = entity.EntityId,
                Pose = pose.ToProto(),
                Shape = new PipeCollisionShape
                {
                    AssetId = key.AssetId,
                    Offset = key.Offset.ToProto(),
                    Scale = key.Scale,
                },
            }
        });

        return body;
    }

    public BodyHandle CreateKineticEntity(BaseEntity entity)
    {
        _logger.Debug("CreateKineticEntity Base {entityId}", entity.EntityId);
        var assetId = entity.Collision.HitboxCollisionId;
        var offset = Vector3.Zero;
        var scale = entity.Collision.Scale;
        var pose = new RigidPose { Position = entity.Position, Orientation = Quaternion.Inverse(entity.Orientation) };
        var key = new AssetCompoundKey(assetId, offset, scale);
        var shape = GetAssetShape(key);
        var body = Simulation.Bodies.Add(BodyDescription.CreateKinematic(pose, shape, -1));
        _bodyToEntityId[body] = entity.EntityId;
        _entityIdToBody[entity.EntityId] = body;
        _entityIdToAssetKey[entity.EntityId] = key;

        _ = DebugPipe?.SendAsync(new PipeMessage
        {
            CreateKineticEntity = new CreateKineticEntity
            {
                EntityId = entity.EntityId,
                Pose = pose.ToProto(),
                Shape = new PipeCollisionShape
                {
                    AssetId = assetId,
                    Offset = offset.ToProto(),
                    Scale = scale,
                }
            }
        });

        return body;
    }

    public void UpdateEntity(CharacterEntity entity)
    {
        if (!_entityIdToBody.ContainsKey(entity.EntityId))
        {
            return;
        }

        var bodyHandle = _entityIdToBody[entity.EntityId];
        var body = Simulation.Bodies[bodyHandle];
        ref var currentPose = ref body.Pose;
        var currentShape = body.Collidable.Shape;
        AssetCompoundKey key = GetCharacterPoseAsset(entity);
        var shape = GetAssetShape(key);

        var orientation = Quaternion.Inverse(entity.Orientation);
        if (currentPose.Position != entity.Position || currentPose.Orientation != orientation || currentShape != shape)
        {
            _entityIdToAssetKey[entity.EntityId] = key;
            body.Awake = true;
            body.SetShape(shape);
            currentPose.Position = entity.Position;
            currentPose.Orientation = orientation;
        }
    }

    public void UpdateEntity(BaseEntity entity)
    {
        if (!_entityIdToBody.ContainsKey(entity.EntityId))
        {
            return;
        }

        var bodyHandle = _entityIdToBody[entity.EntityId];
        ref var currentPose = ref Simulation.Bodies[bodyHandle].Pose;

        var orientation = Quaternion.Inverse(entity.Orientation);
        if (currentPose.Position != entity.Position || currentPose.Orientation != orientation)
        {
            var body = Simulation.Bodies[bodyHandle];
            body.Awake = true;
            currentPose.Position = entity.Position;
            currentPose.Orientation = orientation;
        }
    }

    /// <summary>
    ///     Whether this entity has a body in the simulation. Most entity types don't, so anything cleaning up
    ///     after an arbitrary entity should ask before calling <see cref="RemoveEntity"/>, which warns.
    /// </summary>
    public bool HasBody(IEntity entity)
    {
        return _entityIdToBody.ContainsKey(entity.EntityId);
    }

    public void RemoveEntity(IEntity entity)
    {
        if (!_entityIdToBody.ContainsKey(entity.EntityId))
        {
            _logger.Warning("RemoveEntity was called for {entity} but there is no body!", entity.ToString());
            return;
        }

        var bodyHandle = _entityIdToBody[entity.EntityId];
        _entityIdToAssetKey.Remove(entity.EntityId);
        _entityIdToBody.Remove(entity.EntityId);
        _bodyToEntityId.Remove(bodyHandle);
        Simulation.Bodies.Remove(bodyHandle);

        _ = DebugPipe?.SendAsync(new PipeMessage
        {
            RemoveEntity = new RemoveEntity
            {
                EntityId = entity.EntityId,
            }
        });
    }

    public ProjectileHitResult? ProjectileRayCast(Vector3 origin, Vector3 direction, CharacterEntity source, uint trace)
    {
        var speed = 500f;
        var maxRange = 500f;

        // Same guard as TargetRayCast: a shooter whose body has already been removed has no shot to fire.
        if (!_entityIdToBody.TryGetValue(source.EntityId, out var sourceBody))
        {
            return null;
        }

        DebugProjectileHitCallbacks?.SendDebugProjectileSpawn(source, trace, origin, direction, speed);

        var hitHandler = default(RayHitHandler);
        hitHandler.T = maxRange;
        hitHandler.AvoidSourceBody = true;
        hitHandler.SourceBody = sourceBody;

        Simulation.RayCast(origin, direction, float.MaxValue, BufferPool, ref hitHandler);
        if (hitHandler.T < maxRange)
        {
            var hitPosition = origin + (direction * hitHandler.T);
            _logger.Debug("HitHandler {Mobility} T {T} HitCollidable {HitCollidable} at {HitPosition}", hitHandler.HitCollidable.Mobility, hitHandler.T, hitHandler.HitCollidable, hitPosition);

            DebugProjectileHitCallbacks?.SendDebugProjectileImpact(source, trace, hitPosition, hitHandler.Normal);

            if (hitHandler.HitCollidable.Mobility == CollidableMobility.Kinematic)
            {
                var bodyPosition = Simulation.Bodies[hitHandler.HitCollidable.BodyHandle].Pose.Position;
                bodyPosition.Z -= 0.9f;
                DebugProjectileHitCallbacks?.SendDebugProjectilePoseHit(source, trace, hitPosition, bodyPosition);

                var hitEntityId = _bodyToEntityId.GetValueOrDefault(hitHandler.HitCollidable.BodyHandle);
                if (hitEntityId != 0)
                {
                    var body = Simulation.Bodies[hitHandler.HitCollidable.BodyHandle];
                    var shape = body.Collidable.Shape;
                    bool headshot = false;
                    bool crit = false;
                    float damageMod = 1f;
                    if (_poseCompoundToAssetId.ContainsKey(shape))
                    {
                        var poseId = _poseCompoundToAssetId[shape];
                        var poseData = _assetIdToPoseCompoundData[poseId];
                        var poseShapeData = poseData[hitHandler.ChildIndex];
                        var physicsMaterial = SDBInterface.GetPhysicsMaterial((uint)poseShapeData.Material);

                        headshot = poseShapeData.ShapeFlags.Headshot;
                        crit = physicsMaterial?.IsCritHit == 1;
                        if (poseShapeData.DamageMod > 0)
                        {
                            damageMod = poseShapeData.DamageMod;
                        }

                        _logger.Debug($"ProjectileRayCast Impact on {poseShapeData.Name} of {hitEntityId}");
                    }

                    return new ProjectileHitResult
                    {
                        HitEntityId = hitEntityId,
                        Position = hitPosition,
                        Headshot = headshot,
                        Crit = crit,
                        DamageMod = damageMod,
                    };
                }
            }

            // Struck the world, or a body with no entity behind it. This used to return null and the shot
            // was simply discarded, which is right for a rifle and wrong for anything that explodes: a
            // grenade landing between two enemies hit the ground, the ground has no health, and the shot
            // did nothing at all. The impact POSITION is real whatever was standing there, so it is
            // reported with no entity attached and the caller decides whether a miss still does damage.
            return new ProjectileHitResult
            {
                HitEntityId = 0,
                Position = hitPosition,
            };
        }
        else
        {
            var timeoutPosition = origin + (direction * maxRange);
            var timeoutDirection = -Vector3.Normalize(direction);
            DebugProjectileHitCallbacks?.SendDebugProjectileTimeout(source, trace, timeoutPosition, timeoutDirection);
        }

        return null;
    }

    /// <summary>
    ///     The height of the world under <paramref name="at"/>, or false where there is nothing under it.
    ///
    ///     This is the query the server did not have until terrain loaded, and its absence is the whole of
    ///     <c>DATA-23</c>: every height in the AI is otherwise borrowed from a player, because a player
    ///     standing on the ground was the only ground measurement that reached this server. It looks at
    ///     statics only — a creature standing on another creature's head is not standing on the ground,
    ///     and neither is one inside a deployable.
    ///
    ///     False is a real answer and callers must handle it: zone collision has holes in it
    ///     (<c>DATA-22</c>), and anywhere outside the loaded chunks has no ground by definition. Every
    ///     caller here falls back to what it did before terrain, which is a worse answer rather than none.
    /// </summary>
    public bool TryGetGroundHeight(Vector3 at, out float groundZ)
    {
        groundZ = at.Z;

        var origin = new Vector3(at.X, at.Y, at.Z + GroundProbeHeadroom);
        var hitHandler = default(RayHitHandler);
        hitHandler.T = GroundProbeReach;
        hitHandler.StaticsOnly = true;

        Simulation.RayCast(origin, -Vector3.UnitZ, GroundProbeReach, BufferPool, ref hitHandler);

        if (hitHandler.T >= GroundProbeReach)
        {
            return false;
        }

        groundZ = origin.Z - hitHandler.T;
        return true;
    }

    public (bool, Vector3, ulong) TargetRayCast(Vector3 origin, Vector3 direction, CharacterEntity source, float maxRange = 500f)
    {
        bool outHit = false;
        Vector3 outPos = Vector3.Zero;
        ulong outEnt = 0;

        // An entity can lose its physics body while something else is still holding a reference to it.
        // A destroyed thumper removes its surviving wave members outright, and the AI tick already under
        // way then asks one of them for a line of sight. There is no body to cast from, so the answer is
        // "nothing hit". Indexing here instead threw KeyNotFoundException on the shard thread, which is
        // unhandled and takes the whole server down with it -- the 2026-08-16 crash in Block 6.
        if (!_entityIdToBody.TryGetValue(source.EntityId, out var sourceBody))
        {
            return (outHit, outPos, outEnt);
        }

        var hitHandler = default(RayHitHandler);
        hitHandler.T = maxRange;
        hitHandler.AvoidSourceBody = true;
        hitHandler.SourceBody = sourceBody;
        Simulation.RayCast(origin, direction, float.MaxValue, BufferPool, ref hitHandler);
        if (hitHandler.T < maxRange)
        {
            outHit = true;
            outPos = origin + (direction * hitHandler.T);

            // Same race from the other side: whatever the ray struck can be gone by the time it is named.
            // Zero reads as "hit something that is not an entity", which every caller already handles.
            outEnt = _bodyToEntityId.GetValueOrDefault(hitHandler.HitCollidable.BodyHandle);
        }

        return (outHit, outPos, outEnt);
    }

    partial void DebugInitialize(bool isDebugPipeClient, uint zoneId);

    private BodyDescription CreateTestBall(Vector3 pos)
    {
        var bulletShape = new Sphere(3f);
        var bulletDescription = BodyDescription.CreateDynamic(new Vector3(), bulletShape.ComputeInertia(100), new(Simulation.Shapes.Add(bulletShape), 0.1f), 0.01f);
        bulletDescription.Pose.Position = pos;
        Simulation.Bodies.Add(bulletDescription);
        return bulletDescription;
    }

    private struct RayHitHandler : IRayHitHandler
    {
        public float T;
        public CollidableReference HitCollidable;
        public bool AvoidSourceBody;
        public BodyHandle SourceBody;
        public Vector3 Normal;
        public int ChildIndex;

        /// <summary>
        ///     Ignore everything that is not world geometry. Set by <see cref="TryGetGroundHeight"/>,
        ///     which is asking where the ground is rather than what is in the way.
        /// </summary>
        public bool StaticsOnly;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public readonly bool AllowTest(CollidableReference collidable)
        {
            return Allow(collidable);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public readonly bool AllowTest(CollidableReference collidable, int childIndex)
        {
            return Allow(collidable);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void OnRayHit(in RayData ray, ref float maximumT, float t, Vector3 normal, CollidableReference collidable, int childIndex)
        {
            maximumT = t;
            T = t;
            HitCollidable = collidable;
            Normal = normal;
            ChildIndex = childIndex;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private readonly bool Allow(CollidableReference collidable)
        {
            if (StaticsOnly && collidable.Mobility != CollidableMobility.Static)
            {
                return false;
            }

            if (AvoidSourceBody && collidable.Mobility != CollidableMobility.Static && collidable.BodyHandle.Equals(SourceBody))
            {
                return false;
            }

            return true;
        }
    }
}
