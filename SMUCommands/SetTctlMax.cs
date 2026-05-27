namespace ZenStates.Core.SMUCommands
{
    internal class SetTctlMax : BaseSMUCommand
    {
        public SetTctlMax(SMU smu) : base(smu) { }
        public CmdResult Execute(uint arg = 0U)
        {
            if (CanExecute())
            {
                var limit = smu.SMU_TYPE == SMU.SmuType.TYPE_CPU9 ?
                    arg * 1000 : arg; // Value * 1000 on Bristol

                result.args[0] = limit;

                // Fix for some mobile APUs, firstly apply MP1 variant
                var olderSmu = smu.SMU_TYPE == SMU.SmuType.TYPE_APU0 || smu.SMU_TYPE == SMU.SmuType.TYPE_APU1;

                SMU.Status status;
                uint cmdMp1 = smu.Mp1Smu.SMU_MSG_SetTctlMax;
                uint cmdRsmu = smu.Rsmu.SMU_MSG_SetTctlMax;

                if (olderSmu)
                {
                    status = cmdMp1 != 0 ? smu.SendMp1Command(cmdMp1, ref result.args)
                        : smu.SendRsmuCommand(cmdRsmu, ref result.args);
                }
                else
                {
                    status = smu.SendRsmuCommand(cmdRsmu, ref result.args);
                    if (status != SMU.Status.OK)
                    {
                        result.args = Utils.MakeCmdArgs(limit);
                        status = smu.SendMp1Command(cmdMp1, ref result.args);
                    }
                }

                result.status = status;
            }

            return base.Execute();
        }
    }
}
