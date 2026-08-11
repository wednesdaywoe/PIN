using System;
using System.Linq;
using AeroMessages.GSS.V66.Character;
using AeroMessages.GSS.V66.Character.Command;
using AeroMessages.GSS.V66.Character.Event;
using GameServer.Entities.Character;
using GameServer.Enums.GSS.Character;
using GameServer.Extensions;
using GameServer.Packets;
using GameServer.StaticDB;
using GameServer.Systems.Aptitude;
using Serilog;

namespace GameServer.Controllers.Character;

[ControllerID(Enums.GSS.Controllers.Character_CombatController)]
public class CombatController : Base
{
    private ILogger _logger;

    public override void Init(INetworkClient client, IPlayer player, IShard shard, ILogger logger)
    {
        _logger = logger.ForContext<CharacterEntity>();
    }

    [MessageID((byte)Commands.FireInputIgnored)]
    public void FireInputIgnored(INetworkClient client, IPlayer player, ulong entityId, GamePacket packet)
    {
        // TODO: Implement
    }

    [MessageID((byte)Commands.FireBurst)]
    public void FireBurst(INetworkClient client, IPlayer player, ulong entityId, GamePacket packet)
    {
        var query = packet.Unpack<FireBurst>();
        player.CharacterEntity.SetFireBurst(query.Time);
    }

    [MessageID((byte)Commands.FireWeaponProjectile)]
    public void FireWeaponProjectile(INetworkClient client, IPlayer player, ulong entityId, GamePacket packet)
    {
        var fireWeaponProjectile = packet.Unpack<FireWeaponProjectile>();

        player.HandleFireWeaponProjectile(fireWeaponProjectile.Time, fireWeaponProjectile.AimDirection);

        var weaponProjectileFired = new WeaponProjectileFired
        {
            ShortTime = (ushort)fireWeaponProjectile.Time,
            Aim = fireWeaponProjectile.AimDirection,
            HaveShooterVelocity = fireWeaponProjectile.HaveShooterVelocity,
            ShooterVelocity = fireWeaponProjectile.ShooterVelocity
        };

        client.NetChannels[ChannelType.ReliableGss].SendMessage(weaponProjectileFired, player.CharacterEntity.EntityId);
    }

    [MessageID((byte)Commands.FireEnd)]
    public void FireEnd(INetworkClient client, IPlayer player, ulong entityId, GamePacket packet)
    {
        var query = packet.Unpack<FireEnd>();
        player.CharacterEntity.SetFireEnd(query.Time);
    }

    [MessageID((byte)Commands.FireCancel)]
    public void FireCancel(INetworkClient client, IPlayer player, ulong entityId, GamePacket packet)
    {
        var query = packet.Unpack<FireCancel>();
        player.CharacterEntity.SetFireCancel(query.Time);
    }

    [MessageID((byte)Commands.UseScope)]
    public void UseScope(INetworkClient client, IPlayer player, ulong entityId, GamePacket packet)
    {
        var query = packet.Unpack<UseScope>();
        player.CharacterEntity.SetFireMode(1, new FireModeData
        {
           Mode = (byte)query.InScope,
           Time = query.Time,
        });
    }

    [MessageID((byte)Commands.SelectWeapon)]
    public void SelectWeapon(INetworkClient client, IPlayer player, ulong entityId, GamePacket packet)
    {
        var query = packet.Unpack<SelectWeapon>();
        player.CharacterEntity.SetWeaponIndex(new WeaponIndexData
        {
            Index = query.SelectedWeaponIndex,
            Unk1 = query.Unk3,
            Unk2 = 0,
            Time = query.Time,
        });
    }

    [MessageID((byte)Commands.SelectFireMode)]
    public void SelectFireMode(INetworkClient client, IPlayer player, ulong entityId, GamePacket packet)
    {
        var query = packet.Unpack<SelectFireMode>();
        player.CharacterEntity.SetFireMode(0, new FireModeData
        {
           Mode = query.FireMode,
           Time = query.Time,
        });
    }

    [MessageID((byte)Commands.ReloadWeapon)]
    public void ReloadWeapon(INetworkClient client, IPlayer player, ulong entityId, GamePacket packet)
    {
        var query = packet.Unpack<ReloadWeapon>();
        player.CharacterEntity.SetWeaponReloaded(query.Time);
    }

    [MessageID((byte)Commands.CancelReload)]
    public void CancelReload(INetworkClient client, IPlayer player, ulong entityId, GamePacket packet)
    {
        var query = packet.Unpack<CancelReload>();
        player.CharacterEntity.SetWeaponReloadCancelled(query.Time);
    }

    [MessageID((byte)Commands.ActivateConsumable)]
    public void ActivateConsumable(INetworkClient client, IPlayer player, ulong entityId, GamePacket packet)
    {
        var query = packet.Unpack<ActivateConsumable>();
        _logger.Information("ActivateConsumable {ItemSdbId}", query?.ItemSdbId);
        if (query == null)
        {
            return;
        }

        var abilityModule = SDBInterface.GetAbilityModule(query.ItemSdbId);
        if (abilityModule == null)
        {
            return;
        }

        uint abilityId = abilityModule.AbilityChainId;
        if (abilityId != 0)
        {
            var character = player.CharacterEntity;
            var activationTime = query.Time;

            var initiator = character as IAptitudeTarget;
            var shard = player.CharacterEntity.Shard;
            var targets = new AptitudeTargets();
            shard.Abilities.HandleActivateAbility(shard, initiator, abilityId, activationTime, targets);

            // Same ordering as ActivateAbility: confirm after the chain, not before it.
            SendAbilityActivated(character, abilityId, activationTime);
        }
    }

