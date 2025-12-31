namespace ZenStates.Core
{
    public sealed class MP1Mailbox : Mailbox
    {
        // Configurable commands
        public uint SMU_MSG_GetBiosIfVersion { get; set; } = 0x3;
        public uint SMU_MSG_SetToolsDramAddress { get; set; } = 0x0;
        public uint SMU_MSG_EnableOcMode { get; set; } = 0x0;
        public uint SMU_MSG_DisableOcMode { get; set; } = 0x0;
        public uint SMU_MSG_SetOverclockFrequencyAllCores { get; set; } = 0x0;
        public uint SMU_MSG_SetOverclockFrequencyPerCore { get; set; } = 0x0;
        public uint SMU_MSG_SetBoostLimitFrequencyAllCores { get; set; } = 0x0;
        public uint SMU_MSG_SetBoostLimitFrequency { get; set; } = 0x0;
        public uint SMU_MSG_SetOverclockCpuVid { get; set; } = 0x0;
        public uint SMU_MSG_SetDldoPsmMargin { get; set; } = 0x0;
        public uint SMU_MSG_SetAllDldoPsmMargin { get; set; } = 0x0;
        public uint SMU_MSG_GetDldoPsmMargin { get; set; } = 0x0;
        public uint SMU_MSG_SetGpuPsmMargin { get; set; } = 0x0;
        public uint SMU_MSG_GetGpuPsmMargin { get; set; } = 0x0;
        public uint SMU_MSG_SetPBOScalar { get; set; } = 0x0;
        public uint SMU_MSG_SetEDCVDDLimit { get; set; } = 0x0;
        public uint SMU_MSG_SetTDCVDDLimit { get; set; } = 0x0;
        public uint SMU_MSG_SetPPTLimit { get; set; } = 0x0;
        public uint SMU_MSG_SetHTCLimit { get; set; } = 0x0;


        public uint SMU_MSG_SetStapmLimit { get; set; } = 0x0;
        public uint SMU_MSG_SetFastLimit { get; set; } = 0x0;
        public uint SMU_MSG_SetSlowLimit { get; set; } = 0x0;
        public uint SMU_MSG_SetSlowTime { get; set; } = 0x0;
        public uint SMU_MSG_SetStapmTime { get; set; } = 0x0;
        public uint SMU_MSG_SetVrmCurrent { get; set; } = 0x0;
        public uint SMU_MSG_SetVrmSocCurrent { get; set; } = 0x0;
        public uint SMU_MSG_SetVrmGfxCurrent { get; set; } = 0x0;
        public uint SMU_MSG_SetVrmVipCurrent { get; set; } = 0x0;
        public uint SMU_MSG_SetVrmMaxCurrent { get; set; } = 0x0;
        public uint SMU_MSG_SetVrmGfxMaxCurrent { get; set; } = 0x0;
        public uint SMU_MSG_SetVrmSocMaxCurrent { get; set; } = 0x0;
        public uint SMU_MSG_SetPsi0Current { get; set; } = 0x0;
        public uint SMU_MSG_SetPsi3CpuCurrent { get; set; } = 0x0;
        public uint SMU_MSG_SetPsi0SocCurrent { get; set; } = 0x0;
        public uint SMU_MSG_SetPsi3GfxCurrent { get; set; } = 0x0;
        public uint SMU_MSG_SetMaxGfxClkFreq { get; set; } = 0x0;
        public uint SMU_MSG_SetMinGfxClkFreq { get; set; } = 0x0;
        public uint SMU_MSG_SetMaxSocClkFreq { get; set; } = 0x0;
        public uint SMU_MSG_SetMinSocClkFreq { get; set; } = 0x0;
        public uint SMU_MSG_SetMaxFclkFreq { get; set; } = 0x0;
        public uint SMU_MSG_SetMinFclkFreq { get; set; } = 0x0;
        public uint SMU_MSG_SetMaxVcn { get; set; } = 0x0;
        public uint SMU_MSG_SetMinVcn { get; set; } = 0x0;
        public uint SMU_MSG_SetMaxLclk { get; set; } = 0x0;
        public uint SMU_MSG_SetMinLclk { get; set; } = 0x0;
        public uint SMU_MSG_SetProchotDeassertionRamp { get; set; } = 0x0;
        public uint SMU_MSG_SetApuSkinTempLimit { get; set; } = 0x0;
        public uint SMU_MSG_SetDgpuSkinTempLimit { get; set; } = 0x0;
        public uint SMU_MSG_SetApuSlowLimit { get; set; } = 0x0;
        public uint SMU_MSG_SetSkinTempPowerLimit { get; set; } = 0x0;
        public uint SMU_MSG_SetGfxClk { get; set; } = 0x0;
        public uint SMU_MSG_SetPowerSaving { get; set; } = 0x0;
        public uint SMU_MSG_SetMaxPerformance { get; set; } = 0x0;
    }
}