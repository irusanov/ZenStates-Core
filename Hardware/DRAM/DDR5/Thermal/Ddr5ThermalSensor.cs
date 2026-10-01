using ZenStates.Core.Drivers;
using ZenStates.Core.Hardware.DRAM.DDR5.Hub;
using static ZenStates.Core.Hardware.DRAM.DDR5.Hub.Spd5118Registers;

namespace ZenStates.Core.Hardware.DRAM.DDR5.Thermal
{
    /// <summary>
    /// Temperature sensor built into the SPD5118 hub (JESD300-5 MR26~MR51). The registers are volatile, so the hub
    /// must be on NVM page 0 (see <see cref="Spd5118Hub.RestorePage0NoLock"/>). All methods expect the SMBus mutex
    /// to be held and the bus to be on the hub port.
    /// </summary>
    internal static class Ddr5ThermalSensor
    {
        // MR19: clears MR51 [3:0]
        private const byte CLEAR_ALL_STATUS = MR51_CRIT_LOW | MR51_CRIT_HIGH | MR51_LOW | MR51_HIGH;

        // Temperature and limit registers are 16 bits, low byte first.
        private static bool ReadTemperatureNoLock(SmbusDriverBase smbus, byte addr7, byte mr, out int milliC)
        {
            milliC = 0;
            if (!smbus.ReadByteDataNoLock(addr7, mr, out byte lo) ||
                !smbus.ReadByteDataNoLock(addr7, (byte)(mr + 1), out byte hi))
                return false;

            milliC = TemperatureToMilliC((hi << 8) | lo);
            return true;
        }

        private static void ReadTemperatureInto(SmbusDriverBase smbus, byte addr7, byte mr, ref int field)
        {
            if (ReadTemperatureNoLock(smbus, addr7, mr, out int milliC))
                field = milliC;
        }

        /// <summary>True when the device is an SPD5118 hub that reports a temperature sensor (MR5 [1]).</summary>
        internal static bool DetectNoLock(SmbusDriverBase smbus, byte addr7)
        {
            try
            {
                return Spd5118Hub.IsHubNoLock(smbus, addr7) &&
                       smbus.ReadByteDataNoLock(addr7, MR5_CAPABILITY, out byte capability) &&
                       (capability & MR5_TS_SUPPORT) != 0;
            }
            catch
            {
                return false;
            }
        }

        // Reads the alarm flags into td and clears them on the device.
        private static bool ReadAndClearAlarmStatusNoLock(SmbusDriverBase smbus, byte addr7, Ddr5ThermalData td)
        {
            if (!smbus.ReadByteDataNoLock(addr7, MR51_TS_STATUS, out byte status))
                return false;

            td.AlarmHigh = (status & MR51_HIGH) != 0;
            td.AlarmLow = (status & MR51_LOW) != 0;
            td.AlarmCritHigh = (status & MR51_CRIT_HIGH) != 0;
            td.AlarmCritLow = (status & MR51_CRIT_LOW) != 0;

            smbus.WriteByteDataNoLock(addr7, MR19_CLEAR_TS_STATUS, CLEAR_ALL_STATUS);
            return true;
        }

        /// <summary>
        /// Updates the temperature and alarm flags of <paramref name="td"/> in place, keeping the limits read before.
        /// Clears the alarm flags on the device.
        /// </summary>
        internal static bool RefreshTemperatureAndStatusNoLock(SmbusDriverBase smbus, byte addr7, Ddr5ThermalData td)
        {
            try
            {
                if (!ReadTemperatureNoLock(smbus, addr7, MR49_TS_TEMPERATURE, out int milliC))
                    return false;

                td.TemperatureMilliC = milliC;
                return ReadAndClearAlarmStatusNoLock(smbus, addr7, td);
            }
            catch
            {
                return false;
            }
        }

        /// <summary>Reads the configuration, temperature, limits and alarm flags.</summary>
        internal static Ddr5ThermalData ReadAllRegsNoLock(SmbusDriverBase smbus, byte addr7)
        {
            Ddr5ThermalData td = new Ddr5ThermalData();

            try
            {
                td.TempSensorSupported = DetectNoLock(smbus, addr7);
                if (!td.TempSensorSupported)
                    return td;

                if (!smbus.ReadByteDataNoLock(addr7, MR26_TS_CONFIG, out byte config))
                    return td;

                td.TempSensorEnabled = (config & MR26_TS_DISABLE) == 0;
                td.IsValid = true;

                if (!td.TempSensorEnabled)
                    return td;

                ReadTemperatureInto(smbus, addr7, MR49_TS_TEMPERATURE, ref td.TemperatureMilliC);
                ReadTemperatureInto(smbus, addr7, MR28_TS_HIGH_LIMIT, ref td.TempMaxMilliC);
                ReadTemperatureInto(smbus, addr7, MR30_TS_LOW_LIMIT, ref td.TempMinMilliC);
                ReadTemperatureInto(smbus, addr7, MR32_TS_CRIT_HIGH, ref td.TempCritMilliC);
                ReadTemperatureInto(smbus, addr7, MR34_TS_CRIT_LOW, ref td.TempLCritMilliC);

                ReadAndClearAlarmStatusNoLock(smbus, addr7, td);
            }
            catch
            {
                td.IsValid = false;
            }

            return td;
        }
    }
}
