using System.Numerics;
using GameServer.Entities.Character;
using GameServer.StaticDB.Records.customdata;

namespace GameServer.Systems.Aptitude.Commands.Deployable;

public class DeployableSpawnCommand : Command, ICommand
{
    public DeployableSpawnCommandDef Params;

    public DeployableSpawnCommand(DeployableSpawnCommandDef par)
    : base(par)
    {
        Params = par;
    }

    public bool Execute(Context context)
    {
        var position = context.InitPosition;
        var orientation = Quaternion.Identity;

        if (Params.DeployableTypeId != null && Params.DeployableTypeId != 0)
        {
            var typeId = (uint)Params.DeployableTypeId;
            // Owned by whoever used the ability, on their side: its own abilities read the owner's module stats, and
            // it fights the owner's enemies (Fungal Bloom's fungus)
            var owner = context.Initiator as CharacterEntity ?? context.Initiator?.Owner;
            var entity = context.Shard.EntityMan.SpawnDeployable(typeId, position, orientation, owner, useOwnerFaction: owner != null, fromUltimate: context.FromUltimate);

            if (entity == null)
            {
                Logger.Warning("{Command} {CommandId}, Failed to spawn?", nameof(DeployableSpawnCommand), Params.Id);
                return false;
            }

            if (Params.Lifetime != null && Params.Lifetime != 0)
            {
                context.Shard.EntityMan.SetRemainingLifetime(entity, (uint)Params.Lifetime);
            }

            return true;
        }
        else
        {
            Logger.Warning("Don't know which deployable to spawn in {Command} {CommandId}, failing.", nameof(DeployableSpawnCommand), Params.Id);
            return false;
        }
    }
}