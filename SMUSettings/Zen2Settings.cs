namespace ZenStates.Core.SMUSettings
{
    // Ryzen 3000 (Matisse), TR 3000 (Castle Peak)
    public class Zen2Settings : SMU
    {
        public Zen2Settings()
        {
            SMU_TYPE = SmuType.TYPE_CPU2;

            // RSMU
            Rsmu.SMU_ADDR_MSG = 0x03B10524;
            Rsmu.SMU_ADDR_RSP = 0x03B10570;
            Rsmu.SMU_ADDR_ARG = 0x03B10A40;

            // DPTC interface
            Rsmu.SMU_MSG_SetFastLimit = 0x53;
            Rsmu.SMU_MSG_SetTctlMax = 0x56;
            Rsmu.SMU_MSG_SetTDCVDDLimit = 0x54;
            Rsmu.SMU_MSG_SetEDCVDDLimit = 0x55;

            // Overclock Options
            Rsmu.SMU_MSG_EnableOcMode = 0x5A;
            Rsmu.SMU_MSG_DisableOcMode = 0x5B;
            Rsmu.SMU_MSG_SetOverclockFrequencyAllCores = 0x5C;
            Rsmu.SMU_MSG_SetOverclockFrequencyPerCore = 0x5D;
            Rsmu.SMU_MSG_SetOverclockCpuVid = 0x61;
            Rsmu.SMU_MSG_SetPBOScalar = 0x58;
            Rsmu.SMU_MSG_GetPBOScalar = 0x6C;
            Rsmu.SMU_MSG_IsOverclockable = 0x6F;
            Rsmu.SMU_MSG_GetBoostLimitFrequency = 0x6E;

            // Curve Optimizer
            Rsmu.SMU_MSG_SetDldoPsmMargin = 0xA; // Not sure
            Rsmu.SMU_MSG_SetAllDldoPsmMargin = 0xB; // Not sure
            Rsmu.SMU_MSG_GetDldoPsmMargin = 0x7C; // Not sure

            // Debug
            Rsmu.SMU_MSG_GetDramBaseAddress = 0x6;
            Rsmu.SMU_MSG_GetTableVersion = 0x8;
            Rsmu.SMU_MSG_TransferTableToDram = 0x5;
            Rsmu.SMU_MSG_GetFastestCoreofSocket = 0x59;


            // MP1
            Mp1Smu.SMU_ADDR_MSG = 0x3B10530;
            Mp1Smu.SMU_ADDR_RSP = 0x3B1057C;
            Mp1Smu.SMU_ADDR_ARG = 0x3B109C4;

            // Smu features
            Mp1Smu.SMU_MSG_EnableSmuFeatures = 0x5;
            Mp1Smu.SMU_MSG_DisableSmuFeatures = 0x6;

            // DPTC interface
            Mp1Smu.SMU_MSG_SetFastLimit = 0x3C; // was misaligned
            Mp1Smu.SMU_MSG_SetTctlMax = 0x3D;
            Mp1Smu.SMU_MSG_SetTDCVDDLimit = 0x3A;
            Mp1Smu.SMU_MSG_SetEDCVDDLimit = 0x3B;

            // Overclock Options
            Mp1Smu.SMU_MSG_EnableOcMode = 0x24;
            Mp1Smu.SMU_MSG_DisableOcMode = 0x25;
            Mp1Smu.SMU_MSG_SetOverclockFrequencyPerCore = 0x27;
            Mp1Smu.SMU_MSG_SetOverclockFrequencyAllCores = 0x26;
            Mp1Smu.SMU_MSG_SetOverclockCpuVid = 0x28;
            Mp1Smu.SMU_MSG_SetPBOScalar = 0x2F;
            Mp1Smu.SMU_MSG_SetPBO_EN = 0x33;

            // Curve Optimizer
            Mp1Smu.SMU_MSG_SetDldoPsmMargin = 0x34;
            Mp1Smu.SMU_MSG_SetAllDldoPsmMargin = 0x35;

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
