namespace ZenStates.Core.SMUSettings
{
    // Ryzen 5000 (Vermeer), TR 5000 (Chagall)?
    public class Zen3Settings : Zen2Settings
    {
        public Zen3Settings()
        {
            SMU_TYPE = SmuType.TYPE_CPU3;

            Rsmu.SMU_MSG_SetDldoPsmMargin = 0xA;
            Rsmu.SMU_MSG_SetAllDldoPsmMargin = 0xB;
            Rsmu.SMU_MSG_GetDldoPsmMargin = 0x7C;

            Mp1Smu.SMU_MSG_SetDldoPsmMargin = 0x35;
            Mp1Smu.SMU_MSG_SetAllDldoPsmMargin = 0x36;
            Mp1Smu.SMU_MSG_GetDldoPsmMargin = 0x48;
        }
    }
}
