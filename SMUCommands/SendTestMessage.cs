namespace ZenStates.Core.SMUCommands
{
    internal class SendTestMessage : BaseSMUCommand
    {
        public bool IsSumCorrect = false;
        public SendTestMessage(SMU smu) : base(smu) { }
        public CmdResult Execute(uint testArg = 1)
        {
            if (CanExecute())
            {
                result.args[0] = testArg;
                result.status = smu.SendRsmuCommand(smu.Rsmu.SMU_MSG_TestMessage, ref result.args);
                this.IsSumCorrect = result.args[0] == testArg + 1;
            }

            return base.Execute();
        }
    }
}
