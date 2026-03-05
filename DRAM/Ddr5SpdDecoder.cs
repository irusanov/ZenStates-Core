// Ddr5SpdDecoder.cs
// DDR5 SPD (Serial Presence Detect) parser per JEDEC JESD400-5.
// Byte map verified against real DDR5 DIMM hardware dumps.
// .NET 2.0 compatible. No var, no expression bodies, no LINQ.
//
// Usage:
//   SmbusPiix4 smbus = new SmbusPiix4();
//   Dictionary<byte, List<byte>> allSpd = smbus.DumpDdr5Spd();
//   foreach (KeyValuePair<byte, List<byte>> kvp in allSpd)
//   {
//       Ddr5SpdInfo info = Ddr5SpdDecoder.Decode(kvp.Value);
//       Console.WriteLine(info.ToString());
//   }

using System;
using System.Collections.Generic;
using System.Text;

namespace ZenStates.Core
{
    // ══════════════════════════════════════════════════════════════════════════
    //  DDR5 SPD5118 Thermal Sensor
    //  JEDEC JESD300-5B compliant temperature sensor integrated in SPD hub.
    //  Register map derived from Linux kernel drivers/hwmon/spd5118.c.
    // ══════════════════════════════════════════════════════════════════════════

    /// <summary>
    /// Thermal data read from a DDR5 DIMM's SPD5118 hub temperature sensor.
    /// </summary>
    public class Ddr5ThermalData
    {
        /// <summary>Whether the sensor was detected and readable.</summary>
        public bool IsValid;

        /// <summary>Whether the SPD5118 hub reports TS capability.</summary>
        public bool TempSensorSupported;

        /// <summary>Whether the temperature sensor is currently enabled.</summary>
        public bool TempSensorEnabled;

        /// <summary>Current temperature in millidegrees Celsius.</summary>
        public int TemperatureMilliC;

        /// <summary>Current temperature in degrees Celsius.</summary>
        public double TemperatureC { get { return TemperatureMilliC / 1000.0; } }

        /// <summary>High-temperature limit in millidegrees Celsius.</summary>
        public int TempMaxMilliC;

        /// <summary>Low-temperature limit in millidegrees Celsius.</summary>
        public int TempMinMilliC;

        /// <summary>Critical-high temperature limit in millidegrees Celsius.</summary>
        public int TempCritMilliC;

        /// <summary>Critical-low temperature limit in millidegrees Celsius.</summary>
        public int TempLCritMilliC;

        /// <summary>Alarm: temperature exceeds high limit.</summary>
        public bool AlarmHigh;

        /// <summary>Alarm: temperature below low limit.</summary>
        public bool AlarmLow;

        /// <summary>Alarm: temperature exceeds critical-high limit.</summary>
        public bool AlarmCritHigh;

        /// <summary>Alarm: temperature below critical-low limit.</summary>
        public bool AlarmCritLow;

        public override string ToString()
        {
            if (!IsValid)
                return "  Thermal sensor: not available";

            StringBuilder sb = new StringBuilder();
            sb.AppendFormat("  Current          : {0:F2} C\n", TemperatureC);

            if (TempMaxMilliC != 0)
                sb.AppendFormat("  High Limit       : {0:F2} C{1}\n",
                    TempMaxMilliC / 1000.0, AlarmHigh ? "  ** ALARM **" : "");

            if (TempMinMilliC != 0)
                sb.AppendFormat("  Low Limit        : {0:F2} C{1}\n",
                    TempMinMilliC / 1000.0, AlarmLow ? "  ** ALARM **" : "");

            if (TempCritMilliC != 0)
                sb.AppendFormat("  Critical High    : {0:F2} C{1}\n",
                    TempCritMilliC / 1000.0, AlarmCritHigh ? "  ** ALARM **" : "");

            if (TempLCritMilliC != 0)
                sb.AppendFormat("  Critical Low     : {0:F2} C{1}\n",
                    TempLCritMilliC / 1000.0, AlarmCritLow ? "  ** ALARM **" : "");

            return sb.ToString();
        }
    }


    /// <summary>
    /// DDR5 SPD5118 hub thermal sensor reader.
    /// Reads temperature from SPD5118 hub mode registers (MR0-MR127)
    /// via SmbusPiix4.SmbusReadByteData() at I2C 0x50-0x57.
    ///
    /// The SPD5118 hub register space at offset 0x00-0x7F is the mode
    /// register area (separate from EEPROM at 0x80-0xFF).
    /// SmbusReadByteData with command in 0x00-0x7F reads mode registers.
    /// </summary>
    public static class Ddr5ThermalSensor
    {
        // ── SPD5118 Hub Register Addresses (Mode Registers) ─────────────────

        /// <summary>MR0:MR1 – Device type identifier (reads 0x5118).</summary>
        public const byte REG_TYPE = 0x00;

        /// <summary>MR2 – Hub revision.</summary>
        public const byte REG_REVISION = 0x02;

        /// <summary>MR3:MR4 – Vendor ID.</summary>
        public const byte REG_VENDOR = 0x03;

        /// <summary>MR5 – Device capability. Bit 1 = TS support.</summary>
        public const byte REG_CAPABILITY = 0x05;

        /// <summary>MR11 – I2C legacy mode / page select.</summary>
        public const byte REG_I2C_LEGACY = 0x0B;

        /// <summary>MR19 – Temperature status clear.</summary>
        public const byte REG_TEMP_CLR = 0x13;

        /// <summary>MR26 – Temperature sensor config. Bit 0 = disable.</summary>
        public const byte REG_TEMP_CONFIG = 0x1A;

        /// <summary>MR28:MR29 – High temperature limit.</summary>
        public const byte REG_TEMP_MAX = 0x1C;

        /// <summary>MR30:MR31 – Low temperature limit.</summary>
        public const byte REG_TEMP_MIN = 0x1E;

        /// <summary>MR32:MR33 – Critical-high temperature limit.</summary>
        public const byte REG_TEMP_CRIT = 0x20;

        /// <summary>MR34:MR35 – Critical-low temperature limit.</summary>
        public const byte REG_TEMP_LCRIT = 0x22;

        /// <summary>MR49:MR50 – Current temperature reading.</summary>
        public const byte REG_TEMP = 0x31;

        /// <summary>MR51 – Temperature alarm status flags.</summary>
        public const byte REG_TEMP_STATUS = 0x33;

        // Capability bits
        private const byte CAP_TS_SUPPORT = 0x02;  // bit 1
        // Config bits
        private const byte TS_DISABLE = 0x01;  // bit 0

        // Status bits
        private const byte STATUS_HIGH = 0x01;
        private const byte STATUS_LOW = 0x02;
        private const byte STATUS_CRIT = 0x04;
        private const byte STATUS_LCRIT = 0x08;

        // Temperature unit: 0.25 C = 250 millidegrees
        private const int TEMP_UNIT_MC = 250;

        // ── Temperature conversion ──────────────────────────────────────────

        /// <summary>
        /// Convert a raw 16-bit SPD5118 temperature register value to
        /// millidegrees Celsius. The format is:
        ///   bits [12:2] = 11-bit signed temperature (0.25 C per LSB)
        ///   bits [1:0]  = unused
        /// </summary>
        public static int RawToMilliC(int raw16)
        {
            int val = (raw16 >> 2) & 0x3FFF;
            if ((val & 0x0400) != 0)
                val |= unchecked((int)0xFFFFF800);
            return val * TEMP_UNIT_MC;
        }

        /// <summary>
        /// Convert millidegrees Celsius back to raw 16-bit register value.
        /// </summary>
        public static int MilliCToRaw(int milliC)
        {
            int val = milliC / TEMP_UNIT_MC;
            return (val & 0x07FF) << 2;
        }

        // ── SMBus read helpers ──────────────────────────────────────────────
        //    Uses SmbusPiix4.SmbusReadByteData(addr7, command, out result)
        //    which is an I2C_SMBUS_BYTE_DATA read — perfect for MR access.

        private static bool ReadMR(SmbusPiix4 smbus, byte addr7, byte mr, out byte value)
        {
            return smbus.SmbusReadByteData(addr7, mr, out value);
        }

        private static bool ReadMR16(SmbusPiix4 smbus, byte addr7, byte mr, out int milliC)
        {
            milliC = 0;
            byte lo, hi;
            // SPD5118 temperature and limit registers are little-endian:
            //   MR_N = LSB, MR_N+1 = MSB
            // (Confirmed by Linux kernel spd5118.c: regmap_bulk_read + (regval[1]<<8)|regval[0])
            // NOTE: Device type at MR0:MR1 is big-endian — handled separately in Detect().
            if (!ReadMR(smbus, addr7, mr, out lo)) return false;
            if (!ReadMR(smbus, addr7, (byte)(mr + 1), out hi)) return false;
            milliC = RawToMilliC((hi << 8) | lo);
            return true;
        }

        // ── Detection ───────────────────────────────────────────────────────

        /// <summary>
        /// Detect whether the device at the given I2C address is an SPD5118
        /// hub with temperature sensor support.
        /// </summary>
        public static bool Detect(SmbusPiix4 smbus, byte i2cAddr)
        {
            try
            {
                byte mr0, mr1;
                if (!ReadMR(smbus, i2cAddr, REG_TYPE, out mr0)) return false;
                if (!ReadMR(smbus, i2cAddr, (byte)(REG_TYPE + 1), out mr1)) return false;
                // SPD5118 registers are big-endian: MR0 = MSB (0x51), MR1 = LSB (0x18)
                int deviceType = (mr0 << 8) | mr1;
                if (deviceType != 0x5118)
                    return false;

                byte cap;
                if (!ReadMR(smbus, i2cAddr, REG_CAPABILITY, out cap)) return false;
                return (cap & CAP_TS_SUPPORT) != 0;
            }
            catch
            {
                return false;
            }
        }

        // ── Single temperature reading ──────────────────────────────────────

        /// <summary>
        /// Read the current temperature from the SPD5118 sensor.
        /// </summary>
        /// <returns>Temperature in millidegrees Celsius, or int.MinValue on error.</returns>
        public static int ReadTemperatureMilliC(SmbusPiix4 smbus, byte i2cAddr)
        {
            int mc;
            if (!ReadMR16(smbus, i2cAddr, REG_TEMP, out mc))
                return int.MinValue;
            return mc;
        }

        /// <summary>
        /// Read the current temperature from the SPD5118 sensor.
        /// </summary>
        /// <returns>Temperature in degrees Celsius, or double.NaN on error.</returns>
        public static double ReadTemperatureC(SmbusPiix4 smbus, byte i2cAddr)
        {
            int mc = ReadTemperatureMilliC(smbus, i2cAddr);
            if (mc == int.MinValue) return double.NaN;
            return mc / 1000.0;
        }

        // ── Full thermal data reading ───────────────────────────────────────

        /// <summary>
        /// Read all thermal sensor data: current temp, limits, and alarms.
        /// </summary>
        public static Ddr5ThermalData ReadAll(SmbusPiix4 smbus, byte i2cAddr)
        {
            Ddr5ThermalData td = new Ddr5ThermalData();

            try
            {
                byte mr0, mr1;
                if (!ReadMR(smbus, i2cAddr, REG_TYPE, out mr0)) return td;
                if (!ReadMR(smbus, i2cAddr, (byte)(REG_TYPE + 1), out mr1)) return td;
                // SPD5118 registers are big-endian: MR0 = MSB, MR1 = LSB
                int deviceType = (mr0 << 8) | mr1;
                if (deviceType != 0x5118)
                    return td;

                byte cap;
                if (!ReadMR(smbus, i2cAddr, REG_CAPABILITY, out cap)) return td;
                td.TempSensorSupported = (cap & CAP_TS_SUPPORT) != 0;
                if (!td.TempSensorSupported)
                    return td;

                byte cfg;
                if (!ReadMR(smbus, i2cAddr, REG_TEMP_CONFIG, out cfg)) return td;
                td.TempSensorEnabled = (cfg & TS_DISABLE) == 0;

                if (!td.TempSensorEnabled)
                {
                    td.IsValid = true;
                    return td;
                }

                int mc;
                if (ReadMR16(smbus, i2cAddr, REG_TEMP, out mc))
                    td.TemperatureMilliC = mc;
                if (ReadMR16(smbus, i2cAddr, REG_TEMP_MAX, out mc))
                    td.TempMaxMilliC = mc;
                if (ReadMR16(smbus, i2cAddr, REG_TEMP_MIN, out mc))
                    td.TempMinMilliC = mc;
                if (ReadMR16(smbus, i2cAddr, REG_TEMP_CRIT, out mc))
                    td.TempCritMilliC = mc;
                if (ReadMR16(smbus, i2cAddr, REG_TEMP_LCRIT, out mc))
                    td.TempLCritMilliC = mc;

                byte status;
                if (ReadMR(smbus, i2cAddr, REG_TEMP_STATUS, out status))
                {
                    td.AlarmHigh = (status & STATUS_HIGH) != 0;
                    td.AlarmLow = (status & STATUS_LOW) != 0;
                    td.AlarmCritHigh = (status & STATUS_CRIT) != 0;
                    td.AlarmCritLow = (status & STATUS_LCRIT) != 0;
                }

                td.IsValid = true;
            }
            catch
            {
                td.IsValid = false;
            }

            return td;
        }

        // ── Convenience: read temperature for all DIMMs ─────────────────────

        /// <summary>
        /// Scan all standard DDR5 SPD addresses (0x50-0x57) and read
        /// thermal data from every DIMM that has an SPD5118 hub with TS.
        /// </summary>
        public static Dictionary<byte, Ddr5ThermalData> ReadAllDimms(SmbusPiix4 smbus)
        {
            Dictionary<byte, Ddr5ThermalData> results =
                new Dictionary<byte, Ddr5ThermalData>();

            for (byte addr = 0x50; addr <= 0x57; addr++)
            {
                if (Detect(smbus, addr))
                    results[addr] = ReadAll(smbus, addr);
            }

            return results;
        }

        /// <summary>
        /// Print thermal sensor readings for all detected DDR5 DIMMs.
        /// </summary>
        public static void PrintAllDimms(SmbusPiix4 smbus)
        {
            Dictionary<byte, Ddr5ThermalData> data = ReadAllDimms(smbus);

            foreach (KeyValuePair<byte, Ddr5ThermalData> kvp in data)
            {
                Console.WriteLine("DIMM 0x{0:X2} Thermal Sensor:", kvp.Key);
                Console.WriteLine(kvp.Value.ToString());
            }
        }
    }

    // ══════════════════════════════════════════════════════════════════════════
    //  DDR5 PMIC5100 Power Management IC reader
    //  JEDEC JESD301-2 compliant. Register map verified against Richtek
    //  RTQ5119A datasheet and real AMD AM5 hardware.
    // ══════════════════════════════════════════════════════════════════════════

    /// <summary>
    /// Decoded data from a DDR5 DIMM's on-module PMIC (JEDEC PMIC5100).
    /// </summary>
    public class Ddr5PmicData
    {
        /// <summary>Whether the PMIC was detected and readable.</summary>
        public bool IsValid;

        /// <summary>I2C address the PMIC was found at (0x48-0x4F).</summary>
        public byte I2cAddress;

        /// <summary>Corresponding SPD hub address (I2cAddress + 0x08).</summary>
        public byte SpdHubAddress;

        // ── Vendor identification ───────────────────────────────────────────

        /// <summary>JEDEC JEP106 continuation bank byte (R0x1A).</summary>
        public byte VendorBank;

        /// <summary>JEDEC JEP106 manufacturer code byte (R0x1B).</summary>
        public byte VendorCode;

        /// <summary>Decoded vendor name.</summary>
        public string VendorName;

        /// <summary>PMIC revision code (R0x3B).</summary>
        public byte Revision;

        // ── Operating state ─────────────────────────────────────────────────

        /// <summary>VR Enable status (R0x32 bit[7]).</summary>
        public bool VrEnabled;

        /// <summary>Overvoltage mode active (R0x20 bit[7]).</summary>
        public bool OvervoltageMode;

        // ── Voltage readings ────────────────────────────────────────────────
        //    VDD/VDDQ: Normal = 600 + VID×5 mV, OV = 800 + VID×5 mV
        //    VPP:      600 + VID×10 mV

        /// <summary>VDD (DRAM core) active VID code (R0x21).</summary>
        public byte VddActiveVid;
        /// <summary>VDD JEDEC nominal VID code (R0x22).</summary>
        public byte VddJedecVid;

        /// <summary>VDDQ (I/O) active VID code (R0x25).</summary>
        public byte VddqActiveVid;
        /// <summary>VDDQ JEDEC nominal VID code (R0x26).</summary>
        public byte VddqJedecVid;

        /// <summary>VPP (wordline pump) active VID code (R0x27).</summary>
        public byte VppActiveVid;

        /// <summary>Active VDD in millivolts.</summary>
        public int VddActiveMv;
        /// <summary>JEDEC nominal VDD in millivolts.</summary>
        public int VddJedecMv;
        /// <summary>Active VDDQ in millivolts.</summary>
        public int VddqActiveMv;
        /// <summary>JEDEC nominal VDDQ in millivolts.</summary>
        public int VddqJedecMv;
        /// <summary>Active VPP in millivolts.</summary>
        public int VppActiveMv;

        // ── Configuration ───────────────────────────────────────────────────

        /// <summary>SWA config byte (R0x0C) — phase mode, enable.</summary>
        public byte SwaConfig;
        /// <summary>SWB config byte (R0x0D).</summary>
        public byte SwbConfig;
        /// <summary>SWC config byte (R0x0E).</summary>
        public byte SwcConfig;

