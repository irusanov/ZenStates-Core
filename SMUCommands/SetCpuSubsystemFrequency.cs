namespace ZenStates.Core.SMUCommands
{
    internal class SetCpuSubsystemFrequency : BaseSMUCommand
    {
        public SetCpuSubsystemFrequency(SMU smu) : base(smu) { }

        public CmdResult Execute(Cpu.CpuSubsystem subsystem, uint freq, bool maxFreq = true)
        {
            if (CanExecute())
            {
                uint cmdMp1 = 0;
                uint cmdRsmu = 0;
                uint cmdGpuMb = 0;

                switch (subsystem)
                {
                    case Cpu.CpuSubsystem.Cpu:
                        cmdMp1 = maxFreq ? smu.Mp1Smu.SMU_MSG_SetMaxCpuFreq : smu.Mp1Smu.SMU_MSG_SetMinCpuFreq;
                        cmdRsmu = maxFreq ? smu.Rsmu.SMU_MSG_SetMaxCpuFreq : smu.Rsmu.SMU_MSG_SetMinCpuFreq;
                        break;
                    case Cpu.CpuSubsystem.Gpu:
                        cmdMp1 = maxFreq ? smu.Mp1Smu.SMU_MSG_SetMaxGfxClkFreq : smu.Mp1Smu.SMU_MSG_SetMinGfxClkFreq;
                        cmdRsmu = maxFreq ? smu.Rsmu.SMU_MSG_SetMaxGfxClkFreq : smu.Rsmu.SMU_MSG_SetMinGfxClkFreq;
                        cmdGpuMb = maxFreq ? smu.GpuMb.SMU_MSG_SetMaxGfxClkFreq : smu.GpuMb.SMU_MSG_SetMinGfxClkFreq;
                        break;
                    case Cpu.CpuSubsystem.Soc:
                        cmdMp1 = maxFreq ? smu.Mp1Smu.SMU_MSG_SetMaxSocClkFreq : smu.Mp1Smu.SMU_MSG_SetMinSocClkFreq;
                        cmdRsmu = maxFreq ? smu.Rsmu.SMU_MSG_SetMaxSocClkFreq : smu.Rsmu.SMU_MSG_SetMinSocClkFreq;
                        cmdGpuMb = maxFreq ? smu.GpuMb.SMU_MSG_SetMaxSocClkFreq : smu.GpuMb.SMU_MSG_SetMinSocClkFreq;
                        break;
                    case Cpu.CpuSubsystem.Fclk:
                        cmdMp1 = maxFreq ? smu.Mp1Smu.SMU_MSG_SetMaxFclkFreq : smu.Mp1Smu.SMU_MSG_SetMinFclkFreq;
                        cmdRsmu = maxFreq ? smu.Rsmu.SMU_MSG_SetMaxFclkFreq : smu.Rsmu.SMU_MSG_SetMinFclkFreq;
                        cmdGpuMb = maxFreq ? smu.GpuMb.SMU_MSG_SetMaxFclkFreq : smu.GpuMb.SMU_MSG_SetMinFclkFreq;
                        break;
                    case Cpu.CpuSubsystem.Vcn:
                        cmdMp1 = maxFreq ? smu.Mp1Smu.SMU_MSG_SetMaxVcn : smu.Mp1Smu.SMU_MSG_SetMinVcn;
                        cmdRsmu = maxFreq ? smu.Rsmu.SMU_MSG_SetMaxVcn : smu.Rsmu.SMU_MSG_SetMinVcn;
                        cmdGpuMb = maxFreq ? smu.GpuMb.SMU_MSG_SetMaxVcn : smu.GpuMb.SMU_MSG_SetMinVcn;
                        break;
                    case Cpu.CpuSubsystem.Lclk:
                        cmdMp1 = maxFreq ? smu.Mp1Smu.SMU_MSG_SetMaxLclk : smu.Mp1Smu.SMU_MSG_SetMinLclk;
                        cmdRsmu = maxFreq ? smu.Rsmu.SMU_MSG_SetMaxLclk : smu.Rsmu.SMU_MSG_SetMinLclk;
                        cmdGpuMb = maxFreq ? smu.GpuMb.SMU_MSG_SetMaxLclk : smu.GpuMb.SMU_MSG_SetMinLclk;
                        break;
                }

                result.args[0] = freq;

                SMU.Status status = SMU.Status.UNKNOWN_CMD;
                if (cmdMp1 != 0)
                    status = smu.SendMp1Command(cmdMp1, ref result.args);
                else if (cmdRsmu != 0)
                    status = smu.SendRsmuCommand(cmdRsmu, ref result.args);
                else if (cmdGpuMb != 0)
                    status = smu.SendGpuMbCommand(cmdGpuMb, ref result.args);

                result.status = status;
            }

            return base.Execute();
        }
    }
}
