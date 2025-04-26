namespace ZenStates.Core.SMUSettings
{
    public class APUSettings0_Picasso : APUSettings0
    {
        public APUSettings0_Picasso()
        {
            Rsmu.SMU_MSG_GetPBOScalar = 0x62;
            Rsmu.SMU_MSG_IsOverclockable = 0x87;
        }
    }
}
