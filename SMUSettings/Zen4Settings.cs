namespace ZenStates.Core.SMUSettings
{
    // Ryzen 7000 (Raphael)
    // Seems to be similar to Zen2 and Zen3
    public class Zen4Settings : Zen3Settings
    {
        public Zen4Settings()
        {
            SMU_TYPE = SmuType.TYPE_CPU4;

            // MP1
            Mp1Smu.SMU_MSG_SetTDCVDDLimit = 0x3C;
            Mp1Smu.SMU_MSG_SetEDCVDDLimit = 0x3D;
            Mp1Smu.SMU_MSG_SetPPTLimit = 0x3E;
            Mp1Smu.SMU_MSG_SetHTCLimit = 0x3F;

            // Unknown
            Mp1Smu.SMU_MSG_SetDldoPsmMargin = 0;
            Mp1Smu.SMU_MSG_SetAllDldoPsmMargin = 0;
            Mp1Smu.SMU_MSG_GetDldoPsmMargin = 0;

            // RSMU
            Rsmu.SMU_ADDR_MSG = 0x03B10524;
            Rsmu.SMU_ADDR_RSP = 0x03B10570;
            Rsmu.SMU_ADDR_ARG = 0x03B10A40;

            Rsmu.SMU_MSG_TransferTableToDram = 0x3;
            Rsmu.SMU_MSG_GetDramBaseAddress = 0x4;
            Rsmu.SMU_MSG_GetTableVersion = 0x5;
            Rsmu.SMU_MSG_GetEXPOProfileActive = 0x35;
            Rsmu.SMU_MSG_EnableOcMode = 0x5D;
            Rsmu.SMU_MSG_DisableOcMode = 0x5E;
            Rsmu.SMU_MSG_SetOverclockFrequencyAllCores = 0x5F;
            Rsmu.SMU_MSG_SetOverclockFrequencyPerCore = 0x60;
            Rsmu.SMU_MSG_SetOverclockCpuVid = 0x61;
            Rsmu.SMU_MSG_SetPPTLimit = 0x56;
            Rsmu.SMU_MSG_SetTDCVDDLimit = 0x57;
            Rsmu.SMU_MSG_SetEDCVDDLimit = 0x58;
            Rsmu.SMU_MSG_SetHTCLimit = 0x59;
            Rsmu.SMU_MSG_SetPBOScalar = 0x5B;
            Rsmu.SMU_MSG_GetPBOScalar = 0x6D;
            Rsmu.SMU_MSG_GetBoostLimitFrequency = 0x6E;
            Rsmu.SMU_MSG_SetBoostLimitFrequencyAllCores = 0x70;

            Rsmu.SMU_MSG_SetDldoPsmMargin = 0x6;
            Rsmu.SMU_MSG_SetAllDldoPsmMargin = 0x7;
            Rsmu.SMU_MSG_GetDldoPsmMargin = 0xD5;
            Rsmu.SMU_MSG_GetLN2Mode = 0xDD;
            Rsmu.SMU_MSG_GetPerformanceData = 0x5C;

            // HSMP
            Hsmp.SMU_ADDR_MSG = 0x3B10534;
            Hsmp.SMU_ADDR_RSP = 0x3B10980;
            Hsmp.SMU_ADDR_ARG = 0x3B109E0;
        }
    }
}
