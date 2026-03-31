

using ZenStates.Core.DRAM;
using static ZenStates.Core.JedecPmicRegisters;

namespace ZenStates.Core
{
    internal static class Ddr5PmicDecoder
    {
        // Voltage formulas (JEDEC JESD301-2)
        private const int SWA_SWB_BASE = 800;   // mV base for VDD/VDDQ
        private const int SWC_BASE = 1500;      // mV base for VPP
        private const int VID_STEP = 5;         // mV per VID code

        /// <summary>Decode SWA/SWB VID to millivolts (JEDEC 7-bit: bits [7:1]).</summary>
        public static int SwabVid7ToMv(byte reg) { return SWA_SWB_BASE + ((reg >> 1) & 0x7F) * VID_STEP; }

        /// <summary>Decode SWA/SWB VID to millivolts (OC 8-bit: full byte).</summary>
        public static int SwabVid8ToMv(byte reg) { return SWA_SWB_BASE + reg * VID_STEP; }

        /// <summary>Decode SWC VID to millivolts (JEDEC 7-bit: bits [7:1]).</summary>
        public static int SwcVid7ToMv(byte reg) { return SWC_BASE + ((reg >> 1) & 0x7F) * VID_STEP; }

        /// <summary>Decode SWC VID to millivolts (OC 8-bit: full byte).</summary>
        public static int SwcVid8ToMv(byte reg) { return SWC_BASE + reg * VID_STEP; }

        // Current limiter decode (R0x20)─────────────
        public static int DecodeSwabCurrentLimit(int code)
        {
            switch (code)
            {
                case 0: return 3000;
                case 1: return 4000;
                case 2: return 5000;
                default: return 6000;
            }
        }

        public static int DecodeSwcCurrentLimit(int code)
        {
            switch (code)
            {
                case 0: return 500;
                case 1: return 750;
                case 2: return 1000;
                default: return 1250;
            }
        }

        // OV threshold decode (R0x22/R0x26/R0x28 [5:4])
        public static string DecodeOvThreshold(int code)
        {
            switch (code)
            {
                case 0: return "+7.5%";
                case 1: return "+10%";
                case 2: return "+12.5%";
                default: return "Reserved";
            }
        }

        // UV threshold decode (R0x22/R0x26/R0x28 [3:2])
        public static string DecodeUvThreshold(int code)
        {
            switch (code)
            {
                case 0: return "-10%";
                case 1: return "-12.5%";
                case 2: return "-15%";
                default: return "Reserved";
            }
        }

        // PMIC temperature decode (R0x33 [7:5])
        public static string DecodePmicTemp(int code)
        {
            switch (code)
            {
                case 0: return "< 85 C";
                case 1: return "85 C";
                case 2: return "95 C";
                case 3: return "105 C";
                case 4: return "115 C";
                case 5: return "125 C";
                case 6: return "135 C";
                default: return "> 140 C";
            }
        }

        public static string DecodeShutdownThreshold(int code)
        {
            switch (code & 0x07)
            {
                case 0: return "> 105 C";
                case 1: return "> 115 C";
                case 2: return "> 125 C";
                case 3: return "> 135 C";
                case 4: return "> 145 C";
                default: return "Reserved";
            }
        }

        public static string DecodeModeSelect(int code)
        {
            switch (code & 0x03)
            {
                case 2: return "COT; DCM";
                case 3: return "COT; Forced CCM";
                default: return "Reserved";
            }
        }

        public static string DecodeSwitchingFrequency(int code)
        {
            switch (code & 0x03)
            {
                case 0: return "750 KHz";
                default: return "Vendor specific";
            }
        }

        public static int DecodeLdo18Mv(byte reg)
        {
            switch ((reg >> 6) & 0x03)
            {
                case 0: return 1700;
                case 1: return 1800;
                case 2: return 1900;
                default: return 2000;
            }
        }

        public static int DecodeLdo10Mv(byte reg)
        {
            switch ((reg >> 1) & 0x03)
            {
                case 0: return 900;
                case 1: return 1000;
                case 2: return 1100;
                default: return 1200;
            }
        }

        public static string DecodeAdcInput(int code)
        {
            switch (code & 0x0F)
            {
                case 0x0: return "SWA Output Voltage";
                case 0x2: return "SWB Output Voltage";
                case 0x3: return "SWC Output Voltage";
                case 0x5: return "VIN_Bulk Input Voltage";
                case 0x8: return "VOUT_1.8V Output Voltage";
                case 0x9: return "VOUT_1.0V Output Voltage";
                default: return "Reserved";
            }
        }

