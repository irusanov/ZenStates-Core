namespace ZenStates.Core.SMUSettings
{
    public class APUSettings1_Strix : APUSettings1_Phoenix
    {
        public APUSettings1_Strix()
        {
            // DPTC interface
            Rsmu.SMU_MSG_SetPsi0Current = 0x0; // No PSI option

            // Curve Optimizer
            Rsmu.SMU_MSG_GetDldoPsmMargin = 0xAF;

            // MP1
            Mp1Smu.SMU_ADDR_MSG = 0x03B10928;
            Mp1Smu.SMU_ADDR_RSP = 0x03B10978;
            Mp1Smu.SMU_ADDR_ARG = 0x03B10998;

            // DPTC interface
            Mp1Smu.SMU_MSG_SetPsi0Current = 0x0; // No PSI option
        }
    }
}