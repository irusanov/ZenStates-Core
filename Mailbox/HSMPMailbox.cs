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
        public uint GetLclkDpmLevelRange { get; set; } = 0x0;
        public uint GetMaxDDRBandwidthAndUtilization { get; set; } = 0x0;
        public uint GetDIMMTempRangeAndRefreshRate { get; set; } = 0x0;
        public uint GetDIMMPowerConsumption { get; set; } = 0x0;
        public uint GetDIMMThermalSensor { get; set; } = 0x0;
        public uint PwrCurrentActiveFreqLimitSocket { get; set; } = 0x0;
        public uint PwrCurrentActiveFreqLimitCore { get; set; } = 0x0;
        public uint PwrSviTelemetryAllRails { get; set; } = 0x0;
        public uint GetSocketFreqRange { get; set; } = 0x0;
        public uint GetCurrentIoBandwidth { get; set; } = 0x0;
        public uint GetCurrentXgmiBandwidth { get; set; } = 0x0;
        public uint SetGMI3LinkWidthRange { get; set; } = 0x0;
        public uint ControlPcieLinkRate { get; set; } = 0x0;
        public uint PwrEfficiencyModeSelection { get; set; } = 0x0;
        public uint SetDfPstateRange { get; set; } = 0x0;
    }
}