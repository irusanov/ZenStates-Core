namespace ZenStates.Core.SMUCommands
{
    // Set DLDO Psm margin for a single core
    // CO margin range from -30 to 30 before Zen 4, and from -50 to 50 on Zen 4 and newer
    // Margin arg 16 bits (lowest 16 bits of the command arg)
    // [31-28] ccd index
    // [27-24] ccx index (always 0 for Zen3 where each ccd has just one ccx)
    // [23-20] core index
    // [19-16] reserved?
    // [15-0] CO margin
    internal class SetPsmMarginSingleCore : BaseSMUCommand
    {
        public SetPsmMarginSingleCore(SMU smu) : base(smu) { }

        public override bool CanExecute()
        {
            return base.CanExecute() && (smu.Mp1Smu.SMU_MSG_SetDldoPsmMargin > 0 || smu.Rsmu.SMU_MSG_SetDldoPsmMargin > 0);
        }

        public CmdResult Execute(uint coreMask, int margin)
        {
            if (CanExecute())
            {
                uint m = Utils.MakePsmMarginArg(margin);
                result.args[0] = (coreMask & 0xfff00000) | m;
                if (smu.Mp1Smu.SMU_MSG_SetDldoPsmMargin > 0)
                    result.status = smu.SendMp1Command(smu.Mp1Smu.SMU_MSG_SetDldoPsmMargin, ref result.args);
                else
                    result.status = smu.SendRsmuCommand(smu.Rsmu.SMU_MSG_SetDldoPsmMargin, ref result.args);
            }

            return base.Execute();
        }
    }
}
