using System;
using System.Collections.Generic;
using System.Diagnostics;
using ZenStates.Core.Drivers;

namespace ZenStates.Core.Hardware.DRAM.DDR4.Thermal
{
    /// <summary>
    /// DDR4 module thermal sensors (TSOD): JEDEC TSE2004av, register compatible with JC-42.4.
    ///
    /// The sensor of a module shares the address select pins with its SPD EEPROM (EE1004), so the module
    /// whose SPD answers at 0x50 + n has its sensor at 0x18 + n. Access is read-only: nothing is written
    /// to the sensor or the EEPROM, and the EE1004 page is never changed.
    ///
    /// The 16-bit registers are sent MSB first, the opposite of an SMBus word read, so the bytes are swapped.
    /// </summary>
    internal static class Ddr4ThermalSensor
    {
        // Registers
        private const byte REG_CAPABILITY = 0x00;
        private const byte REG_CONFIG = 0x01;
        private const byte REG_TEMP_HIGH = 0x02;
        private const byte REG_TEMP_LOW = 0x03;
        private const byte REG_TEMP_CRIT = 0x04;
        private const byte REG_TEMP = 0x05;
        private const byte REG_MANUFACTURER = 0x06;
        private const byte REG_DEVICE = 0x07;

        // Capability register
        private const ushort CAP_RESERVED = 0xFF00;
        private const ushort CAP_RANGE = 0x0004;      // bit 2: reads temperatures below 0 C
        private const int CAP_RESOLUTION_SHIFT = 3;   // bits 4:3: 0.5, 0.25, 0.125, 0.0625 C

        // Configuration register
        private const ushort CONFIG_RESERVED = 0xF800;
        private const ushort CONFIG_SHUTDOWN = 0x0100; // bit 8

        // Temperature register flags
        private const ushort TEMP_CRIT = 0x8000;       // at or above the critical limit
        private const ushort TEMP_HIGH = 0x4000;       // above the high limit
        private const ushort TEMP_LOW = 0x2000;        // below the low limit

        // Plausible range of a first reading, to reject devices that only look like a sensor
        private const int MIN_PLAUSIBLE_MILLI_C = -40000;
        private const int MAX_PLAUSIBLE_MILLI_C = 125000;

        private const byte SPD_ADDR_FIRST = 0x50;
        private const byte TSOD_ADDR_FIRST = 0x18;
        private const int SLOT_COUNT = 8;

        // SMBus ports the modules are looked for on, in order (see Ddr5SpdReader)
        private static readonly int[] Ports = new int[] { 0, 2 };

        /// <summary>Sensor vendors by manufacturer ID (register 0x06), as listed by the Linux jc42 driver.</summary>
        private static readonly Dictionary<ushort, string> Vendors = new Dictionary<ushort, string>
        {
            { 0x001F, "Atmel" },
            { 0x004D, "Maxim" },
            { 0x0054, "Microchip" },
            { 0x00B3, "IDT" },
            { 0x104A, "STMicroelectronics" },
            { 0x1114, "Atmel" },
            { 0x1131, "NXP" },
            { 0x11D4, "Analog Devices" },
            { 0x1B09, "ON Semiconductor" },
            { 0x1C68, "Giantec" },
            { 0x1C85, "ABLIC (Seiko)" },
        };

        internal static string GetVendorName(ushort manufacturerId)
        {
            return Vendors.TryGetValue(manufacturerId, out string name) ? name : "Unknown";
        }

        /// <summary>Converts a temperature or limit register to millidegrees Celsius (13-bit two's complement, 0.0625 C per LSB).</summary>
        internal static int RawToMilliC(ushort raw)
        {
            int value = raw & 0x1FFF;
            if ((value & 0x1000) != 0)
                value -= 0x2000;
            return value * 125 / 2;
        }

        private static bool ReadRegisterNoLock(SmbusDriverBase smbus, byte addr7, byte register, out ushort value)
        {
            value = 0;
            if (!smbus.ReadWordDataNoLock(addr7, register, out ushort word))
                return false;

            value = (ushort)((word >> 8) | ((word & 0xFF) << 8));
            return true;
        }

