namespace ZenStates.Core.SMUCommands
{
    internal class SendTestMessage : BaseSMUCommand
    {
        private readonly Mailbox mbox;

        public bool IsSumCorrect = false;
        public SendTestMessage(SMU smu, Mailbox mbox = null) : base(smu)
        {
            this.mbox = mbox ?? smu.Rsmu;
        }
        public CmdResult Execute(uint testArg = 1)
        {
            if (CanExecute())
            {
                result.args[0] = testArg;
                result.status = smu.SendSmuCommand(mbox, mbox.SMU_MSG_TestMessage, ref result.args);
                this.IsSumCorrect = result.args[0] == testArg + 1;
            }

            return base.Execute();
        }
    }
}
