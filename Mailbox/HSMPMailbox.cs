namespace ZenStates.Core
{
    // HSMP
    // Processor Programming Reference (PPR) for Family 19h Model 01h, Revision B1 Processors, vol 2
    // https://www.amd.com/system/files/TechDocs/55898_pub.zip
    // Interface Version Supported Function IDs
    // 0001h 01h through 11h
    // 0002h 01h through 12h
    // 0003h 01h through 14h
    // 0004h 01h through 15h
    // 0005h 01h through 22h
    public sealed class HSMPMailbox : Mailbox
    {
        public readonly uint InterfaceVersion;
        public readonly uint HighestSupportedFunction;
        public HSMPMailbox(SMU smu, int maxArgs = Constants.HSMP_MAILBOX_ARGS) : base(maxArgs)
        {
            uint[] args = Utils.MakeCmdArgs(0, this.MAX_ARGS);
            // SendSmuCommand would not execute if mailbox addresses are not defined
            if (smu.SendSmuCommand(this, this.GetInterfaceVersion, ref args) == SMU.Status.OK)
            {
                InterfaceVersion = args[0];
                HighestSupportedFunction = GetHighestSupportedId();
            }
        }

        private uint GetHighestSupportedId()
        {
            switch (InterfaceVersion)
            {
                case 1:
                    return 0x11;
                case 2:
                    return 0x12;
                case 3:
                    return 0x14;
                case 4:
                    return 0x15;
                case 5:
                    return 0x22;
                default:
                    return 0x22;
            }
        }

        public bool IsSupported => InterfaceVersion > 0;
        public uint GetInterfaceVersion { get; set; } = 0x3;
        public uint ReadSocketPower { get; set; } = 0x4;
        public uint WriteSocketPowerLimit { get; set; } = 0x5;
        public uint ReadSocketPowerLimit { get; set; } = 0x6;
        public uint ReadMaxSocketPowerLimit { get; set; } = 0x7;
        public uint WriteBoostLimit { get; set; } = 0x8;
        public uint WriteBoostLimitAllCores { get; set; } = 0x9;
        public uint ReadBoostLimit { get; set; } = 0xA;
        public uint ReadProchotStatus { get; set; } = 0xB;
        public uint SetXgmiLinkWidthRange { get; set; } = 0xC;
        public uint APBDisable { get; set; } = 0xD;
        public uint APBEnable { get; set; } = 0xE;
        public uint ReadCurrentFclkMemclk { get; set; } = 0xF;
        public uint ReadCclkFrequencyLimit { get; set; } = 0x10;
        public uint ReadSocketC0Residency { get; set; } = 0x11;
        public uint SetLclkDpmLevelRange { get; set; } = 0x12;
        public uint GetLclkDpmLevelRange { get; set; } = 0x13;
        public uint GetMaxDDRBandwidthAndUtilization { get; set; } = 0x14;
        // Reserved = 0x15
        public uint GetDIMMTempRangeAndRefreshRate { get; set; } = 0x16;
        public uint GetDIMMPowerConsumption { get; set; } = 0x17;
        public uint GetDIMMThermalSensor { get; set; } = 0x18;
        public uint PwrCurrentActiveFreqLimitSocket { get; set; } = 0x19;
        public uint PwrCurrentActiveFreqLimitCore { get; set; } = 0x1A;
        public uint PwrSviTelemetryAllRails { get; set; } = 0x1B;
        public uint GetSocketFreqRange { get; set; } = 0x1C;
        public uint GetCurrentIoBandwidth { get; set; } = 0x1D;
        public uint GetCurrentXgmiBandwidth { get; set; } = 0xE;
        public uint SetGMI3LinkWidthRange { get; set; } = 0x1F;
        public uint ControlPcieLinkRate { get; set; } = 0x20;
        public uint PwrEfficiencyModeSelection { get; set; } = 0x21;
        public uint SetDfPstateRange { get; set; } = 0x22;
    }
}