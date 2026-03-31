using System;
using System.Collections.Generic;
using ZenStates.Core.DRAM;

namespace ZenStates.Core
{
    internal static class Ddr5PmicReader
    {
        // PMIC I2C address range

        public const byte PMIC_ADDR_BASE = 0x48;
        public const byte PMIC_ADDR_LAST = 0x4F;
        public const byte SPD_PMIC_OFFSET = 0x08; // SPD_addr - PMIC_addr

        // PMIC5100 Register Offsets (JEDEC JESD301-2)

        // Status
        public const byte REG_STATUS_0 = 0x08;
        public const byte REG_STATUS_1 = 0x09;
        public const byte REG_STATUS_2 = 0x0A;
        public const byte REG_STATUS_3 = 0x0B;

        // Current limiter thresholds
        public const byte REG_CURRENT_LIMIT = 0x20;  // SWA [7:6], SWB [3:2], SWC [1:0]

        // Voltage VID registers (bits [7:1] = 7-bit VID, bit [0] = PG low threshold)
        public const byte REG_SWA_VID = 0x21;  // VDD  = 800 + VID[7:1] x 5 mV
        public const byte REG_SWA_THRESH = 0x22;  // SWA protection thresholds
        public const byte REG_SWB_VID = 0x25;  // VDDQ = 800 + VID[7:1] x 5 mV
        public const byte REG_SWB_THRESH = 0x26;  // SWB protection thresholds
        public const byte REG_SWC_VID = 0x27;  // VPP  = 1500 + VID[7:1] x 5 mV
        public const byte REG_SWC_THRESH = 0x28;  // SWC protection thresholds

        // Rail config / LDO / ADC
        public const byte REG_RAIL_CONFIG_A = 0x29;
        public const byte REG_RAIL_CONFIG_B = 0x2A;
        public const byte REG_LDO_SETTINGS = 0x2B;
        public const byte REG_SHUTDOWN_TEMP = 0x2E;
        public const byte REG_TELEMETRY_SELECT = 0x30;
        public const byte REG_TELEMETRY_VALUE = 0x31;

        // Control
        public const byte REG_VR_ENABLE = 0x32;  // [7]=VR Enable

        // PMIC temperature (coarse)
        public const byte REG_PMIC_TEMP = 0x33;  // [7:5] = temperature code, [2] = VOUT_1.0V PG

        // Revision
        public const byte REG_REVISION = 0x3B;  // [5:4]=major+1, [3:1]=minor

        // Vendor ID (JEP106 with parity)
        public const byte REG_VENDOR_BANK = 0x3C;  // VENDOR_ID_BYTE0
        public const byte REG_VENDOR_CODE = 0x3D;  // VENDOR_ID_BYTE1

        // Persistent NVM settings
        public const byte REG_NVM_LDO_SETTINGS = 0x51;

        // Voltage formulas (JEDEC JESD301-2)────────
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
        private static int DecodeSwabCurrentLimit(int code)
        {
            switch (code)
            {
                case 0: return 3000;
                case 1: return 4000;
                case 2: return 5000;
                default: return 6000;
            }
        }

        private static int DecodeSwcCurrentLimit(int code)
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
        private static string DecodeOvThreshold(int code)
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
        private static string DecodeUvThreshold(int code)
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
        private static string DecodePmicTemp(int code)
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

        private static string DecodeShutdownThreshold(int code)
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

        private static string DecodeModeSelect(int code)
        {
            switch (code & 0x03)
            {
                case 2: return "COT; DCM";
                case 3: return "COT; Forced CCM";
                default: return "Reserved";
            }
        }

        private static string DecodeSwitchingFrequency(int code)
        {
            switch (code & 0x03)
            {
                case 0: return "750 KHz";
                default: return "Vendor specific";
            }
        }

        private static int DecodeLdo18Mv(byte reg)
        {
            switch ((reg >> 6) & 0x03)
            {
                case 0: return 1700;
                case 1: return 1800;
                case 2: return 1900;
                default: return 2000;
            }
        }

