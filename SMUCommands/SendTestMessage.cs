namespace ZenStates.Core.SMUCommands
{
    internal class SendTestMessage : BaseSMUCommand
    {
        public SendTestMessage(SMU smu) : base(smu) { }
        public override CmdResult Execute()
        {
            if (CanExecute())
                result.status = smu.SendRsmuCommand(smu.Rsmu.SMU_MSG_TestMessage, ref result.args);

            return base.Execute();
        }
    }
}
