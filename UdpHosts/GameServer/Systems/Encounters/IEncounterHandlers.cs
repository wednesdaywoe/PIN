using System.Collections.Generic;
using AeroMessages.GSS.V66.Character.Command;
using GameServer.Entities;
using GameServer.Entities.Character;

namespace GameServer.Systems.Encounters;

public interface IInteractionHandler
{
    void OnInteraction(BaseEntity actingEntity, BaseEntity target);
}

public interface IDonationHandler
{
    void OnDonation(UiQueryResponse response, INetworkPlayer player);
}

public interface IProximityHandler
{
    public HashSet<INetworkPlayer> Participants { get; }
    void OnProximity(BaseEntity sourceEntity, INetworkPlayer player);
}

public interface IExitAttachmentHandler
{
    void OnExitAttachment(BaseEntity targetEntity, INetworkPlayer player);
}

/// <summary>
///     Told when a character carrying this encounter's <see cref="EncounterComponent"/> dies, via the
///     same <c>CharacterDiedEvent</c> the kill rewards ride. Gated on <see cref="EncounterComponent.Event.Death"/>.
/// </summary>
public interface IDeathHandler
{
    void OnMemberDied(CharacterEntity victim, CharacterEntity killer);
}

/// <summary>
///     Told when a destructible non-character entity owned by this encounter is destroyed. Dispatched by
///     the entity itself from its damage path, so there's no flag to opt into; carrying the component is
///     the opt-in.
/// </summary>
public interface IDestructionHandler
{
    void OnDestroyed(BaseEntity entity, CharacterEntity attacker);
}