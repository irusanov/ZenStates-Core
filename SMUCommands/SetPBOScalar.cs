namespace ZenStates.Core.SMUCommands
{
    internal class SetPBOScalar : BaseSMUCommand
    {
        public SetPBOScalar(SMU smu) : base(smu) { }

        public CmdResult Execute(uint arg = 1)
        {
            if (CanExecute())
            {
                if (smu.Mp1Smu.SMU_MSG_SetPBO_EN > 0) // Allow PBO overdrive on Zen 2 / Zen 3
                    smu.SendMp1Command(smu.Mp1Smu.SMU_MSG_SetPBO_EN, ref result.args);

                var limit = arg * 100;

                result.args = Utils.MakeCmdArgs(limit);

                var status = smu.SendRsmuCommand(smu.Rsmu.SMU_MSG_SetPBOScalar, ref result.args);
                if (status != SMU.Status.OK)
                {
                    result.args = Utils.MakeCmdArgs(limit);
                    status = smu.SendMp1Command(smu.Mp1Smu.SMU_MSG_SetPBOScalar, ref result.args);
                }

                result.status = status;
            }

            return base.Execute();
        }
    }
}