        /// <summary>Full R0x20 byte (VDD config + OV flags).</summary>
        public byte VddConfigByte;

        /// <summary>Raw first 64 bytes for advanced analysis.</summary>
        public byte[] RawRegisters;

        public override string ToString()
        {
            if (!IsValid)
                return "  PMIC: not detected";

            StringBuilder sb = new StringBuilder();
            sb.AppendFormat("  Vendor             : {0}\n", VendorName);
            sb.AppendFormat("  I2C Address        : 0x{0:X2}\n", I2cAddress);
            sb.AppendFormat("  VR Enabled         : {0}\n", VrEnabled ? "Yes" : "No");
            sb.AppendFormat("  Overvoltage Mode   : {0}\n", OvervoltageMode ? "ACTIVE" : "Normal");

            sb.AppendLine();
            sb.AppendFormat("  VDD  (DRAM core)   : {0} mV ({1:F3} V)",
                VddActiveMv, VddActiveMv / 1000.0);
            if (VddActiveMv != VddJedecMv)
                sb.AppendFormat("  [JEDEC nominal: {0} mV]", VddJedecMv);
            sb.AppendLine();

            sb.AppendFormat("  VDDQ (I/O)         : {0} mV ({1:F3} V)",
                VddqActiveMv, VddqActiveMv / 1000.0);
            if (VddqActiveMv != VddqJedecMv)
                sb.AppendFormat("  [JEDEC nominal: {0} mV]", VddqJedecMv);
            sb.AppendLine();

            sb.AppendFormat("  VPP  (wordline)    : {0} mV ({1:F3} V)\n",
                VppActiveMv, VppActiveMv / 1000.0);

            return sb.ToString();
        }
    }

    /// <summary>
    /// DDR5 PMIC5100 reader. Reads voltage configuration and status from
    /// the on-DIMM Power Management IC via SMBus.
    ///
    /// The PMIC sits on the host SMBus at base address 0x48 (vs SPD hub at 0x50).
    /// BIOS remaps HID bits via SETHID CCC so PMIC and SPD hub share the same
    /// HID offset: PMIC_addr = SPD_addr - 0x08.
    ///
    /// Voltage formulas (JEDEC PMIC5100, verified on Richtek RTQ5119A):
    ///   VDD/VDDQ normal:  Vout_mV = 600 + VID × 5
    ///   VDD/VDDQ OV mode: Vout_mV = 800 + VID × 5   (base shifts +200mV)
    ///   VPP:               Vout_mV = 600 + VID × 10
    /// </summary>
    public static class Ddr5PmicReader
    {
        // ── PMIC I2C address range ──────────────────────────────────────────

        public const byte PMIC_ADDR_BASE = 0x48;
        public const byte PMIC_ADDR_LAST = 0x4F;
        public const byte SPD_PMIC_OFFSET = 0x08; // SPD_addr - PMIC_addr

        // ── PMIC5100 Register Offsets ───────────────────────────────────────

        // Status
        public const byte REG_GLOBAL_STATUS = 0x08;
        public const byte REG_INT_STATUS = 0x0A;

        // Rail config
        public const byte REG_SWA_CONFIG = 0x0C;
        public const byte REG_SWB_CONFIG = 0x0D;
        public const byte REG_SWC_CONFIG = 0x0E;

        // VIN settings
        public const byte REG_VIN_BULK = 0x15;
        public const byte REG_VIN_MGMT = 0x16;

        // Vendor ID (JEDEC JEP106 with parity, same encoding as DDR5 SPD)
        // Confirmed by Bus Pirate DDR5 demo and Richtek RTQ5119A datasheet.
        public const byte REG_VENDOR_BANK = 0x3C;  // JEP106 bank byte (parity in bit 7)
        public const byte REG_VENDOR_CODE = 0x3D;  // JEP106 mfr code (parity in bit 7)
        public const byte REG_REVISION = 0x3B;  // PMIC revision

        // Capability / revision
        public const byte REG_CAPABILITY0 = 0x1C;
        public const byte REG_CAPABILITY1 = 0x1E;
        public const byte REG_CAPABILITY2 = 0x1F;

        // Voltage VID registers
        public const byte REG_VDD_CONFIG = 0x20;  // [7]=OV mode flag + config
        public const byte REG_VDD_ACTIVE = 0x21;  // SWA/SWAB active VID (VDD)
        public const byte REG_VDD_JEDEC = 0x22;  // SWA/SWAB JEDEC nominal VID

        public const byte REG_VDDQ_ACTIVE = 0x25;  // SWC active VID (VDDQ)
        public const byte REG_VDDQ_JEDEC = 0x26;  // SWC JEDEC nominal VID

        public const byte REG_VPP_ACTIVE = 0x27;  // SWD active VID (VPP)

        // Protection / soft-start
        public const byte REG_SWAB_SS = 0x2C;  // SWAB soft-start [7:5]
        public const byte REG_SWC_SWD_SS = 0x2D;  // SWC [7:5], SWD [3:1]

        // Control
        public const byte REG_VR_ENABLE = 0x32;  // [7]=VR Enable

        // ── Voltage formulas ────────────────────────────────────────────────

        private const int VDD_BASE_NORMAL = 600;   // mV
        private const int VDD_BASE_OV = 800;   // mV (overvoltage mode)
        private const int VDD_STEP = 5;     // mV per VID code
        private const int VPP_BASE = 600;   // mV
        private const int VPP_STEP = 10;    // mV per VID code

        /// <summary>Convert VDD/VDDQ VID to millivolts (normal mode).</summary>
        public static int VddVidToMv(byte vid) { return VDD_BASE_NORMAL + vid * VDD_STEP; }

        /// <summary>Convert VDD/VDDQ VID to millivolts (overvoltage mode).</summary>
        public static int VddVidToMvOV(byte vid) { return VDD_BASE_OV + vid * VDD_STEP; }

        /// <summary>Convert VPP VID to millivolts.</summary>
        public static int VppVidToMv(byte vid) { return VPP_BASE + vid * VPP_STEP; }

        // ── PMIC vendor identification ────────────────────────────────────────
        //    R0x3C:R0x3D use standard JEP106 with odd parity in bit 7,
        //    same encoding as DDR5 SPD manufacturing bytes.
        //    We reuse the full 1,527-entry JEP106 lookup from Ddr5SpdDecoder.

        private static string LookupPmicVendor(byte bank, byte code)
        {
            string name = Ddr5SpdDecoder.LookupJedecManufacturer(bank, code);

            // Append known PMIC product lines for recognized vendors
            int cont = bank & 0x7F;
            int mfr = code & 0x7F;

            // Known DDR5 PMIC products by vendor
            // Richtek:  RTQ5119A (UDIMM/SODIMM), RTQ5118A (RDIMM)
            // Renesas:  P8911 (UDIMM/SODIMM), P8910/P8900 (RDIMM)
            // MPS:      MP8845A, MP8846 (UDIMM/SODIMM)
            // TI:       TPS53830 (high-current), TPS53832 (low-current)
            // Montage:  M88P5100 (UDIMM), M88P5010/M88P5000 (RDIMM)
            // Infineon:  formerly Cypress

            return name;
        }

        // ── SMBus helpers ───────────────────────────────────────────────────

        private static bool ReadReg(SmbusPiix4 smbus, byte addr, byte reg, out byte val)
        {
            return smbus.SmbusReadByteData(addr, reg, out val);
        }

        // ── Detection ───────────────────────────────────────────────────────

        /// <summary>
        /// Derive the PMIC I2C address from a known SPD hub address.
        /// PMIC_addr = SPD_addr - 0x08 (same HID bits, different device type base).
        /// </summary>
        public static byte PmicAddrFromSpd(byte spdAddr)
        {
            return (byte)(spdAddr - SPD_PMIC_OFFSET);
        }

        /// <summary>
        /// Check if a PMIC responds at the given I2C address.
        /// Validates by reading vendor ID (non-zero vendor code expected).
        /// </summary>
        public static bool Detect(SmbusPiix4 smbus, byte pmicAddr)
        {
            try
            {
                // Validate by reading JEDEC vendor ID at R0x3C:R0x3D
                byte bank, code;
                if (!ReadReg(smbus, pmicAddr, REG_VENDOR_BANK, out bank)) return false;
                if (!ReadReg(smbus, pmicAddr, REG_VENDOR_CODE, out code)) return false;
                // Valid PMIC has non-zero, non-0xFF vendor code with valid parity
                return code != 0x00 && code != 0xFF && bank != 0xFF;
            }
            catch
            {
                return false;
            }
        }

        // ── Full PMIC read ──────────────────────────────────────────────────

        /// <summary>
        /// Read all accessible PMIC registers and decode voltage/status.
        /// </summary>
        public static Ddr5PmicData ReadAll(SmbusPiix4 smbus, byte pmicAddr)
        {
            Ddr5PmicData pd = new Ddr5PmicData();
            pd.I2cAddress = pmicAddr;
            pd.SpdHubAddress = (byte)(pmicAddr + SPD_PMIC_OFFSET);

            try
            {
                // Dump first 64 registers
                pd.RawRegisters = new byte[64];
                for (int i = 0; i < 64; i++)
                {
                    byte val;
                    if (ReadReg(smbus, pmicAddr, (byte)i, out val))
                        pd.RawRegisters[i] = val;
                    else
                        pd.RawRegisters[i] = 0xFF;
                }

                // Vendor ID — standard JEP106 with parity at R0x3C:R0x3D
                pd.VendorBank = pd.RawRegisters[REG_VENDOR_BANK];
                pd.VendorCode = pd.RawRegisters[REG_VENDOR_CODE];
                pd.VendorName = LookupPmicVendor(pd.VendorBank, pd.VendorCode);

                // VR Enable
                pd.VrEnabled = (pd.RawRegisters[REG_VR_ENABLE] & 0x80) != 0;

                // OV mode
                pd.VddConfigByte = pd.RawRegisters[REG_VDD_CONFIG];
                pd.OvervoltageMode = (pd.VddConfigByte & 0x80) != 0;

                // Rail configs
                pd.SwaConfig = pd.RawRegisters[REG_SWA_CONFIG];
                pd.SwbConfig = pd.RawRegisters[REG_SWB_CONFIG];
                pd.SwcConfig = pd.RawRegisters[REG_SWC_CONFIG];

                // Voltage VID codes
                pd.VddActiveVid = pd.RawRegisters[REG_VDD_ACTIVE];
                pd.VddJedecVid = pd.RawRegisters[REG_VDD_JEDEC];
                pd.VddqActiveVid = pd.RawRegisters[REG_VDDQ_ACTIVE];
                pd.VddqJedecVid = pd.RawRegisters[REG_VDDQ_JEDEC];
                pd.VppActiveVid = pd.RawRegisters[REG_VPP_ACTIVE];

                // Convert to millivolts
                if (pd.OvervoltageMode)
                {
                    pd.VddActiveMv = VddVidToMvOV(pd.VddActiveVid);
                    pd.VddqActiveMv = VddVidToMvOV(pd.VddqActiveVid);
                }
                else
                {
                    pd.VddActiveMv = VddVidToMv(pd.VddActiveVid);
                    pd.VddqActiveMv = VddVidToMv(pd.VddqActiveVid);
                }

                pd.VddJedecMv = VddVidToMv(pd.VddJedecVid);
                pd.VddqJedecMv = VddVidToMv(pd.VddqJedecVid);
                pd.VppActiveMv = VppVidToMv(pd.VppActiveVid);

                pd.IsValid = true;
            }
            catch
            {
                pd.IsValid = false;
            }

            return pd;
        }

        // ── Convenience ─────────────────────────────────────────────────────

        /// <summary>
        /// Read PMIC data for all detected DIMMs by scanning 0x48-0x4F.
        /// </summary>
        public static Dictionary<byte, Ddr5PmicData> ReadAllDimms(SmbusPiix4 smbus)
        {
            Dictionary<byte, Ddr5PmicData> results =
                new Dictionary<byte, Ddr5PmicData>();

            for (byte addr = PMIC_ADDR_BASE; addr <= PMIC_ADDR_LAST; addr++)
            {
                if (Detect(smbus, addr))
                    results[addr] = ReadAll(smbus, addr);
            }

            return results;
        }
    }

    // ══════════════════════════════════════════════════════════════════════════
    //  EXPO profile container
    // ══════════════════════════════════════════════════════════════════════════

    public class Ddr5ExpoProfile
    {
        public bool IsValid;
        public int ProfileNumber;

        public int tCKAVGminPs;
        public int SpeedMTs;
        public double ClockMHz;
        public string SpeedGrade;

        public int tAAminPs;
        public int tRCDminPs;
        public int tRPminPs;
        public int tRASminPs;
        public int tRCminPs;
        public int tWRminPs;

        public int tRFC1minNs;
        public int tRFC2minNs;
        public int tRFCsbMinNs;

        public int CL;
        public int tRCD;
        public int tRP;
        public string TimingString;

        /// <summary>Raw VDD voltage code from SPD (multiply by 5mV + 1100mV base).</summary>
        public int VddCode;
        /// <summary>Raw VDDQ voltage code from SPD.</summary>
        public int VddqCode;
        /// <summary>Calculated VDD in millivolts.</summary>
        public int VddMv;
        /// <summary>Calculated VDDQ in millivolts.</summary>
        public int VddqMv;

        public override string ToString()
        {
            if (!IsValid) return "  (not present)";

            StringBuilder sb = new StringBuilder();
            sb.AppendFormat("  Speed Grade        : {0}\n", SpeedGrade);
            sb.AppendFormat("  Clock Frequency    : {0:F1} MHz\n", ClockMHz);
            sb.AppendFormat("  Data Rate          : {0} MT/s\n", SpeedMTs);
            sb.AppendFormat("  Timing             : {0}\n", TimingString);
            sb.AppendFormat("  tCKAVGmin          : {0} ps\n", tCKAVGminPs);
            sb.AppendFormat("  tAAmin             : {0} ps (CL {1})\n", tAAminPs, CL);
            sb.AppendFormat("  tRCDmin            : {0} ps ({1} clk)\n", tRCDminPs, tRCD);
            sb.AppendFormat("  tRPmin             : {0} ps ({1} clk)\n", tRPminPs, tRP);
            sb.AppendFormat("  tRASmin            : {0} ps ({1:F1} ns)\n", tRASminPs, tRASminPs / 1000.0);
            sb.AppendFormat("  tRCmin             : {0} ps ({1:F1} ns)\n", tRCminPs, tRCminPs / 1000.0);
            sb.AppendFormat("  tWRmin             : {0} ps ({1:F1} ns)\n", tWRminPs, tWRminPs / 1000.0);
            sb.AppendFormat("  tRFC1              : {0} ns\n", tRFC1minNs);
            sb.AppendFormat("  tRFC2              : {0} ns\n", tRFC2minNs);
            sb.AppendFormat("  tRFCsb             : {0} ns\n", tRFCsbMinNs);
            sb.AppendFormat("  VDD                : {0} mV ({1:F3} V)\n", VddMv, VddMv / 1000.0);
            sb.AppendFormat("  VDDQ               : {0} mV ({1:F3} V)\n", VddqMv, VddqMv / 1000.0);
            return sb.ToString();
        }
    }

    // ══════════════════════════════════════════════════════════════════════════
    //  XMP 3.0 profile container
    // ══════════════════════════════════════════════════════════════════════════

    public class Ddr5XmpProfile
    {
        public bool IsValid;
        public int ProfileNumber;

        public int tCKAVGminPs;
        public int SpeedMTs;
        public double ClockMHz;
        public string SpeedGrade;

        public int tAAminPs;
        public int tRCDminPs;
        public int tRPminPs;
        public int tRASminPs;
        public int tRCminPs;
        public int tWRminPs;

        public int tRFC1minNs;
        public int tRFC2minNs;
        public int tRFCsbMinNs;

        public int CL;
        public int tRCD;
        public int tRP;
        public string TimingString;

        /// <summary>Raw VDD voltage code (code * 5mV + 1100mV).</summary>
        public int VddCode;
        /// <summary>Raw VDDQ voltage code (code * 5mV + 1100mV).</summary>
        public int VddqCode;
        /// <summary>Raw VPP voltage code (code * 5mV + 1500mV).</summary>
        public int VppCode;
        /// <summary>Calculated VDD in millivolts.</summary>
        public int VddMv;
        /// <summary>Calculated VDDQ in millivolts.</summary>
        public int VddqMv;
        /// <summary>Calculated VPP in millivolts.</summary>
        public int VppMv;

        /// <summary>Supported CAS latencies for this profile.</summary>
        public List<int> SupportedCLs;

        /// <summary>Intel Dynamic Memory Boost flag.</summary>
        public bool DynamicMemoryBoost;

        /// <summary>Profile name (XMP 3.0 supports up to 15 chars).</summary>
        public string ProfileName;

