namespace ZenStates.Core.Hardware.Smu.Commands
{
    internal class GetSystemConfiguredPowerLimit : BaseSMUCommand
    {
        public GetSystemConfiguredPowerLimit(SMU smu) : base(smu) { }

        public Cpu.SystemPowerLimit Limits { get; private set; }
        public override CmdResult Execute()
        {
            if (CanExecute())
            {
                Cpu.SystemPowerLimit limits = new Cpu.SystemPowerLimit();

                if (smu.Rsmu.SMU_MSG_GetSustainedPowerAndThmLimit > 0)
                {
                    result.status = smu.SendRsmuCommand(smu.Rsmu.SMU_MSG_GetSustainedPowerAndThmLimit, ref result.args);
                    limits.PowerLimit = (int)((result.args[0] & 0x00FF0000) >> 16);
                    limits.TemperatureLimit = (int)(result.args[0] & 0xFF);
                }
                else if (smu.Mp1Smu.SMU_MSG_GetSustainedPowerAndThmLimit > 0)
                {
                    result.status = smu.SendMp1Command(smu.Mp1Smu.SMU_MSG_GetSustainedPowerAndThmLimit, ref result.args);
                    limits.PowerLimit = (int)((result.args[0] & 0x00FF0000) >> 16);
                    limits.TemperatureLimit = (int)(result.args[0] & 0xFF);
                }
                else if (smu.GpuMb.SMU_MSG_GetSustainedPowerAndThmLimit > 0 && smu.SMU_TYPE == SMU.SmuType.TYPE_CPU9)
                {
                    result.status = smu.SendGpuMbCommand(smu.GpuMb.SMU_MSG_GetSustainedPowerAndThmLimit, ref result.args);
                    limits.PowerLimit = (int)result.args[0];
                    limits.TemperatureLimit = 95; // Hardcoded in Smu fw
                }

                Limits = limits;
            }

            return base.Execute();
        }
    }
}
