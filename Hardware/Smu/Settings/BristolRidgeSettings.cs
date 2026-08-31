namespace ZenStates.Core.Hardware.Smu.Settings
{
    public class BristolRidgeSettings : SMU
    {
        public BristolRidgeSettings()
        {
            SMU_TYPE = SmuType.TYPE_CPU9;

            SMU_OFFSET_ADDR = 0xB8;
            SMU_OFFSET_DATA = 0xBC;

            Rsmu.SMU_ADDR_MSG = 0xFFF00724; 
            Rsmu.SMU_ADDR_RSP = 0xFFF00764; 
            Rsmu.SMU_ADDR_ARG = 0xFFF007A4;

            Rsmu.SMU_MSG_GetTableVersion = 0x40;
            Rsmu.SMU_MSG_TransferTableToDram = 0x2C;

            Mp1Smu.SMU_ADDR_MSG = 0x13000000;
            Mp1Smu.SMU_ADDR_RSP = 0x13000010;
            Mp1Smu.SMU_ADDR_ARG = 0x13000020;

            // Smu features
            Mp1Smu.SMU_MSG_EnableSmuFeatures = 0x5F;
            Mp1Smu.SMU_MSG_DisableSmuFeatures = 0x60;

            // DPTC interface
            Mp1Smu.SMU_MSG_SetStapmLimit = 0x6C; // Arg 0
            Mp1Smu.SMU_MSG_SetStapmTime = 0x6C; // Arg 3
            Mp1Smu.SMU_MSG_SetFastLimit = 0x69;
            Mp1Smu.SMU_MSG_SetSlowLimit = 0x6A; // Arg 0 AC, Arg 1 DC
            Mp1Smu.SMU_MSG_SetTctlMax = 0x7C; // Temp * 1000
            Mp1Smu.SMU_MSG_SetTDCVDDLimit = 0x67; // Arg 0
            Mp1Smu.SMU_MSG_SetTDCSocLimit = 0x67; // Arg 1
            Mp1Smu.SMU_MSG_SetEDCVDDLimit = 0x6B; // Arg 0
            Mp1Smu.SMU_MSG_SetEDCSocLimit = 0x6B; // Arg 1
            Mp1Smu.SMU_MSG_SetPsi0Current = 0x82; // Arg 0
            Mp1Smu.SMU_MSG_SetPsi0SocCurrent = 0x82; // Arg 1
            Mp1Smu.SMU_MSG_SetProchotDeassertionRamp = 0x81; // 0 - 100

            // Power Saving interface
            Mp1Smu.SMU_MSG_SetPowerSaving = 0x62;
            Mp1Smu.SMU_MSG_SetMaxPerformance = 0x61;

            // Overclock Options
            Mp1Smu.SMU_MSG_SetOverclockCpuVid = 0x88; // Removed on almost all platforms but persist on Desktop
            
            // Curve Optimizer
            Mp1Smu.SMU_MSG_SetGpuPsmMargin = 0x3B;
            Mp1Smu.SMU_MSG_SetGpuPsmMarginAlt = 0x3A; // When motherboard don't have dedicated voltage rail for iGPU and use SoC voltage
            Mp1Smu.SMU_MSG_SetAllDldoPsmMargin = 0x80;
            Mp1Smu.SMU_MSG_GetDldoPsmMargin = 0x7E; // Curve Lo
            Mp1Smu.SMU_MSG_GetSecondaryDldoPsmMargin = 0x7D; // Curve Hi

            // Boot Time Calibration
            Mp1Smu.SMU_MSG_AcBtcStartCal = 0x77;
            
            // AmdGpu
            GpuMb.SMU_ADDR_MSG = 0xFFF00700; 
            GpuMb.SMU_ADDR_RSP = 0xFFF00740; 
            GpuMb.SMU_ADDR_ARG = 0xFFF00780;
            
            // Subsystem frequencies
            GpuMb.SMU_MSG_SetMaxGfxClkFreq = 0x13;
            GpuMb.SMU_MSG_SetMinGfxClkFreq = 0x14;
            GpuMb.SMU_MSG_SetMinVcn = 0x20;
            GpuMb.SMU_MSG_SetMaxVcn = 0x21;
            GpuMb.SMU_MSG_SetMinLclk = 0x18;
            GpuMb.SMU_MSG_SetMaxLclk = 0x19;
            GpuMb.SMU_MSG_SetMinFclkFreq = 0x28;
            GpuMb.SMU_MSG_SetMaxFclkFreq = 0x29;
            
            // Overclock Options
            GpuMb.SMU_MSG_EnableOcMode = 0x2C;
            GpuMb.SMU_MSG_EnableOcModeAlt = 0x2A; // Soft pstates can still throttle
            GpuMb.SMU_MSG_DisableOcMode = 0x56;
            
            // Debug
            GpuMb.SMU_MSG_GetSustainedPowerAndThmLimit = 0x4B;
            GpuMb.SMU_MSG_GetMemoryFrequency = 0x53;
        }
    }
}
