namespace ZenStates.Core.SMUCommands
{
    internal class SetOverclockCpuVid : BaseSMUCommand
    {
        public SetOverclockCpuVid(SMU smuInstance) : base(smuInstance) { }

        public CmdResult Execute(uint vid)
        {
            if (CanExecute())
            {
                bool olderSmu = smu.SMU_TYPE == SMU.SmuType.TYPE_APU0 || smu.SMU_TYPE == SMU.SmuType.TYPE_CPU0;

                result.args[0] = vid;

                // This will return Status.UNKNOWN_CMD if command is undefined (0) or mailbox is invalid
                var status = smu.SendRsmuCommand(smu.Rsmu.SMU_MSG_SetOverclockCpuVid, ref result.args);

                if (smu.Mp1Smu.SMU_MSG_SetOverclockCpuVid > 0 &&
                    (status != SMU.Status.OK || olderSmu)) // Apply BOTH commands on older hardware
                {
                    // Re-assign the arguments as it may have been reset to 0 if the first command failed
                    result.args = Utils.MakeCmdArgs(vid);
                    status = smu.SendMp1Command(smu.Mp1Smu.SMU_MSG_SetOverclockCpuVid, ref result.args);
                }

                result.status = status;
            }
            return base.Execute();
        }
    }
}