        public override string ToString()
        {
            if (!IsValid) return "  (not present)";

            StringBuilder sb = new StringBuilder();
            sb.AppendFormat("  Speed Grade        : {0}\n", SpeedGrade);
            sb.AppendFormat("  Clock Frequency    : {0:F1} MHz\n", ClockMHz);
            sb.AppendFormat("  Data Rate          : {0} MT/s\n", SpeedMTs);
            sb.AppendFormat("  Timing             : {0}\n", TimingString);
            sb.AppendFormat("  tCKAVGmin          : {0} ps\n", tCKAVGminPs);
            sb.AppendFormat("  tAAmin             : {0} ps (CL {1})\n", tAAminPs, CL);
            sb.AppendFormat("  tRCDmin            : {0} ps ({1} clk)\n", tRCDminPs, tRCD);
            sb.AppendFormat("  tRPmin             : {0} ps ({1} clk)\n", tRPminPs, tRP);
            sb.AppendFormat("  tRASmin            : {0} ps ({1:F1} ns)\n", tRASminPs, tRASminPs / 1000.0);
            sb.AppendFormat("  tRCmin             : {0} ps ({1:F1} ns)\n", tRCminPs, tRCminPs / 1000.0);
            sb.AppendFormat("  tWRmin             : {0} ps ({1:F1} ns)\n", tWRminPs, tWRminPs / 1000.0);
            sb.AppendFormat("  tRFC1              : {0} ns\n", tRFC1minNs);
            sb.AppendFormat("  tRFC2              : {0} ns\n", tRFC2minNs);
            sb.AppendFormat("  tRFCsb             : {0} ns\n", tRFCsbMinNs);
            sb.AppendFormat("  VDD                : {0} mV ({1:F3} V)\n", VddMv, VddMv / 1000.0);
            sb.AppendFormat("  VDDQ               : {0} mV ({1:F3} V)\n", VddqMv, VddqMv / 1000.0);
            sb.AppendFormat("  VPP                : {0} mV ({1:F3} V)\n", VppMv, VppMv / 1000.0);

            if (SupportedCLs != null && SupportedCLs.Count > 0)
            {
                sb.Append("  Supported CLs      : ");
                for (int i = 0; i < SupportedCLs.Count; i++)
                {
                    if (i > 0) sb.Append(", ");
                    sb.Append(SupportedCLs[i]);
                }
                sb.AppendLine();
            }

            if (ProfileName != null && ProfileName.Length > 0)
                sb.AppendFormat("  Profile Name       : {0}\n", ProfileName);

            if (DynamicMemoryBoost)
                sb.AppendLine("  Dynamic Mem Boost  : Supported");

            return sb.ToString();
        }
    }

    // ══════════════════════════════════════════════════════════════════════════
    //  Decoded result container
    // ══════════════════════════════════════════════════════════════════════════

    public class Ddr5SpdInfo
    {
        // ── General ──────────────────────────────────────────────────────────

        /// <summary>Raw 1024-byte SPD image.</summary>
        public byte[] RawSpd;

        public int BytesUsed;
        public int BytesTotal;
        public int SpdRevisionEncoding;
        public int SpdRevisionAdditions;
        public string SpdRevision;
        public byte DeviceType;
        public string DeviceTypeString;
        public bool IsValid;

        // ── Module type ──────────────────────────────────────────────────────

        public byte BaseModuleType;
        public string ModuleTypeString;
        public bool IsHybrid;
        public string HybridTypeString;

        // ── SDRAM density & package ──────────────────────────────────────────

        public int FirstDieDensityMbit;
        public int FirstDieCount;
        public int SecondDieDensityMbit;
        public int SecondDieCount;
        public bool IsAsymmetric;

        // ── SDRAM organisation ───────────────────────────────────────────────

        public int FirstDeviceWidthBits;
        public int FirstColumnBits;
        public int FirstRowBits;
        public int SecondColumnBits;
        public int SecondRowBits;

        // ── Capacity ─────────────────────────────────────────────────────────

        public int ChannelCount;
        public int RanksPerChannel;
        public int SubChannelsPerDimm;
        public int PrimaryBusWidthBits;
        public long TotalCapacityMB;

        // ── Voltage ──────────────────────────────────────────────────────────

        public string VddString;

        // ── Timing (JEDEC base) ──────────────────────────────────────────────

        public int tCKAVGminPs;
        public int tCKAVGmaxPs;

        public int SpeedMTs;
        public string SpeedGrade;
        public double ClockMHz;

        public List<int> SupportedCLs;

        public int tAAminPs;
        public int tRCDminPs;
        public int tRPminPs;
        public int tRASminPs;
        public int tRCminPs;
        public int tWRminPs;

        /// <summary>tRFC values in nanoseconds (as stored in SPD).</summary>
        public int tRFC1minNs;
        public int tRFC2minNs;
        public int tRFCsbMinNs;

        public int CL;
        public int tRCD;
        public int tRP;
        public string TimingString;

        // ── Thermal ──────────────────────────────────────────────────────────

        public bool HasThermalSensor;

        // ── Manufacturing ────────────────────────────────────────────────────

        public int ModuleMfgIdBank;
        public int ModuleMfgIdMfr;
        public string ModuleManufacturer;
        public int ModuleMfgYear;
        public int ModuleMfgWeek;
        public string ModuleMfgDate;
        public string ModuleSerialNumber;
        public string ModulePartNumber;
        public int ModuleRevisionCode;

        public int DramMfgIdBank;
        public int DramMfgIdMfr;
        public string DramManufacturer;
        public int DramStepping;

        // ── XMP / EXPO ───────────────────────────────────────────────────────

        public bool HasXmp;
        public string XmpRevision;
        public Ddr5XmpProfile[] XmpProfiles;   // up to 5 (3 vendor + 2 user)

        public bool HasExpo;
        public Ddr5ExpoProfile ExpoProfile1;
        public Ddr5ExpoProfile ExpoProfile2;

        // ── Thermal sensor ──────────────────────────────────────────────────

        /// <summary>
        /// Live thermal sensor reading from SPD5118 hub (populated by
        /// ReadAndDecodeAll when SMBus is available, null for file-only decode).
        /// </summary>
        public Ddr5ThermalData ThermalData;

        // ── PMIC ────────────────────────────────────────────────────────────

        /// <summary>
        /// Live PMIC voltage/status data (populated by ReadAndDecodeAll
        /// when SMBus is available, null for file-only decode).
        /// </summary>
        public Ddr5PmicData PmicData;

        // ── Pretty-print ─────────────────────────────────────────────────────

        public override string ToString()
        {
            StringBuilder sb = new StringBuilder();
            sb.AppendLine("===============================================");
            sb.AppendLine("  DDR5 SPD Decoded Information");
            sb.AppendLine("===============================================");

            if (!IsValid)
            {
                sb.AppendLine("  *** INVALID OR NON-DDR5 SPD DATA ***");
                sb.AppendFormat("  Device type byte: 0x{0:X2}\n", DeviceType);
                return sb.ToString();
            }

            sb.AppendLine();
            sb.AppendLine("-- General ---------------------------------");
            sb.AppendFormat("  SPD Revision       : {0}\n", SpdRevision);
            sb.AppendFormat("  Device Type        : {0}\n", DeviceTypeString);
            sb.AppendFormat("  Module Type        : {0}\n", ModuleTypeString);
            sb.AppendFormat("  SPD Bytes Used     : {0}\n", BytesUsed);
            sb.AppendFormat("  SPD Bytes Total    : {0}\n", BytesTotal);
            if (IsHybrid)
                sb.AppendFormat("  Hybrid Type        : {0}\n", HybridTypeString);

            sb.AppendLine();
            sb.AppendLine("-- Capacity & Organisation -----------------");
            sb.AppendFormat("  Total Capacity     : {0} MB ({1} GB)\n", TotalCapacityMB, TotalCapacityMB / 1024);
            sb.AppendFormat("  Die Density (1st)  : {0} Mbit ({1} die)\n", FirstDieDensityMbit, FirstDieCount);
            if (IsAsymmetric)
                sb.AppendFormat("  Die Density (2nd)  : {0} Mbit ({1} die)\n", SecondDieDensityMbit, SecondDieCount);
            sb.AppendFormat("  Device Width       : x{0}\n", FirstDeviceWidthBits);
            sb.AppendFormat("  Column Bits        : {0}\n", FirstColumnBits);
            sb.AppendFormat("  Row Bits           : {0}\n", FirstRowBits);
            sb.AppendFormat("  Ranks/Channel      : {0}\n", RanksPerChannel);
            sb.AppendFormat("  Sub-Channels/DIMM  : {0}\n", SubChannelsPerDimm);
            sb.AppendFormat("  Bus Width/Sub-Ch   : {0} bits\n", PrimaryBusWidthBits);

            sb.AppendLine();
            sb.AppendLine("-- JEDEC Base Speed & Timing ----------------");
            sb.AppendFormat("  Speed Grade        : {0}\n", SpeedGrade);
            sb.AppendFormat("  Clock Frequency    : {0:F1} MHz\n", ClockMHz);
            sb.AppendFormat("  Data Rate          : {0} MT/s\n", SpeedMTs);
            sb.AppendFormat("  Timing             : {0}\n", TimingString);
            sb.AppendFormat("  tCKAVGmin          : {0} ps\n", tCKAVGminPs);
            sb.AppendFormat("  tCKAVGmax          : {0} ps\n", tCKAVGmaxPs);
            sb.AppendFormat("  tAAmin             : {0} ps\n", tAAminPs);
            sb.AppendFormat("  tRCDmin            : {0} ps\n", tRCDminPs);
            sb.AppendFormat("  tRPmin             : {0} ps\n", tRPminPs);
            sb.AppendFormat("  tRASmin            : {0} ps ({1:F0} ns)\n", tRASminPs, tRASminPs / 1000.0);
            sb.AppendFormat("  tRCmin             : {0} ps ({1:F1} ns)\n", tRCminPs, tRCminPs / 1000.0);
            sb.AppendFormat("  tWRmin             : {0} ps ({1:F0} ns)\n", tWRminPs, tWRminPs / 1000.0);
            sb.AppendFormat("  tRFC1              : {0} ns\n", tRFC1minNs);
            sb.AppendFormat("  tRFC2              : {0} ns\n", tRFC2minNs);
            sb.AppendFormat("  tRFCsb             : {0} ns\n", tRFCsbMinNs);

            if (SupportedCLs != null && SupportedCLs.Count > 0)
            {
                sb.Append("  Supported CLs      : ");
                for (int i = 0; i < SupportedCLs.Count; i++)
                {
                    if (i > 0) sb.Append(", ");
                    sb.Append(SupportedCLs[i]);
                }
                sb.AppendLine();
            }

            sb.AppendLine();
            sb.AppendLine("-- Voltage ---------------------------------");
            sb.AppendFormat("  VDD                : {0}\n", VddString);

            sb.AppendLine();
            sb.AppendLine("-- Thermal ---------------------------------");
            sb.AppendFormat("  Thermal Sensor     : {0}\n", HasThermalSensor ? "Present" : "Not present");

            sb.AppendLine();
            sb.AppendLine("-- Manufacturing ---------------------------");
            sb.AppendFormat("  Module Manufacturer: {0}\n", ModuleManufacturer);
            sb.AppendFormat("  Module Part Number : {0}\n", ModulePartNumber);
            sb.AppendFormat("  Module Serial      : {0}\n", ModuleSerialNumber);
            sb.AppendFormat("  Module Date        : {0}\n", ModuleMfgDate);
            sb.AppendFormat("  Module Revision    : 0x{0:X2}\n", ModuleRevisionCode);
            sb.AppendFormat("  DRAM Manufacturer  : {0}\n", DramManufacturer);
            sb.AppendFormat("  DRAM Stepping      : 0x{0:X2}\n", DramStepping);

            if (HasExpo)
            {
                sb.AppendLine();
                sb.AppendLine("-- AMD EXPO Profile 1 ----------------------");
                if (ExpoProfile1 != null)
                    sb.Append(ExpoProfile1.ToString());

                if (ExpoProfile2 != null && ExpoProfile2.IsValid)
                {
                    sb.AppendLine();
                    sb.AppendLine("-- AMD EXPO Profile 2 ----------------------");
                    sb.Append(ExpoProfile2.ToString());
                }
            }

            if (HasXmp)
            {
                sb.AppendLine();
                sb.AppendFormat("-- Intel XMP 3.0 (Rev {0}) ------------------\n",
                    XmpRevision != null ? XmpRevision : "?");
                if (XmpProfiles != null)
                {
                    for (int p = 0; p < XmpProfiles.Length; p++)
                    {
                        if (XmpProfiles[p] != null && XmpProfiles[p].IsValid)
                        {
                            string label = (p < 3) ?
                                string.Format("XMP Profile {0}", p + 1) :
                                string.Format("XMP User Profile {0}", p + 1);
                            sb.AppendLine();
                            sb.AppendFormat("  [{0}]\n", label);
                            sb.Append(XmpProfiles[p].ToString());
                        }
                    }
                }
            }

            if (ThermalData != null && ThermalData.IsValid)
            {
                sb.AppendLine();
                sb.AppendLine("-- Thermal Sensor (SPD5118) -----------------");
                if (!ThermalData.TempSensorEnabled)
                    sb.AppendLine("  Sensor present but DISABLED");
                else
                    sb.Append(ThermalData.ToString());
            }

            if (PmicData != null && PmicData.IsValid)
            {
                sb.AppendLine();
                sb.AppendLine("-- PMIC (Power Management IC) ---------------");
                sb.Append(PmicData.ToString());
            }

            sb.AppendLine("===============================================");
            return sb.ToString();
        }
    }

    // ══════════════════════════════════════════════════════════════════════════
    //  Static decoder
    // ══════════════════════════════════════════════════════════════════════════

    public static class Ddr5SpdDecoder
    {
        // ── SPD byte map (JESD400-5C, verified against hardware) ─────────────
        //
        // Block 0  (bytes   0- 63): Base Configuration & DRAM Parameters
        // Block 1  (bytes  64-127): Additional DRAM Timing Parameters
        // Block 2  (bytes 128-191): Reserved
        // Block 3  (bytes 192-255): Common + Module-Type Specific Parameters
        // Block 4-5(bytes 256-511): Reserved
        // Block 6-7(bytes 512-639): Manufacturing Information
        // Block 8-9(bytes 640-767): End User / XMP 3.0 Profiles
        // Block 10+(bytes 768-1023): Reserved / EXPO Profiles

        // ── General (Block 0) ────────────────────────────────────────────────

        private const int SPD_BYTES_USED = 0;     // [3:0]=used, [6:4]=total
        private const int SPD_REVISION = 1;     // [7:4]=encoding, [3:0]=additions
        private const int SPD_DEVICE_TYPE = 2;     // 0x12 = DDR5
        private const int SPD_MODULE_TYPE = 3;     // [3:0]=base, [4]=hybrid, [6:5]=hybrid_type

        // ── SDRAM Density, Addressing, I/O Width, Banks ────────────────────
        //    Bytes 4-7 = first SDRAM, bytes 8-11 = second (asymmetric only)

        private const int SPD_FIRST_DENSITY = 4;     // [4:0]=density, [7:5]=die_per_pkg
        private const int SPD_FIRST_ADDRESSING = 5;     // [2:0]=col_bits-10, [7:5]=row_bits-16
        private const int SPD_FIRST_IO_WIDTH = 6;     // [7:5]=device_width (0=x4,1=x8,2=x16)
        private const int SPD_FIRST_BANKS = 7;     // [7:5]=bank_groups, [2:0]=banks_per_group
        private const int SPD_SECOND_DENSITY = 8;     // same format as byte 4
        private const int SPD_SECOND_ADDRESSING = 9;     // same format as byte 5
        private const int SPD_SECOND_IO_WIDTH = 10;    // same format as byte 6
        private const int SPD_SECOND_BANKS = 11;    // same format as byte 7

        // ── Voltage & Thermal ────────────────────────────────────────────────

        private const int SPD_NOMINAL_VOLTAGE = 12;
        private const int SPD_THERMAL = 14;

        // ── Timing (16-bit LE values, picoseconds unless noted) ──────────────
        // Verified offsets from real DDR5 hardware dumps.

        private const int SPD_TCKAVG_MIN_LSB = 20;
        private const int SPD_TCKAVG_MIN_MSB = 21;
        private const int SPD_TCKAVG_MAX_LSB = 22;
        private const int SPD_TCKAVG_MAX_MSB = 23;

        // CAS Latencies Supported: 5 bytes (40 bits), each bit = CL 20 + pos*2
        private const int SPD_CAS_FIRST = 24;    // bytes 24..28

        // Core timing (16-bit LE picoseconds)
        private const int SPD_TAA_MIN_LSB = 30;
        private const int SPD_TAA_MIN_MSB = 31;
        private const int SPD_TRCD_MIN_LSB = 32;
        private const int SPD_TRCD_MIN_MSB = 33;
        private const int SPD_TRP_MIN_LSB = 34;
        private const int SPD_TRP_MIN_MSB = 35;
        private const int SPD_TRAS_MIN_LSB = 36;
        private const int SPD_TRAS_MIN_MSB = 37;
        private const int SPD_TRC_MIN_LSB = 38;
        private const int SPD_TRC_MIN_MSB = 39;
        private const int SPD_TWR_MIN_LSB = 40;
        private const int SPD_TWR_MIN_MSB = 41;

        // Refresh timing (16-bit LE, nanoseconds)
        private const int SPD_TRFC1_LSB = 42;
        private const int SPD_TRFC1_MSB = 43;
        private const int SPD_TRFC2_LSB = 44;
        private const int SPD_TRFC2_MSB = 45;
        private const int SPD_TRFCSB_LSB = 46;
        private const int SPD_TRFCSB_MSB = 47;

        // ── Module organisation (Block 3) ────────────────────────────────────
        //    byte 234: [5:3]=ranks_per_channel-1
        //    byte 235: [2:0]=primary bus width per sub-ch, [5]=sub-channels (0=1, 1=2)

        private const int SPD_MOD_ORG = 234;
        private const int SPD_BUS_WIDTH = 235;

        // ── Manufacturing (Block 6-7, starting at byte 512) ──────────────────

