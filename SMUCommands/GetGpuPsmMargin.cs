namespace ZenStates.Core.SMUCommands
{
    internal class GetGpuPsmMargin : BaseSMUCommand
    {
        public GetGpuPsmMargin(SMU smu) : base(smu) { }
        public CmdResult Execute(uint args = 0)
        {
            if (CanExecute())
            {
                result.args[0] = args;
                // Will return Status.UNKNOWN_CMD if the command is not defined
                result.status = smu.SendRsmuCommand(smu.Rsmu.SMU_MSG_GetGpuPsmMargin, ref result.args);
            }

            return base.Execute();
        }
    }
}
