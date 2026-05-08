namespace ZenStates.Core.SMUCommands
{
    internal class SetGpuSttLimit : BaseSMUCommand
    {
        public SetGpuSttLimit(SMU smu) : base(smu) { }
        public CmdResult Execute(uint cmdRsmu, uint cmdMp1 = 0, uint arg = 0U)
        {
            if (CanExecute()) 
            {
                result.args[0] = arg * 256;
                
                
                SMU.Status status = smu.SendRsmuCommand(cmdRsmu, ref result.args);
                if (status != SMU.Status.OK)
                {
                    status = smu.SendMp1Command(cmdMp1, ref result.args);
                }
                
                result.status = status;
            }

            return base.Execute();
        }
    }
}
