namespace ZenStates.Core
{
    public sealed class HSMPMailbox : Mailbox
    {
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