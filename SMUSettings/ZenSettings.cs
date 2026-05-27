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

            // DPTC interface
            Rsmu.SMU_MSG_SetFastLimit = 0x58;

            // Overclock Options
            Rsmu.SMU_MSG_EnableOcMode = 0x63; // Disable PROCHOT, platform OC cap enabled is REQUIRED
            Rsmu.SMU_MSG_DisableOcMode = 0x18; // Disable vid override
            Rsmu.SMU_MSG_SetOverclockCpuVid = 0x17;

            // Curve Optimizer
            Rsmu.SMU_MSG_SetDldoPsmMargin = 0x5D; // Works sometimes, need more checking
            Rsmu.SMU_MSG_SetAllDldoPsmMargin = 0x5E; // Only positive

            // Debug
            Rsmu.SMU_MSG_GetSustainedPowerAndThmLimit = 0x5F;
            Rsmu.SMU_MSG_GetDramBaseAddress = 0xC;
            Rsmu.SMU_MSG_GetTableVersion = 0xD;
            Rsmu.SMU_MSG_TransferTableToDram = 0xA;


            // MP1
            Mp1Smu.SMU_ADDR_MSG = 0x03B10528;
            Mp1Smu.SMU_ADDR_RSP = 0x03B10564;
            Mp1Smu.SMU_ADDR_ARG = 0x03B10598;

            // Smu features
            Mp1Smu.SMU_MSG_EnableSmuFeatures = 0x9;
            Mp1Smu.SMU_MSG_DisableSmuFeatures = 0xA;

            // DPTC interface
            Mp1Smu.SMU_MSG_SetFastLimit = 0x31;
            Mp1Smu.SMU_MSG_SetTctlMax = 0x40; // Temp << 16; and freq on low
            Mp1Smu.SMU_MSG_SetTDCVDDLimit = 0x29; // Not quite set command, just Check Compatibility
            Mp1Smu.SMU_MSG_SetEDCVDDLimit = 0x2A;
            Mp1Smu.SMU_MSG_SetProchotDeassertionRamp = 0x26;

            // Overclock Options
            Mp1Smu.SMU_MSG_EnableOcMode = 0x37; // Args 1
            Mp1Smu.SMU_MSG_DisableOcMode = 0x37; // Args 0
            Mp1Smu.SMU_MSG_SetOverclockFrequencyAllCores = 0x39;
            Mp1Smu.SMU_MSG_SetOverclockCpuVid = 0x38;

            // Boot Time Calibration
            Mp1Smu.SMU_MSG_AcBtcStartCal = 0x23;
            Mp1Smu.SMU_MSG_AcBtcStopCal = 0x24;
            Mp1Smu.SMU_MSG_AcBtcEndCal = 0x25;

            // Debug
            Mp1Smu.SMU_MSG_GetSustainedPowerAndThmLimit = 0x36;
        }
    }
}
