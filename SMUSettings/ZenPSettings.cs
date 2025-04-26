namespace ZenStates.Core.SMUSettings
{
    // Zen+ (Pinnacle Ridge)
    public class ZenPSettings : SMU
    {
        public ZenPSettings()
        {
            SMU_TYPE = SmuType.TYPE_CPU1;

            // RSMU
            Rsmu.SMU_ADDR_MSG = 0x03B1051C;
            Rsmu.SMU_ADDR_RSP = 0x03B10568;
            Rsmu.SMU_ADDR_ARG = 0x03B10590;

            Rsmu.SMU_MSG_TransferTableToDram = 0xA;
            Rsmu.SMU_MSG_GetDramBaseAddress = 0xC;
            Rsmu.SMU_MSG_EnableOcMode = 0x6B; //0x63; <-- Disable PROCHOT?
            //SMU_MSG_DisableOcMode = 0x64;
            Rsmu.SMU_MSG_SetOverclockFrequencyAllCores = 0x6C;
            Rsmu.SMU_MSG_SetOverclockFrequencyPerCore = 0x6D;
            Rsmu.SMU_MSG_SetOverclockCpuVid = 0x6E;

            Rsmu.SMU_MSG_SetPPTLimit = 0x64; // ?
            Rsmu.SMU_MSG_SetTDCVDDLimit = 0x65; // ?
            Rsmu.SMU_MSG_SetEDCVDDLimit = 0x66;
            Rsmu.SMU_MSG_SetHTCLimit = 0x68;

            Rsmu.SMU_MSG_SetPBOScalar = 0x6A;
            Rsmu.SMU_MSG_GetPBOScalar = 0x6F;

            // MP1
            Mp1Smu.SMU_ADDR_MSG = 0x03B10528;
            Mp1Smu.SMU_ADDR_RSP = 0x03B10564;
            Mp1Smu.SMU_ADDR_ARG = 0x03B10598;
        }
    }
}
