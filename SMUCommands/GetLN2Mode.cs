namespace ZenStates.Core.SMUCommands
{
    internal class GetLN2Mode : BaseSMUCommand
    {
        public GetLN2Mode(SMU smu) : base(smu) { }
        public override CmdResult Execute()
        {
            if (CanExecute())
                result.status = smu.SendRsmuCommand(smu.Rsmu.SMU_MSG_GetLN2Mode, ref result.args);

            return base.Execute();
        }
    }
}
