using GameServer.StaticDB.Records.customdata;

namespace GameServer.Systems.Aptitude.Commands.Duration;

public class ReplenishableDurationCommand : Command, ICommand
{
    // Reconstructed, not decompiled: this is a server-only command and its def shipped as an id and nothing else. As a
    // placeholder it returned true for ever, so every effect it governs (446 abilities, Poison Ball's poison among them)
    // never ended. The register holds the duration in seconds where the data loads one first (Poison Ball loads its
    // "Poison Ball Duration" stat, 8-10 s, the step before); elsewhere it holds whatever the applying chain left, so an
    // unusable value falls back to a default and every value is capped.
    private const float DefaultSeconds = 10f;
    private const float MaxSeconds = 120f;

    private ReplenishableDurationCommandDef Params;

    public ReplenishableDurationCommand(ReplenishableDurationCommandDef par)
: base(par)
    {
        Params = par;
    }

    public bool Execute(Context context)
    {
        var seconds = float.IsFinite(context.Register) && context.Register > 0 ? System.Math.Min(context.Register, MaxSeconds) : DefaultSeconds;
        var elapsed = (int)(context.Shard.CurrentTime - context.InitTime);
        if (elapsed < seconds * 1000)
        {
            return true;
        }

        Logger.Debug("{Command} {CommandId} ended its effect after {Seconds} s (register {Register})", nameof(ReplenishableDurationCommand), Params?.Id, seconds, context.Register);
        return false;
    }
}
