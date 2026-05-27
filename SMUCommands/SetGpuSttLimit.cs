namespace ZenStates.Core.SMUCommands
{
    internal class SetGpuSttLimit : BaseSMUCommand
    {
        public SetGpuSttLimit(SMU smu) : base(smu) { }
        public CmdResult Execute(uint cmdRsmu, uint cmdMp1 = 0, uint arg = 0U)
        {
            if (CanExecute())
            {
                var limit = arg * 256;
                result.args[0] = limit;

                SMU.Status status = smu.SendRsmuCommand(cmdRsmu, ref result.args);

                if (status != SMU.Status.OK)
                {
                    // Reset arg for MP1 command if RSMU command fails
                    result.args[0] = limit;
                    status = smu.SendMp1Command(cmdMp1, ref result.args);
                }

                result.status = status;
            }

            return base.Execute();
        }
    }
}
