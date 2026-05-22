namespace ZenStates.Core.SMUSettings
{

    // Renoir, Cezanne, Rembrandt
    public class APUSettings1 : SMU
    {
        public APUSettings1()
        {
            SMU_TYPE = SmuType.TYPE_APU1;

            Rsmu.SMU_ADDR_MSG = 0x03B10A20;
            Rsmu.SMU_ADDR_RSP = 0x03B10A80;
            Rsmu.SMU_ADDR_ARG = 0x03B10A88;

            // DPTC interface
            Rsmu.SMU_MSG_SetStapmLimit = 0x31;
            Rsmu.SMU_MSG_SetStapmTime = 0x36;
            Rsmu.SMU_MSG_SetFastLimit = 0x32;
            Rsmu.SMU_MSG_SetSlowLimit = 0x33;
            Rsmu.SMU_MSG_SetSlowTime = 0x35;
            Rsmu.SMU_MSG_SetTctlMax = 0x37;
            Rsmu.SMU_MSG_SetTDCVDDLimit = 0x38;
            Rsmu.SMU_MSG_SetTDCSocLimit = 0x39;
            Rsmu.SMU_MSG_SetEDCVDDLimit = 0x3A;
            Rsmu.SMU_MSG_SetEDCSocLimit = 0x3B;
            Rsmu.SMU_MSG_SetPsi0Current = 0x3C;
            Rsmu.SMU_MSG_SetPsi0SocCurrent = 0x3D;
            Rsmu.SMU_MSG_SetProchotDeassertionRamp = 0x3E;
            Rsmu.SMU_MSG_SetApuSlowLimit = 0x34;
            Rsmu.SMU_MSG_SetApuSkinTempLimit = 0x91;
            Rsmu.SMU_MSG_SetDgpuSkinTempLimit = 0x92;

            // Overclock Options
            Rsmu.SMU_MSG_EnableOcMode = 0x17;
            Rsmu.SMU_MSG_DisableOcMode = 0x18;
            Rsmu.SMU_MSG_SetOverclockFrequencyAllCores = 0x19;
            Rsmu.SMU_MSG_SetOverclockFrequencyPerCore = 0x1A;
            Rsmu.SMU_MSG_SetOverclockCpuVid = 0x1B;
            Rsmu.SMU_MSG_SetPBOScalar = 0x3F;
            Rsmu.SMU_MSG_GetPBOScalar = 0xF;
            Rsmu.SMU_MSG_IsOverclockable = 0x82;
            Rsmu.SMU_MSG_SetGfxclkOverdriveByFreqVid = 0x1C; // Available on several systems
            Rsmu.SMU_MSG_GetBoostLimitFrequency = 0x42;
            Rsmu.SMU_MSG_SetFixedGfxClkFreq = 0x89;

            // Curve Optimizer
            Rsmu.SMU_MSG_SetDldoPsmMargin = 0x52;
            Rsmu.SMU_MSG_SetAllDldoPsmMargin = 0xB1;
            Rsmu.SMU_MSG_SetGpuPsmMargin = 0x53;

            // Debug
            Rsmu.SMU_MSG_GetPboFusedPowerLimit = 0x11;
            Rsmu.SMU_MSG_GetPboFusedSlowLimit = 0x12;
            Rsmu.SMU_MSG_GetPboFusedFastLimit = 0x13;
            Rsmu.SMU_MSG_GetPboFusedApuSlowLimit = 0x14;
            Rsmu.SMU_MSG_GetPboFusedVrmVddTdcCurrent = 0x15;
            Rsmu.SMU_MSG_GetPboFusedVrmSocTdcCurrent = 0x16;
            Rsmu.SMU_MSG_GetDramBaseAddress = 0x66;
            Rsmu.SMU_MSG_GetTableVersion = 0x6;
            Rsmu.SMU_MSG_TransferTableToDram = 0x65;


            // MP1
            Mp1Smu.SMU_ADDR_MSG = 0x03B10528;
            Mp1Smu.SMU_ADDR_RSP = 0x03B10564;
            Mp1Smu.SMU_ADDR_ARG = 0x03B10998;

            // Smu features
            Mp1Smu.SMU_MSG_EnableSmuFeatures = 0x5;
            Mp1Smu.SMU_MSG_DisableSmuFeatures = 0x7;

            // DPTC interface
            Mp1Smu.SMU_MSG_SetStapmLimit = 0x14;
            Mp1Smu.SMU_MSG_SetStapmTime = 0x18;
            Mp1Smu.SMU_MSG_SetFastLimit = 0x15;
            Mp1Smu.SMU_MSG_SetSlowLimit = 0x16;
            Mp1Smu.SMU_MSG_SetSlowTime = 0x17;
            Mp1Smu.SMU_MSG_SetTctlMax = 0x19;
            Mp1Smu.SMU_MSG_SetTDCVDDLimit = 0x1A;
            Mp1Smu.SMU_MSG_SetTDCSocLimit = 0x1B;
            Mp1Smu.SMU_MSG_SetEDCVDDLimit = 0x1C;
            Mp1Smu.SMU_MSG_SetEDCSocLimit = 0x1D;
            Mp1Smu.SMU_MSG_SetPsi0Current = 0x1E;
            Mp1Smu.SMU_MSG_SetPsi0SocCurrent = 0x1F;
            Mp1Smu.SMU_MSG_SetProchotDeassertionRamp = 0x20;
            Mp1Smu.SMU_MSG_SetApuSlowLimit = 0x21;
            Mp1Smu.SMU_MSG_SetSkinTempPowerLimit = 0x53;
            Mp1Smu.SMU_MSG_SetApuSkinTempLimit = 0x38;
            Mp1Smu.SMU_MSG_SetDgpuSkinTempLimit = 0x39;

            // Power Saving interface
            Mp1Smu.SMU_MSG_SetMaxPerformance = 0x11;
            Mp1Smu.SMU_MSG_SetPowerSaving = 0x12;

            // Overclock Options
            Mp1Smu.SMU_MSG_EnableOcMode = 0x2F;
            Mp1Smu.SMU_MSG_DisableOcMode = 0x30;
            Mp1Smu.SMU_MSG_SetOverclockFrequencyAllCores = 0x31;
            Mp1Smu.SMU_MSG_SetOverclockFrequencyPerCore = 0x32;
            Mp1Smu.SMU_MSG_SetOverclockCpuVid = 0x33;
            Mp1Smu.SMU_MSG_SetPBOScalar = 0x49;
            Mp1Smu.SMU_MSG_SetGfxclkOverdriveByFreqVid = 0x34;

            // Curve Optimizer
            Mp1Smu.SMU_MSG_SetDldoPsmMargin = 0x54;
            Mp1Smu.SMU_MSG_SetAllDldoPsmMargin = 0x55;

            // Debug
            Mp1Smu.SMU_MSG_GetSustainedPowerAndThmLimit = 0x5B;
        }
    }
}
