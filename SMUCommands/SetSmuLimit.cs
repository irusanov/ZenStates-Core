namespace ZenStates.Core.SMUCommands
{
    internal class SetSmuLimit : BaseSMUCommand
    {
        public SetSmuLimit(SMU smu) : base(smu) { }
        public CmdResult Execute(uint cmd, uint fallbackCmd = 0, uint arg = 0U)
        {
            if (CanExecute())
            {
                result.args[0] = arg * 1000;
                var status = smu.SendRsmuCommand(cmd, ref result.args);
                if (status != SMU.Status.OK)
                {
                    status = smu.SendMp1Command(fallbackCmd, ref result.args);
                }
                result.status = status;
            }

            return base.Execute();
        }
    }
}
