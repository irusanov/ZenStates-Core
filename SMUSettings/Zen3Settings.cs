namespace ZenStates.Core.SMUSettings
{
    // Ryzen 5000 (Vermeer), TR 5000 (Chagall)?
    public class Zen3Settings : Zen2Settings
    {
        public Zen3Settings()
        {
            SMU_TYPE = SmuType.TYPE_CPU3;

            // MP1
            Mp1Smu.SMU_ADDR_MSG = 0x3B10530;
            Mp1Smu.SMU_ADDR_RSP = 0x3B1057C;
            Mp1Smu.SMU_ADDR_ARG = 0x3B109C4;

            // DPTC interface
            Mp1Smu.SMU_MSG_SetFastLimit = 0x3D;
            Mp1Smu.SMU_MSG_SetTctlMax = 0x3E;
            Mp1Smu.SMU_MSG_SetTDCVDDLimit = 0x3B;
            Mp1Smu.SMU_MSG_SetEDCVDDLimit = 0x3C;

            // Curve Optimizer
            Mp1Smu.SMU_MSG_SetDldoPsmMargin = 0x35;
            Mp1Smu.SMU_MSG_SetAllDldoPsmMargin = 0x36;

            // HSMP
            Hsmp.SMU_ADDR_MSG = 0x3B10534;
            Hsmp.SMU_ADDR_RSP = 0x3B10980;
            Hsmp.SMU_ADDR_ARG = 0x3B109E0;
        }
    }
}
