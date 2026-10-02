using System.Text;

namespace ZenStates.Core.Hardware.DRAM.DDR4.Thermal
{
    /// <summary>
    /// Readings of a DDR4 module thermal sensor (TSOD, JEDEC TSE2004av / JC-42.4).
    /// </summary>
    public class Ddr4ThermalData
    {
        /// <summary>Whether the sensor was detected and read.</summary>
        public bool IsValid;

        /// <summary>7-bit address of the sensor (0x18-0x1F).</summary>
        public byte I2cAddress;

        /// <summary>7-bit address of the SPD EEPROM of the same module (0x50-0x57).</summary>
        public byte SpdAddress;

        /// <summary>SMBus port the module was found on.</summary>
        public int Port;

        /// <summary>Capability register (0x00).</summary>
        public ushort Capabilities;

        /// <summary>Configuration register (0x01).</summary>
        public ushort Configuration;

        /// <summary>Manufacturer ID register (0x06).</summary>
        public ushort ManufacturerId;

        /// <summary>Device ID (high byte) and revision (low byte) register (0x07).</summary>
        public ushort DeviceId;

        /// <summary>Sensor vendor, from the manufacturer ID.</summary>
        public string Vendor;

        /// <summary>False when the sensor is shut down (configuration bit 8).</summary>
        public bool TempSensorEnabled;

        /// <summary>Resolution of the temperature reading in millidegrees Celsius (500, 250, 125 or 62).</summary>
        public int ResolutionMilliC;

        /// <summary>Whether the sensor reads temperatures below 0 °C (capability bit 2).</summary>
        public bool SupportsNegative;

        /// <summary>Current temperature in millidegrees Celsius.</summary>
        public int TemperatureMilliC;

        /// <summary>Current temperature in degrees Celsius.</summary>
        public double TemperatureC { get { return TemperatureMilliC / 1000.0; } }

        /// <summary>High limit (alarm window upper bound) in millidegrees Celsius.</summary>
        public int TempMaxMilliC;

        /// <summary>Low limit (alarm window lower bound) in millidegrees Celsius.</summary>
        public int TempMinMilliC;

        /// <summary>Critical limit in millidegrees Celsius.</summary>
        public int TempCritMilliC;

        /// <summary>Temperature is above the high limit.</summary>
        public bool AlarmHigh;

        /// <summary>Temperature is below the low limit.</summary>
        public bool AlarmLow;

        /// <summary>Temperature is at or above the critical limit.</summary>
        public bool AlarmCritHigh;

        public override string ToString()
        {
            if (!IsValid)
                return "  Thermal sensor: not available";

            StringBuilder sb = new StringBuilder();
            sb.AppendFormat("  Sensor           : 0x{0:X2} (SPD 0x{1:X2}, port {2})\n", I2cAddress, SpdAddress, Port);
            sb.AppendFormat("  Vendor           : {0} (0x{1:X4}), device 0x{2:X2} rev 0x{3:X2}\n",
                Vendor, ManufacturerId, DeviceId >> 8, DeviceId & 0xFF);
            sb.AppendFormat("  Capabilities     : 0x{0:X4}, Config: 0x{1:X4}\n", Capabilities, Configuration);

            if (!TempSensorEnabled)
            {
                sb.Append("  Sensor is shut down\n");
                return sb.ToString();
            }

            sb.AppendFormat("  Current          : {0:F2} C (resolution {1:F4} C)\n", TemperatureC, ResolutionMilliC / 1000.0);
            sb.AppendFormat("  High Limit       : {0}{1}\n", FormatLimit(TempMaxMilliC), AlarmHigh ? "  ** ALARM **" : "");
            sb.AppendFormat("  Low Limit        : {0}{1}\n", FormatLimit(TempMinMilliC), AlarmLow ? "  ** ALARM **" : "");
            sb.AppendFormat("  Critical         : {0}{1}\n", FormatLimit(TempCritMilliC), AlarmCritHigh ? "  ** ALARM **" : "");

            return sb.ToString();
        }

        // A limit of 0 C or below is the power-on default: not programmed by the BIOS.
        private static string FormatLimit(int milliC)
        {
            return milliC > 0 ? string.Format("{0:F2} C", milliC / 1000.0) : "not set";
        }
    }
}
