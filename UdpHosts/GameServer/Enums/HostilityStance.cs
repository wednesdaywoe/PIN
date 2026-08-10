namespace GameServer.Enums;

/// <summary>
///     How one faction regards another, resolved from dbcharacter::FactionRelations.
///     The client tracks the same three states per player in PersonalFactionStanceData,
///     which carries a Friendly and a Hostile bitfield with neutral being neither.
/// </summary>
public enum HostilityStance
{
    Hostile,
    Neutral,
    Friendly,
}
