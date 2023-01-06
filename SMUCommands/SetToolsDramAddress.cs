namespace ZenStates.Core.SMUCommands
{
    internal class SetToolsDramAddress : BaseSMUCommand
    {
        public SetToolsDramAddress(SMU smu) : base(smu) { }
        public CmdResult Execute(uint arg = 0U)
        {
            if (CanExecute())
            {
                result.args[0] = arg;
                result.status = smu.SendMp1Command(smu.Mp1Smu.SMU_MSG_SetToolsDramAddress, ref result.args);
            }

            return base.Execute();
        }
    }
}
