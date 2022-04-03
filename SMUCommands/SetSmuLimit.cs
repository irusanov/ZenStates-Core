namespace ZenStates.Core.SMUCommands
{
    internal class SetSmuLimit : BaseSMUCommand
    {
        public SetSmuLimit(SMU smu) : base(smu) { }
        public CmdResult Execute(uint cmd, uint arg = 0U)
        {
            if (CanExecute())
            {
                result.args[0] = arg * 1000;
                result.status = smu.SendRsmuCommand(cmd, ref result.args);
            }

            return base.Execute();
        }
    }
}