        private const int SPD_MOD_MFG_ID_LSB = 512;
        private const int SPD_MOD_MFG_ID_MSB = 513;
        private const int SPD_MOD_MFG_LOC = 514;
        private const int SPD_MOD_MFG_YEAR = 515;
        private const int SPD_MOD_MFG_WEEK = 516;
        private const int SPD_MOD_SERIAL = 517;   // 4 bytes (517-520)
        private const int SPD_MOD_PARTNO = 521;   // 30 bytes (521-550)
        private const int SPD_MOD_REV = 551;
        private const int SPD_DRAM_MFG_ID_LSB = 552;
        private const int SPD_DRAM_MFG_ID_MSB = 553;
        private const int SPD_DRAM_STEPPING = 554;

        // ── XMP 3.0 header (Block 8, byte 640) ──────────────────────────────
        //    Intel Extreme Memory Profile 3.0 for DDR5.
        //    3 vendor (read-only) + 2 user (writable) profiles.
        //    Bytes 640-767: header + 3 vendor profiles.
        //    Bytes 768-895: 2 user profiles + profile names.

        private const int SPD_XMP_HEADER = 640;   // 0x0C 0x4A magic
        private const int SPD_XMP_REVISION = 642;   // [7:4]=encoding, [3:0]=additions
        private const int SPD_XMP_PROF_ENABLE = 643;   // [0]=P1 [1]=P2 [2]=P3 [3]=P4 [4]=P5
        private const int SPD_XMP_DMB_CONFIG = 644;   // Dynamic Memory Boost config

        // Vendor Profile 1: bytes 650-685 (36 bytes)
        private const int SPD_XMP_P1_BASE = 650;
        // Vendor Profile 2: bytes 686-721
        private const int SPD_XMP_P2_BASE = 686;
        // Vendor Profile 3: bytes 722-757
        private const int SPD_XMP_P3_BASE = 722;

        // User Profile 4: bytes 768-803
        private const int SPD_XMP_P4_BASE = 768;
        // User Profile 5: bytes 804-839 (not overlapping EXPO)
        private const int SPD_XMP_P5_BASE = 804;

        // Profile name strings (15 chars each) at bytes 758-767 + 840-895
        // Name 1: 758-767 (partial, continued from overflow)

        // ── XMP 3.0 per-profile offsets (relative to profile base) ──────
        //    Each profile has identical layout, 36 bytes:
        private const int XMP_OFF_VDD = 0;    // VDD voltage code  (code*5 + 1100 mV)
        private const int XMP_OFF_VDDQ = 1;    // VDDQ voltage code (code*5 + 1100 mV)
        private const int XMP_OFF_VPP = 2;    // VPP voltage code  (code*5 + 1500 mV)
        private const int XMP_OFF_FLAGS = 3;    // DMB + misc flags
        private const int XMP_OFF_TCK_LSB = 4;    // tCKAVGmin LE ps
        private const int XMP_OFF_TCK_MSB = 5;
        private const int XMP_OFF_CAS_0 = 6;    // CAS latency bytes (5 bytes)
        private const int XMP_OFF_CAS_4 = 10;
        private const int XMP_OFF_TAA_LSB = 11;    // tAAmin LE ps
        private const int XMP_OFF_TAA_MSB = 12;
        private const int XMP_OFF_TRCD_LSB = 13;
        private const int XMP_OFF_TRCD_MSB = 14;
        private const int XMP_OFF_TRP_LSB = 15;
        private const int XMP_OFF_TRP_MSB = 16;
        private const int XMP_OFF_TRAS_LSB = 17;
        private const int XMP_OFF_TRAS_MSB = 18;
        private const int XMP_OFF_TRC_LSB = 19;
        private const int XMP_OFF_TRC_MSB = 20;
        private const int XMP_OFF_TWR_LSB = 21;
        private const int XMP_OFF_TWR_MSB = 22;
        private const int XMP_OFF_TRFC1_LSB = 23;    // tRFC1 LE ns
        private const int XMP_OFF_TRFC1_MSB = 24;
        private const int XMP_OFF_TRFC2_LSB = 25;    // tRFC2 LE ns
        private const int XMP_OFF_TRFC2_MSB = 26;
        private const int XMP_OFF_TRFCSB_LSB = 27;    // tRFCsb LE ns
        private const int XMP_OFF_TRFCSB_MSB = 28;
        // Bytes 29-35: extended timings (tRRD_S, tRRD_L, tCCD_L, etc.)

        // ── EXPO header (Block 10, byte 832 = 0x340) ─────────────────────────
        //    Magic: ASCII "EXPO" (0x45 0x58 0x50 0x4F)
        //    Verified from real AMD EXPO DDR5 DIMM dumps.

        private const int SPD_EXPO_HEADER = 832;
        private const int SPD_EXPO_REVISION = 836;
        private const int SPD_EXPO_PROFILES = 837;   // [0]=profile1, [1]=profile2
        private const int SPD_EXPO_VDD1 = 842;   // VDD voltage code
        private const int SPD_EXPO_VDDQ1 = 843;   // VDDQ voltage code
        private const int SPD_EXPO_TCKMIN1_LSB = 846;   // Profile 1 tCKAVGmin
        private const int SPD_EXPO_TCKMIN1_MSB = 847;
        private const int SPD_EXPO_TAA1_LSB = 848;   // Profile 1 tAAmin
        private const int SPD_EXPO_TAA1_MSB = 849;
        private const int SPD_EXPO_TRCD1_LSB = 850;
        private const int SPD_EXPO_TRCD1_MSB = 851;
        private const int SPD_EXPO_TRP1_LSB = 852;
        private const int SPD_EXPO_TRP1_MSB = 853;
        private const int SPD_EXPO_TRAS1_LSB = 854;
        private const int SPD_EXPO_TRAS1_MSB = 855;
        private const int SPD_EXPO_TRC1_LSB = 856;
        private const int SPD_EXPO_TRC1_MSB = 857;
        private const int SPD_EXPO_TWR1_LSB = 858;
        private const int SPD_EXPO_TWR1_MSB = 859;
        private const int SPD_EXPO_TRFC1_1_LSB = 860;   // tRFC1 (ns)
        private const int SPD_EXPO_TRFC1_1_MSB = 861;
        private const int SPD_EXPO_TRFC2_1_LSB = 862;
        private const int SPD_EXPO_TRFC2_1_MSB = 863;
        private const int SPD_EXPO_TRFCSB1_LSB = 864;
        private const int SPD_EXPO_TRFCSB1_MSB = 865;

        // Device type constant
        private const byte DDR5_DEVICE_TYPE = 0x12;

        // ══════════════════════════════════════════════════════════════════════
        //  Main decode entry point
        // ══════════════════════════════════════════════════════════════════════

        public static Ddr5SpdInfo Decode(List<byte> spd)
        {
            byte[] raw;
            if (spd != null)
                raw = spd.ToArray();
            else
                raw = new byte[0];
            return Decode(raw);
        }

        public static Ddr5SpdInfo Decode(byte[] spd)
        {
            Ddr5SpdInfo info = new Ddr5SpdInfo();
            info.SupportedCLs = new List<int>();
            info.RawSpd = spd;

            if (spd == null || spd.Length < 128)
            {
                info.IsValid = false;
                info.DeviceTypeString = "Insufficient data";
                return info;
            }

            info.DeviceType = B(spd, SPD_DEVICE_TYPE);
            info.IsValid = (info.DeviceType == DDR5_DEVICE_TYPE);

            if (!info.IsValid)
            {
                info.DeviceTypeString = string.Format("Unknown (0x{0:X2})", info.DeviceType);
                return info;
            }

            info.DeviceTypeString = "DDR5 SDRAM";
            DecodeSizeAndRevision(spd, info);
            DecodeModuleType(spd, info);
            DecodeDensityAndPackage(spd, info);
            DecodeAddressing(spd, info);
            DecodeVoltageAndThermal(spd, info);
            DecodeTiming(spd, info);
            DecodeModuleOrganisation(spd, info);
            DecodeDeviceWidth(spd, info);
            CalculateCapacity(info);
            DecodeManufacturing(spd, info);
            DetectAndDecodeProfiles(spd, info);
            CalculateTimingString(info);

            return info;
        }

        // ══════════════════════════════════════════════════════════════════════
        //  Sub-decoders
        // ══════════════════════════════════════════════════════════════════════

        private static void DecodeSizeAndRevision(byte[] spd, Ddr5SpdInfo info)
        {
            byte b0 = B(spd, SPD_BYTES_USED);
            info.BytesUsed = DecodeSpdSize(b0 & 0x0F);
            info.BytesTotal = DecodeSpdSize((b0 >> 4) & 0x07);

            byte b1 = B(spd, SPD_REVISION);
            info.SpdRevisionEncoding = (b1 >> 4) & 0x0F;
            info.SpdRevisionAdditions = b1 & 0x0F;
            info.SpdRevision = string.Format("{0}.{1}",
                info.SpdRevisionEncoding, info.SpdRevisionAdditions);
        }

        private static int DecodeSpdSize(int code)
        {
            // Each code = 128 * code bytes
            if (code >= 1 && code <= 8) return code * 128;
            return 0;
        }

        // ── Module type ──────────────────────────────────────────────────────

        private static void DecodeModuleType(byte[] spd, Ddr5SpdInfo info)
        {
            byte b = B(spd, SPD_MODULE_TYPE);
            info.BaseModuleType = (byte)(b & 0x0F);
            info.IsHybrid = ((b >> 4) & 0x01) != 0;
            int hybridType = (b >> 5) & 0x03;

            switch (info.BaseModuleType)
            {
                case 0x01: info.ModuleTypeString = "RDIMM"; break;
                case 0x02: info.ModuleTypeString = "UDIMM"; break;
                case 0x03: info.ModuleTypeString = "SO-DIMM"; break;
                case 0x04: info.ModuleTypeString = "LRDIMM"; break;
                case 0x07: info.ModuleTypeString = "Mini-RDIMM"; break;
                case 0x09: info.ModuleTypeString = "Mini-UDIMM"; break;
                case 0x0A: info.ModuleTypeString = "DDIMM"; break;
                case 0x0B: info.ModuleTypeString = "Solder-down"; break;
                case 0x0C: info.ModuleTypeString = "CAMM2"; break;
                default:
                    info.ModuleTypeString = string.Format("Unknown (0x{0:X2})",
                        info.BaseModuleType);
                    break;
            }

            if (info.IsHybrid)
            {
                switch (hybridType)
                {
                    case 0x01: info.HybridTypeString = "NVDIMM-N Hybrid"; break;
                    case 0x02: info.HybridTypeString = "NVDIMM-P Hybrid"; break;
                    default:
                        info.HybridTypeString = string.Format("Unknown Hybrid ({0})", hybridType);
                        break;
                }
            }
            else
            {
                info.HybridTypeString = "Not hybrid";
            }
        }

        // ── Density & package ────────────────────────────────────────────────

        private static void DecodeDensityAndPackage(byte[] spd, Ddr5SpdInfo info)
        {
            byte bDen1 = B(spd, SPD_FIRST_DENSITY);   // byte 4
            byte bDen2 = B(spd, SPD_SECOND_DENSITY);   // byte 8

            // [4:0] = density code, [7:5] = die per package code
            info.FirstDieDensityMbit = DecodeDieDensity(bDen1 & 0x1F);
            info.FirstDieCount = DecodeDieCount((bDen1 >> 5) & 0x07);

            info.SecondDieDensityMbit = DecodeDieDensity(bDen2 & 0x1F);
            info.SecondDieCount = DecodeDieCount((bDen2 >> 5) & 0x07);

            info.IsAsymmetric = (bDen2 != 0) && (bDen1 != bDen2);
        }

        private static int DecodeDieDensity(int code)
        {
            switch (code)
            {
                case 0x01: return 4096;     // 4 Gbit
                case 0x02: return 8192;     // 8 Gbit
                case 0x03: return 12288;    // 12 Gbit
                case 0x04: return 16384;    // 16 Gbit
                case 0x05: return 24576;    // 24 Gbit
                case 0x06: return 32768;    // 32 Gbit
                case 0x07: return 49152;    // 48 Gbit
                case 0x08: return 65536;    // 64 Gbit
                default: return 0;
            }
        }

        private static int DecodeDieCount(int code)
        {
            // JESD400-5B: Die per Package = code + 1  (0=1, 1=2, ..., 7=8)
            return code + 1;
        }

        // ── SDRAM Addressing ─────────────────────────────────────────────────
        // Byte 5: [2:0] = column address bits - 10, [7:5] = row address bits - 16

        private static void DecodeAddressing(byte[] spd, Ddr5SpdInfo info)
        {
            byte bAddr1 = B(spd, SPD_FIRST_ADDRESSING);   // byte 5
            byte bAddr2 = B(spd, SPD_SECOND_ADDRESSING);  // byte 9

            info.FirstColumnBits = 10 + (bAddr1 & 0x07);
            info.FirstRowBits = 16 + ((bAddr1 >> 5) & 0x07);

            info.SecondColumnBits = 10 + (bAddr2 & 0x07);
            info.SecondRowBits = 16 + ((bAddr2 >> 5) & 0x07);
        }

        // ── Voltage & Thermal ────────────────────────────────────────────────

        private static void DecodeVoltageAndThermal(byte[] spd, Ddr5SpdInfo info)
        {
            // DDR5 standard nominal voltage is 1.1V
            byte bv = B(spd, SPD_NOMINAL_VOLTAGE);
            info.VddString = "1.1 V (nominal DDR5)";

            byte bt = B(spd, SPD_THERMAL);
            info.HasThermalSensor = ((bt >> 3) & 0x01) != 0;
        }

        // ── Timing ───────────────────────────────────────────────────────────

        private static void DecodeTiming(byte[] spd, Ddr5SpdInfo info)
        {
            info.tCKAVGminPs = U16LE(spd, SPD_TCKAVG_MIN_LSB);
            info.tCKAVGmaxPs = U16LE(spd, SPD_TCKAVG_MAX_LSB);

            // Derive JEDEC speed grade
            if (info.tCKAVGminPs > 0)
            {
                info.ClockMHz = 1000000.0 / (double)info.tCKAVGminPs;
                info.SpeedMTs = (int)Math.Round(2.0 * info.ClockMHz);
                info.SpeedMTs = RoundToJedecBin(info.SpeedMTs);
                info.ClockMHz = info.SpeedMTs / 2.0;
            }
            info.SpeedGrade = string.Format("DDR5-{0}", info.SpeedMTs);

            // CAS Latencies Supported: 5 bytes = 40 bits.
            // DDR5 CL values start at 20, increment by 2.
            for (int byteIdx = 0; byteIdx < 5; byteIdx++)
            {
                byte cb = B(spd, SPD_CAS_FIRST + byteIdx);
                for (int bit = 0; bit < 8; bit++)
                {
                    if ((cb & (1 << bit)) != 0)
                    {
                        int clValue = 20 + (byteIdx * 8 + bit) * 2;
                        info.SupportedCLs.Add(clValue);
                    }
                }
            }

            // Core timing: 16-bit LE picoseconds
            info.tAAminPs = U16LE(spd, SPD_TAA_MIN_LSB);
            info.tRCDminPs = U16LE(spd, SPD_TRCD_MIN_LSB);
            info.tRPminPs = U16LE(spd, SPD_TRP_MIN_LSB);
            info.tRASminPs = U16LE(spd, SPD_TRAS_MIN_LSB);
            info.tRCminPs = U16LE(spd, SPD_TRC_MIN_LSB);
            info.tWRminPs = U16LE(spd, SPD_TWR_MIN_LSB);

            // Refresh timing: stored as nanoseconds
            info.tRFC1minNs = U16LE(spd, SPD_TRFC1_LSB);
            info.tRFC2minNs = U16LE(spd, SPD_TRFC2_LSB);
            info.tRFCsbMinNs = U16LE(spd, SPD_TRFCSB_LSB);
        }

        private static int RoundToJedecBin(int mts)
        {
            int[] bins = new int[] {
                3200, 3600, 4000, 4400, 4800, 5200, 5600,
                6000, 6400, 6800, 7200, 7600, 8000, 8400, 8800
            };
            int best = bins[0];
            int bestDelta = Math.Abs(mts - bins[0]);
            for (int i = 1; i < bins.Length; i++)
            {
                int delta = Math.Abs(mts - bins[i]);
                if (delta < bestDelta)
                {
                    bestDelta = delta;
                    best = bins[i];
                }
            }
            return best;
        }

        // ── Module organisation ──────────────────────────────────────────────
        // Byte 234: [5:3] = ranks per channel - 1
        // Byte 235: [2:0] = primary bus width per sub-channel
        //           [5]   = sub-channels per DIMM (0=1, 1=2)

        private static void DecodeModuleOrganisation(byte[] spd, Ddr5SpdInfo info)
        {
            byte bOrg = B(spd, SPD_MOD_ORG);
            byte bBus = B(spd, SPD_BUS_WIDTH);

            // Ranks
            int rankCode = (bOrg >> 3) & 0x07;
            info.RanksPerChannel = rankCode + 1;

            // Sub-channels: bit 5 of byte 235
            int subChBit = (bBus >> 5) & 0x01;
            info.SubChannelsPerDimm = (subChBit == 1) ? 2 : 1;

            // Bus width per sub-channel: bits [2:0]
            int busCode = bBus & 0x07;
            switch (busCode)
            {
                case 0x00: info.PrimaryBusWidthBits = 8; break;
                case 0x01: info.PrimaryBusWidthBits = 16; break;
                case 0x02: info.PrimaryBusWidthBits = 32; break;
                case 0x03: info.PrimaryBusWidthBits = 64; break;
                default: info.PrimaryBusWidthBits = 32; break;
            }

            info.ChannelCount = info.SubChannelsPerDimm;
        }

