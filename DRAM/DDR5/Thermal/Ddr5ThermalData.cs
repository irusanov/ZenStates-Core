using System.Text;

namespace ZenStates.Core
{
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
}
