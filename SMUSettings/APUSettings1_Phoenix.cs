namespace ZenStates.Core.SMUSettings
{
    public class APUSettings1_Phoenix : APUSettings1
    {
        public APUSettings1_Phoenix()
        {
            SMU_TYPE = SmuType.TYPE_APU2;

            // DPTC interface
            Rsmu.SMU_MSG_SetPsi0SocCurrent = 0x0; // No PSI option
            Rsmu.SMU_MSG_SetProchotDeassertionRamp = 0x3D;

            // Overclock Options
            Rsmu.SMU_MSG_SetPBOScalar = 0x3E;

            // Curve Optimizer
            Rsmu.SMU_MSG_SetDldoPsmMargin = 0x53;
            Rsmu.SMU_MSG_SetAllDldoPsmMargin = 0x5D;
            Rsmu.SMU_MSG_GetDldoPsmMargin = 0xE1;
            Rsmu.SMU_MSG_SetGpuPsmMargin = 0x1F;
            Rsmu.SMU_MSG_GetGpuPsmMargin = 0x20;

            // Debug
            Rsmu.SMU_MSG_GetPboFusedPowerLimit = 0x11;
            Rsmu.SMU_MSG_GetPboFusedSlowLimit = 0x12;
            Rsmu.SMU_MSG_GetPboFusedFastLimit = 0x13;
            Rsmu.SMU_MSG_GetPboFusedApuSlowLimit = 0x14;
            Rsmu.SMU_MSG_GetPboFusedVrmVddTdcCurrent = 0x15;
            Rsmu.SMU_MSG_GetPboFusedVrmSocTdcCurrent = 0x16;
            Rsmu.SMU_MSG_GetEXPOProfileActive = 0xDB;
            Rsmu.SMU_MSG_GetPerformanceData = 0xB;
            Rsmu.SMU_MSG_GetLN2Mode = 0xC4;
            Rsmu.SMU_MSG_SetBoostLimitFrequencyAllCores = 0x47;
            Rsmu.SMU_MSG_SetBoostLimitFrequencyGpu = 0xCA;

            // MP1
            Mp1Smu.SMU_ADDR_MSG = 0x03B10528;
            Mp1Smu.SMU_ADDR_RSP = 0x03B10578;
            Mp1Smu.SMU_ADDR_ARG = 0x03B10998;

            // DPTC interface
            Mp1Smu.SMU_MSG_SetPsi0SocCurrent = 0x0; // No PSI option
            Mp1Smu.SMU_MSG_SetProchotDeassertionRamp = 0x1F;
            Mp1Smu.SMU_MSG_SetApuSlowLimit = 0x23;
            Mp1Smu.SMU_MSG_SetSkinTempPowerLimit = 0x4A;
            Mp1Smu.SMU_MSG_SetApuSkinTempLimit = 0x33;
            Mp1Smu.SMU_MSG_SetDgpuSkinTempLimit = 0x34;

            // Overclock Options
            Mp1Smu.SMU_MSG_EnableOcMode = 0x57;
            Mp1Smu.SMU_MSG_DisableOcMode = 0x58;
            Mp1Smu.SMU_MSG_SetOverclockFrequencyAllCores = 0x59;
            Mp1Smu.SMU_MSG_SetOverclockFrequencyPerCore = 0x5A;
            Mp1Smu.SMU_MSG_SetOverclockCpuVid = 0x5B;
            Mp1Smu.SMU_MSG_SetPBOScalar = 0x63;
            Mp1Smu.SMU_MSG_SetGfxclkOverdriveByFreqVid = 0x5C;

            // Curve Optimizer
            Mp1Smu.SMU_MSG_SetDldoPsmMargin = 0x4B;
            Mp1Smu.SMU_MSG_SetAllDldoPsmMargin = 0x4C;

            // Debug
            Mp1Smu.SMU_MSG_GetSustainedPowerAndThmLimit = 0x5F;
            Mp1Smu.SMU_MSG_GetCpuName = 0x4;
        }
    }
}