        private static int DecodeLdo10Mv(byte reg)
        {
            switch ((reg >> 1) & 0x03)
            {
                case 0: return 900;
                case 1: return 1000;
                case 2: return 1100;
                default: return 1200;
            }
        }

        private static string DecodeAdcInput(int code)
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

        private static string DecodeAdcUpdateFrequency(int code)
        {
            switch (code & 0x03)
            {
                case 0: return "1 ms";
                case 1: return "2 ms";
                case 2: return "4 ms";
                default: return "8 ms";
            }
        }

        private static int DecodeAdcMv(int adcSelect, byte raw)
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
        private static string LookupPmicVendor(byte bank, byte code)
        {
            return ManufacturerMapping.Lookup(bank, code);
        }

        // SMBus helpers
        private static bool ReadReg(SmbusPiix4 smbus, byte addr, byte reg, out byte val)
        {
            return smbus.ReadByteData(addr, reg, out val);
        }

        private static bool ReadRegNoLock(SmbusPiix4 smbus, byte addr, byte reg, out byte val)
        {
            return smbus.ReadByteDataNoLock(addr, reg, out val);
        }

        private static bool WriteRegNoLock(SmbusPiix4 smbus, byte addr, byte reg, byte val)
        {
            return smbus.WriteByteDataNoLock(addr, reg, val);
        }

        public static void ReadAdcVoltage(SmbusPiix4 smbus, byte pmicAddr, byte selectCode, out int mv)
        {
            mv = 0;

            byte reg30 = (byte)(0x80 | ((selectCode & 0x0F) << 3));
            //byte raw;
            //if (!ReadReg(smbus, pmicAddr, REG_TELEMETRY_SELECT, out raw))
            //    return;

            smbus.WriteByteData(pmicAddr, REG_TELEMETRY_SELECT, reg30);
            // Program ADC select; host should wait 9 ms per JESD301-2.
            // Sleep is handled in the PawnIO module
            //System.Threading.Thread.Sleep(10);

            if (!ReadReg(smbus, pmicAddr, REG_TELEMETRY_VALUE, out byte raw))
                return;

            mv = DecodeAdcMv(selectCode, raw);
        }

        private static bool ReadAdcVoltageNoLock(SmbusPiix4 smbus, byte pmicAddr, byte selectCode, out int mv)
        {
            mv = 0;

            byte reg30 = (byte)(0x80 | ((selectCode & 0x0F) << 3));
            byte raw;

            // Program ADC select; host should wait 9 ms per JESD301-2.
            if (!WriteRegNoLock(smbus, pmicAddr, REG_TELEMETRY_SELECT, reg30))
                return false;

            System.Threading.Thread.Sleep(10);

            // First read may still be previous/stale sample after mux switch.
            if (!ReadRegNoLock(smbus, pmicAddr, REG_TELEMETRY_VALUE, out raw))
                return false;

            // Small extra delay and read again for the settled value.
            System.Threading.Thread.Sleep(1);

            if (!ReadRegNoLock(smbus, pmicAddr, REG_TELEMETRY_VALUE, out raw))
                return false;

            mv = DecodeAdcMv(selectCode, raw);
            return true;
        }

        internal static void ReadAllAdcVoltagesNoLock(SmbusPiix4 smbus, byte pmicAddr, Ddr5PmicData pd)
        {
            byte originalReg30 = 0;

            ReadRegNoLock(smbus, pmicAddr, REG_TELEMETRY_SELECT, out originalReg30);

            try
            {
                ReadAdcVoltageNoLock(smbus, pmicAddr, 0x5, out pd.VinBulkMv);
                ReadAdcVoltageNoLock(smbus, pmicAddr, 0x0, out pd.SwaAdcMv);
                ReadAdcVoltageNoLock(smbus, pmicAddr, 0x2, out pd.SwbAdcMv);
                ReadAdcVoltageNoLock(smbus, pmicAddr, 0x3, out pd.SwcAdcMv);
                ReadAdcVoltageNoLock(smbus, pmicAddr, 0x8, out pd.Vout18AdcMv);
                ReadAdcVoltageNoLock(smbus, pmicAddr, 0x9, out pd.Vout10AdcMv);
            }
            finally
            {
                WriteRegNoLock(smbus, pmicAddr, REG_TELEMETRY_SELECT, originalReg30);
            }
        }

