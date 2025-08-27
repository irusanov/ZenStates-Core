namespace ZenStates.Core.SMUSettings
{
    // Zen (Summit Ridge), ThreadRipper (Whitehaven)
    public class ZenSettings : SMU
    {
        public ZenSettings()
        {
            SMU_TYPE = SmuType.TYPE_CPU0;

            // RSMU
            Rsmu.SMU_ADDR_MSG = 0x03B1051C;
            Rsmu.SMU_ADDR_RSP = 0x03B10568;
            Rsmu.SMU_ADDR_ARG = 0x03B10590;

            Rsmu.SMU_MSG_TransferTableToDram = 0xA;
            Rsmu.SMU_MSG_GetDramBaseAddress = 0xC;
            // Rsmu.SMU_MSG_EnableOcMode = 0x63; // Disable PROCHOT?

            // MP1
            Mp1Smu.SMU_ADDR_MSG = 0x03B10528;
            Mp1Smu.SMU_ADDR_RSP = 0x03B10564;
            Mp1Smu.SMU_ADDR_ARG = 0x03B10598;

            //Mp1Smu.SMU_MSG_TransferTableToDram = 0x21; // ?
            Mp1Smu.SMU_MSG_EnableOcMode = 0x23;
            Mp1Smu.SMU_MSG_DisableOcMode = 0x24; // is this still working?
            Mp1Smu.SMU_MSG_SetOverclockFrequencyAllCores = 0x26;
            Mp1Smu.SMU_MSG_SetOverclockFrequencyPerCore = 0x27;
            Mp1Smu.SMU_MSG_SetOverclockCpuVid = 0x28;
        }
    }
}
