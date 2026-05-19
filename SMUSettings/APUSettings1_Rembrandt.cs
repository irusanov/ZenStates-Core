namespace ZenStates.Core.SMUSettings
{
    public class APUSettings1_Rembrandt : APUSettings1_Phoenix
    {
        public APUSettings1_Rembrandt()
        {
            // Curve Optimizer
            Rsmu.SMU_MSG_GetDldoPsmMargin = 0x2F;
            Rsmu.SMU_MSG_SetGpuPsmMargin = 0xB7;
            Rsmu.SMU_MSG_GetGpuPsmMargin = 0x30;
        }
    }
}