        private static int ResolutionMilliC(ushort capabilities)
        {
            switch ((capabilities >> CAP_RESOLUTION_SHIFT) & 0x3)
            {
                case 0: return 500;
                case 1: return 250;
                case 2: return 125;
                default: return 62;
            }
        }

        /// <summary>
        /// Whether a limit register holds a real limit. The limits power up as 0 C and many BIOSes never program
        /// them, and the sensor still compares against them: with a critical limit of 0 C the "at or above
        /// critical" flag is set at any room temperature.
        /// </summary>
        internal static bool IsLimitSet(int limitMilliC)
        {
            return limitMilliC > 0;
        }

        private static void ApplyTemperature(Ddr4ThermalData td, ushort raw)
        {
            td.TemperatureMilliC = RawToMilliC(raw);

            // The flags only mean something against limits that were actually set.
            td.AlarmCritHigh = (raw & TEMP_CRIT) != 0 && IsLimitSet(td.TempCritMilliC);
            td.AlarmHigh = (raw & TEMP_HIGH) != 0 && IsLimitSet(td.TempMaxMilliC);
            td.AlarmLow = (raw & TEMP_LOW) != 0 && IsLimitSet(td.TempMinMilliC);
        }

        /// <summary>
        /// Identifies a TSE2004av / JC-42.4 sensor at <paramref name="addr7"/> and reads it.
        /// </summary>
        /// <returns>The sensor data, or null when no sensor is found at the address.</returns>
        internal static Ddr4ThermalData ReadAllNoLock(SmbusDriverBase smbus, byte addr7)
        {
            try
            {
                if (!ReadRegisterNoLock(smbus, addr7, REG_CAPABILITY, out ushort capabilities)
                    || !ReadRegisterNoLock(smbus, addr7, REG_CONFIG, out ushort config)
                    || !ReadRegisterNoLock(smbus, addr7, REG_MANUFACTURER, out ushort manufacturer)
                    || !ReadRegisterNoLock(smbus, addr7, REG_DEVICE, out ushort device)
                    || !ReadRegisterNoLock(smbus, addr7, REG_TEMP, out ushort temperature))
                    return null;

                // Same checks as the Linux jc42 driver: reserved bits clear, a real manufacturer ID
                if ((capabilities & CAP_RESERVED) != 0 || (config & CONFIG_RESERVED) != 0)
                    return null;
                if (manufacturer == 0x0000 || manufacturer == 0xFFFF)
                    return null;

                Ddr4ThermalData td = new Ddr4ThermalData
                {
                    I2cAddress = addr7,
                    Capabilities = capabilities,
                    Configuration = config,
                    ManufacturerId = manufacturer,
                    DeviceId = device,
                    Vendor = GetVendorName(manufacturer),
                    TempSensorEnabled = (config & CONFIG_SHUTDOWN) == 0,
                    ResolutionMilliC = ResolutionMilliC(capabilities),
                    SupportsNegative = (capabilities & CAP_RANGE) != 0,
                };

                // Limits first: the alarm flags of the temperature register are judged against them
                if (ReadRegisterNoLock(smbus, addr7, REG_TEMP_HIGH, out ushort high))
                    td.TempMaxMilliC = RawToMilliC(high);
                if (ReadRegisterNoLock(smbus, addr7, REG_TEMP_LOW, out ushort low))
                    td.TempMinMilliC = RawToMilliC(low);
                if (ReadRegisterNoLock(smbus, addr7, REG_TEMP_CRIT, out ushort crit))
                    td.TempCritMilliC = RawToMilliC(crit);

                ApplyTemperature(td, temperature);

                if (td.TempSensorEnabled
                    && (td.TemperatureMilliC < MIN_PLAUSIBLE_MILLI_C || td.TemperatureMilliC > MAX_PLAUSIBLE_MILLI_C))
                    return null;

                td.IsValid = true;
                return td;
            }
            catch (Exception ex)
            {
                Debug.WriteLine(string.Format("DDR4 thermal sensor 0x{0:X2}: {1}", addr7, ex.Message));
                return null;
            }
        }