        // Detection

        /// <summary>Derive PMIC I2C address from SPD hub address.</summary>
        public static byte CalculatePmicAddrFromSpd(byte spdAddr)
        {
            return (byte)(spdAddr - SPD_PMIC_OFFSET);
        }

        /// <summary>Check if a PMIC responds at the given I2C address.</summary>
        public static bool Detect(SmbusPiix4 smbus, byte pmicAddr)
        {
            try
            {
                if (!ReadReg(smbus, pmicAddr, REG_VENDOR_BANK, out byte bank)) return false;
                if (!ReadReg(smbus, pmicAddr, REG_VENDOR_CODE, out byte code)) return false;
                return code != 0x00 && code != 0xFF && bank != 0xFF;
            }
            catch
            {
                return false;
            }
        }

        // Full PMIC read

        /// <summary>Read all accessible PMIC registers and decode per JEDEC JESD301-2.</summary>
        public static Ddr5PmicData ReadAll(SmbusPiix4 smbus, byte pmicAddr)
        {
            Ddr5PmicData pd = new Ddr5PmicData();
            pd.I2cAddress = pmicAddr;
            pd.SpdHubAddress = (byte)(pmicAddr + SPD_PMIC_OFFSET);

            try
            {
                // Dump first 0x52 registers so we can decode LDO NVM defaults too.
                pd.RawRegisters = new byte[0x52];
                if (!Mutexes.WaitSmbus(5000))
                    throw new TimeoutException("Timeout waiting for SMBus mutex.");

                try
                {
                    for (int i = 0; i < pd.RawRegisters.Length; i++)
                    {
                        byte val;
                        if (ReadRegNoLock(smbus, pmicAddr, (byte)i, out val))
                            pd.RawRegisters[i] = val;
                        else
                            pd.RawRegisters[i] = 0xFF;
                    }

                    // Best-effort live ADC reads for all documented voltage channels.
                    ReadAllAdcVoltagesNoLock(smbus, pmicAddr, pd);
                }
                finally
                {
                    Mutexes.ReleaseSmbus();
                }

                // Vendor ID (R0x3C:R0x3D — JEP106 with parity)
                pd.VendorBank = pd.RawRegisters[REG_VENDOR_BANK];
                pd.VendorCode = pd.RawRegisters[REG_VENDOR_CODE];
                pd.VendorName = LookupPmicVendor(pd.VendorBank, pd.VendorCode);

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
                pd.AdcSelectedInput = DecodeAdcInput((reg30 >> 3) & 0x0F);
                pd.AdcUpdateFrequency = DecodeAdcUpdateFrequency(reg30 & 0x03);
                pd.TelemetryReportsPower = (reg1B & 0x40) != 0;
                pd.TelemetryReportsTotalPower = (reg1A & 0x02) != 0;

                // PMIC temperature (R0x33 [7:5]) and LDO PG
                byte reg33 = pd.RawRegisters[REG_PMIC_TEMP];
                pd.PmicTemperature = DecodePmicTemp((reg33 >> 5) & 0x07);
                pd.Vout10PowerGood = (reg33 & 0x04) == 0;

                // LDO runtime / NVM defaults
                pd.Vout18SettingMv = DecodeLdo18Mv(pd.RawRegisters[REG_LDO_SETTINGS]);
                pd.Vout10SettingMv = DecodeLdo10Mv(pd.RawRegisters[REG_LDO_SETTINGS]);
                pd.Vout18SettingNvmMv = DecodeLdo18Mv(pd.RawRegisters[REG_NVM_LDO_SETTINGS]);
                pd.Vout10SettingNvmMv = DecodeLdo10Mv(pd.RawRegisters[REG_NVM_LDO_SETTINGS]);

                // Main rail voltage decode
                byte swaVid = pd.RawRegisters[REG_SWA_VID];
                byte swbVid = pd.RawRegisters[REG_SWB_VID];
                byte swcVid = pd.RawRegisters[REG_SWC_VID];
                pd.VddMv = SwabVid7ToMv(swaVid);
                pd.VddqMv = SwabVid7ToMv(swbVid);
                pd.VppMv = SwcVid7ToMv(swcVid);
                pd.VddMv8bit = SwabVid8ToMv(swaVid);
                pd.VddqMv8bit = SwabVid8ToMv(swbVid);
                pd.VppMv8bit = SwcVid8ToMv(swcVid);

                // Telemetry raw registers
                pd.SwaTelemetryRaw = pd.RawRegisters[0x0C];
                pd.SwbTelemetryRaw = pd.RawRegisters[0x0E] & 0x3F;
                pd.SwcTelemetryRaw = pd.RawRegisters[0x0F] & 0x3F;

                // Current limiter
                byte clim = pd.RawRegisters[REG_CURRENT_LIMIT];
                pd.SwaCurrentLimitMa = DecodeSwabCurrentLimit((clim >> 6) & 0x03);
                pd.SwbCurrentLimitMa = DecodeSwabCurrentLimit((clim >> 2) & 0x03);
                pd.SwcCurrentLimitMa = DecodeSwcCurrentLimit(clim & 0x03);

                // Protection thresholds
                byte swaThresh = pd.RawRegisters[REG_SWA_THRESH];
                byte swbThresh = pd.RawRegisters[REG_SWB_THRESH];
                byte swcThresh = pd.RawRegisters[REG_SWC_THRESH];
                pd.VddOvThreshold = DecodeOvThreshold((swaThresh >> 4) & 0x03);
                pd.VddUvThreshold = DecodeUvThreshold((swaThresh >> 2) & 0x03);
                pd.VddqOvThreshold = DecodeOvThreshold((swbThresh >> 4) & 0x03);
                pd.VddqUvThreshold = DecodeUvThreshold((swbThresh >> 2) & 0x03);
                pd.VppOvThreshold = DecodeOvThreshold((swcThresh >> 4) & 0x03);
                pd.VppUvThreshold = DecodeUvThreshold((swcThresh >> 2) & 0x03);

                // Rail config
                byte reg29 = pd.RawRegisters[REG_RAIL_CONFIG_A];
                byte reg2A = pd.RawRegisters[REG_RAIL_CONFIG_B];
                byte reg2E = pd.RawRegisters[REG_SHUTDOWN_TEMP];
                pd.SwaMode = DecodeModeSelect((reg29 >> 6) & 0x03);
                pd.SwaSwitchingFrequency = DecodeSwitchingFrequency((reg29 >> 4) & 0x03);
                pd.SwbMode = DecodeModeSelect((reg2A >> 6) & 0x03);
                pd.SwbSwitchingFrequency = DecodeSwitchingFrequency((reg2A >> 4) & 0x03);
                pd.SwcMode = DecodeModeSelect((reg2A >> 2) & 0x03);
                pd.SwcSwitchingFrequency = DecodeSwitchingFrequency(reg2A & 0x03);
                pd.ShutdownTemperatureThreshold = DecodeShutdownThreshold(reg2E & 0x07);

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

                pd.IsValid = true;
            }
            catch
            {
                pd.IsValid = false;
            }

            return pd;
        }

        /// <summary>Read PMIC data for all detected DIMMs by scanning 0x48-0x4F.</summary>
        public static Dictionary<byte, Ddr5PmicData> ReadAllDimms(SmbusPiix4 smbus)
        {
            Dictionary<byte, Ddr5PmicData> results = new Dictionary<byte, Ddr5PmicData>();

            for (byte addr = PMIC_ADDR_BASE; addr <= PMIC_ADDR_LAST; addr++)
            {
                if (Detect(smbus, addr))
                    results[addr] = ReadAll(smbus, addr);
            }

            return results;
        }
    }
}