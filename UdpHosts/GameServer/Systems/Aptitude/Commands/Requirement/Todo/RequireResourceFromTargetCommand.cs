using GameServer.StaticDB.Records.aptfs;

namespace GameServer.Systems.Aptitude.Commands.Requirement;

/// <summary>
///     Left a stub deliberately. The table has exactly one row (id 452574) and it asks for 1 of
///     resource 1, which is not an item id — placeholder data that was never filled in. There is nothing
///     to implement against. The charging half it would have shared lives in
///     <see cref="RequireResourceCommand" />, which does have 41 real rows.
/// </summary>
public class RequireResourceFromTargetCommand : Command, ICommand
{
    private RequireResourceFromTargetCommandDef Params;

    public RequireResourceFromTargetCommand(RequireResourceFromTargetCommandDef par)
: base(par)
    {
        Params = par;
    }

    public bool Execute(Context context)
    {
        return true;
    }
}