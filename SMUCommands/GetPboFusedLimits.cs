namespace ZenStates.Core.SMUCommands
{
    internal class GetPboFusedLimits : BaseSMUCommand
    {
        public GetPboFusedLimits(SMU smu) : base(smu) { }

        public Cpu.PboFusedLimits Limits { get; private set; }
        public override CmdResult Execute()
        {
            if (CanExecute())
            {
                bool olderSmu = smu.SMU_TYPE == SMU.SmuType.TYPE_APU0;

                Cpu.PboFusedLimits limits = new Cpu.PboFusedLimits();

                if (smu.Rsmu.SMU_MSG_GetPboFusedPowerLimit > 0)
                {
                    result.status = smu.SendRsmuCommand(smu.Rsmu.SMU_MSG_GetPboFusedPowerLimit, ref result.args);
                    limits.PowerLimit = (int)result.args[0];
                }
                if (smu.Rsmu.SMU_MSG_GetPboFusedSlowLimit > 0)
                {
                    ResetArgs();
                    result.status = smu.SendRsmuCommand(smu.Rsmu.SMU_MSG_GetPboFusedSlowLimit, ref result.args);
                    limits.SlowLimit = (int)result.args[0];
                }
                if (smu.Rsmu.SMU_MSG_GetPboFusedFastLimit > 0)
                {
                    ResetArgs();
                    result.status = smu.SendRsmuCommand(smu.Rsmu.SMU_MSG_GetPboFusedFastLimit, ref result.args);
                    limits.FastLimit = (int)result.args[0];
                }
                if (smu.Rsmu.SMU_MSG_GetPboFusedApuSlowLimit > 0)
                {
                    ResetArgs();
                    result.status = smu.SendRsmuCommand(smu.Rsmu.SMU_MSG_GetPboFusedApuSlowLimit, ref result.args);
                    limits.ApuSlowLimit = (int)result.args[0];
                }
                if (smu.Rsmu.SMU_MSG_GetPboFusedVrmVddTdcCurrent > 0)
                {
                    ResetArgs();

                    if (olderSmu)
                        result.args[0] = 4;

                    result.status = smu.SendRsmuCommand(smu.Rsmu.SMU_MSG_GetPboFusedVrmVddTdcCurrent, ref result.args);
                    limits.VrmVddTdcCurrent = (int)result.args[0];
                }
                if (smu.Rsmu.SMU_MSG_GetPboFusedVrmSocTdcCurrent > 0)
                {
                    ResetArgs();

                    if (olderSmu)
                        result.args[0] = 6;

                    result.status = smu.SendRsmuCommand(smu.Rsmu.SMU_MSG_GetPboFusedVrmSocTdcCurrent, ref result.args);
                    limits.VrmSocTdcCurrent = (int)result.args[0];
                }

                Limits = limits;
            }

            return base.Execute();
        }
    }
}