        public static string DecodeAdcUpdateFrequency(int code)
        {
            switch (code & 0x03)
            {
                case 0: return "1 ms";
                case 1: return "2 ms";
                case 2: return "4 ms";
                default: return "8 ms";
            }
        }

        public static int DecodeAdcMv(int adcSelect, byte raw)
        {
            if (raw == 0)
                return 0;

            // JESD301-2 Table 137:
            // - SWA/SWB/SWC/VOUT_1.8V/VOUT_1.0V = 15 mV per LSB
            // - VIN_Bulk = 70 mV per LSB
            if (adcSelect == 0x5)
                return raw * 70;

            switch (adcSelect & 0x0F)
            {
                case 0x0:
                case 0x2:
                case 0x3:
                case 0x8:
                case 0x9:
                    return raw * 15;
                default:
                    return 0;
            }
        }

        // PMIC vendor identification
        public static string LookupPmicVendor(byte bank, byte code)
        {
            return ManufacturerMapping.Lookup(bank, code);
        }

        public static Ddr5PmicData Decode(byte pmicAddr, byte[] rawRegisters)
        {
            if (rawRegisters != null || rawRegisters.Length == 0)
                return new Ddr5PmicData { IsValid = false };

            Ddr5PmicData pd = new Ddr5PmicData();
            pd.I2cAddress = pmicAddr;
            pd.SpdHubAddress = (byte)(pmicAddr + SPD_PMIC_OFFSET);
            pd.RawRegisters = rawRegisters;

            // Vendor ID (R0x3C:R0x3D — JEP106 with parity)
            pd.VendorBank = pd.RawRegisters[REG_VENDOR_BANK];
            pd.VendorCode = pd.RawRegisters[REG_VENDOR_CODE];
            pd.VendorName = Ddr5PmicDecoder.LookupPmicVendor(pd.VendorBank, pd.VendorCode);

            // Revision (R0x3B)
            byte rev = pd.RawRegisters[REG_REVISION];
            pd.RevisionMajor = ((rev >> 4) & 0x03) + 1;
            pd.RevisionMinor = (rev >> 1) & 0x07;

            // VR Enable (R0x32 [7])
            pd.VrEnabled = (pd.RawRegisters[REG_VR_ENABLE] & 0x80) != 0;

            // ADC / telemetry mode
            byte reg30 = pd.RawRegisters[REG_TELEMETRY_SELECT];
            byte reg1A = pd.RawRegisters[0x1A];
            byte reg1B = pd.RawRegisters[0x1B];
            pd.AdcEnabled = (reg30 & 0x80) != 0;
            pd.AdcSelectedInput = Ddr5PmicDecoder.DecodeAdcInput((reg30 >> 3) & 0x0F);
            pd.AdcUpdateFrequency = Ddr5PmicDecoder.DecodeAdcUpdateFrequency(reg30 & 0x03);
            pd.TelemetryReportsPower = (reg1B & 0x40) != 0;
            pd.TelemetryReportsTotalPower = (reg1A & 0x02) != 0;

            // PMIC temperature (R0x33 [7:5]) and LDO PG
            byte reg33 = pd.RawRegisters[REG_PMIC_TEMP];
            pd.PmicTemperature = Ddr5PmicDecoder.DecodePmicTemp((reg33 >> 5) & 0x07);
            pd.Vout10PowerGood = (reg33 & 0x04) == 0;

            // LDO runtime / NVM defaults
            pd.Vout18SettingMv = Ddr5PmicDecoder.DecodeLdo18Mv(pd.RawRegisters[REG_LDO_SETTINGS]);
            pd.Vout10SettingMv = Ddr5PmicDecoder.DecodeLdo10Mv(pd.RawRegisters[REG_LDO_SETTINGS]);
            pd.Vout18SettingNvmMv = Ddr5PmicDecoder.DecodeLdo18Mv(pd.RawRegisters[REG_NVM_LDO_SETTINGS]);
            pd.Vout10SettingNvmMv = Ddr5PmicDecoder.DecodeLdo10Mv(pd.RawRegisters[REG_NVM_LDO_SETTINGS]);

            // Main rail voltage decode
            byte swaVid = pd.RawRegisters[REG_SWA_VID];
            byte swbVid = pd.RawRegisters[REG_SWB_VID];
            byte swcVid = pd.RawRegisters[REG_SWC_VID];
            pd.VddMv = Ddr5PmicDecoder.SwabVid7ToMv(swaVid);
            pd.VddqMv = Ddr5PmicDecoder.SwabVid7ToMv(swbVid);
            pd.VppMv = Ddr5PmicDecoder.SwcVid7ToMv(swcVid);
            pd.VddMv8bit = Ddr5PmicDecoder.SwabVid8ToMv(swaVid);
            pd.VddqMv8bit = Ddr5PmicDecoder.SwabVid8ToMv(swbVid);
            pd.VppMv8bit = Ddr5PmicDecoder.SwcVid8ToMv(swcVid);

            // Telemetry raw registers
            pd.SwaTelemetryRaw = pd.RawRegisters[0x0C];
            pd.SwbTelemetryRaw = pd.RawRegisters[0x0E] & 0x3F;
            pd.SwcTelemetryRaw = pd.RawRegisters[0x0F] & 0x3F;

            // Current limiter
            byte clim = pd.RawRegisters[REG_CURRENT_LIMIT];
            pd.SwaCurrentLimitMa = Ddr5PmicDecoder.DecodeSwabCurrentLimit((clim >> 6) & 0x03);
            pd.SwbCurrentLimitMa = Ddr5PmicDecoder.DecodeSwabCurrentLimit((clim >> 2) & 0x03);
            pd.SwcCurrentLimitMa = Ddr5PmicDecoder.DecodeSwcCurrentLimit(clim & 0x03);

            // Protection thresholds
            byte swaThresh = pd.RawRegisters[REG_SWA_THRESH];
            byte swbThresh = pd.RawRegisters[REG_SWB_THRESH];
            byte swcThresh = pd.RawRegisters[REG_SWC_THRESH];
            pd.VddOvThreshold = Ddr5PmicDecoder.DecodeOvThreshold((swaThresh >> 4) & 0x03);
            pd.VddUvThreshold = Ddr5PmicDecoder.DecodeUvThreshold((swaThresh >> 2) & 0x03);
            pd.VddqOvThreshold = Ddr5PmicDecoder.DecodeOvThreshold((swbThresh >> 4) & 0x03);
            pd.VddqUvThreshold = Ddr5PmicDecoder.DecodeUvThreshold((swbThresh >> 2) & 0x03);
            pd.VppOvThreshold = Ddr5PmicDecoder.DecodeOvThreshold((swcThresh >> 4) & 0x03);
            pd.VppUvThreshold = Ddr5PmicDecoder.DecodeUvThreshold((swcThresh >> 2) & 0x03);

            // Rail config
            byte reg29 = pd.RawRegisters[REG_RAIL_CONFIG_A];
            byte reg2A = pd.RawRegisters[REG_RAIL_CONFIG_B];
            byte reg2E = pd.RawRegisters[REG_SHUTDOWN_TEMP];
            pd.SwaMode = Ddr5PmicDecoder.DecodeModeSelect((reg29 >> 6) & 0x03);
            pd.SwaSwitchingFrequency = Ddr5PmicDecoder.DecodeSwitchingFrequency((reg29 >> 4) & 0x03);
            pd.SwbMode = Ddr5PmicDecoder.DecodeModeSelect((reg2A >> 6) & 0x03);
            pd.SwbSwitchingFrequency = Ddr5PmicDecoder.DecodeSwitchingFrequency((reg2A >> 4) & 0x03);
            pd.SwcMode = Ddr5PmicDecoder.DecodeModeSelect((reg2A >> 2) & 0x03);
            pd.SwcSwitchingFrequency = Ddr5PmicDecoder.DecodeSwitchingFrequency(reg2A & 0x03);
            pd.ShutdownTemperatureThreshold = Ddr5PmicDecoder.DecodeShutdownThreshold(reg2E & 0x07);

            // Status flags
            byte status0 = pd.RawRegisters[REG_STATUS_0];
            byte status1 = pd.RawRegisters[REG_STATUS_1];
            byte status2 = pd.RawRegisters[REG_STATUS_2];
            pd.VinBulkOverVoltage = (status0 & 0x01) != 0;
            pd.SwaPowerGoodFault = (status0 & 0x20) != 0;
            pd.SwbPowerGoodFault = (status0 & 0x08) != 0;
            pd.SwcPowerGoodFault = (status0 & 0x04) != 0;
            pd.HighTemperatureWarning = (status1 & 0x80) != 0;
            pd.CriticalTemperatureShutdown = (status0 & 0x40) != 0;
            pd.PecError = (status2 & 0x08) != 0;
            pd.ParityError = (status2 & 0x04) != 0;

            return pd;
        }
    }
}
