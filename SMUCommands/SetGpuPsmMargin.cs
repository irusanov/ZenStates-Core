namespace ZenStates.Core.SMUCommands
{
    // Set DLDO Psm margin for all cores
    // CO margin range from -30 to 30 before Zen 4, and from -50 to 50 on Zen 4 and newer
    // Margin arg 16 bits (lowest 16 bits of the command arg)
    // [15-0] CO margin
    internal class SetGpuPsmMargin : BaseSMUCommand
    {
        public SetGpuPsmMargin(SMU smu) : base(smu) { }

        public override bool CanExecute()
        {
            return smu.Rsmu.SMU_MSG_SetGpuPsmMargin > 0;
        }

        public CmdResult Execute(int margin)
        {
            if (CanExecute())
            {
                result.args[0] = Utils.MakePsmMarginArg(margin);
                result.status = smu.SendRsmuCommand(smu.Rsmu.SMU_MSG_SetGpuPsmMargin, ref result.args);
            }

            return base.Execute();
        }
    }
}
