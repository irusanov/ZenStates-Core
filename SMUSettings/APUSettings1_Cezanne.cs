namespace ZenStates.Core.SMUSettings
{
    public class APUSettings1_Cezanne : SMU
    {
        public APUSettings1_Cezanne()
        {
            SMU_TYPE = SmuType.TYPE_APU1;

            // RSMU
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
            Rsmu.SMU_MSG_SetPBOScalar = 0x3F;
            Rsmu.SMU_MSG_GetPBOScalar = 0xF;
            Rsmu.SMU_MSG_IsOverclockable = 0x82;
            Rsmu.SMU_MSG_GetBoostLimitFrequency = 0x42;

            // MP1
            Mp1Smu.SMU_ADDR_MSG = 0x03B10528;
            Mp1Smu.SMU_ADDR_RSP = 0x03B10564;
            Mp1Smu.SMU_ADDR_ARG = 0x03B10998;

            Mp1Smu.SMU_MSG_SetMaxPerformance = 0x11;
            Mp1Smu.SMU_MSG_SetPowerSaving = 0x12;

            Mp1Smu.SMU_MSG_SetStapmLimit = 0x14;
            Mp1Smu.SMU_MSG_SetFastLimit = 0x15;
            Mp1Smu.SMU_MSG_SetSlowLimit = 0x16;
            Mp1Smu.SMU_MSG_SetSlowTime = 0x17;
            Mp1Smu.SMU_MSG_SetStapmTime = 0x18;
            Mp1Smu.SMU_MSG_SetHTCLimit = 0x19;

            Mp1Smu.SMU_MSG_SetVrmCurrent = 0x1A;
            Mp1Smu.SMU_MSG_SetVrmSocCurrent = 0x1B;
            Mp1Smu.SMU_MSG_SetVrmMaxCurrent = 0x1C;
            Mp1Smu.SMU_MSG_SetVrmSocMaxCurrent = 0x1D;

            Mp1Smu.SMU_MSG_EnableOcMode = 0x2F;
            Mp1Smu.SMU_MSG_DisableOcMode = 0x30;
            Mp1Smu.SMU_MSG_SetOverclockFrequencyAllCores = 0x31;
            Mp1Smu.SMU_MSG_SetOverclockFrequencyPerCore = 0x32;
            Mp1Smu.SMU_MSG_SetOverclockCpuVid = 0x33;

            Mp1Smu.SMU_MSG_SetPBOScalar = 0x49;
        }
    }
}
