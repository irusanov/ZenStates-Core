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

                var status = smu.SendRsmuCommand(smu.Rsmu.SMU_MSG_SetOverclockFrequencyAllCores, ref result.args);

                if (smu.Mp1Smu.SMU_MSG_SetOverclockFrequencyAllCores > 0 &&
                    (status != SMU.Status.OK || olderSmu)) // Apply BOTH commands on older hardware
                {
                    // Re-set args as result gets overwritten by SendRsmuCommand
                    result.args = Utils.MakeCmdArgs(frequency & 0xfffff);
                    status = smu.SendMp1Command(smu.Mp1Smu.SMU_MSG_SetOverclockFrequencyAllCores, ref result.args);
                }
                result.status = status;
            }

            return base.Execute();
        }
    }
}
