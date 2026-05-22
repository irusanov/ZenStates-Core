namespace ZenStates.Core
{
    public sealed class MP1Mailbox : Mailbox
    {

        // Smu Features

        public uint SMU_MSG_EnableSmuFeatures { get; set; } = 0x0;

        public uint SMU_MSG_DisableSmuFeatures { get; set; } = 0x0;


        // DPTC interface

        public uint SMU_MSG_SetStapmLimit { get; set; } = 0x0;

        public uint SMU_MSG_SetStapmTime { get; set; } = 0x0;

        public uint SMU_MSG_SetFastLimit { get; set; } = 0x0;

        public uint SMU_MSG_SetSlowLimit { get; set; } = 0x0;

        public uint SMU_MSG_SetSlowTime { get; set; } = 0x0;

        public uint SMU_MSG_SetTctlMax { get; set; } = 0x0;

        public uint SMU_MSG_SetTDCVDDLimit { get; set; } = 0x0;

        public uint SMU_MSG_SetTDCSocLimit { get; set; } = 0x0;

        public uint SMU_MSG_SetEDCVDDLimit { get; set; } = 0x0;

        public uint SMU_MSG_SetEDCSocLimit { get; set; } = 0x0;

        public uint SMU_MSG_SetPsi0Current { get; set; } = 0x0;

        public uint SMU_MSG_SetPsi0SocCurrent { get; set; } = 0x0;

        public uint SMU_MSG_SetPsi3CpuCurrent { get; set; } = 0x0;

        public uint SMU_MSG_SetPsi3GfxCurrent { get; set; } = 0x0;

        public uint SMU_MSG_SetProchotDeassertionRamp { get; set; } = 0x0;

        public uint SMU_MSG_SetApuSlowLimit { get; set; } = 0x0;

        public uint SMU_MSG_SetSkinTempPowerLimit { get; set; } = 0x0;

        public uint SMU_MSG_SetApuSkinTempLimit { get; set; } = 0x0;

        public uint SMU_MSG_SetDgpuSkinTempLimit { get; set; } = 0x0;


        // Power Saving interface

        public uint SMU_MSG_SetPowerSaving { get; set; } = 0x0;

        public uint SMU_MSG_SetMaxPerformance { get; set; } = 0x0;


        // Overclock Options

        public uint SMU_MSG_EnableOcMode { get; set; } = 0x0;

        public uint SMU_MSG_DisableOcMode { get; set; } = 0x0;

        public uint SMU_MSG_SetOverclockFrequencyAllCores { get; set; } = 0x0;

        public uint SMU_MSG_SetOverclockFrequencyPerCore { get; set; } = 0x0;

        public uint SMU_MSG_SetBoostLimitFrequencyAllCores { get; set; } = 0x0;

        public uint SMU_MSG_SetOverclockCpuVid { get; set; } = 0x0;

        public uint SMU_MSG_SetPBO_EN { get; set; } = 0x0;

        public uint SMU_MSG_SetPBOScalar { get; set; } = 0x0;

        public uint SMU_MSG_SetGfxclkOverdriveByFreqVid { get; set; } = 0x0;


        // Subsystem frequencies

        public uint SMU_MSG_SetMaxCpuFreq { get; set; } = 0x0;

        public uint SMU_MSG_SetMinCpuFreq { get; set; } = 0x0;

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


        // Curve Optimizer

        public uint SMU_MSG_SetDldoPsmMargin { get; set; } = 0x0;

        public uint SMU_MSG_SetAllDldoPsmMargin { get; set; } = 0x0;


        // Boot Time Calibration

        public uint SMU_MSG_AcBtcStartCal { get; set; } = 0x0;

        public uint SMU_MSG_AcBtcStopCal { get; set; } = 0x0;

        public uint SMU_MSG_AcBtcEndCal { get; set; } = 0x0;

        // Debug

        public uint SMU_MSG_GetSustainedPowerAndThmLimit { get; set; } = 0x0;

        public uint SMU_MSG_GetBiosIfVersion { get; set; } = 0x3;

        public uint SMU_MSG_GetCpuName { get; set; } = 0x0;

        public uint SMU_MSG_SetToolsDramAddress { get; set; } = 0x0;

    }
}