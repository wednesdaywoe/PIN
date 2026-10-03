using GameServer.Entities.Character;
using GameServer.StaticDB.Records.aptfs;
using Flags = AeroMessages.GSS.V66.Character.CombatFlagsData.CharacterCombatFlags;

namespace GameServer.Systems.Aptitude.Commands.SetFlags;

/// <summary>
///     Stuns, roots, knockdowns and the like: forbids the character carrying the effect from moving, shooting, using
///     abilities and so on, for as long as the effect lasts. All 1479 uses are in an effect's apply chain, and the
///     client's tfCombatFlagsCommand (0xebf870) ties them to the effect the same way: set on apply, cleared on remove.
/// </summary>
public class CombatFlagsCommand : Command, ICommand
{
    private CombatFlagsCommandDef Params;

    public CombatFlagsCommand(CombatFlagsCommandDef par)
: base(par)
    {
        Params = par;
    }

    public bool Execute(Context context)
    {
        if (context.Self is CharacterEntity { IsPlayerControlled: true } player && context.Initiator == player)
        {
            // A player's own abilities lock their own character: Healing Wave's cast, the use key's hold, Turret Mode.
            // The client predicts those and applies the flags itself, and 303 of the 1457 effects that carry flags
            // have no duration step the server has built, so here they would never end: a self-cast root would hold
            // the player forever. Flags from someone else (a monster's stun, another player) still apply.
            Logger.Debug("{Command} {CommandId} left to {Self}'s client: a player's own effect", nameof(CombatFlagsCommand), Params.Id, player);
        }
        else if (context.Self is CharacterEntity)
        {
            context.Actives.TryAdd(this, new CombatFlagsActiveContext());
        }
        else
        {
            Logger.Debug("{Command} {CommandId} ignored on {Self}, which is not a character", nameof(CombatFlagsCommand), Params.Id, context.Self);
        }

        return true;
    }

    public void OnApply(Context context, ICommandActiveContext activeCommandContext)
    {
        if (context.Self is CharacterEntity character)
        {
            var flags = ToWire();
            character.AddCombatFlags(activeCommandContext, flags, Params.ImmuneDeath == 1, Params.ImmunePhysics == 1);
            Logger.Debug("{Command} {CommandId} on {Self}: {Flags}{Death}{Physics}", nameof(CombatFlagsCommand), Params.Id, character, flags,
                Params.ImmuneDeath == 1 ? ", immune to death" : string.Empty, Params.ImmunePhysics == 1 ? ", immune to physics" : string.Empty);
        }
    }

    public void OnRemove(Context context, ICommandActiveContext activeCommandContext)
    {
        if (context.Self is CharacterEntity character)
        {
            character.RemoveCombatFlags(activeCommandContext);
            Logger.Debug("{Command} {CommandId} lifted from {Self}, now {Flags}", nameof(CombatFlagsCommand), Params.Id, character, character.ActiveCombatFlags);
        }
    }

    internal Flags ToWire()
    {
        Flags flags = 0;
        Set(Params.RestrictMovement, Flags.restrict_movement);
        Set(Params.RestrictWeapon, Flags.restrict_weapon);
        Set(Params.RemoveHitboxes, Flags.remove_hitboxes);
        Set(Params.RestrictAbilities, Flags.restrict_abilities);
        Set(Params.ImmuneFalldamage, Flags.immune_falldamage);
        Set(Params.RestrictSprint, Flags.restrict_sprint);
        Set(Params.RestrictMelee, Flags.restrict_melee);
        Set(Params.RestrictInteraction, Flags.restrict_interaction);
        Set(Params.KnockDown, Flags.knock_down);
        Set(Params.RestrictStumble, Flags.restrict_stumble);
        Set(Params.MoveThroughObjects, Flags.move_through_objects);
        Set(Params.ReversedControls, Flags.reversed_controls);
        return flags;

        void Set(byte param, Flags bit)
        {
            if (param == 1)
            {
                flags |= bit;
            }
        }
    }
}

public class CombatFlagsActiveContext : ICommandActiveContext
{
}
