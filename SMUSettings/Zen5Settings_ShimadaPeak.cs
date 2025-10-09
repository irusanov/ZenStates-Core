namespace ZenStates.Core.SMUSettings
{
    public class Zen5Settings_ShimadaPeak: SMU
    {
        public Zen5Settings_ShimadaPeak()
        {
            SMU_TYPE = SmuType.TYPE_CPU4;

            // RSMU
            Rsmu.SMU_ADDR_MSG = 0x03B10924;
            Rsmu.SMU_ADDR_RSP = 0x03B10970;
            Rsmu.SMU_ADDR_ARG = 0x03B10A40;

            Rsmu.SMU_MSG_TransferTableToDram = 0x3;
            Rsmu.SMU_MSG_GetDramBaseAddress = 0x4;
            Rsmu.SMU_MSG_GetTableVersion = 0x5;

            Rsmu.SMU_MSG_SetDldoPsmMargin = 0x6;
            Rsmu.SMU_MSG_SetAllDldoPsmMargin = 0x7;

            Rsmu.SMU_MSG_SetPPTLimit = 0x56;
            Rsmu.SMU_MSG_SetTDCVDDLimit = 0x57;
            Rsmu.SMU_MSG_SetEDCVDDLimit = 0x58;
            Rsmu.SMU_MSG_SetHTCLimit = 0x59;
            Rsmu.SMU_MSG_EnableOcMode = 0x5D;
            Rsmu.SMU_MSG_DisableOcMode = 0x5E;
            Rsmu.SMU_MSG_SetOverclockFrequencyAllCores = 0x5F;
            Rsmu.SMU_MSG_SetOverclockFrequencyPerCore = 0x60;
            Rsmu.SMU_MSG_SetOverclockCpuVid = 0x61;
            Rsmu.SMU_MSG_GetPBOScalar = 0x6D;
            Rsmu.SMU_MSG_SetPBOScalar = 0x5B;
            Rsmu.SMU_MSG_GetPerformanceData = 0x5C;
            Rsmu.SMU_MSG_GetLN2Mode = 0xA6;
            Rsmu.SMU_MSG_IsOverclockable = 0x6F;
            Rsmu.SMU_MSG_GetDldoPsmMargin = 0xA3;
            Rsmu.SMU_MSG_SetBoostLimitFrequencyAllCores = 0x70;
        }
    }
}
