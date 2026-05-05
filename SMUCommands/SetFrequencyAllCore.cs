namespace ZenStates.Core.SMUCommands
{
    internal class SetFrequencyAllCore : BaseSMUCommand
    {
        public SetFrequencyAllCore(SMU smu) : base(smu) { }
        public CmdResult Execute(uint frequency)
        {
            if (CanExecute())
            {
                bool olderSmu = smu.SMU_TYPE == SMU.SmuType.TYPE_APU0 || smu.SMU_TYPE == SMU.SmuType.TYPE_CPU0;
                
                // TODO: Add Manual OC mode
                // TODO: Add lo and hi frequency limits
                result.args[0] = frequency & 0xfffff;
                
                var status = smu.Rsmu.SMU_MSG_SetOverclockFrequencyAllCores > 0 ? 
                    smu.SendRsmuCommand(smu.Rsmu.SMU_MSG_SetOverclockFrequencyAllCores, ref result.args) 
                    : SMU.Status.UNKNOWN_CMD;
                
                if (smu.Mp1Smu.SMU_MSG_SetOverclockFrequencyAllCores > 0 && 
                    (status != SMU.Status.OK || olderSmu)) // Apply BOTH commands on older hardware
                {
                    status = smu.SendMp1Command(smu.Mp1Smu.SMU_MSG_SetOverclockFrequencyAllCores, ref result.args);
                }
                result.status = status;
            }

            return base.Execute();
        }
    }
}
