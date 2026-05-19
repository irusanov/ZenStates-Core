namespace ZenStates.Core.SMUCommands
{
    internal class SetPowerSavingMode : BaseSMUCommand
    {
        public SetPowerSavingMode(SMU smu) : base(smu) { }

        public CmdResult Execute(bool maxPerformance)
        {
            if (CanExecute())
            {
                uint cmd = maxPerformance ? smu.Mp1Smu.SMU_MSG_SetMaxPerformance : smu.Mp1Smu.SMU_MSG_SetPowerSaving;

                SMU.Status status = SMU.Status.UNKNOWN_CMD;
                if (cmd != 0)
                    status = smu.SendMp1Command(cmd, ref result.args);
                
                result.status = status;
            }

            return base.Execute();
        }
    }
}
