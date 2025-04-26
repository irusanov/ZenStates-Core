namespace ZenStates.Core.SMUSettings
{
    // RavenRidge, RavenRidge 2, FireFlight, Picasso
    public class APUSettings0 : SMU
    {
        public APUSettings0()
        {
            SMU_TYPE = SmuType.TYPE_APU0;

            Rsmu.SMU_ADDR_MSG = 0x03B10A20;
            Rsmu.SMU_ADDR_RSP = 0x03B10A80;
            Rsmu.SMU_ADDR_ARG = 0x03B10A88;

            Rsmu.SMU_MSG_GetDramBaseAddress = 0xB;
            Rsmu.SMU_MSG_GetTableVersion = 0xC;
            Rsmu.SMU_MSG_TransferTableToDram = 0x3D;
            Rsmu.SMU_MSG_SetDldoPsmMargin = 0x58;
            Rsmu.SMU_MSG_SetAllDldoPsmMargin = 0x59;
            Rsmu.SMU_MSG_EnableOcMode = 0x69;
            Rsmu.SMU_MSG_DisableOcMode = 0x6A;
            Rsmu.SMU_MSG_SetOverclockFrequencyAllCores = 0x7D;
            Rsmu.SMU_MSG_SetOverclockFrequencyPerCore = 0x7E;
            Rsmu.SMU_MSG_SetOverclockCpuVid = 0x7F;
            Rsmu.SMU_MSG_GetPBOScalar = 0x68;
            Rsmu.SMU_MSG_IsOverclockable = 0x88;

            // MP1
            Mp1Smu.SMU_ADDR_MSG = 0x03B10528;
            Mp1Smu.SMU_ADDR_RSP = 0x03B10564;
            Mp1Smu.SMU_ADDR_ARG = 0x03B10998;
        }
    }
}
