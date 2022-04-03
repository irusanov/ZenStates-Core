namespace ZenStates.Core.SMUCommands
{
    // Set DLDO Psm margin for all cores
    // CO margin range seems to be from -30 to 30
    // Margin arg seems to be 16 bits (lowest 16 bits of the command arg)
    // [15-0] CO margin
    internal class SetPsmMarginAllCores : BaseSMUCommand
    {
        public SetPsmMarginAllCores(SMU smu) : base(smu) { }
        public CmdResult Execute(int margin)
        {
            if (CanExecute())
            {
                result.args[0] = Utils.MakePsmMarginArg(margin);
                result.status = smu.SendMp1Command(smu.Mp1Smu.SMU_MSG_SetAllDldoPsmMargin, ref result.args);
            }

            return base.Execute();
        }
    }
}
