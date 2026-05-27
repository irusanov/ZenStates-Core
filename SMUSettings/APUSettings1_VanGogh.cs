namespace ZenStates.Core.SMUSettings
{

    // VanGogh
    public class APUSettings1_VanGogh : APUSettings1
    {
        public APUSettings1_VanGogh()
        {
            // MP1
            Mp1Smu.SMU_ADDR_MSG = 0x03B10528;
            Mp1Smu.SMU_ADDR_RSP = 0x03B10578;
            Mp1Smu.SMU_ADDR_ARG = 0x03B10998;

            // DPTC interface
            Mp1Smu.SMU_MSG_SetPsi3CpuCurrent = 0x20;
            Mp1Smu.SMU_MSG_SetPsi3GfxCurrent = 0x21;
            Mp1Smu.SMU_MSG_SetProchotDeassertionRamp = 0x22;
            Mp1Smu.SMU_MSG_SetApuSlowLimit = 0x23;
            Mp1Smu.SMU_MSG_SetSkinTempPowerLimit = 0x4A;
            Mp1Smu.SMU_MSG_SetApuSkinTempLimit = 0x33;
            Mp1Smu.SMU_MSG_SetDgpuSkinTempLimit = 0x34;

            // Curve Optimizer
            Mp1Smu.SMU_MSG_SetDldoPsmMargin = 0x4B;
            Mp1Smu.SMU_MSG_SetAllDldoPsmMargin = 0x4C;

            // Debug
            Mp1Smu.SMU_MSG_GetSustainedPowerAndThmLimit = 0x54;
        }
    }
}