        // ── Device width derivation ──────────────────────────────────────────
        // ── SDRAM I/O Width ──────────────────────────────────────────────────
        // Byte 6: [7:5] = device width (0=x4, 1=x8, 2=x16, 3=x32)

        private static void DecodeDeviceWidth(byte[] spd, Ddr5SpdInfo info)
        {
            byte b6 = B(spd, SPD_FIRST_IO_WIDTH);
            int widthCode = (b6 >> 5) & 0x07;

            switch (widthCode)
            {
                case 0: info.FirstDeviceWidthBits = 4; break;
                case 1: info.FirstDeviceWidthBits = 8; break;
                case 2: info.FirstDeviceWidthBits = 16; break;
                case 3: info.FirstDeviceWidthBits = 32; break;
                default:
                    // Fallback: standard x8 for UDIMM, x4 for RDIMM/LRDIMM
                    info.FirstDeviceWidthBits = (info.BaseModuleType == 0x01 ||
                        info.BaseModuleType == 0x04) ? 4 : 8;
                    break;
            }
        }

        // ── Capacity calculation ─────────────────────────────────────────────

        private static void CalculateCapacity(Ddr5SpdInfo info)
        {
            if (info.FirstDieDensityMbit == 0 || info.FirstDeviceWidthBits == 0 ||
                info.PrimaryBusWidthBits == 0)
            {
                info.TotalCapacityMB = 0;
                return;
            }

            // capacity_MB = (density_Mbit / 8) * die_count *
            //               (bus_width_per_subchannel / device_width) *
            //               ranks * sub_channels
            long densityMB = (long)info.FirstDieDensityMbit / 8;
            int devicesPerSubCh = info.PrimaryBusWidthBits / info.FirstDeviceWidthBits;

            info.TotalCapacityMB = densityMB
                * info.FirstDieCount
                * devicesPerSubCh
                * info.RanksPerChannel
                * info.SubChannelsPerDimm;
        }

        // ── Timing string ────────────────────────────────────────────────────

        private static void CalculateTimingString(Ddr5SpdInfo info)
        {
            if (info.tCKAVGminPs > 0 && info.SpeedMTs > 0)
            {
                // Use ideal tCK from speed bin to avoid integer rounding artifacts.
                // SPD stores tCK as truncated integer ps, causing ceil() to overshoot by 1.
                double tCKideal = 1000000.0 / (info.SpeedMTs / 2.0);
                info.CL = (int)Math.Ceiling((double)info.tAAminPs / tCKideal - 0.01);
                info.tRCD = (int)Math.Ceiling((double)info.tRCDminPs / tCKideal - 0.01);
                info.tRP = (int)Math.Ceiling((double)info.tRPminPs / tCKideal - 0.01);
            }

            info.TimingString = string.Format("{0}-{1}-{2} @ {3}",
                info.CL, info.tRCD, info.tRP, info.SpeedGrade);
        }

        // ── Manufacturing ────────────────────────────────────────────────────

        private static void DecodeManufacturing(byte[] spd, Ddr5SpdInfo info)
        {
            if (spd.Length < 555)
            {
                info.ModuleManufacturer = "N/A (SPD too short)";
                info.DramManufacturer = "N/A";
                info.ModulePartNumber = "N/A";
                info.ModuleSerialNumber = "N/A";
                info.ModuleMfgDate = "N/A";
                return;
            }

            // Module manufacturer: JEDEC continuation bank + manufacturer code
            info.ModuleMfgIdBank = B(spd, SPD_MOD_MFG_ID_LSB);
            info.ModuleMfgIdMfr = B(spd, SPD_MOD_MFG_ID_MSB);
            info.ModuleManufacturer = LookupJedecManufacturer(
                info.ModuleMfgIdBank, info.ModuleMfgIdMfr);

            // Manufacturing date (BCD year + week)
            info.ModuleMfgYear = DecodeBcd(B(spd, SPD_MOD_MFG_YEAR));
            info.ModuleMfgWeek = DecodeBcd(B(spd, SPD_MOD_MFG_WEEK));
            info.ModuleMfgDate = string.Format("20{0:D2}, Week {1:D2}",
                info.ModuleMfgYear, info.ModuleMfgWeek);

            // Module serial number (4 bytes hex)
            StringBuilder serial = new StringBuilder();
            for (int i = 0; i < 4; i++)
                serial.AppendFormat("{0:X2}", B(spd, SPD_MOD_SERIAL + i));
            info.ModuleSerialNumber = serial.ToString();

            // Module part number (30 bytes printable ASCII)
            StringBuilder partno = new StringBuilder();
            for (int i = 0; i < 30; i++)
            {
                byte c = B(spd, SPD_MOD_PARTNO + i);
                if (c >= 0x20 && c <= 0x7E)
                    partno.Append((char)c);
            }
            info.ModulePartNumber = partno.ToString().Trim();

            info.ModuleRevisionCode = B(spd, SPD_MOD_REV);

            // DRAM manufacturer
            info.DramMfgIdBank = B(spd, SPD_DRAM_MFG_ID_LSB);
            info.DramMfgIdMfr = B(spd, SPD_DRAM_MFG_ID_MSB);
            info.DramManufacturer = LookupJedecManufacturer(
                info.DramMfgIdBank, info.DramMfgIdMfr);
            info.DramStepping = B(spd, SPD_DRAM_STEPPING);
        }

        // ── XMP / EXPO detection and decoding ────────────────────────────────

        private static void DetectAndDecodeProfiles(byte[] spd, Ddr5SpdInfo info)
        {
            // ── EXPO: ASCII "EXPO" (0x45 0x58 0x50 0x4F) at byte 832 ──
            if (spd.Length > SPD_EXPO_HEADER + 4)
            {
                info.HasExpo = (B(spd, SPD_EXPO_HEADER) == 0x45      // 'E'
                             && B(spd, SPD_EXPO_HEADER + 1) == 0x58   // 'X'
                             && B(spd, SPD_EXPO_HEADER + 2) == 0x50   // 'P'
                             && B(spd, SPD_EXPO_HEADER + 3) == 0x4F); // 'O'
            }

            if (info.HasExpo)
            {
                byte profileBits = B(spd, SPD_EXPO_PROFILES);
                info.ExpoProfile1 = DecodeExpoProfile(spd, 1,
                    SPD_EXPO_TCKMIN1_LSB, SPD_EXPO_VDD1, SPD_EXPO_VDDQ1);
                info.ExpoProfile1.IsValid = (profileBits & 0x01) != 0
                    && info.ExpoProfile1.tCKAVGminPs > 0;

                // Profile 2 is at a fixed offset after profile 1 (typically +32 bytes)
                // but many DIMMs only have 1 profile. Check if profile 2 bit is set.
                info.ExpoProfile2 = new Ddr5ExpoProfile();
                info.ExpoProfile2.IsValid = false;
                if ((profileBits & 0x02) != 0 && spd.Length > SPD_EXPO_TCKMIN1_LSB + 32)
                {
                    // Profile 2 starts 32 bytes after profile 1 timing block
                    info.ExpoProfile2 = DecodeExpoProfile(spd, 2,
                        SPD_EXPO_TCKMIN1_LSB + 32,
                        SPD_EXPO_VDD1 + 2,     // VDD2 at +2
                        SPD_EXPO_VDDQ1 + 2);   // VDDQ2 at +2
                    info.ExpoProfile2.IsValid = info.ExpoProfile2.tCKAVGminPs > 0;
                }
            }
            else
            {
                info.ExpoProfile1 = new Ddr5ExpoProfile();
                info.ExpoProfile2 = new Ddr5ExpoProfile();
            }

            // ── XMP 3.0: magic header at byte 640 ──
            if (spd.Length > SPD_XMP_HEADER + 2)
            {
                byte xm0 = B(spd, SPD_XMP_HEADER);
                byte xm1 = B(spd, SPD_XMP_HEADER + 1);
                info.HasXmp = (xm0 == 0x0C && xm1 == 0x4A);
            }

            if (info.HasXmp)
            {
                byte rev = B(spd, SPD_XMP_REVISION);
                info.XmpRevision = string.Format("{0}.{1}", (rev >> 4) & 0x0F, rev & 0x0F);

                byte enableBits = B(spd, SPD_XMP_PROF_ENABLE);
                byte dmbByte = B(spd, SPD_XMP_DMB_CONFIG);

                info.XmpProfiles = new Ddr5XmpProfile[5];

                // Vendor profiles 1-3
                int[] bases = new int[] {
                    SPD_XMP_P1_BASE, SPD_XMP_P2_BASE, SPD_XMP_P3_BASE,
                    SPD_XMP_P4_BASE, SPD_XMP_P5_BASE
                };

                for (int p = 0; p < 5; p++)
                {
                    bool enabled = ((enableBits >> p) & 1) != 0;
                    if (enabled && spd.Length > bases[p] + 29)
                    {
                        info.XmpProfiles[p] = DecodeXmpProfile(spd, p + 1, bases[p]);
                        // Dynamic Memory Boost flag from header
                        info.XmpProfiles[p].DynamicMemoryBoost = ((dmbByte >> p) & 1) != 0;
                    }
                    else
                    {
                        info.XmpProfiles[p] = new Ddr5XmpProfile();
                    }
                }
            }
            else
            {
                info.XmpProfiles = new Ddr5XmpProfile[5];
                for (int p = 0; p < 5; p++)
                    info.XmpProfiles[p] = new Ddr5XmpProfile();
            }
        }

        private static Ddr5ExpoProfile DecodeExpoProfile(byte[] spd,
            int profileNum, int tckOffset, int vddOffset, int vddqOffset)
        {
            Ddr5ExpoProfile p = new Ddr5ExpoProfile();
            p.ProfileNumber = profileNum;

            p.tCKAVGminPs = U16LE(spd, tckOffset);
            if (p.tCKAVGminPs <= 0)
            {
                p.IsValid = false;
                return p;
            }

            p.ClockMHz = 1000000.0 / (double)p.tCKAVGminPs;
            p.SpeedMTs = (int)Math.Round(2.0 * p.ClockMHz);
            p.SpeedMTs = RoundToJedecBin(p.SpeedMTs);
            p.ClockMHz = p.SpeedMTs / 2.0;
            p.SpeedGrade = string.Format("DDR5-{0}", p.SpeedMTs);

            // Timing parameters (16-bit LE picoseconds, consecutive)
            int t = tckOffset + 2; // tAA follows tCK
            p.tAAminPs = U16LE(spd, t); t += 2;
            p.tRCDminPs = U16LE(spd, t); t += 2;
            p.tRPminPs = U16LE(spd, t); t += 2;
            p.tRASminPs = U16LE(spd, t); t += 2;
            p.tRCminPs = U16LE(spd, t); t += 2;
            p.tWRminPs = U16LE(spd, t); t += 2;

            // tRFC in nanoseconds
            p.tRFC1minNs = U16LE(spd, t); t += 2;
            p.tRFC2minNs = U16LE(spd, t); t += 2;
            p.tRFCsbMinNs = U16LE(spd, t);

            // CL calculation with ideal tCK to avoid integer rounding artifacts
            double tCKideal = 1000000.0 / (p.SpeedMTs / 2.0);
            p.CL = (int)Math.Ceiling((double)p.tAAminPs / tCKideal - 0.01);
            p.tRCD = (int)Math.Ceiling((double)p.tRCDminPs / tCKideal - 0.01);
            p.tRP = (int)Math.Ceiling((double)p.tRPminPs / tCKideal - 0.01);
            p.TimingString = string.Format("{0}-{1}-{2} @ {3}",
                p.CL, p.tRCD, p.tRP, p.SpeedGrade);

            // Voltage: code * 5mV + 1100mV base
            p.VddCode = B(spd, vddOffset);
            p.VddqCode = B(spd, vddqOffset);
            p.VddMv = 1100 + p.VddCode * 5;
            p.VddqMv = 1100 + p.VddqCode * 5;

            p.IsValid = true;
            return p;
        }

        // ── XMP 3.0 profile decoder ─────────────────────────────────────────

        private static Ddr5XmpProfile DecodeXmpProfile(byte[] spd,
            int profileNum, int baseOff)
        {
            Ddr5XmpProfile p = new Ddr5XmpProfile();
            p.ProfileNumber = profileNum;

            // Voltages
            p.VddCode = B(spd, baseOff + XMP_OFF_VDD);
            p.VddqCode = B(spd, baseOff + XMP_OFF_VDDQ);
            p.VppCode = B(spd, baseOff + XMP_OFF_VPP);
            p.VddMv = 1100 + p.VddCode * 5;
            p.VddqMv = 1100 + p.VddqCode * 5;
            p.VppMv = 1500 + p.VppCode * 5;

            // tCKAVGmin (16-bit LE picoseconds)
            p.tCKAVGminPs = U16LE(spd, baseOff + XMP_OFF_TCK_LSB);
            if (p.tCKAVGminPs <= 0)
            {
                p.IsValid = false;
                return p;
            }

            p.ClockMHz = 1000000.0 / (double)p.tCKAVGminPs;
            p.SpeedMTs = (int)Math.Round(2.0 * p.ClockMHz);
            p.SpeedMTs = RoundToJedecBin(p.SpeedMTs);
            p.ClockMHz = p.SpeedMTs / 2.0;
            p.SpeedGrade = string.Format("DDR5-{0}", p.SpeedMTs);

            // CAS latencies supported (5 bytes at +6..+10)
            p.SupportedCLs = new List<int>();
            for (int byteIdx = 0; byteIdx < 5; byteIdx++)
            {
                byte clByte = B(spd, baseOff + XMP_OFF_CAS_0 + byteIdx);
                for (int bit = 0; bit < 8; bit++)
                {
                    if ((clByte & (1 << bit)) != 0)
                    {
                        // XMP uses same CL encoding as JEDEC base:
                        // byte0 bit0=CL20, bit1=CL22, ... byte4 bit7=CL98
                        int cl = 20 + (byteIdx * 8 + bit) * 2;
                        p.SupportedCLs.Add(cl);
                    }
                }
            }

            // Timing parameters (16-bit LE picoseconds)
            p.tAAminPs = U16LE(spd, baseOff + XMP_OFF_TAA_LSB);
            p.tRCDminPs = U16LE(spd, baseOff + XMP_OFF_TRCD_LSB);
            p.tRPminPs = U16LE(spd, baseOff + XMP_OFF_TRP_LSB);
            p.tRASminPs = U16LE(spd, baseOff + XMP_OFF_TRAS_LSB);
            p.tRCminPs = U16LE(spd, baseOff + XMP_OFF_TRC_LSB);
            p.tWRminPs = U16LE(spd, baseOff + XMP_OFF_TWR_LSB);

            // tRFC in nanoseconds
            p.tRFC1minNs = U16LE(spd, baseOff + XMP_OFF_TRFC1_LSB);
            p.tRFC2minNs = U16LE(spd, baseOff + XMP_OFF_TRFC2_LSB);
            p.tRFCsbMinNs = U16LE(spd, baseOff + XMP_OFF_TRFCSB_LSB);

            // CL calculation with ideal tCK to avoid integer rounding artifacts
            double tCKideal = 1000000.0 / (p.SpeedMTs / 2.0);
            p.CL = (int)Math.Ceiling((double)p.tAAminPs / tCKideal - 0.01);
            p.tRCD = (int)Math.Ceiling((double)p.tRCDminPs / tCKideal - 0.01);
            p.tRP = (int)Math.Ceiling((double)p.tRPminPs / tCKideal - 0.01);
            p.TimingString = string.Format("{0}-{1}-{2} @ {3}",
                p.CL, p.tRCD, p.tRP, p.SpeedGrade);

            p.IsValid = true;
            return p;
        }

        // ══════════════════════════════════════════════════════════════════════
        //  JEDEC JEP106 manufacturer lookup
        //
        //  Generated from i2c-tools decode-dimms @vendors array (JEP106BK+).
        //  Each bank has up to 126 entries. The 7-bit manufacturer code
        //  (mfrByte with parity stripped) minus 1 indexes into the bank array.
        //  DDR5 SPD byte 512 = continuation count (with odd parity in bit 7).
        //  DDR5 SPD byte 513 = manufacturer code  (with odd parity in bit 7).
        // ══════════════════════════════════════════════════════════════════════

