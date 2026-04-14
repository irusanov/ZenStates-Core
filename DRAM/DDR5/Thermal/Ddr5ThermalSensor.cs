using System.Collections.Generic;
using System.Diagnostics;
using ZenStates.Core.Drivers;

namespace ZenStates.Core
{
    internal static class Ddr5ThermalSensor
    {
        // SPD5118 Hub Register Addresses (Mode Registers)
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

        // Temperature conversion
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

        // SMBus read helpers
        // Uses SmbusPiix4.SmbusReadByteData(addr7, command, out result)
        private static bool ReadMRNoLock(SmbusDriverBase smbus, byte addr7, byte mr, out byte value)
        {
            return smbus.ReadByteDataNoLock(addr7, mr, out value);
        }

        private static bool ReadMR16NoLock(SmbusDriverBase smbus, byte addr7, byte mr, out int milliC)
        {
            milliC = 0;
            byte lo, hi;
            // SPD5118 temperature and limit registers are little-endian:
            //   MR_N = LSB, MR_N+1 = MSB
            // (Confirmed by Linux kernel spd5118.c: regmap_bulk_read + (regval[1]<<8)|regval[0])
            // NOTE: Device type at MR0:MR1 is big-endian — handled separately in Detect().
            if (!ReadMRNoLock(smbus, addr7, mr, out lo)) return false;
            if (!ReadMRNoLock(smbus, addr7, (byte)(mr + 1), out hi)) return false;
            milliC = RawToMilliC((hi << 8) | lo);
            return true;
        }

        /// <summary>
        /// Detect whether the device at the given I2C address is an SPD5118
        /// hub with temperature sensor support.
        /// </summary>
        internal static bool DetectNoLock(SmbusDriverBase smbus, byte i2cAddr)
        {
            try
            {
                byte mr0, mr1;
                if (!ReadMRNoLock(smbus, i2cAddr, REG_TYPE, out mr0)) return false;
                if (!ReadMRNoLock(smbus, i2cAddr, (byte)(REG_TYPE + 1), out mr1)) return false;
                // SPD5118 registers are big-endian: MR0 = MSB (0x51), MR1 = LSB (0x18)
                int deviceType = (mr0 << 8) | mr1;
                if (deviceType != 0x5118)
                    return false;

                byte cap;
                if (!ReadMRNoLock(smbus, i2cAddr, REG_CAPABILITY, out cap)) return false;
                return (cap & CAP_TS_SUPPORT) != 0;
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// Read the current temperature from the SPD5118 sensor.
        /// </summary>
        /// <returns>Temperature in millidegrees Celsius, or int.MinValue on error.</returns>
        internal static int ReadTemperatureMilliC(SmbusDriverBase smbus, byte i2cAddr)
        {
            int mc;
            if (!ReadMR16NoLock(smbus, i2cAddr, REG_TEMP, out mc))
                return int.MinValue;
            return mc;
        }

        /// <summary>
        /// Read the current temperature from the SPD5118 sensor.
        /// </summary>
        /// <returns>Temperature in degrees Celsius, or double.NaN on error.</returns>
        internal static double ReadTemperatureC(SmbusDriverBase smbus, byte i2cAddr)
        {
            int mc = ReadTemperatureMilliC(smbus, i2cAddr);
            if (mc == int.MinValue) return double.NaN;
            return mc / 1000.0;
        }

        /// <summary>
        /// Read all thermal sensor data: current temp, limits, and alarms.
        /// </summary>
        internal static Ddr5ThermalData ReadAllNoLock(SmbusDriverBase smbus, byte i2cAddr)
        {
            Ddr5ThermalData td = new Ddr5ThermalData();

            try
            {
                byte mr0, mr1;
                if (!ReadMRNoLock(smbus, i2cAddr, REG_TYPE, out mr0)) return td;
                if (!ReadMRNoLock(smbus, i2cAddr, (byte)(REG_TYPE + 1), out mr1)) return td;
                // SPD5118 registers are big-endian: MR0 = MSB, MR1 = LSB
                int deviceType = (mr0 << 8) | mr1;
                if (deviceType != 0x5118)
                    return td;

                byte cap;
                if (!ReadMRNoLock(smbus, i2cAddr, REG_CAPABILITY, out cap)) return td;
                td.TempSensorSupported = (cap & CAP_TS_SUPPORT) != 0;
                if (!td.TempSensorSupported)
                    return td;

                byte cfg;
                if (!ReadMRNoLock(smbus, i2cAddr, REG_TEMP_CONFIG, out cfg)) return td;
                td.TempSensorEnabled = (cfg & TS_DISABLE) == 0;

                if (!td.TempSensorEnabled)
                {
                    td.IsValid = true;
                    return td;
                }

                int mc;
                if (ReadMR16NoLock(smbus, i2cAddr, REG_TEMP, out mc))
                    td.TemperatureMilliC = mc;
                if (ReadMR16NoLock(smbus, i2cAddr, REG_TEMP_MAX, out mc))
                    td.TempMaxMilliC = mc;
                if (ReadMR16NoLock(smbus, i2cAddr, REG_TEMP_MIN, out mc))
                    td.TempMinMilliC = mc;
                if (ReadMR16NoLock(smbus, i2cAddr, REG_TEMP_CRIT, out mc))
                    td.TempCritMilliC = mc;
                if (ReadMR16NoLock(smbus, i2cAddr, REG_TEMP_LCRIT, out mc))
                    td.TempLCritMilliC = mc;

                byte status;
                if (ReadMRNoLock(smbus, i2cAddr, REG_TEMP_STATUS, out status))
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

        /// <summary>
        /// Scan all standard DDR5 SPD addresses (0x50-0x57) and read
        /// thermal data from every DIMM that has an SPD5118 hub with TS.
        /// </summary>
        internal static Dictionary<byte, Ddr5ThermalData> ReadAllDimmsNoLock(SmbusDriverBase smbus)
        {
            Dictionary<byte, Ddr5ThermalData> results =
                new Dictionary<byte, Ddr5ThermalData>();

            for (byte addr = 0x50; addr <= 0x57; addr++)
            {
                if (DetectNoLock(smbus, addr))
                    results[addr] = ReadAllNoLock(smbus, addr);
            }

            return results;
        }

        /// <summary>
        /// Print thermal sensor readings for all detected DDR5 DIMMs.
        /// </summary>
        public static void PrintAllDimms(SmbusDriverBase smbus)
        {
            if (!Mutexes.WaitSmbus(5000))
            {
                Debug.WriteLine("Failed to acquire SMBus mutex for reading DDR5 thermal sensors.");
                return;
            }

            Dictionary<byte, Ddr5ThermalData> data = default;

            try
            {
               data = ReadAllDimmsNoLock(smbus);

            }
            finally
            {
                Mutexes.ReleaseSmbus();
            }

            foreach (KeyValuePair<byte, Ddr5ThermalData> kvp in data)
            {
                Debug.WriteLine($"DIMM 0x{0:X2} Thermal Sensor: {kvp.Key}");
                Debug.WriteLine(kvp.Value.ToString());
            }
        }
    }
}
