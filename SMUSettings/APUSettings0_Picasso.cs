namespace ZenStates.Core.SMUSettings
{
    public class APUSettings0_Picasso : APUSettings0
    {
        public APUSettings0_Picasso()
        {
            // DPTC interface
            Rsmu.SMU_MSG_SetApuSlowLimit = 0x75;
            
            // Overclock Options
            Rsmu.SMU_MSG_EnableOcMode = 0x78; 
            Rsmu.SMU_MSG_SetOverclockFrequencyAllCores = 0x79;
            Rsmu.SMU_MSG_SetOverclockFrequencyPerCore = 0x7A; 
            Rsmu.SMU_MSG_SetOverclockCpuVid = 0x7B;
            Rsmu.SMU_MSG_SetPBOScalar = 0x7C; 
            Rsmu.SMU_MSG_IsOverclockable = 0x87;
            Rsmu.SMU_MSG_GetBoostLimitFrequency = 0x86;
            
            // Debug
            Rsmu.SMC_MSG_GetPboFusedPowerLimit = 0x7F;
            Rsmu.SMC_MSG_GetPboFusedSlowLimit = 0x80;
            Rsmu.SMC_MSG_GetPboFusedFastLimit = 0x81;
            Rsmu.SMC_MSG_GetPboFusedApuSlowLimit = 0x82;
            Rsmu.SMC_MSG_GetPboFusedVrmVddTdcCurrent = 0x83;
            Rsmu.SMC_MSG_GetPboFusedVrmSocTdcCurrent = 0x84;

            // MP1
            // DPTC interface
            Mp1Smu.SMU_MSG_SetApuSlowLimit = 0x54;
            
            // Overclock Options
            Mp1Smu.SMU_MSG_EnableOcMode = 0x58;
            Mp1Smu.SMU_MSG_SetOverclockFrequencyAllCores = 0x59;
            Mp1Smu.SMU_MSG_SetOverclockFrequencyPerCore = 0x5A;
            Mp1Smu.SMU_MSG_SetOverclockCpuVid = 0x5B;
            Mp1Smu.SMU_MSG_SetPBOScalar = 0x57;
        }
    }
}
