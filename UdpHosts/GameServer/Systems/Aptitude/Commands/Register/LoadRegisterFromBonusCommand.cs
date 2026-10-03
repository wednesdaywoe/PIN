using System;
using GameServer.StaticDB.Records.apt;

namespace GameServer.Systems.Aptitude.Commands.Register;

public class LoadRegisterFromBonusCommand : Command, ICommand
{
    private LoadRegisterFromBonusCommandDef Params;

    public LoadRegisterFromBonusCommand(LoadRegisterFromBonusCommandDef par)
: base(par)
    {
        Params = par;
    }

    public bool Execute(Context context)
    {
        // As the client (apt::LoadRegisterFromBonusCommand, FUN_00bbfaf0): the register gets RegisterVal_N with
        // N = (a + bonus) / b, capped at 10. Which of the two bytes is a and which b isn't visible in the client; every
        // row in the SDB has BonusTrackCount 3 and BonusTrack 1-3, which only reads sensibly as the track added and the
        // count divided by. The client also applies a register op stored after the values, which this def doesn't load,
        // so the value is assigned.
        var index = Params.BonusTrackCount == 0 ? 0 : Math.Min(10, (Params.BonusTrack + Math.Max(0, context.Bonus)) / Params.BonusTrackCount);
        context.Register = index switch
        {
            0 => Params.RegisterVal_0,
            1 => Params.RegisterVal_1,
            2 => Params.RegisterVal_2,
            3 => Params.RegisterVal_3,
            4 => Params.RegisterVal_4,
            5 => Params.RegisterVal_5,
            6 => Params.RegisterVal_6,
            7 => Params.RegisterVal_7,
            8 => Params.RegisterVal_8,
            9 => Params.RegisterVal_9,
            _ => Params.RegisterVal_10,
        };
        return true;
    }
}
