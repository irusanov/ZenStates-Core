namespace ZenStates.Core.SMUCommands
{
    internal class GetCorePerformanceData : BaseSMUCommand
    {
        public GetCorePerformanceData(SMU smu) : base(smu) { }
        public CmdResult Execute(uint coreIndex)
        {
            if (CanExecute())
            {
                result.args[0] = coreIndex;
                result.status = smu.SendRsmuCommand(smu.Rsmu.SMU_MSG_GetPerformanceData, ref result.args);
            }

            return base.Execute();
        }
    }
}