    [MessageID((byte)Commands.ActivateAbility)]
    public void ActivateAbility(INetworkClient client, IPlayer player, ulong entityId, GamePacket packet)
    {
        var activateAbility = packet.Unpack<ActivateAbility>();
        _logger.Information("ActivateAbility Slot {AbilitySlotIndex}", activateAbility?.AbilitySlotIndex);
        if (activateAbility == null)
        {
            return;
        }

        // Get the ability id based on the slotted ability
        var abilitySlot = activateAbility.AbilitySlotIndex;
        var character = player.CharacterEntity;
        uint abilityId = 0;

        // Using the local data until we can get the loadout remotely
        if (character.CurrentLoadout != null)
        {
            var moduleId = character.CurrentLoadout.GetAbilityModuleIdBySlotIndex(abilitySlot);
            if (moduleId != 0)
            {
                var abilityModule = SDBInterface.GetAbilityModule(moduleId);
                if (abilityModule != null)
                {
                    abilityId = abilityModule.AbilityChainId;
                }
            }
        }

        // Defaults if we failed
        if (abilityId == 0)
        {
            // Ability1 - Default button 1
            if (abilitySlot == 0)
            {
            }

            // Ability2 - Default button 2
            if (abilitySlot == 1)
            {
            }

            // Ability3 - Default button 3
            if (abilitySlot == 2)
            {
            }

            // AbilityHKM - Default button 4
            if (abilitySlot == 3)
            {
            }

            // AbilityInteract - Default button E
            if (abilitySlot == 4)
            {
                abilityId = 187; // Interact
            }

            // Auxiliary - Default button G
            if (abilitySlot == 5)
            {
            }

            // AbilityMedical - Default button Q
            if (abilitySlot == 6)
            {
            }

            // AbilitySIN - Default button F
            if (abilitySlot == 13)
            {
                abilityId = 43; // 40? SIN Targetting
            }

            // Vehicle - Default button V
            if (abilitySlot == 16)
            {
            }

            // Auxiliary - Default button T
            if (abilitySlot == 17)
            {
            }
        }

        if (abilityId != 0)
        {
            var activationTime = activateAbility.Time;

            var initiator = character as IAptitudeTarget;
            var shard = player.CharacterEntity.Shard;
            var targets = activateAbility.Targets
            .Where(entityId =>
            {
                try
                {
                    return shard.Entities[entityId.Backing & 0xffffffffffffff00] != null;
                }
                catch
                {
                    return false;
                }
            })
            .Select(entityId => (IAptitudeTarget)shard.Entities[entityId.Backing & 0xffffffffffffff00])
            .ToArray();

            shard.Abilities.HandleActivateAbility(shard, initiator, abilityId, activationTime, new AptitudeTargets(targets));

            SendAbilityActivated(character, abilityId, activationTime);
        }
    }

    [MessageID((byte)Commands.DeactivateAbility)]
    public void DeactivateAbility(INetworkClient client, IPlayer player, ulong entityId, GamePacket packet)
    {
        var deactivateAbility = packet.Unpack<DeactivateAbility>();
        _logger.Information("DeactivateAbility Slot {AbilitySlotIndex}", deactivateAbility?.AbilitySlotIndex);
        if (deactivateAbility == null)
        {
            return;
        }

        var character = player.CharacterEntity;
        if (character?.CurrentLoadout == null)
        {
            return;
        }

        var moduleId = character.CurrentLoadout.GetAbilityModuleIdBySlotIndex(deactivateAbility.AbilitySlotIndex);
        if (moduleId == 0)
        {
            return;
        }

        var abilityId = SDBInterface.GetAbilityModule(moduleId)?.AbilityChainId ?? 0;
        if (abilityId == 0)
        {
            return;
        }

        // Sent when a held ability ends. Effects only expire on their own if they have a
        // DurationChain (AbilitySystem.ProcessTarget), so without this the ones applied here
        // stay forever. charge leaves the client's camera pitch-locked until you relog.
        var shard = character.Shard;
        foreach (var activeEffect in character.GetActiveEffects())
        {
            if (activeEffect?.Context?.AbilityId == abilityId)
            {
                shard.Abilities.DoRemoveEffect(activeEffect);
            }
        }
    }

    // Sent after the chain runs, because the chain's InstantActivation command sits last in SDB and
    // that's where the confirmation belongs. We used to send it the moment the packet arrived, ahead
    // of every effect netfield the chain applies.
    //
    // This did NOT fix Charge's stuck camera (D5b in Docs/In-Game-Tests/Charge-Camera.md), so don't
    // read it as a cure for anything. It's kept only because it matches the order SDB describes.
    //
    // Belongs in InstantActivationCommand once the cooldown groups are worked out; that command has
    // the original send commented out and reads GlobalCooldown from its def, where this hardcodes it.
    private void SendAbilityActivated(CharacterEntity character, uint abilityId, uint activationTime)
    {
        if (!character.IsPlayerControlled)
        {
            return;
        }

        var message = new AbilityActivated
        {
            ActivatedAbilityId = abilityId,
            ActivatedTime = activationTime,
            AbilityCooldownsData = new AbilityCooldownsData
            {
                ActiveCooldowns_Group1 = Array.Empty<ActiveCooldown>(),
                ActiveCooldowns_Group2 = Array.Empty<ActiveCooldown>(),
                Unk = 0,
                GlobalCooldown_Activated_Time = activationTime,
                GlobalCooldown_ReadyAgain_Time = activationTime + 300,
            }
        };

        _logger.ForContext<AbilitySystem>()
               .Information("ActivateAbility {ActivatedAbilityId} at {ActivatedTime}", message.ActivatedAbilityId, message.ActivatedTime);
        character.Player.NetChannels[ChannelType.ReliableGss].SendMessage(message, character.EntityId);
    }
}