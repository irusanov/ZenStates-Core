namespace ZenStates.Core.SMUCommands
{
    internal class GetBoostLimitFrequency : BaseSMUCommand
    {
        public GetBoostLimitFrequency(SMU smu) : base(smu) { }
        public override CmdResult Execute()
        {
            if (CanExecute())
                result.status = smu.SendRsmuCommand(smu.Rsmu.SMU_MSG_GetBoostLimitFrequency, ref result.args);

            return base.Execute();
        }
    }
}