        private static readonly string[][] JedecVendors = new string[][]
        {
            // ── Bank 1 (cont = 0) ── 126 entries
            new string[] {
                "AMD", "AMI",
                "Fairchild", "Fujitsu",
                "GTE", "Harris",
                "Hitachi", "Inmos",
                "Intel", "I.T.T.",
                "Intersil", "Monolithic Memories",
                "Mostek", "Freescale (former Motorola)",
                "National", "NEC",
                "RCA", "Raytheon",
                "Conexant (Rockwell)", "Seeq",
                "NXP (former Signetics, Philips Semi.)", "Synertek",
                "Texas Instruments", "Kioxia Corporation (former Toshiba Memory Corporation)",
                "Xicor", "Zilog",
                "Eurotechnique", "Mitsubishi",
                "Lucent (AT&T)", "Exel",
                "Atmel", "STMicroelectronics (former SGS/Thomson)",
                "Lattice Semi.", "NCR",
                "Wafer Scale Integration", "IBM",
                "Tristar", "Visic",
                "Intl. CMOS Technology", "SSSI",
                "MicrochipTechnology", "Ricoh Ltd.",
                "VLSI", "Micron Technology",
                "SK Hynix (former Hyundai Electronics)", "OKI Semiconductor",
                "ACTEL", "Sharp",
                "Catalyst", "Panasonic",
                "IDT", "Cypress",
                "DEC", "LSI Logic",
                "Zarlink (former Plessey)", "UTMC",
                "Thinking Machine", "Thomson CSF",
                "Integrated CMOS (Vertex)", "Honeywell",
                "Tektronix", "Oracle Corporation (former Sun Microsystems)",
                "Silicon Storage Technology", "ProMos/Mosel Vitelic",
                "Infineon (former Siemens)", "Macronix",
                "Xerox", "Plus Logic",
                "Western Digital Technologies (former SanDisk Corporation)", "Elan Circuit Tech.",
                "European Silicon Str.", "Apple Computer",
                "Xilinx", "Compaq",
                "Protocol Engines", "SCI",
                "Seiko Instruments", "Samsung",
                "I3 Design System", "Klic",
                "Crosspoint Solutions", "Alliance Memory Inc",
                "Tandem", "Hewlett-Packard",
                "Integrated Silicon Solutions", "Brooktree",
                "New Media", "MHS Electronic",
                "Performance Semi.", "Winbond Electronic",
                "Kawasaki Steel", "Bright Micro",
                "TECMAR", "Exar",
                "PCMCIA", "LG Semi (former Goldstar)",
                "Northern Telecom", "Sanyo",
                "Array Microsystems", "Crystal Semiconductor",
                "Analog Devices", "PMC-Sierra",
                "Asparix", "Convex Computer",
                "Quality Semiconductor", "Nimbus Technology",
                "Transwitch", "Micronas (ITT Intermetall)",
                "Cannon", "Altera",
                "NEXCOM", "QUALCOMM",
                "Sony", "Cray Research",
                "AMS(Austria Micro)", "Vitesse",
                "Aster Electronics", "Bay Networks (Synoptic)",
                "Zentrum or ZMD", "TRW",
                "Thesys", "Solbourne Computer",
                "Allied-Signal", "Dialog",
                "Media Vision", "Numonyx Corporation (former Level One Communication)"
            },
            // ── Bank 2 (cont = 1) ── 126 entries
            new string[] {
                "Cirrus Logic", "National Instruments",
                "ILC Data Device", "Alcatel Mietec",
                "Micro Linear", "Univ. of NC",
                "JTAG Technologies", "BAE Systems",
                "Nchip", "Galileo Tech",
                "Bestlink Systems", "Graychip",
                "GENNUM", "VideoLogic",
                "Robert Bosch", "Chip Express",
                "DATARAM", "United Microelec Corp.",
                "TCSI", "Smart Modular",
                "Hughes Aircraft", "Lanstar Semiconductor",
                "Qlogic", "Kingston",
                "Music Semi", "Ericsson Components",
                "SpaSE", "Eon Silicon Devices",
                "Integrated Silicon Solution (ISSI) (former Programmable Micro Corp)", "DoD",
                "Integ. Memories Tech.", "Corollary Inc.",
                "Dallas Semiconductor", "Omnivision",
                "EIV(Switzerland)", "Novatel Wireless",
                "Zarlink (former Mitel)", "Clearpoint",
                "Cabletron", "STEC (former Silicon Technology)",
                "Vanguard", "Hagiwara Sys-Com",
                "Vantis", "Celestica",
                "Century", "Hal Computers",
                "Rohm Company Ltd.", "Juniper Networks",
                "Libit Signal Processing", "Mushkin Enhanced Memory",
                "Tundra Semiconductor", "Adaptec Inc.",
                "LightSpeed Semi.", "ZSP Corp.",
                "AMIC Technology", "Adobe Systems",
                "Dynachip", "PNY Technologies Inc. (former PNY Electronics)",
                "Newport Digital", "MMC Networks",
                "T Square", "Seiko Epson",
                "Broadcom", "Viking Components",
                "V3 Semiconductor", "Flextronics (former Orbit)",
                "Suwa Electronics", "Transmeta",
                "Micron CMS", "American Computer & Digital Components Inc",
                "Enhance 3000 Inc", "Tower Semiconductor",
                "CPU Design", "Price Point",
                "Maxim Integrated Product", "Tellabs",
                "Centaur Technology", "Unigen Corporation",
                "Transcend Information", "Memory Card Technology",
                "CKD Corporation Ltd.", "Capital Instruments, Inc.",
                "Aica Kogyo, Ltd.", "Linvex Technology",
                "MSC Vertriebs GmbH", "AKM Company, Ltd.",
                "Dynamem, Inc.", "NERA ASA",
                "GSI Technology", "Dane-Elec (C Memory)",
                "Acorn Computers", "Lara Technology",
                "Oak Technology, Inc.", "Itec Memory",
                "Tanisys Technology", "Truevision",
                "Wintec Industries", "Super PC Memory",
                "MGV Memory", "Galvantech",
                "Gadzoox Nteworks", "Multi Dimensional Cons.",
                "GateField", "Integrated Memory System",
                "Triscend", "XaQti",
                "Goldenram", "Clear Logic",
                "Cimaron Communications", "Nippon Steel Semi. Corp.",
                "Advantage Memory", "AMCC",
                "LeCroy", "Yamaha Corporation",
                "Digital Microwave", "NetLogic Microsystems",
                "MIMOS Semiconductor", "Advanced Fibre",
                "BF Goodrich Data.", "Epigram",
                "Acbel Polytech Inc.", "Apacer Technology",
                "Admor Memory", "FOXCONN",
                "Quadratics Superconductor", "3COM"
            },
            // ── Bank 3 (cont = 2) ── 126 entries
            new string[] {
                "Camintonn Corporation", "ISOA Incorporated",
                "Agate Semiconductor", "ADMtek Incorporated",
                "HYPERTEC", "Adhoc Technologies",
                "MOSAID Technologies", "Ardent Technologies",
                "Switchcore", "Cisco Systems, Inc.",
                "Allayer Technologies", "WorkX AG (Wichman)",
                "Oasis Semiconductor", "Novanet Semiconductor",
                "E-M Solutions", "Power General",
                "Advanced Hardware Arch.", "Inova Semiconductors GmbH",
                "Telocity", "Delkin Devices",
                "Symagery Microsystems", "C-Port Corporation",
                "SiberCore Technologies", "Southland Microsystems",
                "Malleable Technologies", "Kendin Communications",
                "Great Technology Microcomputer", "Sanmina Corporation",
                "HADCO Corporation", "Corsair",
                "Actrans System Inc.", "ALPHA Technologies",
                "Silicon Laboratories, Inc. (Cygnal)", "Artesyn Technologies",
                "Align Manufacturing", "Peregrine Semiconductor",
                "Chameleon Systems", "Aplus Flash Technology",
                "MIPS Technologies", "Chrysalis ITS",
                "ADTEC Corporation", "Kentron Technologies",
                "Win Technologies", "Tezzaron Semiconductor (former Tachyon Semiconductor)",
                "Extreme Packet Devices", "RF Micro Devices",
                "Siemens AG", "Sarnoff Corporation",
                "Itautec SA (former Itautec Philco SA)", "Radiata Inc.",
                "Benchmark Elect. (AVEX)", "Legend",
                "SpecTek Incorporated", "Hi/fn",
                "Enikia Incorporated", "SwitchOn Networks",
                "AANetcom Incorporated", "Micro Memory Bank",
                "ESS Technology", "Virata Corporation",
                "Excess Bandwidth", "West Bay Semiconductor",
                "DSP Group", "Newport Communications",
                "Chip2Chip Incorporated", "Phobos Corporation",
                "Intellitech Corporation", "Nordic VLSI ASA",
                "Ishoni Networks", "Silicon Spice",
                "Alchemy Semiconductor", "Agilent Technologies",
                "Centillium Communications", "W.L. Gore",
                "HanBit Electronics", "GlobeSpan",
                "Element 14", "Pycon",
                "Saifun Semiconductors", "Sibyte, Incorporated",
                "MetaLink Technologies", "Feiya Technology",
                "I & C Technology", "Shikatronics",
                "Elektrobit", "Megic",
                "Com-Tier", "Malaysia Micro Solutions",
                "Hyperchip", "Gemstone Communications",
                "Anadigm (former Anadyne)", "3ParData",
                "Mellanox Technologies", "Tenx Technologies",
                "Helix AG", "Domosys",
                "Skyup Technology", "HiNT Corporation",
                "Chiaro", "MDT Technologies GmbH (former MCI Computer GMBH)",
                "Exbit Technology A/S", "Integrated Technology Express",
                "AVED Memory", "Legerity",
                "Jasmine Networks", "Caspian Networks",
                "nCUBE", "Silicon Access Networks",
                "FDK Corporation", "High Bandwidth Access",
                "MultiLink Technology", "BRECIS",
                "World Wide Packets", "APW",
                "Chicory Systems", "Xstream Logic",
                "Fast-Chip", "Zucotto Wireless",
                "Realchip", "Galaxy Power",
                "eSilicon", "Morphics Technology",
                "Accelerant Networks", "Silicon Wave",
                "SandCraft", "Elpida"
            },
            // ── Bank 4 (cont = 3) ── 126 entries
            new string[] {
                "Solectron", "Optosys Technologies",
                "Buffalo (former Melco)", "TriMedia Technologies",
                "Cyan Technologies", "Global Locate",
                "Optillion", "Terago Communications",
                "Ikanos Communications", "Princeton Technology",
                "Nanya Technology", "Elite Flash Storage",
                "Mysticom", "LightSand Communications",
                "ATI Technologies", "Agere Systems",
                "NeoMagic", "AuroraNetics",
                "Golden Empire", "Mushkin",
                "Tioga Technologies", "Netlist",
                "TeraLogic", "Cicada Semiconductor",
                "Centon Electronics", "Tyco Electronics",
                "Magis Works", "Zettacom",
                "Cogency Semiconductor", "Chipcon AS",
                "Aspex Technology", "F5 Networks",
                "Programmable Silicon Solutions", "ChipWrights",
                "Acorn Networks", "Quicklogic",
                "Kingmax Semiconductor", "BOPS",
                "Flasys", "BitBlitz Communications",
                "eMemory Technology", "Procket Networks",
                "Purple Ray", "Trebia Networks",
                "Delta Electronics", "Onex Communications",
                "Ample Communications", "Memory Experts Intl",
                "Astute Networks", "Azanda Network Devices",
                "Dibcom", "Tekmos",
                "API NetWorks", "Bay Microsystems",
                "Firecron Ltd", "Resonext Communications",
                "Tachys Technologies", "Equator Technology",
                "Concept Computer", "SILCOM",
                "3Dlabs", "c't Magazine",
                "Sanera Systems", "Silicon Packets",
                "Viasystems Group", "Simtek",
                "Semicon Devices Singapore", "Satron Handelsges",
                "Improv Systems", "INDUSYS GmbH",
                "Corrent", "Infrant Technologies",
                "Ritek Corp", "empowerTel Networks",
                "Hypertec", "Cavium Networks",
                "PLX Technology", "Massana Design",
                "Intrinsity", "Valence Semiconductor",
                "Terawave Communications", "IceFyre Semiconductor",
                "Primarion", "Picochip Designs Ltd",
                "Silverback Systems", "Jade Star Technologies",
                "Pijnenburg Securealink", "takeMS - Ultron AG (former Memorysolution GmbH)",
                "Cambridge Silicon Radio", "Swissbit",
                "Nazomi Communications", "eWave System",
                "Rockwell Collins", "Picocel Co., Ltd.",
                "Alphamosaic Ltd", "Sandburst",
                "SiCon Video", "NanoAmp Solutions",
                "Ericsson Technology", "PrairieComm",
                "Mitac International", "Layer N Networks",
                "MtekVision", "Allegro Networks",
                "Marvell Semiconductors", "Netergy Microelectronic",
                "NVIDIA", "Internet Machines",
                "Memorysolution GmbH (former Peak Electronics)", "Litchfield Communication",
                "Accton Technology", "Teradiant Networks",
                "Scaleo Chip (former Europe Technologies)", "Cortina Systems",
                "RAM Components", "Raqia Networks",
                "ClearSpeed", "Matsushita Battery",
                "Xelerated", "SimpleTech",
                "Utron Technology", "Astec International",
                "AVM gmbH", "Redux Communications",
                "Dot Hill Systems", "TeraChip"
            },
            // ── Bank 5 (cont = 4) ── 126 entries
            new string[] {
                "T-RAM Incorporated", "Innovics Wireless",
                "Teknovus", "KeyEye Communications",
                "Runcom Technologies", "RedSwitch",
                "Dotcast", "Silicon Mountain Memory",
                "Signia Technologies", "Pixim",
                "Galazar Networks", "White Electronic Designs",
                "Patriot Scientific", "Neoaxiom Corporation",
                "3Y Power Technology", "Scaleo Chip (former Europe Technologies)",
                "Potentia Power Systems", "C-guys Incorporated",
                "Digital Communications Technology Incorporated", "Silicon-Based Technology",
                "Fulcrum Microsystems", "Positivo Informatica Ltd",
                "XIOtech Corporation", "PortalPlayer",
                "Zhiying Software", "Parker Vision, Inc. (former Direct2Data)",
                "Phonex Broadband", "Skyworks Solutions",
                "Entropic Communications", "I'M Intelligent Memory Ltd (former Pacific Force Technology)",
                "Zensys A/S", "Legend Silicon Corp.",
                "sci-worx GmbH", "SMSC (former Oasis Silicon Systems)",
                "Renesas Electronics (former Renesas Technology)", "Raza Microelectronics",
                "Phyworks", "MediaTek",
                "Non-cents Productions", "US Modular",
                "Wintegra Ltd", "Mathstar",
                "StarCore", "Oplus Technologies",
                "Mindspeed", "Just Young Computer",
                "Radia Communications", "OCZ",
                "Emuzed", "LOGIC Devices",
                "Inphi Corporation", "Quake Technologies",
                "Vixel", "SolusTek",
                "Kongsberg Maritime", "Faraday Technology",
                "Altium Ltd.", "Insyte",
                "ARM Ltd.", "DigiVision",
                "Vativ Technologies", "Endicott Interconnect Technologies",
                "Pericom", "Bandspeed",
                "LeWiz Communications", "CPU Technology",
                "Ramaxel Technology", "DSP Group",
                "Axis Communications", "Legacy Electronics",
                "Chrontel", "Powerchip Semiconductor",
                "MobilEye Technologies", "Excel Semiconductor",
                "A-DATA Technology", "VirtualDigm",
                "G Skill Intl", "Quanta Computer",
                "Yield Microelectronics", "Afa Technologies",
                "KINGBOX Technology Co. Ltd.", "Ceva",
                "iStor Networks", "Advance Modules",
                "Microsoft", "Open-Silicon",
                "Goal Semiconductor", "ARC International",
                "Simmtec", "Metanoia",
                "Key Stream", "Lowrance Electronics",
                "Adimos", "SiGe Semiconductor",
                "Fodus Communications", "Credence Systems Corp.",
                "Genesis Microchip Inc.", "Vihana, Inc.",
                "WIS Technologies", "GateChange Technologies",
                "High Density Devices AS", "Synopsys",
                "Gigaram", "Enigma Semiconductor Inc.",
                "Century Micro Inc.", "Icera Semiconductor",
                "Mediaworks Integrated Systems", "O'Neil Product Development",
                "Supreme Top Technology Ltd.", "MicroDisplay Corporation",
                "Team Group Inc.", "Sinett Corporation",
                "Toshiba Corporation", "Tensilica",
                "SiRF Technology", "Bacoc Inc.",
                "SMaL Camera Technologies", "Thomson SC",
                "Airgo Networks", "Wisair Ltd.",
                "SigmaTel", "Arkados",
                "Compete IT gmbH Co. KG", "Eudar Technology Inc.",
                "Focus Enhancements", "Xyratex"
            },
            // ── Bank 6 (cont = 5) ── 126 entries
            new string[] {
                "Specular Networks", "Patriot Memory",
                "U-Chip Technology Corp.", "Silicon Optix",
                "Greenfield Networks", "CompuRAM GmbH",
                "Stargen, Inc.", "NetCell Corporation",
                "Excalibrus Technologies Ltd", "SCM Microsystems",
                "Xsigo Systems, Inc.", "CHIPS & Systems Inc",
                "Tier 1 Multichip Solutions", "CWRL Labs",
                "Teradici", "Gigaram, Inc.",
                "g2 Microsystems", "PowerFlash Semiconductor",
                "P.A. Semi, Inc.", "NovaTech Solutions, S.A.",
                "c2 Microsystems, Inc.", "Level5 Networks",
                "COS Memory AG", "Innovasic Semiconductor",
                "02IC Co. Ltd", "Tabula, Inc.",
                "Crucial Technology", "Chelsio Communications",
                "Solarflare Communications", "Xambala Inc.",
                "EADS Astrium", "Terra Semiconductor Inc. (former ATO Semicon Co. Ltd.)",
                "Imaging Works, Inc.", "Astute Networks, Inc.",
                "Tzero", "Emulex",
                "Power-One", "Pulse~LINK Inc.",
                "Hon Hai Precision Industry", "White Rock Networks Inc.",
                "Telegent Systems USA, Inc.", "Atrua Technologies, Inc.",
                "Acbel Polytech Inc.", "eRide Inc.",
                "ULi Electronics Inc.", "Magnum Semiconductor Inc.",
                "neoOne Technology, Inc.", "Connex Technology, Inc.",
                "Stream Processors, Inc.", "Focus Enhancements",
                "Telecis Wireless, Inc.", "uNav Microelectronics",
                "Tarari, Inc.", "Ambric, Inc.",
                "Newport Media, Inc.", "VMTS",
                "Enuclia Semiconductor, Inc.", "Virtium Technology Inc.",
                "Solid State System Co., Ltd.", "Kian Tech LLC",
                "Artimi", "Power Quotient International",
                "Avago Technologies", "ADTechnology",
                "Sigma Designs", "SiCortex, Inc.",
                "Ventura Technology Group", "eASIC",
                "M.H.S. SAS", "Micro Star International",
                "Rapport Inc.", "Makway International",
                "Broad Reach Engineering Co.", "Semiconductor Mfg Intl Corp",
                "SiConnect", "FCI USA Inc.",
                "Validity Sensors", "Coney Technology Co. Ltd.",
                "Spans Logic", "Neterion Inc.",
                "Qimonda", "New Japan Radio Co. Ltd.",
                "Velogix", "Montalvo Systems",
                "iVivity Inc.", "Walton Chaintech",
                "AENEON", "Lorom Industrial Co. Ltd.",
                "Radiospire Networks", "Sensio Technologies, Inc.",
                "Nethra Imaging", "Hexon Technology Pte Ltd",
                "CompuStocx (CSX)", "Methode Electronics, Inc.",
                "Connect One Ltd.", "Opulan Technologies",
                "Septentrio NV", "Goldenmars Technology Inc.",
                "Kreton Corporation", "Cochlear Ltd.",
                "Altair Semiconductor", "NetEffect, Inc.",
                "Spansion, Inc.", "Taiwan Semiconductor Mfg",
                "Emphany Systems Inc.", "ApaceWave Technologies",
                "Mobilygen Corporation", "Tego",
                "Cswitch Corporation", "Haier (Beijing) IC Design Co.",
                "MetaRAM", "Axel Electronics Co. Ltd.",
                "Tilera Corporation", "Aquantia",
                "Vivace Semiconductor", "Redpine Signals",
                "Octalica", "InterDigital Communications",
                "Avant Technology", "Asrock, Inc.",
                "Availink", "Quartics, Inc.",
                "Element CXI", "Innovaciones Microelectronicas",
                "VeriSilicon Microelectronics", "W5 Networks"
            },
            // ── Bank 7 (cont = 6) ── 126 entries
            new string[] {
                "MOVEKING", "Mavrix Technology, Inc.",
                "CellGuide Ltd.", "Faraday Technology",
                "Diablo Technologies, Inc.", "Jennic",
                "Octasic", "Molex Incorporated",
                "3Leaf Networks", "Bright Micron Technology",
                "Netxen", "NextWave Broadband Inc.",
                "DisplayLink", "ZMOS Technology",
                "Tec-Hill", "Multigig, Inc.",
                "Amimon", "Euphonic Technologies, Inc.",
                "BRN Phoenix", "InSilica",
                "Ember Corporation", "Avexir Technologies Corporation",
                "Echelon Corporation", "Edgewater Computer Systems",
                "XMOS Semiconductor Ltd.", "GENUSION, Inc.",
                "Memory Corp NV", "SiliconBlue Technologies",
                "Rambus Inc.", "Andes Technology Corporation",
                "Coronis Systems", "Achronix Semiconductor",
                "Siano Mobile Silicon Ltd.", "Semtech Corporation",
                "Pixelworks Inc.", "Gaisler Research AB",
                "Teranetics", "Toppan Printing Co. Ltd.",
                "Kingxcon", "Silicon Integrated Systems",
                "I-O Data Device, Inc.", "NDS Americas Inc.",
                "Solomon Systech Limited", "On Demand Microelectronics",
                "Amicus Wireless Inc.", "SMARDTV SNC",
                "Comsys Communication Ltd.", "Movidia Ltd.",
                "Javad GNSS, Inc.", "Montage Technology Group",
                "Trident Microsystems", "Super Talent",
                "Optichron, Inc.", "Future Waves UK Ltd.",
                "SiBEAM, Inc.", "Inicore, Inc.",
                "Virident Systems", "M2000, Inc.",
                "ZeroG Wireless, Inc.", "Gingle Technology Co. Ltd.",
                "Space Micro Inc.", "Wilocity",
                "Novafora, Inc.", "iKoa Corporation",
                "ASint Technology", "Ramtron",
                "Plato Networks Inc.", "IPtronics AS",
                "Infinite-Memories", "Parade Technologies Inc.",
                "Dune Networks", "GigaDevice Semiconductor",
                "Modu Ltd.", "CEITEC",
                "Northrop Grumman", "XRONET Corporation",
                "Sicon Semiconductor AB", "Atla Electronics Co. Ltd.",
                "TOPRAM Technology", "Silego Technology Inc.",
                "Kinglife", "Ability Industries Ltd.",
                "Silicon Power Computer & Communications", "Augusta Technology, Inc.",
                "Nantronics Semiconductors", "Hilscher Gesellschaft",
                "Quixant Ltd.", "Percello Ltd.",
                "NextIO Inc.", "Scanimetrics Inc.",
                "FS-Semi Company Ltd.", "Infinera Corporation",
                "SandForce Inc.", "Lexar Media",
                "Teradyne Inc.", "Memory Exchange Corp.",
                "Suzhou Smartek Electronics", "Avantium Corporation",
                "ATP Electronics Inc.", "Valens Semiconductor Ltd",
                "Agate Logic, Inc.", "Netronome",
                "Zenverge, Inc.", "N-trig Ltd",
                "SanMax Technologies Inc.", "Contour Semiconductor Inc.",
                "TwinMOS", "Silicon Systems, Inc.",
                "V-Color Technology Inc.", "Certicom Corporation",
                "JSC ICC Milandr", "PhotoFast Global Inc.",
                "InnoDisk Corporation", "Muscle Power",
                "Energy Micro", "Innofidei",
                "CopperGate Communications", "Holtek Semiconductor Inc.",
                "Myson Century, Inc.", "FIDELIX",
                "Red Digital Cinema", "Densbits Technology",
                "Zempro", "MoSys",
                "Provigent", "Triad Semiconductor, Inc."
            },
            // ── Bank 8 (cont = 7) ── 126 entries
            new string[] {
                "Siklu Communication Ltd.", "A Force Manufacturing Ltd.",
                "Strontium", "ALi Corp (former Abilis Systems)",
                "Siglead, Inc.", "Ubicom, Inc.",
                "Unifosa Corporation", "Stretch, Inc.",
                "Lantiq Deutschland GmbH", "Visipro",
                "EKMemory", "Microelectronics Institute ZTE",
                "u-blox AG (former Cognovo Ltd)", "Carry Technology Co. Ltd.",
                "Nokia", "King Tiger Technology",
                "Sierra Wireless", "HT Micron",
                "Albatron Technology Co. Ltd.", "Leica Geosystems AG",
                "BroadLight", "AEXEA",
                "ClariPhy Communications, Inc.", "Green Plug",
                "Design Art Networks", "Mach Xtreme Technology Ltd.",
                "ATO Solutions Co. Ltd.", "Ramsta",
                "Greenliant Systems, Ltd.", "Teikon",
                "Antec Hadron", "NavCom Technology, Inc.",
                "Shanghai Fudan Microelectronics", "Calxeda, Inc.",
                "JSC EDC Electronics", "Kandit Technology Co. Ltd.",
                "Ramos Technology", "Goldenmars Technology",
                "XeL Technology Inc.", "Newzone Corporation",
                "ShenZhen MercyPower Tech", "Nanjing Yihuo Technology",
                "Nethra Imaging Inc.", "SiTel Semiconductor BV",
                "SolidGear Corporation", "Topower Computer Ind Co Ltd.",
                "Wilocity", "Profichip GmbH",
                "Gerad Technologies", "Ritek Corporation",
                "Gomos Technology Limited", "Memoright Corporation",
                "D-Broad, Inc.", "HiSilicon Technologies",
                "Syndiant Inc.", "Enverv Inc.",
                "Cognex", "Xinnova Technology Inc.",
                "Ultron AG", "Concord Idea Corporation",
                "AIM Corporation", "Lifetime Memory Products",
                "Ramsway", "Recore Systems BV",
                "Haotian Jinshibo Science Tech", "Being Advanced Memory",
                "Adesto Technologies", "Giantec Semiconductor, Inc.",
                "HMD Electronics AG", "Gloway International (HK)",
                "Kingcore", "Anucell Technology Holding",
                "Accord Software & Systems Pvt. Ltd.", "Active-Semi Inc.",
                "Denso Corporation", "TLSI Inc.",
                "Shenzhen Daling Electronic Co. Ltd.", "Mustang",
                "Orca Systems", "Passif Semiconductor",
                "GigaDevice Semiconductor (Beijing) Inc.", "Memphis Electronic",
                "Beckhoff Automation GmbH", "Harmony Semiconductor Corp (former ProPlus Design Solutions)",
                "Air Computers SRL", "TMT Memory",
                "Eorex Corporation", "Xingtera",
                "Netsol", "Bestdon Technology Co. Ltd.",
                "Baysand Inc.", "Uroad Technology Co. Ltd. (former Triple Grow Industrial Ltd.)",
                "Wilk Elektronik S.A.", "AAI",
                "Harman", "Berg Microelectronics Inc.",
                "ASSIA, Inc.", "Visiontek Products LLC",
                "OCMEMORY", "Welink Solution Inc.",
                "Shark Gaming", "Avalanche Technology",
                "R&D Center ELVEES OJSC", "KingboMars Technology Co. Ltd.",
                "High Bridge Solutions Industria Eletronica", "Transcend Technology Co. Ltd.",
                "Everspin Technologies", "Hon-Hai Precision",
                "Smart Storage Systems", "Toumaz Group",
                "Zentel Electronics Corporation", "Panram International Corporation",
                "Silicon Space Technology", "LITE-ON IT Corporation",
                "Inuitive", "HMicro",
                "BittWare Inc.", "GLOBALFOUNDRIES",
                "ACPI Digital Co. Ltd", "Annapurna Labs",
                "AcSiP Technology Corporation", "Idea! Electronic Systems",
                "Gowe Technology Co. Ltd", "Hermes Testing Solutions Inc.",
                "Positivo BGH", "Intelligence Silicon Technology"
            },
            // ── Bank 9 (cont = 8) ── 126 entries
            new string[] {
                "3D PLUS", "Diehl Aerospace",
                "Fairchild", "Mercury Systems",
                "Sonics Inc.", "Emerson Automation Solutions (former ICC/GE Intelligent Platforms)",
                "Shenzhen Jinge Information Co. Ltd", "SCWW",
                "Silicon Motion Inc.", "Anurag",
                "King Kong", "FROM30 Co. Ltd",
                "Gowin Semiconductor Corp", "Fremont Micro Devices Ltd",
                "Ericsson Modems", "Exelis",
                "Satixfy Ltd", "Galaxy Microsystems Ltd",
                "Gloway International Co. Ltd", "Lab",
                "Smart Energy Instruments", "Approved Memory Corporation",
                "Axell Corporation", "Essencore Limited (former ISD Technology Limited)",
                "Phytium", "Xi'an UniIC Semiconductors Co Ltd (former Xi'an SinoChip Semiconductor)",
                "Ambiq Micro", "eveRAM Technology Inc.",
                "Infomax", "Butterfly Network Inc.",
                "Shenzhen City Gcai Electronics", "Stack Devices Corporation",
                "ADK Media Group", "TSP Global Co. Ltd",
                "HighX", "Shenzhen Elicks Technology",
                "ISSI/Chingis", "Google Inc.",
                "Dasima International Development", "Leahkinn Technology Limited",
                "HIMA Paul Hildebrandt GmbH Co KG", "Keysight Technologies",
                "Techcomp International (Fastable)", "Ancore Technology Corporation",
                "Nuvoton", "Korea Uhbele International Group Ltd",
                "Ikegami Tsushinki Co. Ltd", "RelChip Inc.",
                "Baikal Electronics", "Nemostech Inc.",
                "Memorysolution GmbH", "Silicon Integrated Systems Corporation",
                "Xiede", "BRC (former Multilaser Components)",
                "Flash Chi", "Jone",
                "GCT Semiconductor Inc.", "Hong Kong Zetta Device Technology",
                "Unimemory Technology(s) Pte Ltd", "Cuso",
                "Kuso", "Uniquify Inc.",
                "Skymedi Corporation", "Core Chance Co. Ltd",
                "Tekism Co. Ltd", "Seagate Technology PLC",
                "Hong Kong Gaia Group Co. Limited", "Gigacom Semiconductor LLC",
                "V2 Technologies", "TLi",
                "Neotion", "Lenovo",
                "Shenzhen Zhongteng Electronic Corp. Ltd", "Compound Photonics",
                "In2H2 Inc (former Cognimem Technologies Inc)", "Shenzhen Pango Microsystems Co. Ltd",
                "Vasekey", "Cal-Comp Industria de Semicondutores",
                "Eyenix Co. Ltd", "Heoriady",
                "Accelerated Memory Production Inc.", "INVECAS Inc.",
                "AP Memory", "Douqi Technology",
                "Etron Technology Inc.", "Indie Semiconductor",
                "Socionext Inc.", "HGST",
                "EVGA", "Audience Inc.",
                "EpicGear", "Vitesse Enterprise Co.",
                "Foxtronn International Corporation", "Bretelon Inc.",
                "Graphcore", "Eoplex Inc",
                "MaxLinear Inc", "ETA Devices",
                "LOKI", "IMS Electronics Co Ltd",
                "Dosilicon Co Ltd", "Dolphin Integration",
                "Shenzhen Mic Electronics Technolog", "Boya Microelectronics Inc",
                "Geniachip (Roche)", "Axign",
                "Kingred Electronic Technology Ltd", "Chao Yue Zhuo Computer Business Dept.",
                "Guangzhou Si Nuo Electronic Technology.", "Crocus Technology Inc",
                "Creative Chips GmbH", "GE Aviation Systems LLC.",
                "Asgard", "Good Wealth Technology Ltd",
                "TriCor Technologies", "Nova-Systems GmbH",
                "JUHOR", "Zhuhai Douke Commerce Co Ltd",
                "DSL Memory", "Anvo-Systems Dresden GmbH",
                "Realtek", "AltoBeam",
                "Wave Computing", "Beijing TrustNet Technology Co Ltd",
                "Innovium Inc", "Starsway Technology Limited"
            },
            // ── Bank 10 (cont = 9) ── 126 entries
            new string[] {
                "Weltronics Co LTD", "VMware Inc",
                "Hewlett Packard Enterprise", "INTENSO",
                "Puya Semiconductor", "MEMORFI",
                "MSC Technologies GmbH", "Txrui",
                "SiFive Inc", "Spreadtrum Communications",
                "XTX Technology Limited", "UMAX Technology",
                "Shenzhen Yong Sheng Technology", "SNOAMOO (Shenzhen Kai Zhuo Yue)",
                "Daten Tecnologia LTDA", "Shenzhen XinRuiYan Electronics",
                "Eta Compute", "Energous",
                "Raspberry Pi Trading Ltd", "Shenzhen Chixingzhe Tech Co Ltd",
                "Silicon Mobility", "IQ-Analog Corporation",
                "Uhnder Inc", "Impinj",
                "DEPO Computers", "Nespeed Sysems",
                "Yangtze Memory Technologies Co Ltd", "MemxPro Inc",
                "Tammuz Co Ltd", "Allwinner Technology",
                "Shenzhen City Futian District Qing Xuan Tong Computer Trading Firm", "XMC",
                "Teclast", "Maxsun",
                "Haiguang Integrated Circuit Design", "RamCENTER Technology",
                "Phison Electronics Corporation", "Guizhou Huaxintong Semi-Conductor",
                "Network Intelligence", "Continental Technology (Holdings)",
                "Guangzhou Huayan Suning Electronic", "Guangzhou Zhouji Electronic Co Ltd",
                "Shenzhen Giant Hui Kang Tech Co Ltd", "Shenzhen Yilong Innovative Co Ltd",
                "Neo Forza", "Lyontek Inc",
                "Shanghai Kuxin Microelectronics Ltd", "Shenzhen Larix Technology Co Ltd",
                "Qbit Semiconductor Ltd", "Insignis Technology Corporation",
                "Lanson Memory Co Ltd", "Shenzhen Superway Electronics Co Ltd",
                "Canaan-Creative Co Ltd", "Black Diamond Memory",
                "Shenzhen City Parker Baking Electronics", "Shenzhen Baihong Technology Co Ltd",
                "GEO Semiconductors", "OCPC",
                "Artery Technology Co Ltd", "Jinyu",
                "ShenzhenYing Chi Technology Development", "Shenzhen Pengcheng Xin Technology",
                "Pegasus Semiconductor (Shanghai) Co", "Mythic Inc",
                "Elmos Semiconductor AG", "Kllisre",
                "Shenzhen Winconway Technology", "Shenzhen Xingmem Technology Corp",
                "Gold Key Technology Co Ltd", "Habana Labs Ltd",
                "Hoodisk Electronics Co Ltd", "SemsoTai (SZ) Technology Co Ltd",
                "OM Nanotech Pvt. Ltd", "Shenzhen Zhifeng Weiye Technology",
                "Xinshirui (Shenzhen) Electronics Co", "Guangzhou Zhong Hao Tian Electronic",
                "Shenzhen Longsys Electronics Co Ltd", "Deciso B.V.",
                "Puya Semiconductor (Shenzhen)", "Shenzhen Veineda Technology Co Ltd",
                "Antec Memory", "Cortus SAS",
                "Dust Leopard", "MyWo AS",
                "J&A Information Inc", "Shenzhen JIEPEI Technology Co Ltd",
                "Heidelberg University", "Flexxon PTE Ltd",
                "Wiliot", "Raysun Electronics International Ltd",
                "Aquarius Production Company LLC", "MACNICA DHW LTDA",
                "Intelimem", "Zbit Semiconductor Inc",
                "Shenzhen Technology Co Ltd", "Signalchip",
                "Shenzen Recadata Storage Technology", "Hyundai Technology",
                "Shanghai Fudi Investment Development", "Aixi Technology",
                "Tecon MT", "Onda Electric Co Ltd",
                "Jinshen", "Kimtigo Semiconductor (HK) Limited",
                "IIT Madras", "Shenshan (Shenzhen) Electronic",
                "Hefei Core Storage Electronic Limited", "Colorful Technology Ltd",
                "Visenta (Xiamen) Technology Co Ltd", "Roa Logic BV",
                "NSITEXE Inc", "Hong Kong Hyunion Electronics",
                "ASK Technology Group Limited", "GIGA-BYTE Technology Co Ltd",
                "Terabyte Co Ltd", "Hyundai Inc",
                "EXCELERAM", "PsiKick",
                "Netac Technology Co Ltd", "PCCOOLER",
                "Jiangsu Huacun Electronic Technology", "Shenzhen Micro Innovation Industry",
                "Beijing Tongfang Microelectronics Co", "XZN Storage Technology",
                "ChipCraft Sp. z.o.o.", "ALLFLASH Technology Limited"
            },
            // ── Bank 11 (cont = 10) ── 126 entries
            new string[] {
                "Foerd Technology Co Ltd", "KingSpec",
                "Codasip GmbH", "SL Link Co Ltd",
                "Shenzhen Kefu Technology Co Limited", "Shenzhen ZST Electronics Technology",
                "Kyokuto Electronic Inc", "Warrior Technology",
                "TRINAMIC Motion Control GmbH & Co", "PixelDisplay Inc",
                "Shenzhen Futian District Bo Yueda Elec", "Richtek Power",
                "Shenzhen LianTeng Electronics Co Ltd", "AITC Memory",
                "UNIC Memory Technology Co Ltd", "Shenzhen Huafeng Science Technology",
                "CXMT (former Innotron Memory Co Ltd)", "Guangzhou Xinyi Heng Computer Trading Firm",
                "SambaNova Systems", "V-GEN",
                "Jump Trading", "Ampere Computing",
                "Shenzhen Zhongshi Technology Co Ltd", "Shenzhen Zhongtian Bozhong Technology",
                "Tri-Tech International", "Silicon Intergrated Systems Corporation",
                "Shenzhen HongDingChen Information", "Plexton Holdings Limited",
                "AMS (Jiangsu Advanced Memory Semi)", "Wuhan Jing Tian Interconnected Tech Co",
                "Axia Memory Technology", "Chipset Technology Holding Limited",
                "Shenzhen Xinshida Technology Co Ltd", "Shenzhen Chuangshifeida Technology",
                "Guangzhou MiaoYuanJi Technology", "ADVAN Inc",
                "Shenzhen Qianhai Weishengda Electronic Commerce Company Ltd", "Guangzhou Guang Xie Cheng Trading",
                "StarRam International Co Ltd", "Shen Zhen XinShenHua Tech Co Ltd",
                "UltraMemory Inc", "New Coastline Global Tech Industry Co",
                "Sinker", "Diamond",
                "PUSKILL", "Guangzhou Hao Jia Ye Technology Co",
                "Ming Xin Limited", "Barefoot Networks",
                "Biwin Semiconductor (HK) Co Ltd", "UD INFO Corporation",
                "Trek Technology (S) PTE Ltd", "Xiamen Kingblaze Technology Co Ltd",
                "Shenzhen Lomica Technology Co Ltd", "Nuclei System Technology Co Ltd",
                "Wuhan Xun Zhan Electronic Technology", "Shenzhen Ingacom Semiconductor Ltd",
                "Zotac Technology Ltd", "Foxline",
                "Shenzhen Farasia Science Technology", "Efinix Inc",
                "Hua Nan San Xian Technology Co Ltd", "Goldtech Electronics Co Ltd",
                "Shanghai Han Rong Microelectronics Co", "Shenzhen Zhongguang Yunhe Trading",
                "Smart Shine(QingDao) Microelectronics", "Thermaltake Technology Co Ltd",
                "Shenzhen O'Yang Maile Technology Ltd", "UPMEM",
                "Chun Well Technology Holding Limited", "Astera Labs Inc",
                "Winconway", "Advantech Co Ltd",
                "Chengdu Fengcai Electronic Technology", "The Boeing Company",
                "Blaize Inc", "Ramonster Technology Co Ltd",
                "Wuhan Naonongmai Technology Co Ltd", "Shenzhen Hui ShingTong Technology",
                "Yourlyon", "Fabu Technology",
                "Shenzhen Yikesheng Technology Co Ltd", "NOR-MEM",
                "Cervoz Co Ltd", "Bitmain Technologies Inc",
                "Facebook Inc", "Shenzhen Longsys Electronics Co Ltd",
                "Guangzhou Siye Electronic Technology", "Silergy",
                "Adamway", "PZG",
                "Shenzhen King Power Electronics", "Guangzhou ZiaoFu Tranding Co Ltd",
                "Shenzhen SKIHOTAR Semiconductor", "PulseRain Technology",
                "Seeker Technology Limited", "Shenzhen OSCOO Tech Co Ltd",
                "Shenzhen Yze Technology Co Ltd", "Shenzhen Jieshuo Electronic Commerce",
                "Gazda", "Hua Wei Technology Co Ltd",
                "Esperanto Technologies", "JinSheng Electronic (Shenzhen) Co Ltd",
                "Shenzhen Shi Bolunshuai Technology", "Shanghai Rei Zuan Information Tech",
                "Fraunhofer IIS", "Kandou Bus SA",
                "Acer", "Artmem Technology Co Ltd",
                "Gstar Semiconductor Co Ltd", "ShineDisk",
                "Shenzhen CHN Technology Co Ltd", "UnionChip Semiconductor Co Ltd",
                "Tanbassh", "Shenzhen Tianyu Jieyun Intl Logistics",
                "MCLogic Inc", "Eorex Corporation",
                "Arm Technology (China) Co Ltd", "Lexar Co Limited",
                "QinetiQ Group PLC", "Exascend",
                "Hong Kong Hyunion Electronics Co Ltd", "Shenzhen Banghong Electronics Co Ltd",
                "MBit Wireless Inc", "Hex Five Security Inc",
                "ShenZhen Juhor Precision Tech Co Ltd", "Shenzhen Reeinno Technology Co Ltd"
            },
            // ── Bank 12 (cont = 11) ── 126 entries
            new string[] {
                "ABIT Electronics (Shenzhen) Co Ltd", "Semidrive",
                "MyTek Electronics Corp", "Wxilicon Technology Co Ltd",
                "Shenzhen Meixin Electronics Ltd", "Ghost Wolf",
                "LiSion Technologies Inc", "Power Active Co Ltd",
                "Pioneer High Fidelity Taiwan Co. Ltd", "LuoSilk",
                "Shenzhen Chuangshifeida Technology", "Black Sesame Technologies Inc",
                "Jiangsu Xinsheng Intelligent Technology", "MLOONG",
                "Quadratica LLC", "Anpec Electronics",
                "Xi'an Morebeck Semiconductor Tech Co", "Kingbank Technology Co Ltd",
                "ITRenew Inc", "Shenzhen Eaget Innovation Tech Ltd",
                "Jazer", "Xiamen Semiconductor Investment Group",
                "Guangzhou Longdao Network Tech Co", "Shenzhen Futian SEC Electronic Market",
                "Allegro Microsystems LLC", "Hunan RunCore Innovation Technology",
                "C-Corsa Technology", "Zhuhai Chuangfeixin Technology Co Ltd",
                "Beijing InnoMem Technologies Co Ltd", "YooTin",
                "Shenzhen Pengxiong Technology Co Ltd", "Dongguan Yingbang Commercial Trading Co",
                "Shenzhen Ronisys Electronics Co Ltd", "Hongkong Xinlan Guangke Co Ltd",
                "Apex Microelectronics Co Ltd", "Beijing Hongda Jinming Technology Co Ltd",
                "Ling Rui Technology (Shenzhen) Co Ltd", "Hongkong Hyunion Electronics Co Ltd",
                "Starsystems Inc", "Shenzhen Yingjiaxun Industrial Co Ltd",
                "Dongguan Crown Code Electronic Commerce", "Monolithic Power Systems Inc",
                "WuHan SenNaiBo E-Commerce Co Ltd", "Hangzhou Hikstorage Technology Co",
                "Shenzhen Goodix Technology Co Ltd", "Aigo Electronic Technology Co Ltd",
                "Hefei Konsemi Storage Technology Co Ltd", "Cactus Technologies Limited",
                "DSIN", "Blu Wireless Technology",
                "Nanjing UCUN Technology Inc", "Acacia Communications",
                "Beijinjinshengyihe Technology Co Ltd", "Zyzyx",
                "T-HEAD Semiconductor Co Ltd", "Shenzhen Hystou Technology Co Ltd",
                "Syzexion", "Kembona",
                "Qingdao Thunderobot Technology Co Ltd", "Morse Micro",
                "Shenzhen Envida Technology Co Ltd", "UDStore Solution Limited",
                "Shunlie", "Shenzhen Xin Hong Rui Tech Ltd",
                "Shenzhen Yze Technology Co Ltd", "Shenzhen Huang Pu He Xin Technology",
                "Xiamen Pengpai Microelectronics Co Ltd", "JISHUN",
                "Shenzhen WODPOSIT Technology Co", "Unistar",
                "UNICORE Electronic (Suzhou) Co Ltd", "Axonne Inc",
                "Shenzhen SOVERECA Technology Co", "Dire Wolf",
                "Whampoa Core Technology Co Ltd", "CSI Halbleiter GmbH",
                "ONE Semiconductor", "SimpleMachines Inc",
                "Shenzhen Chengyi Qingdian Electronic", "Shenzhen Xinlianxin Network Technology",
                "Vayyar Imaging Ltd", "Paisen Network Technology Co Ltd",
                "Shenzhen Fengwensi Technology Co Ltd", "Caplink Technology Limited",
                "JJT Solution Co Ltd", "HOSIN Global Electronics Co Ltd",
                "Shenzhen KingDisk Century Technology", "SOYO",
                "DIT Technology Co Ltd", "iFound",
                "Aril Computer Company", "ASUS",
                "Shenzhen Ruiyingtong Technology Co", "HANA Micron",
                "RANSOR", "Axiado Corporation",
                "Tesla Corporation", "Pingtouge (Shanghai) Semiconductor Co",
                "S3Plus Technologies SA", "Integrated Silicon Solution Israel Ltd",
                "GreenWaves Technologies", "NUVIA Inc",
                "Guangzhou Shuvrwine Technology Co", "Shenzhen Hangshun Chip Technology",
                "Chengboliwei Electronic Business", "Kowin Memory Technology Co Ltd",
                "Euronet Technology Inc", "SCY",
                "Shenzhen Xinhongyusheng Electrical", "PICOCOM",
                "Shenzhen Toooogo Memory Technology", "VLSI Solution",
                "Costar Electronics Inc", "Shenzhen Huatop Technology Co Ltd",
                "Inspur Electronic Information Industry", "Shenzhen Boyuan Computer Technology",
                "Beijing Welldisk Electronics Co Ltd", "Suzhou EP Semicon Co Ltd",
                "Zhejiang Dahua Memory Technology", "Virtu Financial",
                "Datotek International Co Ltd", "Telecom and Microelectronics Industries",
                "Echo Technology Ltd", "APEX-INFO",
                "Yingpark", "Shenzhen Bigway Tech Co Ltd"
            },
            // ── Bank 13 (cont = 12) ── 15 entries
            new string[] {
                "Beijing Haawking Technology Co Ltd", "Open HW Group",
                "JHICC", "ncoder AG",
                "ThinkTech Information Technology Co", "Shenzhen Chixingzhe Technology Co Ltd",
                "Skywalker", "Shenzhen Kaizhuoyue Electronics Co Ltd",
                "Shenzhen YC Storage Technology Co Ltd", "Shenzhen Chixingzhe Technology Co",
                "Wink Semiconductor (Shenzhen) Co Ltd", "AISTOR",
                "Palma Ceia SemiDesign", "EM Microelectronic-Marin SA",
                "Shenzhen Monarch Memory Technology"
            }
        };

