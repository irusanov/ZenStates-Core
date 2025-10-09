namespace ZenStates.Core.SMUSettings
{
    public class APUSettings1_Phoenix : SMU
    {
        public APUSettings1_Phoenix()
        {
            SMU_TYPE = SmuType.TYPE_APU2;

            Rsmu.SMU_ADDR_MSG = 0x03B10A20;
            Rsmu.SMU_ADDR_RSP = 0x03B10A80;
            Rsmu.SMU_ADDR_ARG = 0x03B10A88;

            Rsmu.SMU_MSG_GetTableVersion = 0x6;
            Rsmu.SMU_MSG_TransferTableToDram = 0x65;
            Rsmu.SMU_MSG_GetDramBaseAddress = 0x66;
            Rsmu.SMU_MSG_EnableOcMode = 0x17;
            Rsmu.SMU_MSG_DisableOcMode = 0x18;
            Rsmu.SMU_MSG_SetOverclockFrequencyAllCores = 0x19;
            Rsmu.SMU_MSG_SetOverclockFrequencyPerCore = 0x1A;
            Rsmu.SMU_MSG_SetOverclockCpuVid = 0x1B;
            Rsmu.SMU_MSG_SetPPTLimit = 0x33;
            Rsmu.SMU_MSG_SetHTCLimit = 0x37;
            Rsmu.SMU_MSG_SetTDCVDDLimit = 0x38;
            Rsmu.SMU_MSG_SetTDCSOCLimit = 0x39;
            Rsmu.SMU_MSG_SetEDCVDDLimit = 0x3A;
            Rsmu.SMU_MSG_SetEDCSOCLimit = 0x3B;
            Rsmu.SMU_MSG_SetPBOScalar = 0x3E;
            Rsmu.SMU_MSG_GetPBOScalar = 0xF;
            Rsmu.SMU_MSG_IsOverclockable = 0x82;
            Rsmu.SMU_MSG_GetBoostLimitFrequency = 0x42;
            Rsmu.SMU_MSG_SetDldoPsmMargin = 0x53;
            Rsmu.SMU_MSG_SetAllDldoPsmMargin = 0x5D;
            Rsmu.SMU_MSG_GetDldoPsmMargin = 0xE1; // 0x2F;
            Rsmu.SMU_MSG_SetGpuPsmMargin = 0x1F; // 0xB7;
            Rsmu.SMU_MSG_GetGpuPsmMargin = 0x20; // 0x30;
            Rsmu.SMU_MSG_GetEXPOProfileActive = 0xDB;
            Rsmu.SMU_MSG_GetPerformanceData = 0xB;
            Rsmu.SMU_MSG_GetLN2Mode = 0xC4;
            Rsmu.SMU_MSG_SetBoostLimitFrequencyAllCores = 0x47;
            Rsmu.SMU_MSG_SetBoostLimitFrequencyGpu = 0xCA;

            // MP1
            // https://github.com/FlyGoat/RyzenAdj/blob/master/lib/nb_smu_ops.h#L45
            Mp1Smu.SMU_ADDR_MSG = 0x03B10528;
            Mp1Smu.SMU_ADDR_RSP = 0x03B10578;
            Mp1Smu.SMU_ADDR_ARG = 0x03B10998;

            Mp1Smu.SMU_MSG_EnableOcMode = 0x2F;
            Mp1Smu.SMU_MSG_DisableOcMode = 0x30;
            Mp1Smu.SMU_MSG_SetOverclockFrequencyAllCores = 0x31;
            Mp1Smu.SMU_MSG_SetOverclockFrequencyPerCore = 0x32;
            Mp1Smu.SMU_MSG_SetOverclockCpuVid = 0x33;
            Mp1Smu.SMU_MSG_SetHTCLimit = 0x19;
            Mp1Smu.SMU_MSG_SetPBOScalar = 0x49;
            Mp1Smu.SMU_MSG_SetDldoPsmMargin = 0x4B;
            Mp1Smu.SMU_MSG_SetAllDldoPsmMargin = 0x4C;
        }
    }
}
