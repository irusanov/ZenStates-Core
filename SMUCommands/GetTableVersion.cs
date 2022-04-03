namespace ZenStates.Core.SMUCommands
{
    internal class GetTableVersion : BaseSMUCommand
    {
        public GetTableVersion(SMU smu) : base(smu) { }
        public override CmdResult Execute()
        {
            if (CanExecute())
                result.status = smu.SendRsmuCommand(smu.Rsmu.SMU_MSG_GetTableVersion, ref result.args);

            return base.Execute();
        }
    }
}