        /// <summary>
        /// Reads the current temperature and alarm flags into <paramref name="td"/>. The bus must be on the sensor's port.
        /// </summary>
        internal static bool RefreshTemperatureNoLock(SmbusDriverBase smbus, Ddr4ThermalData td)
        {
            if (td == null || !td.IsValid || !td.TempSensorEnabled)
                return false;

            try
            {
                if (!ReadRegisterNoLock(smbus, td.I2cAddress, REG_TEMP, out ushort raw))
                    return false;

                ApplyTemperature(td, raw);
                return true;
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// Finds the installed modules and reads their sensors. Tries the SMBus ports in order and stops at the
        /// first one with a module SPD on it. Restores the previously selected port.
        /// </summary>
        /// <returns>
        /// One entry per module SPD found, keyed by the SPD address and in address order (the order of the
        /// modules). The value is null for a module without a (working) sensor.
        /// </returns>
        internal static Dictionary<byte, Ddr4ThermalData> ScanNoLock(SmbusDriverBase smbus)
        {
            Dictionary<byte, Ddr4ThermalData> result = new Dictionary<byte, Ddr4ThermalData>();

            if (smbus == null || !smbus.ChangePortNoLock(-1, out int savedPort))
                return result;

            try
            {
                for (int p = 0; p < Ports.Length; p++)
                {
                    if (!smbus.ChangePortNoLock(Ports[p]))
                        continue;

                    for (int slot = 0; slot < SLOT_COUNT; slot++)
                    {
                        byte spdAddr = (byte)(SPD_ADDR_FIRST + slot);

                        // An EE1004 answers a read on any page; the page itself is left alone.
                        if (!smbus.ReadByteDataNoLock(spdAddr, 0, out byte _))
                            continue;

                        Ddr4ThermalData td = ReadAllNoLock(smbus, (byte)(TSOD_ADDR_FIRST + slot));
                        if (td != null)
                        {
                            td.SpdAddress = spdAddr;
                            td.Port = Ports[p];
                        }

                        result[spdAddr] = td;
                    }

                    if (result.Count > 0)
                        break;
                }
            }
            finally
            {
                if (savedPort >= 0)
                    smbus.ChangePortNoLock(savedPort);
            }

            return result;
        }

        /// <summary>
        /// Refreshes the temperature of every sensor in <paramref name="sensors"/>, switching to each sensor's
        /// port as needed. Restores the previously selected port.
        /// </summary>
        /// <returns>True if at least one sensor was read.</returns>
        internal static bool RefreshAllNoLock(SmbusDriverBase smbus, Dictionary<byte, Ddr4ThermalData> sensors)
        {
            if (smbus == null || sensors == null || sensors.Count == 0)
                return false;

            if (!smbus.ChangePortNoLock(-1, out int savedPort))
                return false;

            bool updated = false;
            int currentPort = savedPort;

            try
            {
                foreach (Ddr4ThermalData td in sensors.Values)
                {
                    if (td == null || !td.IsValid || !td.TempSensorEnabled)
                        continue;

                    if (td.Port != currentPort)
                    {
                        if (!smbus.ChangePortNoLock(td.Port))
                            continue;
                        currentPort = td.Port;
                    }

                    if (RefreshTemperatureNoLock(smbus, td))
                        updated = true;
                }
            }
            finally
            {
                if (savedPort >= 0 && currentPort != savedPort)
                    smbus.ChangePortNoLock(savedPort);
            }

            return updated;
        }

        /// <summary>Finds and reads the sensors of all modules, holding the SMBus mutex.</summary>
        internal static Dictionary<byte, Ddr4ThermalData> ReadAll(SmbusDriverBase smbus)
        {
            if (!Mutexes.WaitSmbus(5000))
            {
                Debug.WriteLine("Failed to acquire SMBus mutex for reading DDR4 thermal sensors.");
                return new Dictionary<byte, Ddr4ThermalData>();
            }

            try
            {
                return ScanNoLock(smbus);
            }
            catch (Exception ex)
            {
                Debug.WriteLine(string.Format("DDR4 thermal sensors: {0}", ex.Message));
                return new Dictionary<byte, Ddr4ThermalData>();
            }
            finally
            {
                Mutexes.ReleaseSmbus();
            }
        }
    }
}
