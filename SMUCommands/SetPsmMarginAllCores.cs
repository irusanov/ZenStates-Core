namespace ZenStates.Core.SMUCommands
{
    // Set DLDO Psm margin for all cores
    // CO margin range from -30 to 30 before Zen 4, and from -50 to 50 on Zen 4 and newer
    // Margin arg 16 bits (lowest 16 bits of the command arg)
    // [15-0] CO margin
    internal class SetPsmMarginAllCores : BaseSMUCommand
    {
        public SetPsmMarginAllCores(SMU smu) : base(smu) { }

        public override bool CanExecute()
        {
            return base.CanExecute() && (smu.Mp1Smu.SMU_MSG_SetAllDldoPsmMargin > 0 || smu.Rsmu.SMU_MSG_SetAllDldoPsmMargin > 0);
        }

        public CmdResult Execute(int margin)
        {
            var olderSmu = smu.SMU_TYPE == SMU.SmuType.TYPE_CPU0;
            var negativeMargin = margin < 0;

            if (CanExecute() && !(olderSmu && negativeMargin))
            {
                result.args[0] = Utils.MakePsmMarginArg(margin);
                result.status = smu.Mp1Smu.SMU_MSG_SetAllDldoPsmMargin > 0
                    ? smu.SendMp1Command(smu.Mp1Smu.SMU_MSG_SetAllDldoPsmMargin, ref result.args)
                    : smu.SendRsmuCommand(smu.Rsmu.SMU_MSG_SetAllDldoPsmMargin, ref result.args);
            }
            else
            {
                if (smu.Rsmu.SMU_MSG_SetOverclockCpuVid > 0)
                {
                    result.args[0] = Utils.CurveOptimizerToVid(margin);
                    result.status = smu.SendRsmuCommand(smu.Rsmu.SMU_MSG_SetOverclockCpuVid, ref result.args);
                }
            }

            return base.Execute();
        }
    }
}
