using GameServer.Entities;
using GameServer.Entities.Character;

namespace GameServer.Systems.SystemEvents;

public readonly record struct DebugChatDirectMessageEvent(
    string Message, INetworkClient Target);
public readonly record struct DebugChatBroadcastMessageEvent(
    string Message, IEntity Source);

/// <summary>
///     Raised once a character's death has been processed. <see cref="Killer"/> is null for
///     environmental deaths, matching <see cref="CharacterEntity.Die"/>'s own parameter.
/// </summary>
public readonly record struct CharacterDiedEvent(
    CharacterEntity Victim, CharacterEntity Killer);