namespace ZenStates.Core.SMUSettings
{
    public class APUSettings1_Phoenix : APUSettings1_Rembrandt
    {
        public APUSettings1_Phoenix()
        {
            Rsmu.SMU_MSG_GetEXPOProfileActive = 0xDB;
        }
    }
}
