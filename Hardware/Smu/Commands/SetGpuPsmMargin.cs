namespace ZenStates.Core.Hardware.Smu.Commands
{
    // Set DLDO Psm margin for all cores
    // CO margin range from -30 to 30 before Zen 4, and from -50 to 50 on Zen 4 and newer
    // Margin arg 16 bits (lowest 16 bits of the command arg)
    // [15-0] CO margin
    internal class SetGpuPsmMargin : BaseSMUCommand
    {
        public SetGpuPsmMargin(SMU smu) : base(smu) { }
        public CmdResult Execute(int margin)
        {
            if (CanExecute())
            {
                if (smu.Rsmu.SMU_MSG_SetGpuPsmMargin > 0)
                {
                    result.args[0] = Utils.MakePsmMarginArg(margin);
                    result.status = smu.SendRsmuCommand(smu.Rsmu.SMU_MSG_SetGpuPsmMargin, ref result.args);
                }
                else if (smu.SMU_TYPE == SMU.SmuType.TYPE_CPU9)
                {
                    result.args[0] = Utils.CurveOptimizerToGfxArg(margin);
                    result.status = smu.SendMp1Command(smu.Mp1Smu.SMU_MSG_SetGpuPsmMargin, ref result.args);
                    if (result.status != SMU.Status.OK)
                    {
                        result.args[0] = Utils.CurveOptimizerToGfxArg(margin);
                        result.status = smu.SendMp1Command(smu.Mp1Smu.SMU_MSG_SetGpuPsmMarginAlt, ref result.args);
                    }
                }
            }

            return base.Execute();
        }
    }
}
