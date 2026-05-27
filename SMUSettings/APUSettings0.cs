namespace ZenStates.Core.SMUSettings
{
    // RavenRidge, RavenRidge 2, FireFlight, Picasso
    public class APUSettings0 : SMU
    {
        public APUSettings0()
        {
            SMU_TYPE = SmuType.TYPE_APU0;

            Rsmu.SMU_ADDR_MSG = 0x03B10A20;
            Rsmu.SMU_ADDR_RSP = 0x03B10A80;
            Rsmu.SMU_ADDR_ARG = 0x03B10A88;

            // DPTC interface
            Rsmu.SMU_MSG_SetStapmLimit = 0x2E;
            Rsmu.SMU_MSG_SetStapmTime = 0x32;
            Rsmu.SMU_MSG_SetFastLimit = 0x2F;
            Rsmu.SMU_MSG_SetSlowLimit = 0x30;
            Rsmu.SMU_MSG_SetSlowTime = 0x31;
            Rsmu.SMU_MSG_SetTctlMax = 0x33;
            Rsmu.SMU_MSG_SetTDCVDDLimit = 0x34;
            Rsmu.SMU_MSG_SetTDCSocLimit = 0x35;
            Rsmu.SMU_MSG_SetEDCVDDLimit = 0x36;
            Rsmu.SMU_MSG_SetEDCSocLimit = 0x37;
            Rsmu.SMU_MSG_SetPsi0Current = 0x38;
            Rsmu.SMU_MSG_SetPsi0SocCurrent = 0x39;
            Rsmu.SMU_MSG_SetProchotDeassertionRamp = 0x3A;

            // Overclock Options
            Rsmu.SMU_MSG_EnableOcMode = 0x63; // Disable prochot
            Rsmu.SMU_MSG_DisableOcMode = 0xE; // Locked
            Rsmu.SMU_MSG_SetOverclockFrequencyAllCores = 0x41; // Locked
            Rsmu.SMU_MSG_SetOverclockFrequencyPerCore = 0xD; // Locked
            Rsmu.SMU_MSG_SetOverclockCpuVid = 0xF; // Locked
            Rsmu.SMU_MSG_SetPBOScalar = 0x3B; // Locked
            Rsmu.SMU_MSG_GetPBOScalar = 0x62;
            Rsmu.SMU_MSG_IsOverclockable = 0x4C;
            Rsmu.SMU_MSG_SetGfxclkOverdriveByFreqVid = 0x61; // Available on several systems
            Rsmu.SMU_MSG_GetBoostLimitFrequency = 0x12; // With arg 3

            // Curve Optimizer
            Rsmu.SMU_MSG_SetDldoPsmMargin = 0x58;
            Rsmu.SMU_MSG_SetAllDldoPsmMargin = 0x59;
            Rsmu.SMU_MSG_SetGpuPsmMargin = 0x59; // Same power rail

            // Debug
            Rsmu.SMU_MSG_GetSustainedPowerAndThmLimit = 0x65;
            Rsmu.SMU_MSG_GetPboFusedVrmVddTdcCurrent = 0x64; // Arg 4
            Rsmu.SMU_MSG_GetPboFusedVrmSocTdcCurrent = 0x64; // Arg 6
            Rsmu.SMU_MSG_GetDramBaseAddress = 0xB;
            Rsmu.SMU_MSG_GetTableVersion = 0xC;
            Rsmu.SMU_MSG_TransferTableToDram = 0x3D;


            // MP1
            Mp1Smu.SMU_ADDR_MSG = 0x03B10528;
            Mp1Smu.SMU_ADDR_RSP = 0x03B10564;
            Mp1Smu.SMU_ADDR_ARG = 0x03B10998;

            // Smu features
            Mp1Smu.SMU_MSG_EnableSmuFeatures = 0x5;
            Mp1Smu.SMU_MSG_DisableSmuFeatures = 0x6;

            // DPTC interface
            Mp1Smu.SMU_MSG_SetStapmLimit = 0x1A;
            Mp1Smu.SMU_MSG_SetStapmTime = 0x1E;
            Mp1Smu.SMU_MSG_SetFastLimit = 0x1B;
            Mp1Smu.SMU_MSG_SetSlowLimit = 0x1C;
            Mp1Smu.SMU_MSG_SetSlowTime = 0x1D;
            Mp1Smu.SMU_MSG_SetTctlMax = 0x1F;
            Mp1Smu.SMU_MSG_SetTDCVDDLimit = 0x20;
            Mp1Smu.SMU_MSG_SetTDCSocLimit = 0x21;
            Mp1Smu.SMU_MSG_SetEDCVDDLimit = 0x22;
            Mp1Smu.SMU_MSG_SetEDCSocLimit = 0x23;
            Mp1Smu.SMU_MSG_SetPsi0Current = 0x24;
            Mp1Smu.SMU_MSG_SetPsi0SocCurrent = 0x25;
            Mp1Smu.SMU_MSG_SetProchotDeassertionRamp = 0x26;

            // Power Saving interface
            Mp1Smu.SMU_MSG_SetPowerSaving = 0x19;
            Mp1Smu.SMU_MSG_SetMaxPerformance = 0x18;

            // Overclock Options
            Mp1Smu.SMU_MSG_EnableOcMode = 0x3F; // Args 1
            Mp1Smu.SMU_MSG_DisableOcMode = 0x3F; // Args 0
            Mp1Smu.SMU_MSG_SetOverclockFrequencyAllCores = 0x40;
            Mp1Smu.SMU_MSG_SetOverclockCpuVid = 0x41;
            Mp1Smu.SMU_MSG_SetGfxclkOverdriveByFreqVid = 0x3D;

            // Boot Time Calibration
            Mp1Smu.SMU_MSG_AcBtcStartCal = 0x2F;
            Mp1Smu.SMU_MSG_AcBtcStopCal = 0x30;
            Mp1Smu.SMU_MSG_AcBtcEndCal = 0x31;

            // Debug
            Mp1Smu.SMU_MSG_GetSustainedPowerAndThmLimit = 0x43;


            // AmdGpu
            GpuMb.SMU_ADDR_MSG = 0x3B10A08;
            GpuMb.SMU_ADDR_RSP = 0x3B10A68;
            GpuMb.SMU_ADDR_ARG = 0x3B10A48;

            // Subsystem frequencies
            GpuMb.SMU_MSG_SetMaxSocClkFreq = 0x32;
            GpuMb.SMU_MSG_SetMinSocClkFreq = 0x21;
            GpuMb.SMU_MSG_SetMaxFclkFreq = 0x33;
            GpuMb.SMU_MSG_SetMinFclkFreq = 0x12;
            GpuMb.SMU_MSG_SetMaxVcn = 0x34;
            GpuMb.SMU_MSG_SetMinVcn = 0x28;
            GpuMb.SMU_MSG_SetMaxLclk = 0x14;
            GpuMb.SMU_MSG_SetMinLclk = 0x23;
            GpuMb.SMU_MSG_SetMaxGfxClkFreq = 0x30;
            GpuMb.SMU_MSG_SetMinGfxClkFreq = 0x31;
        }
    }
}
