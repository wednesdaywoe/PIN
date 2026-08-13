using System.Collections.Generic;
using System.Linq;
using GameServer.StaticDB;
using GameServer.StaticDB.Records.customdata;

namespace GameServer.Systems.Admin.Commands;

/// <summary>
///     Places a zone's standing monsters by walking to where they should stand.
/// </summary>
/// <remarks>
///     Authoring this content by typing coordinates into JSON failed three times on 2026-08-13, each
///     time on the height rather than the code: the server holds no terrain, so nothing offline can
///     answer "is this spot on the ground". A player character standing somewhere is the answer, and
///     the client is already a correct 3D view of the map, so the running game is the editor.
///
///     Placements are written where the player is standing and saved immediately, because a position
///     that only exists in memory is one crash away from being walked again.
/// </remarks>
[ServerCommand(
    "Place the zone's standing monsters where you are standing",
    "spawngroup list | new <name> | add <monsterId> [groupId] | remove <groupId> | drop <groupId> <index> | delay <groupId> <seconds> | reload",
    "spawngroup",
    "sg")]
public class SpawnGroupServerCommand : ServerCommand
{
    public override void Execute(string[] parameters, ServerCommandContext context)
    {
        if (parameters.Length == 0)
        {
            SourceFeedback("spawngroup list | new <name> | add <monsterId> [groupId] | remove <groupId> | drop <groupId> <index> | delay <groupId> <seconds> | reload", context);
            return;
        }

        var zoneId = context.Shard.ZoneId;

        switch (parameters[0].ToLowerInvariant())
        {
            case "list": List(zoneId, context); break;
            case "new": New(zoneId, parameters, context); break;
            case "add": Add(zoneId, parameters, context); break;
            case "remove": Remove(zoneId, parameters, context); break;
            case "drop": Drop(zoneId, parameters, context); break;
            case "delay": Delay(zoneId, parameters, context); break;
            case "reload": Reload(context); break;
            default: SourceFeedback($"Unknown spawngroup verb '{parameters[0]}'", context); break;
        }
    }

    private static SpawnGroup Resolve(uint zoneId, string parameter, ServerCommandContext context, out uint groupId)
    {
        groupId = 0;
        var groups = CustomDBInterface.GetZoneSpawnGroups(zoneId);

        if (!uint.TryParse(parameter, out groupId) || !groups.TryGetValue(groupId, out var group))
        {
            return null;
        }

        return group;
    }

    private void List(uint zoneId, ServerCommandContext context)
    {
        var groups = CustomDBInterface.GetZoneSpawnGroups(zoneId);

        if (groups.Count == 0)
        {
            SourceFeedback($"Zone {zoneId} has no spawn groups", context);
            return;
        }

        foreach (var group in groups.Values.OrderBy(g => g.Id))
        {
            SourceFeedback($"[{group.Id}] {group.Name}: {group.Members.Count} monster(s), respawn {group.RespawnDelayMs / 1000}s", context);

            for (var i = 0; i < group.Members.Count; i++)
            {
                var member = group.Members[i];
                var distance = context.SourcePlayer?.CharacterEntity != null
                    ? $", {System.Numerics.Vector3.Distance(member.Position, context.SourcePlayer.CharacterEntity.Position):0}m away"
                    : string.Empty;
                SourceFeedback($"    {i}: monster {member.MonsterTypeId} at {member.Position}{distance}", context);
            }
        }
    }

    private void New(uint zoneId, string[] parameters, ServerCommandContext context)
    {
        if (parameters.Length < 2)
        {
            SourceFeedback("spawngroup new <name>", context);
            return;
        }

        var group = new SpawnGroup
        {
            Id = CustomDBInterface.NextSpawnGroupId(zoneId),
            ZoneId = zoneId,
            Name = string.Join(' ', parameters.Skip(1)),
            RespawnDelayMs = 90000,
            Members = new List<SpawnGroupMember>(),
        };

        CustomDBInterface.AddSpawnGroup(group);
        CustomDBInterface.SaveSpawnGroups();
        SourceFeedback($"Created group [{group.Id}] {group.Name}, empty, respawn 90s. Walk somewhere and 'spawngroup add <monsterId>'.", context);
    }

