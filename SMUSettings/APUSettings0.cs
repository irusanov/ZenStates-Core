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
            Rsmu.SMU_MSG_EnableOcMode = 0x63;
            Rsmu.SMU_MSG_DisableOcMode = 0x5E;
            Rsmu.SMU_MSG_SetOverclockFrequencyAllCores = 0x79;
            Rsmu.SMU_MSG_SetOverclockFrequencyPerCore = 0x7A;
            Rsmu.SMU_MSG_SetOverclockCpuVid = 0x7B;
            Rsmu.SMU_MSG_SetPPTLimit = 0x2F;
            Rsmu.SMU_MSG_SetHTCLimit = 0x33;
            Rsmu.SMU_MSG_SetTDCVDDLimit = 0x34;
            Rsmu.SMU_MSG_SetTDCSOCLimit = 0x35;
            Rsmu.SMU_MSG_SetEDCVDDLimit = 0x36;
            Rsmu.SMU_MSG_SetEDCSOCLimit = 0x37;
            Rsmu.SMU_MSG_GetPBOScalar = 0x62;
            Rsmu.SMU_MSG_IsOverclockable = 0x4C;
            Rsmu.SMU_MSG_GetBoostLimitFrequency = 0x86;

            // MP1
            Mp1Smu.SMU_ADDR_MSG = 0x03B10528;
            Mp1Smu.SMU_ADDR_RSP = 0x03B10564;
            Mp1Smu.SMU_ADDR_ARG = 0x03B10998;
            Mp1Smu.SMU_MSG_EnableOcMode = 0x58;
            Mp1Smu.SMU_MSG_DisableOcMode = 0x3F;
            Mp1Smu.SMU_MSG_SetOverclockFrequencyAllCores = 0x59;
            Mp1Smu.SMU_MSG_SetOverclockFrequencyPerCore = 0x5A;
            Mp1Smu.SMU_MSG_SetOverclockCpuVid = 0x5B;
            Mp1Smu.SMU_MSG_SetHTCLimit = 0x1F;
            Mp1Smu.SMU_MSG_SetPBOScalar = 0x57;

            Mp1Smu.SMU_MSG_SetMaxPerformance = 0x18;
            Mp1Smu.SMU_MSG_SetPowerSaving = 0x19;

            Mp1Smu.SMU_MSG_SetStapmLimit = 0x1A;
            Mp1Smu.SMU_MSG_SetFastLimit = 0x1B;
            Mp1Smu.SMU_MSG_SetSlowLimit = 0x1C;
            Mp1Smu.SMU_MSG_SetSlowTime = 0x1D;
            Mp1Smu.SMU_MSG_SetStapmTime = 0x1E;

            Mp1Smu.SMU_MSG_SetVrmCurrent = 0x20;
            Mp1Smu.SMU_MSG_SetVrmSocCurrent = 0x21;
            Mp1Smu.SMU_MSG_SetVrmMaxCurrent = 0x22;
            Mp1Smu.SMU_MSG_SetVrmSocMaxCurrent = 0x23;
        }
    }
}
