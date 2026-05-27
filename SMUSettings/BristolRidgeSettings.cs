namespace ZenStates.Core.SMUSettings
{
    public class BristolRidgeSettings : SMU
    {
        public BristolRidgeSettings()
        {
            SMU_TYPE = SmuType.TYPE_CPU9;

            SMU_OFFSET_ADDR = 0xB8;
            SMU_OFFSET_DATA = 0xBC;

            Rsmu.SMU_ADDR_MSG = 0x13000000; // Still discovering actual Rsmu addresses
            Rsmu.SMU_ADDR_RSP = 0x13000010; // Added there as fallback
            Rsmu.SMU_ADDR_ARG = 0x13000020;

            Mp1Smu.SMU_ADDR_MSG = 0x13000000;
            Mp1Smu.SMU_ADDR_RSP = 0x13000010;
            Mp1Smu.SMU_ADDR_ARG = 0x13000020;

            // Smu features
            Mp1Smu.SMU_MSG_EnableSmuFeatures = 0x5F;
            Mp1Smu.SMU_MSG_DisableSmuFeatures = 0x60;

            // DPTC interface
            Mp1Smu.SMU_MSG_SetStapmLimit = 0x6C; // Arg 0
            Mp1Smu.SMU_MSG_SetStapmTime = 0x6C; // Arg 3
            Mp1Smu.SMU_MSG_SetFastLimit = 0x69;
            Mp1Smu.SMU_MSG_SetSlowLimit = 0x6A; // Arg 0 AC, Arg 1 DC
            Mp1Smu.SMU_MSG_SetTctlMax = 0x7C; // Temp * 1000
            Mp1Smu.SMU_MSG_SetTDCVDDLimit = 0x67; // Arg 0
            Mp1Smu.SMU_MSG_SetTDCSocLimit = 0x67; // Arg 1
            Mp1Smu.SMU_MSG_SetEDCVDDLimit = 0x6B; // Arg 0
            Mp1Smu.SMU_MSG_SetEDCSocLimit = 0x6B; // Arg 1
            Mp1Smu.SMU_MSG_SetPsi0Current = 0x82; // Arg 0
            Mp1Smu.SMU_MSG_SetPsi0SocCurrent = 0x82; // Arg 1
            Mp1Smu.SMU_MSG_SetProchotDeassertionRamp = 0x81; // 0 - 100

            // Power Saving interface
            Mp1Smu.SMU_MSG_SetPowerSaving = 0x62;
            Mp1Smu.SMU_MSG_SetMaxPerformance = 0x61;

            // Overclock Options
            Mp1Smu.SMU_MSG_SetOverclockCpuVid = 0x88;

            // Boot Time Calibration
            Mp1Smu.SMU_MSG_AcBtcStartCal = 0x77;
        }
    }
}
