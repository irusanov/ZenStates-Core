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

            // MP1
            Mp1Smu.SMU_ADDR_MSG = 0x03B10528;
            Mp1Smu.SMU_ADDR_RSP = 0x03B10564;
            Mp1Smu.SMU_ADDR_ARG = 0x03B10998;

            Mp1Smu.SMU_MSG_EnableOcMode = 0x2F;
            Mp1Smu.SMU_MSG_DisableOcMode = 0x30;
            Mp1Smu.SMU_MSG_SetOverclockFrequencyPerCore = 0x32;
            Mp1Smu.SMU_MSG_SetOverclockCpuVid = 0x33;
            Mp1Smu.SMU_MSG_SetHTCLimit = 0x3E;
            Mp1Smu.SMU_MSG_SetPBOScalar = 0x49;
        }
    }
}
