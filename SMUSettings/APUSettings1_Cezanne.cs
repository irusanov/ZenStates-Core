namespace ZenStates.Core.SMUSettings
{
    public class APUSettings1_Cezanne : APUSettings1
    {
        public APUSettings1_Cezanne()
        {
            // Curve Optimizer
            Rsmu.SMU_MSG_GetDldoPsmMargin = 0xC3;
            Rsmu.SMU_MSG_GetGpuPsmMargin = 0xC6;
        }
    }
}