    private void Add(uint zoneId, string[] parameters, ServerCommandContext context)
    {
        if (context.SourcePlayer?.CharacterEntity == null)
        {
            SourceFeedback("Placing a monster needs a player character to stand where it goes", context);
            return;
        }

        if (parameters.Length is not (2 or 3))
        {
            SourceFeedback("spawngroup add <monsterId> [groupId]", context);
            return;
        }

        var typeId = ParseUIntParameter(parameters[1]);
        if (SDBInterface.GetMonster(typeId) == null)
        {
            SourceFeedback($"No monster data for typeId {typeId}", context);
            return;
        }

        var groups = CustomDBInterface.GetZoneSpawnGroups(zoneId);
        SpawnGroup group;

        if (parameters.Length == 3)
        {
            group = Resolve(zoneId, parameters[2], context, out var requested);
            if (group == null)
            {
                SourceFeedback($"Zone {zoneId} has no group {requested}", context);
                return;
            }
        }
        else if (groups.Count == 1)
        {
            group = groups.Values.First();
        }
        else
        {
            SourceFeedback(
                groups.Count == 0
                    ? "No groups yet. 'spawngroup new <name>' first."
                    : $"{groups.Count} groups in this zone, so say which: 'spawngroup add {typeId} <groupId>'",
                context);
            return;
        }

        var character = context.SourcePlayer.CharacterEntity;
        var position = character.Position;

        if (character.IsAirborne)
        {
            // The whole point of placing from a player is that the position is known-good ground.
            // Airborne, it is known-good air.
            SourceFeedback("You are airborne. Land first, or the monster is placed where you are floating.", context);
            return;
        }

        if (character.PlacedPosition != null)
        {
            // Standing where you were teleported or respawned is not the same as standing on ground.
            // The client reports grounded inside a hillside just as readily; see
            // CharacterEntity.PlacedPosition.
            SourceFeedback("You were put here rather than walking here, which is not proof of ground. Walk a few steps first.", context);
            return;
        }

        group.Members.Add(new SpawnGroupMember { MonsterTypeId = typeId, Position = position });
        CustomDBInterface.SaveSpawnGroups();
        context.Shard.Spawns.Reload();

        SourceFeedback($"[{group.Id}] {group.Name}: monster {typeId} placed at {position}, {group.Members.Count} in the group", context);
    }

    private void Remove(uint zoneId, string[] parameters, ServerCommandContext context)
    {
        if (parameters.Length != 2)
        {
            SourceFeedback("spawngroup remove <groupId>", context);
            return;
        }

        var group = Resolve(zoneId, parameters[1], context, out var groupId);
        if (group == null)
        {
            SourceFeedback($"Zone {zoneId} has no group {groupId}", context);
            return;
        }

        CustomDBInterface.RemoveSpawnGroup(zoneId, groupId);
        CustomDBInterface.SaveSpawnGroups();
        context.Shard.Spawns.Reload();
        SourceFeedback($"Removed group [{groupId}] {group.Name} and its {group.Members.Count} monster(s)", context);
    }

    private void Drop(uint zoneId, string[] parameters, ServerCommandContext context)
    {
        if (parameters.Length != 3)
        {
            SourceFeedback("spawngroup drop <groupId> <index>, index from 'spawngroup list'", context);
            return;
        }

        var group = Resolve(zoneId, parameters[1], context, out var groupId);
        if (group == null)
        {
            SourceFeedback($"Zone {zoneId} has no group {groupId}", context);
            return;
        }

        var index = (int)ParseUIntParameter(parameters[2]);
        if (index < 0 || index >= group.Members.Count)
        {
            SourceFeedback($"Group [{groupId}] has {group.Members.Count} member(s), no index {index}", context);
            return;
        }

        var dropped = group.Members[index];
        group.Members.RemoveAt(index);
        CustomDBInterface.SaveSpawnGroups();
        context.Shard.Spawns.Reload();
        SourceFeedback($"[{groupId}] dropped monster {dropped.MonsterTypeId} at {dropped.Position}", context);
    }

    private void Delay(uint zoneId, string[] parameters, ServerCommandContext context)
    {
        if (parameters.Length != 3)
        {
            SourceFeedback("spawngroup delay <groupId> <seconds>", context);
            return;
        }

        var group = Resolve(zoneId, parameters[1], context, out var groupId);
        if (group == null)
        {
            SourceFeedback($"Zone {zoneId} has no group {groupId}", context);
            return;
        }

        group.RespawnDelayMs = ParseUIntParameter(parameters[2]) * 1000;
        CustomDBInterface.SaveSpawnGroups();
        context.Shard.Spawns.Reload();
        SourceFeedback($"[{groupId}] {group.Name} respawns {group.RespawnDelayMs / 1000}s after a place empties", context);
    }

    private void Reload(ServerCommandContext context)
    {
        context.Shard.Spawns.Reload();
        SourceFeedback($"Reloaded spawn groups from {CustomDBLoader.SpawnGroupPath}", context);
    }
}