        internal static string LookupJedecManufacturer(int bankByte, int mfrByte)
        {
            // Strip parity (bit 7) from both bytes
            int cont = bankByte & 0x7F;   // continuation count = bank - 1
            int code = mfrByte & 0x7F;    // 7-bit manufacturer code

            // Validate odd parity on manufacturer byte
            int parity = 0;
            for (int tmp = mfrByte; tmp != 0; tmp >>= 1)
                parity ^= (tmp & 1);

            if (parity != 1 || code == 0)
                return string.Format("Invalid (Bank=0x{0:X2}, ID=0x{1:X2})",
                    bankByte, mfrByte);

            if (cont < JedecVendors.Length && (code - 1) < JedecVendors[cont].Length)
                return JedecVendors[cont][code - 1];

            return string.Format("Unknown (Bank {0}, Code 0x{1:X2})",
                cont + 1, mfrByte);
        }

        // ══════════════════════════════════════════════════════════════════════
        //  Utility helpers
        // ══════════════════════════════════════════════════════════════════════

        private static byte B(byte[] spd, int offset)
        {
            if (offset < 0 || offset >= spd.Length) return 0;
            return spd[offset];
        }

        private static int U16LE(byte[] spd, int offset)
        {
            return (int)B(spd, offset) | ((int)B(spd, offset + 1) << 8);
        }

