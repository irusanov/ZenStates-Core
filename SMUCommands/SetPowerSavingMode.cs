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
                // This will return Status.UNKNOWN_CMD if command is not defined
                result.status = smu.SendMp1Command(cmd, ref result.args);
            }

            return base.Execute();
        }
    }
}
