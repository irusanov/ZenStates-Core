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
                var status = smu.SendRsmuCommand(smu.Rsmu.SMU_MSG_SetPBOScalar, ref result.args);
                if (status != SMU.Status.OK)
                {
                    status = smu.SendMp1Command(smu.Mp1Smu.SMU_MSG_SetPBOScalar, ref result.args);
                }
                result.status = status;
            }

            return base.Execute();
        }
    }
}
