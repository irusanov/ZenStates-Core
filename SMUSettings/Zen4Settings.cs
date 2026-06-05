namespace ZenStates.Core.SMUSettings
{
    // Ryzen 7000 (Raphael)
    public class Zen4Settings : SMU
    {
        public Zen4Settings()
        {
            SMU_TYPE = SmuType.TYPE_CPU4;

            // RSMU
            Rsmu.SMU_ADDR_MSG = 0x03B10524;
            Rsmu.SMU_ADDR_RSP = 0x03B10570;
            Rsmu.SMU_ADDR_ARG = 0x03B10A40;

            // DPTC interface
            Rsmu.SMU_MSG_SetFastLimit = 0x56;
            Rsmu.SMU_MSG_SetSlowLimit = 0xCB;
            Rsmu.SMU_MSG_SetTctlMax = 0x59;
            Rsmu.SMU_MSG_SetTDCVDDLimit = 0x57;
            Rsmu.SMU_MSG_SetEDCVDDLimit = 0x58;

            // Overclock Options
            Rsmu.SMU_MSG_EnableOcMode = 0x5D;
            Rsmu.SMU_MSG_DisableOcMode = 0x5E;
            Rsmu.SMU_MSG_SetOverclockFrequencyAllCores = 0x5F;
            Rsmu.SMU_MSG_SetOverclockFrequencyPerCore = 0x60;
            Rsmu.SMU_MSG_SetOverclockCpuVid = 0x61;
            Rsmu.SMU_MSG_SetPBOScalar = 0x5B;
            Rsmu.SMU_MSG_GetPBOScalar = 0x6D;
            Rsmu.SMU_MSG_IsOverclockable = 0x6F;
            //Rsmu.SMU_MSG_SetGfxclkOverdriveByFreqVid = 0x61;
            Rsmu.SMU_MSG_GetBoostLimitFrequency = 0x6E;
            Rsmu.SMU_MSG_SetBoostLimitFrequencyAllCores = 0x70;

            // Curve Optimizer
            Rsmu.SMU_MSG_SetDldoPsmMargin = 0x6;
            Rsmu.SMU_MSG_SetAllDldoPsmMargin = 0x7;
            Rsmu.SMU_MSG_SetGpuPsmMargin = 0xA7;
            Rsmu.SMU_MSG_GetDldoPsmMargin = 0xD5;
            Rsmu.SMU_MSG_GetGpuPsmMargin = 0xD7;
            Rsmu.SMU_MSG_SetCurveShaperMargin = 0xA6; // marginHigh << 24 | marginMedium << 16 | marginLow << 8 | someBit << 7 | frequencyTier & 0x7F
            Rsmu.SMU_MSG_GetCurveShaperMargin = 0x84; // first 5 arguments are the frequency tiers [minimum, low, medium, high, maximum], 6th argument seems to be unused

            // Debug
            Rsmu.SMU_MSG_GetPboFusedPowerLimit = 0xDC; // Can be locked on some Zen 4 motherboards, Zen 5 not affected
            Rsmu.SMU_MSG_GetPboFusedApuSlowLimit = 0xDA;
            Rsmu.SMU_MSG_GetPboFusedVrmVddTdcCurrent = 0xDB;
            Rsmu.SMU_MSG_GetPboFusedVrmSocTdcCurrent = 0xD9;
            Rsmu.SMU_MSG_GetDramBaseAddress = 0x4;
            Rsmu.SMU_MSG_GetTableVersion = 0x5;
            Rsmu.SMU_MSG_TransferTableToDram = 0x3;
            Rsmu.SMU_MSG_GetEXPOProfileActive = 0x35;
            Rsmu.SMU_MSG_GetLN2Mode = 0xDD;
            Rsmu.SMU_MSG_GetPerformanceData = 0x5C;

            // MP1
            Mp1Smu.SMU_ADDR_MSG = 0x3B10530;
            Mp1Smu.SMU_ADDR_RSP = 0x3B1057C;
            Mp1Smu.SMU_ADDR_ARG = 0x3B109C4;

            // Smu features
            Mp1Smu.SMU_MSG_EnableSmuFeatures = 0x3;
            Mp1Smu.SMU_MSG_DisableSmuFeatures = 0x4;

            // DPTC interface
            Mp1Smu.SMU_MSG_SetStapmLimit = 0x4F;
            Mp1Smu.SMU_MSG_SetStapmTime = 0x4E;
            Mp1Smu.SMU_MSG_SetFastLimit = 0x3E;
            Mp1Smu.SMU_MSG_SetSlowLimit = 0x5F;
            Mp1Smu.SMU_MSG_SetApuSlowLimit = 0x60;
            Mp1Smu.SMU_MSG_SetSlowTime = 0x61;
            Mp1Smu.SMU_MSG_SetTctlMax = 0x3F;
            Mp1Smu.SMU_MSG_SetTDCVDDLimit = 0x3C;
            Mp1Smu.SMU_MSG_SetEDCVDDLimit = 0x3D;
            Mp1Smu.SMU_MSG_SetSkinTempPowerLimit = 0x5E;

            // Overclock Options
            Mp1Smu.SMU_MSG_EnableOcMode = 0x24;
            Mp1Smu.SMU_MSG_DisableOcMode = 0x25;
            Mp1Smu.SMU_MSG_SetOverclockFrequencyPerCore = 0x27;
            Mp1Smu.SMU_MSG_SetOverclockFrequencyAllCores = 0x26;
            Mp1Smu.SMU_MSG_SetOverclockCpuVid = 0x28;
            Mp1Smu.SMU_MSG_SetPBOScalar = 0x2F;

            // Curve Optimizer
            Mp1Smu.SMU_MSG_SetDldoPsmMargin = 0x35;
            Mp1Smu.SMU_MSG_SetAllDldoPsmMargin = 0x36;

            // Debug
            Mp1Smu.SMU_MSG_GetSustainedPowerAndThmLimit = 0x23;
            Mp1Smu.SMU_MSG_SetToolsDramAddress = 0x6;

            // HSMP
            Hsmp.SMU_ADDR_MSG = 0x3B10534;
            Hsmp.SMU_ADDR_RSP = 0x3B10980;
            Hsmp.SMU_ADDR_ARG = 0x3B109E0;
        }
    }
}
