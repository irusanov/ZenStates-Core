namespace ZenStates.Core.SMUCommands
{
    // Set DLDO Psm margin for a single core
    // CO margin range seems to be from -30 to 30
    // Margin arg seems to be 16 bits (lowest 16 bits of the command arg)
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
            return smu.Mp1Smu.SMU_MSG_SetDldoPsmMargin > 0 || smu.Rsmu.SMU_MSG_SetDldoPsmMargin > 0;
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
