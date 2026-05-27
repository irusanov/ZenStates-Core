namespace ZenStates.Core.SMUSettings
{
    public class APUSettings0_Picasso : APUSettings0
    {
        public APUSettings0_Picasso()
        {
            // DPTC interface
            Rsmu.SMU_MSG_SetApuSlowLimit = 0x75;

            // Overclock Options
            Rsmu.SMU_MSG_EnableOcMode = 0x78;
            Rsmu.SMU_MSG_SetOverclockFrequencyAllCores = 0x79;
            Rsmu.SMU_MSG_SetOverclockFrequencyPerCore = 0x7A;
            Rsmu.SMU_MSG_SetOverclockCpuVid = 0x7B;
            Rsmu.SMU_MSG_SetPBOScalar = 0x7C;
            Rsmu.SMU_MSG_IsOverclockable = 0x87;
            Rsmu.SMU_MSG_GetBoostLimitFrequency = 0x86;

            // Subsystem frequencies
            Rsmu.SMU_MSG_SetMaxCpuFreq = 0x66;
            Rsmu.SMU_MSG_SetMinCpuFreq = 0x67;
            Rsmu.SMU_MSG_SetMaxGfxClkFreq = 0x68;
            Rsmu.SMU_MSG_SetMinGfxClkFreq = 0x69;
            Rsmu.SMU_MSG_SetMaxSocClkFreq = 0x6A;
            Rsmu.SMU_MSG_SetMinSocClkFreq = 0x6B;
            Rsmu.SMU_MSG_SetMaxFclkFreq = 0x6C;
            Rsmu.SMU_MSG_SetMinFclkFreq = 0x6D;
            Rsmu.SMU_MSG_SetMaxVcn = 0x6E;
            Rsmu.SMU_MSG_SetMinVcn = 0x6F;
            Rsmu.SMU_MSG_SetMaxLclk = 0x70;
            Rsmu.SMU_MSG_SetMinLclk = 0x71;

            // Debug
            Rsmu.SMU_MSG_GetPboFusedPowerLimit = 0x7F;
            Rsmu.SMU_MSG_GetPboFusedSlowLimit = 0x80;
            Rsmu.SMU_MSG_GetPboFusedFastLimit = 0x81;
            Rsmu.SMU_MSG_GetPboFusedApuSlowLimit = 0x82;
            Rsmu.SMU_MSG_GetPboFusedVrmVddTdcCurrent = 0x83;
            Rsmu.SMU_MSG_GetPboFusedVrmSocTdcCurrent = 0x84;

            // MP1
            // DPTC interface
            Mp1Smu.SMU_MSG_SetApuSlowLimit = 0x54;

            // Overclock Options
            Mp1Smu.SMU_MSG_EnableOcMode = 0x58;
            Mp1Smu.SMU_MSG_SetOverclockFrequencyAllCores = 0x59;
            Mp1Smu.SMU_MSG_SetOverclockFrequencyPerCore = 0x5A;
            Mp1Smu.SMU_MSG_SetOverclockCpuVid = 0x5B;
            Mp1Smu.SMU_MSG_SetPBOScalar = 0x57;

            // Subsystem frequencies
            Mp1Smu.SMU_MSG_SetMaxCpuFreq = 0x44;
            Mp1Smu.SMU_MSG_SetMinCpuFreq = 0x45;
            Mp1Smu.SMU_MSG_SetMaxGfxClkFreq = 0x46;
            Mp1Smu.SMU_MSG_SetMinGfxClkFreq = 0x47;
            Mp1Smu.SMU_MSG_SetMaxSocClkFreq = 0x48;
            Mp1Smu.SMU_MSG_SetMinSocClkFreq = 0x49;
            Mp1Smu.SMU_MSG_SetMaxFclkFreq = 0x4A;
            Mp1Smu.SMU_MSG_SetMinFclkFreq = 0x4B;
            Mp1Smu.SMU_MSG_SetMaxVcn = 0x4C;
            Mp1Smu.SMU_MSG_SetMinVcn = 0x4D;
            Mp1Smu.SMU_MSG_SetMaxLclk = 0x4E;
            Mp1Smu.SMU_MSG_SetMinLclk = 0x4F;
        }
    }
}
