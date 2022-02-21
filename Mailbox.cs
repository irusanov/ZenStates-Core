namespace ZenStates.Core
{
    public sealed class Mailbox
    {
        // Configurable registers
        public uint SMU_ADDR_MSG { get; set; } = 0x0;
        public uint SMU_ADDR_RSP { get; set; } = 0x0;
        public uint SMU_ADDR_ARG { get; set; } = 0x0;

        // SMU Messages (command IDs)
        // 0x1 and 0x2 seem to be common for all mailboxes
        public uint SMU_MSG_TestMessage { get; } = 0x1;
        public uint SMU_MSG_GetSmuVersion { get; } = 0x2;

        // Configurable commands
        public uint SMU_MSG_GetTableVersion { get; set; } = 0x0;
        public uint SMU_MSG_GetBiosIfVersion { get; set; } = 0x0;
        public uint SMU_MSG_TransferTableToDram { get; set; } = 0x0;
        public uint SMU_MSG_GetDramBaseAddress { get; set; } = 0x0;
        public uint SMU_MSG_EnableSmuFeatures { get; set; } = 0x0;
        public uint SMU_MSG_DisableSmuFeatures { get; set; } = 0x0;
        public uint SMU_MSG_SetOverclockFrequencyAllCores { get; set; } = 0x0;
        public uint SMU_MSG_SetOverclockFrequencyPerCore { get; set; } = 0x0;
        public uint SMU_MSG_SetBoostLimitFrequencyAllCores { get; set; } = 0x0;
        public uint SMU_MSG_SetBoostLimitFrequency { get; set; } = 0x0;
        public uint SMU_MSG_SetOverclockCpuVid { get; set; } = 0x0;
        public uint SMU_MSG_EnableOcMode { get; set; } = 0x0;
        public uint SMU_MSG_DisableOcMode { get; set; } = 0x0;
        public uint SMU_MSG_GetPBOScalar { get; set; } = 0x0;
        public uint SMU_MSG_SetPBOScalar { get; set; } = 0x0;
        public uint SMU_MSG_SetPPTLimit { get; set; } = 0x0;
        public uint SMU_MSG_SetTDCVDDLimit { get; set; } = 0x0;
        public uint SMU_MSG_SetTDCSOCLimit { get; set; } = 0x0;
        public uint SMU_MSG_SetEDCVDDLimit { get; set; } = 0x0;
        public uint SMU_MSG_SetEDCSOCLimit { get; set; } = 0x0;
        public uint SMU_MSG_SetHTCLimit { get; set; } = 0x0;
        public uint SMU_MSG_GetTjMax { get; set; } = 0x0;
        public uint SMU_MSG_SetTjMax { get; set; } = 0x0;
        public uint SMU_MSG_PBO_EN { get; set; } = 0x0;
        public uint SMU_MSG_SetDldoPsmMargin { get; set; } = 0x0;
        public uint SMU_MSG_SetAllDldoPsmMargin { get; set; } = 0x0;
        public uint SMU_MSG_GetDldoPsmMargin { get; set; } = 0x0;



        // HSMP
        // Processor Programming Reference (PPR) for Family 19h Model 01h, Revision B1 Processors, vol 2
        // https://www.amd.com/system/files/TechDocs/55898_pub.zip
        public uint GetInterfaceVersion { get; set; } = 0x0;
        public uint ReadSocketPower { get; set; } = 0x0;
        public uint WriteSocketPowerLimit { get; set; } = 0x0;
        public uint ReadSocketPowerLimit { get; set; } = 0x0;
        public uint ReadMaxSocketPowerLimit { get; set; } = 0x0;
        public uint WriteBoostLimit { get; set; } = 0x0;
        public uint WriteBoostLimitAllCores { get; set; } = 0x0;
        public uint ReadBoostLimit { get; set; } = 0x0;
        public uint ReadProchotStatus { get; set; } = 0x0;
        public uint SetXgmiLinkWidthRange { get; set; } = 0x0;
        public uint APBDisable { get; set; } = 0x0;
        public uint APBEnable { get; set; } = 0x0;
        public uint ReadCurrentFclkMemclk { get; set; } = 0x0;
        public uint ReadCclkFrequencyLimit { get; set; } = 0x0;
        public uint ReadSocketC0Residency { get; set; } = 0x0;
        public uint SetLclkDpmLevelRange { get; set; } = 0x0;
        public uint GetMaxDDRBandwidthAndUtilization { get; set; } = 0x0;
    }
}