using System.Collections.Generic;
using System.Numerics;
using AeroMessages.GSS.V66.Character;
using AeroMessages.GSS.V66.Character.Event;
using GameServer.Entities;
using Serilog;

namespace GameServer.Systems.MovementRelay;

public class MovementRelay
{
    /// <summary>How far a grounded player has to move before their footing is worth writing down again.</summary>
    private const float GroundSampleSpacing = 3f;

    private const ulong GroundSampleIntervalMs = 1000;

    private readonly Shard _shard;
    private readonly ILogger _logger;
    private readonly Dictionary<ulong, (Vector3 Position, ulong Time)> _lastGroundSample = new();

    public MovementRelay(Shard shard)
    {
        _shard = shard;
        _logger = shard.Logger.ForContext<MovementRelay>();
    }

    public void CharacterMovementInput(INetworkClient client, IEntity entity, AeroMessages.GSS.V66.Character.Command.MovementInput input)
    {
        var character = entity as Entities.Character.CharacterEntity;

        // Update our data based on the clients input
        var poseData = input.PoseData;
        var posRotState = poseData.PosRotState;
        character.SetPoseData(poseData, input.ShortTime);

        bool sendJumpActioned = poseData.TimeSinceLastJump < character.TimeSinceLastJump; // Compare the old value before updating 
        character.TimeSinceLastJump = poseData.TimeSinceLastJump;

        character.IsAirborne = poseData.GroundTimePositiveAirTimeNegative < 0;

        RecordGroundSample(character);

        var movementStateValue = posRotState.MovementState;
        character.MovementStateContainer.MovementStateValue = (ushort)movementStateValue;

        // Update with physics
        _shard.Physics.UpdateEntity(character);

        // Confirm the pose with the client
        var confirmedPose = new ConfirmedPoseUpdate
        {
            PoseData = new MovementPoseData
            {
                ShortTime = input.ShortTime,
                MovementType = MovementDataType.PosRotState,
                WaterLevelAndDesc = poseData.WaterLevelAndDesc,
                PosRotState = new MovementPosRotState
                            {
                                Pos = character.Position,
                                Rot = character.Orientation,
                                MovementState = movementStateValue // ToDo: This was ushort previously!
                            },
                Velocity = character.Velocity,
                JetpackEnergy = poseData.JetpackEnergy,
                GroundTimePositiveAirTimeNegative = poseData.GroundTimePositiveAirTimeNegative, // Somehow affects gravity
                TimeSinceLastJump = poseData.TimeSinceLastJump,
                HaveDebugData = 0
            },
            NextShortTime = unchecked((ushort)(input.ShortTime + 90)) // This value has to be in the future, nobody cares why.
        };
        client.NetChannels[ChannelType.UnreliableGss].SendMessage(confirmedPose, character.EntityId);

        // Forward update to remote clients
        var currentPose = new CurrentPoseUpdate
        {
            Data = new AeroMessages.GSS.V66.CurrentPoseUpdateData
            {
                Flags = 0x00,
                ShortTime = character.MovementShortTime,
                UnkAlwaysPresent = 0x79,
                MovementState = (ushort)character.MovementState,
                Position = character.Position,
                Rotation = character.Orientation,
                Aim = character.AimDirection,
            }
        };
        foreach (var remoteClient in _shard.Clients.Values)
        {
            if (remoteClient.Status.Equals(IPlayer.PlayerStatus.Playing))
            {
                if (sendJumpActioned)
                {
                    remoteClient.NetChannels[ChannelType.UnreliableGss].SendMessage(new JumpActioned { ShortTime = input.ShortTime }, character.EntityId);
                }

                remoteClient.NetChannels[ChannelType.UnreliableGss].SendMessage(currentPose, character.EntityId);
            }
        }
    }

    public void VehicleMovementInput(INetworkClient client, IEntity entity, AeroMessages.GSS.V66.Vehicle.Command.MovementInput input)
    {
        var vehicle = entity as Entities.Vehicle.VehicleEntity;
        vehicle.SetPoseData(input);

        // Update with physics
        _shard.Physics.UpdateEntity(vehicle);

        if (vehicle.ControllingPlayer?.CharacterEntity != null)
        {
            var character = vehicle.ControllingPlayer.CharacterEntity;
            character.SetPosition(input.Position);
            CharacterMovementInput(client, character, new AeroMessages.GSS.V66.Character.Command.MovementInput()
            {
                ShortTime = client.AssignedShard.CurrentShortTime,
                PoseData = new MovementPoseData()
                {
                    ShortTime = client.AssignedShard.CurrentShortTime,
                    MovementType = MovementDataType.PosRotState,
                    WaterLevelAndDesc = 0,
                    PosRotState = new MovementPosRotState()
                    {
                        Pos = input.Position,
                        Rot = character.Orientation,
                        MovementState = unchecked((short)0xd000)
                    },
                    Velocity = character.Velocity,
                    JetpackEnergy = 0x639c,
                    GroundTimePositiveAirTimeNegative = 0,
                    TimeSinceLastJump = character.TimeSinceLastJump,
                    HaveDebugData = 0
                }
            });
        }
    }

    /// <summary>
    ///     Writes down where a player is standing, at most once a second and only when they have moved a
    ///     few metres. A walk across a zone therefore leaves a list of positions in the log.
    /// </summary>
    /// <remarks>
    ///     The server holds no terrain: <c>LoadMapsCollision</c> is off and the physics world contains
    ///     entity colliders only, so nothing server-side can answer "how high is the ground here". A
    ///     grounded player's pose is the one place that answer arrives, because the client computed it
    ///     against the real terrain and sent it.
    ///
    ///     Recording it turns any session into a survey of walkable ground, which is what authoring
    ///     [spawn_group.json] anchors needs. The first two cuts of zone 448's spawn groups took their
    ///     heights from shipped object positions instead, on the assumption that an object sits on the
    ///     ground. Objects sit wherever they were placed, and one of those anchors put three Chosen
    ///     somewhere the player could be shot from but never see.
    /// </remarks>
    private void RecordGroundSample(Entities.Character.CharacterEntity character)
    {
        if (character == null || character.IsAirborne)
        {
            return;
        }

        var now = _shard.CurrentTimeLong;
        var position = character.Position;

        // A position the character was put at is not a measurement of the ground; only walking off it
        // is. Until then every sample would just repeat the teleport destination back as if it were a
        // footing, which is how N16 got run from inside a hillside.
        if (character.PlacedPosition is { } placed)
        {
            if (Vector3.Distance(position, placed) < GroundSampleSpacing)
            {
                return;
            }

            character.ClearPlacedPosition();
        }

        // Both conditions have to be satisfied, not either: a tester standing still for five minutes
        // has one footing to report, not three hundred copies of it.
        if (_lastGroundSample.TryGetValue(character.EntityId, out var last)
            && (now < last.Time + GroundSampleIntervalMs
                || Vector3.Distance(position, last.Position) < GroundSampleSpacing))
        {
            return;
        }

        _lastGroundSample[character.EntityId] = (position, now);
        _logger.Debug("Ground sample {Position} from {EntityId}", position, character.EntityId);
    }
}