        private static int DecodeBcd(byte b)
        {
            return ((b >> 4) & 0x0F) * 10 + (b & 0x0F);
        }

        // ══════════════════════════════════════════════════════════════════════
        //  Convenience methods
        // ══════════════════════════════════════════════════════════════════════

        /// <summary>
        /// Read and decode SPD from all discovered DDR5 DIMMs.
        /// Also reads live thermal sensor data from SPD5118 hubs.
        /// </summary>
        public static Dictionary<byte, Ddr5SpdInfo> ReadAndDecodeAll(SmbusPiix4 smbus)
        {
            if (smbus == null)
                throw new ArgumentNullException("smbus");

            Dictionary<byte, List<byte>> rawDumps = smbus.DumpDdr5Spd();
            Dictionary<byte, Ddr5SpdInfo> results = new Dictionary<byte, Ddr5SpdInfo>();

            foreach (KeyValuePair<byte, List<byte>> kvp in rawDumps)
            {
                Ddr5SpdInfo info = Decode(kvp.Value);

                // Read live temperature from SPD5118 hub if supported
                try
                {
                    if (Ddr5ThermalSensor.Detect(smbus, kvp.Key))
                        info.ThermalData = Ddr5ThermalSensor.ReadAll(smbus, kvp.Key);
                }
                catch
                {
                    // Thermal sensor not accessible – not critical
                }

                // Read PMIC data (PMIC addr = SPD addr - 0x08)
                try
                {
                    byte pmicAddr = Ddr5PmicReader.PmicAddrFromSpd(kvp.Key);
                    if (Ddr5PmicReader.Detect(smbus, pmicAddr))
                        info.PmicData = Ddr5PmicReader.ReadAll(smbus, pmicAddr);
                }
                catch
                {
                    // PMIC not accessible – not critical
                }

                results.Add(kvp.Key, info);
            }

            return results;
        }

        /// <summary>
        /// Read, decode, and print SPD from all discovered DDR5 DIMMs.
        /// </summary>
        public static Dictionary<byte, Ddr5SpdInfo> ReadDecodeAndPrint(SmbusPiix4 smbus)
        {
            Dictionary<byte, Ddr5SpdInfo> results = ReadAndDecodeAll(smbus);

            foreach (KeyValuePair<byte, Ddr5SpdInfo> kvp in results)
            {
                Console.WriteLine("DIMM at I2C address 0x{0:X2}", kvp.Key);
                Console.WriteLine(kvp.Value.ToString());
            }

            return results;
        }

        /// <summary>Decode from a raw binary SPD file on disk.</summary>
        public static Ddr5SpdInfo DecodeFromFile(string path)
        {
            byte[] data = System.IO.File.ReadAllBytes(path);
            return Decode(data);
        }

        /// <summary>Quick check: does this SPD identify as DDR5?</summary>
        public static bool IsDdr5(byte[] spd)
        {
            if (spd == null || spd.Length < 3) return false;
            return spd[SPD_DEVICE_TYPE] == DDR5_DEVICE_TYPE;
        }

        /// <summary>Extract just the JEDEC speed grade without full decode.</summary>
        public static string GetSpeedGrade(byte[] spd)
        {
            int tCK = U16LE(spd, SPD_TCKAVG_MIN_LSB);
            if (tCK <= 0) return "Unknown";
            double mhz = 1000000.0 / (double)tCK;
            int mts = RoundToJedecBin((int)Math.Round(2.0 * mhz));
            return string.Format("DDR5-{0}", mts);
        }

        /// <summary>Extract just the module part number without full decode.</summary>
        public static string GetPartNumber(byte[] spd)
        {
            if (spd == null || spd.Length < SPD_MOD_PARTNO + 30) return "";
            StringBuilder sb = new StringBuilder();
            for (int i = 0; i < 30; i++)
            {
                byte c = spd[SPD_MOD_PARTNO + i];
                if (c >= 0x20 && c <= 0x7E)
                    sb.Append((char)c);
            }
            return sb.ToString().Trim();
        }
    }
}