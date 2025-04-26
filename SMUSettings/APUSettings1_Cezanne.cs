namespace ZenStates.Core.SMUSettings
{
    public class APUSettings1_Cezanne : APUSettings1
    {
        public APUSettings1_Cezanne()
        {
            Rsmu.SMU_MSG_SetDldoPsmMargin = 0x52;
            Rsmu.SMU_MSG_SetAllDldoPsmMargin = 0xB1;
            Rsmu.SMU_MSG_GetDldoPsmMargin = 0xC3;
            Rsmu.SMU_MSG_SetGpuPsmMargin = 0x53;
            Rsmu.SMU_MSG_GetGpuPsmMargin = 0xC6;
        }
    }
}
