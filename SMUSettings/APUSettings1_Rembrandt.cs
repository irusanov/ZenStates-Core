namespace ZenStates.Core.SMUSettings
{
    public class APUSettings1_Rembrandt : APUSettings1
    {
        public APUSettings1_Rembrandt()
        {
            SMU_TYPE = SmuType.TYPE_APU2;

            Rsmu.SMU_MSG_SetPBOScalar = 0x3E;

            Rsmu.SMU_MSG_SetDldoPsmMargin = 0x53;
            Rsmu.SMU_MSG_SetAllDldoPsmMargin = 0x5D;
            Rsmu.SMU_MSG_GetDldoPsmMargin = 0xE1; // 0x2F;
            Rsmu.SMU_MSG_SetGpuPsmMargin = 0x1F; // 0xB7;
            Rsmu.SMU_MSG_GetGpuPsmMargin = 0x20; // 0x30;
            Rsmu.SMU_MSG_GetEXPOProfileActive = 0xDB; // 0xD2;

            // MP1
            // https://github.com/FlyGoat/RyzenAdj/blob/master/lib/nb_smu_ops.h#L45
            Mp1Smu.SMU_ADDR_MSG = 0x03B10528;
            Mp1Smu.SMU_ADDR_RSP = 0x03B10578;
            Mp1Smu.SMU_ADDR_ARG = 0x03B10998;

            Mp1Smu.SMU_MSG_SetDldoPsmMargin = 0x4B;
            Mp1Smu.SMU_MSG_SetAllDldoPsmMargin = 0x4C;
        }
    }
}
