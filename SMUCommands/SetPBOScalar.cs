namespace ZenStates.Core.SMUCommands
{
    internal class SetPBOScalar : BaseSMUCommand
    {
        public SetPBOScalar(SMU smu) : base(smu) { }

        public CmdResult Execute(uint arg = 1)
        {
            if (CanExecute())
            {
                result.args[0] = arg * 100;
                result.status = smu.SendRsmuCommand(smu.Rsmu.SMU_MSG_SetPBOScalar, ref result.args);
            }

            return base.Execute();
        }
    }
}
