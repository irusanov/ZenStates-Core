namespace ZenStates.Core.SMUCommands
{
    internal class SetBtcMode : BaseSMUCommand
    {
        public SetBtcMode(SMU smu) : base(smu)
        {
        }

        public CmdResult Execute(bool enabled, uint arg = 0U)
        {
            if (CanExecute())
            {
                result.args[0] = arg;

                SMU.Status status = SMU.Status.UNKNOWN_CMD;

                if (enabled && smu.Mp1Smu.SMU_MSG_AcBtcStartCal != 0)
                {
                    if (result.args[0] > 4)
                        result.args[0] = 4;

                    status = smu.SendMp1Command(smu.Mp1Smu.SMU_MSG_AcBtcStartCal, ref result.args);
                }

                if (!enabled && smu.Mp1Smu.SMU_MSG_AcBtcStopCal != 0 && smu.Mp1Smu.SMU_MSG_AcBtcEndCal != 0)
                {
                    smu.SendMp1Command(smu.Mp1Smu.SMU_MSG_AcBtcStopCal, ref result.args);
                    // Reset args
                    result.args = Utils.MakeCmdArgs(arg);
                    status = smu.SendMp1Command(smu.Mp1Smu.SMU_MSG_AcBtcEndCal, ref result.args);
                }

                result.status = status;
            }

            return base.Execute();
        }
    }
}