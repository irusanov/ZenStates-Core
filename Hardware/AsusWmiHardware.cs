using System;
using System.Collections.Generic;
using System.Globalization;
using System.Management;
using System.Text;

namespace ZenStates.Core.Hardware
{
    /// <summary>
    /// The sensors of the ASUS WMI interface (WMI class ASUSHW in root\wmi), which some ASUS boards
    /// provide on top of the SuperIO chip: voltages, temperatures, fans, currents and water flow,
    /// read by the firmware from the SuperIO and the embedded controller.
    /// <para>
    /// Every sensor is its own WMI method call, so reading all of them takes noticeably longer than a
    /// SuperIO chip. <see cref="SystemInfo.UpdateSensors"/> leaves this group out: call
    /// <see cref="Update()"/> while the sensors are shown, or <see cref="Update(Sensor)"/> for one
    /// sensor.
    /// </para>
    /// </summary>
    public sealed class AsusWmiHardware : IHardware, IDisposable
    {
        /// <summary>
        /// The group name, which is also the heading of the debug report section. The heading and the
        /// "Name: value" lines of <see cref="GetReport"/> are those of the reports before this class,
        /// so <see cref="CreateFromReport"/> reads both.
        /// </summary>
        public const string GroupName = "ASUS WMI";

        private const string Scope = "root\\wmi";
        private const string ClassName = "ASUSHW";

        // Enums from https://github.com/electrified/asus-wmi-sensors
        public enum AsusSensorType : uint
        {
            VOLTAGE = 0x0,
            TEMPERATURE_C = 0x1,
            FAN_RPM = 0x2,
            CURRENT = 0x3,
            WATER_FLOW = 0x4
        }

        public enum AsusSensorDataType : uint
        {
            SIGNED_INT = 0x0,
            UNSIGNED_INT = 0x1,
            BOOL = 0x2,
            SCALED = 0x3
        }

        public enum AsusSensorSource : uint
        {
            SIO = 0x1,
            EC = 0x2
        }

        public enum AsusSensorLocation : uint
        {
            CPU = 0x0,
            CPU_SOC = 0x1,
            DRAM = 0x2,
            MOTHERBOARD = 0x3,
            CHIPSET = 0x4,
            AUX = 0x5,
            VRM = 0x6,
            COOLER = 0x7
        }

        /// <summary>
        /// What the firmware reports about a sensor (sample from a Crosshair VI Hero: "CPU Core Voltage",
        /// data type 3, location 0, source 1, type 0).
        /// </summary>
        public sealed class AsusSensorInfo
        {
            public byte Index { get; internal set; }
            public string Name { get; internal set; }
            public AsusSensorType Type { get; internal set; }
            public AsusSensorDataType DataType { get; internal set; }
            public AsusSensorSource Source { get; internal set; }
            public AsusSensorLocation Location { get; internal set; }
        }

        private readonly object _sync = new object();
        private readonly Sensor[] _sensors;
        private readonly AsusSensorInfo[] _info;
        private readonly AsusSensorSource[] _sources;
        private ManagementObject _instance;
        private bool _disposed;

        private AsusWmiHardware(ManagementObject instance, uint version, List<AsusSensorInfo> info)
        {
            _instance = instance;
            Version = version;
            _info = info.ToArray();
            _sensors = new Sensor[_info.Length];

            List<AsusSensorSource> sources = new List<AsusSensorSource>();
            for (int i = 0; i < _info.Length; i++)
            {
                _sensors[i] = new Sensor(_info[i].Name, i, GetSensorType(_info[i].Type));
                if (!sources.Contains(_info[i].Source))
                    sources.Add(_info[i].Source);
            }

            _sources = sources.ToArray();
        }

        /// <summary>
        /// Opens the interface and reads the sensor list and the first values. Null when the board
        /// has no ASUS WMI sensors.
        /// </summary>
        public static AsusWmiHardware TryCreate()
        {
            ManagementObject instance = null;
            try
            {
                WMI.Connect(Scope);

                string instanceName = WMI.GetInstanceName(Scope, ClassName);
                if (string.IsNullOrEmpty(instanceName))
                    return null;

                instance = new ManagementObject(Scope, $"{ClassName}.InstanceName='{instanceName}'", null);

                TryInvoke(instance, "sensor_get_version", null, 0, out uint version);
                if (!TryInvoke(instance, "sensor_get_number", null, 0, out uint count) || count == 0)
                {
                    instance.Dispose();
                    return null;
                }

                List<AsusSensorInfo> info = new List<AsusSensorInfo>();
                for (uint i = 0; i < count && i <= byte.MaxValue; i++)
                {
                    AsusSensorInfo sensor = ReadSensorInfo(instance, (byte)i);
                    if (sensor != null)
                        info.Add(sensor);
                }

                if (info.Count == 0)
                {
                    instance.Dispose();
                    return null;
                }

                // By type, in firmware order within a type
                info.Sort((a, b) => a.Type != b.Type ? a.Type.CompareTo(b.Type) : a.Index.CompareTo(b.Index));

                AsusWmiHardware hardware = new AsusWmiHardware(instance, version, info);
                hardware.Update();
                return hardware;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"ASUS WMI: {ex.Message}");
                instance?.Dispose();
                return null;
            }
        }

        public HardwareType HardwareType => HardwareType.AsusWmi;

        public string Name => GroupName;

        /// <summary>The version the interface reports (sensor_get_version).</summary>
        public uint Version { get; }

        public IEnumerable<Sensor> Sensors => _sensors;

        public bool HasSensors => _sensors.Length > 0;

        /// <summary>What the firmware reports about each sensor, in the order of <see cref="Sensors"/>.</summary>
        public IList<AsusSensorInfo> SensorInfo => Array.AsReadOnly(_info);

        /// <summary>The sensor with this name, case ignored; null when there is none.</summary>
        public Sensor FindSensor(string name)
        {
            foreach (Sensor sensor in _sensors)
            {
                if (string.Equals(sensor.Name, name, StringComparison.OrdinalIgnoreCase))
                    return sensor;
            }

            return null;
        }

        /// <summary>What the firmware reports about this sensor; null when it isn't one of this group.</summary>
        public AsusSensorInfo GetInfo(Sensor sensor)
        {
            int index = IndexOf(sensor);
            return index >= 0 ? _info[index] : null;
        }

        /// <summary>True for sensors rebuilt from a debug report, whose values are the captured ones.</summary>
        public bool IsReplay => _instance == null;

        /// <summary>Refreshes the firmware buffers, then reads every sensor. Does nothing for a replay.</summary>
        public void Update()
        {
            lock (_sync)
            {
                if (_disposed || IsReplay)
                    return;

                foreach (AsusSensorSource source in _sources)
                    UpdateBuffer(source);

                for (int i = 0; i < _sensors.Length; i++)
                    _sensors[i].Value = ReadValue(_info[i]);
            }
        }

        /// <summary>Refreshes the buffer of this sensor's source, then reads only this sensor.</summary>
        public bool Update(Sensor sensor)
        {
            int index = IndexOf(sensor);
            if (index < 0)
                return false;

            lock (_sync)
            {
                if (_disposed || IsReplay)
                    return _sensors[index].Value.HasValue;

                UpdateBuffer(_info[index].Source);
                _sensors[index].Value = ReadValue(_info[index]);
                return _sensors[index].Value.HasValue;
            }
        }

        /// <summary>The value with its unit, as the ASUS tools show it ("1.3500V", "45°C"); "N/A" without a value.</summary>
        public string FormatValue(Sensor sensor)
        {
            if (sensor == null || !sensor.Value.HasValue)
                return "N/A";

            float value = sensor.Value.Value;
            switch (sensor.SensorType)
            {
                case SensorType.Voltage:
                    return value.ToString("F4", CultureInfo.InvariantCulture) + "V";
                case SensorType.Temperature:
                    return value.ToString("0.##", CultureInfo.InvariantCulture) + "°C";
                case SensorType.Fan:
                    return value.ToString("0", CultureInfo.InvariantCulture) + "RPM";
                case SensorType.Current:
                    return value.ToString("0.##", CultureInfo.InvariantCulture) + "A";
                default:
                    return value.ToString("0.##", CultureInfo.InvariantCulture);
            }
        }

        /// <summary>
        /// The body of the debug report section (headed <see cref="GroupName"/>): a "Name: value" line per
        /// sensor, the value with its unit, in the format of the reports before this class.
        /// </summary>
        public string GetReport()
        {
            StringBuilder report = new StringBuilder();

            foreach (Sensor sensor in _sensors)
                report.AppendLine(string.Format(CultureInfo.InvariantCulture, "{0,-25}{1}", sensor.Name + ": ", FormatValue(sensor)));

            return report.ToString();
        }

        /// <summary>
        /// Rebuilds the sensors from the lines of a debug report's section, old or new: the type comes from
        /// the unit of each value. Null when there are none. The values don't change; <see cref="Update()"/>
        /// does nothing.
        /// </summary>
        public static AsusWmiHardware CreateFromReport(string[] sectionLines)
        {
            if (sectionLines == null)
                return null;

            List<AsusSensorInfo> info = new List<AsusSensorInfo>();
            List<float?> values = new List<float?>();

            foreach (string line in sectionLines)
            {
                string text = line.Trim();
                if (text.Length == 0)
                    continue;

                // A failed read ends the section, the line after it is the error message
                if (text.StartsWith("<FAILED>", StringComparison.Ordinal))
                    break;

                int colon = text.LastIndexOf(':');
                if (colon <= 0 || info.Count > byte.MaxValue)
                    continue;

                string name = text.Substring(0, colon).Trim();
                if (name.Length == 0 || !TryParseFormattedValue(text.Substring(colon + 1).Trim(), out AsusSensorType type, out float? value))
                    continue;

                info.Add(new AsusSensorInfo { Index = (byte)info.Count, Name = name, Type = type });
                values.Add(value);
            }

            if (info.Count == 0)
                return null;

            AsusWmiHardware hardware = new AsusWmiHardware(null, 0, info);
            for (int i = 0; i < values.Count; i++)
                hardware._sensors[i].Value = values[i];

            return hardware;
        }

        /// <summary>
        /// Reads a value as <see cref="FormatValue"/> and the reports before it wrote it: "1.3500V" (or
        /// "1,3500V" in some locales), "45°C", "1200RPM", "3A", a bare number for water flow, "N/A" for none.
        /// </summary>
        internal static bool TryParseFormattedValue(string text, out AsusSensorType type, out float? value)
        {
            type = AsusSensorType.WATER_FLOW;
            value = null;

            if (string.IsNullOrEmpty(text))
                return false;

            if (text == "N/A")
                return true;

            int end = text.Length;
            while (end > 0 && !char.IsDigit(text[end - 1]))
                end--;

            if (end == 0 || !float.TryParse(text.Substring(0, end).Replace(',', '.'), NumberStyles.Float, CultureInfo.InvariantCulture, out float number))
                return false;

            // The unit; the degree sign may have come through as another character
            string unit = text.Substring(end).Trim();
            if (unit.EndsWith("RPM", StringComparison.OrdinalIgnoreCase))
                type = AsusSensorType.FAN_RPM;
            else if (unit.EndsWith("C", StringComparison.Ordinal))
                type = AsusSensorType.TEMPERATURE_C;
            else if (unit == "V")
                type = AsusSensorType.VOLTAGE;
            else if (unit == "A")
                type = AsusSensorType.CURRENT;
            else if (unit.Length != 0)
                return false;

            value = number;
            return true;
        }

        public void Dispose()
        {
            lock (_sync)
            {
                if (_disposed)
                    return;

                _disposed = true;
                _instance?.Dispose();
                _instance = null;
            }
        }

        private int IndexOf(Sensor sensor)
        {
            if (sensor == null)
                return -1;

            for (int i = 0; i < _sensors.Length; i++)
            {
                if (ReferenceEquals(_sensors[i], sensor))
                    return i;
            }

            return -1;
        }

        private void UpdateBuffer(AsusSensorSource source)
        {
            TryInvoke(_instance, "sensor_update_buffer", "Source", (uint)source, out _);
        }

        private float? ReadValue(AsusSensorInfo info)
        {
            if (!TryInvoke(_instance, "sensor_get_value", "Index", info.Index, out uint raw))
                return null;

            return ToValue(info, raw);
        }

        // Voltages come in µV; temperatures in °C, fans in RPM and currents in A, as the Linux
        // asus_wmi_sensors driver reads them.
        internal static float ToValue(AsusSensorInfo info, uint raw)
        {
            float value = info.DataType == AsusSensorDataType.SIGNED_INT ? unchecked((int)raw) : (float)raw;

            if (info.Type == AsusSensorType.VOLTAGE)
                return value / 1000000.0f;

            return value;
        }

        internal static SensorType GetSensorType(AsusSensorType type)
        {
            switch (type)
            {
                case AsusSensorType.VOLTAGE: return SensorType.Voltage;
                case AsusSensorType.TEMPERATURE_C: return SensorType.Temperature;
                case AsusSensorType.FAN_RPM: return SensorType.Fan;
                case AsusSensorType.CURRENT: return SensorType.Current;
                case AsusSensorType.WATER_FLOW: return SensorType.Flow;
                default: return SensorType.Factor;
            }
        }

        private static AsusSensorInfo ReadSensorInfo(ManagementObject instance, byte index)
        {
            try
            {
                using (ManagementBaseObject res = WMI.InvokeMethod(instance, "sensor_get_info", "Index", index))
                {
                    if (res == null)
                        return null;

                    string name = res["Name"] as string;
                    if (string.IsNullOrEmpty(name))
                        return null;

                    return new AsusSensorInfo
                    {
                        Index = index,
                        Name = name.Trim(),
                        DataType = (AsusSensorDataType)Convert.ToUInt32(res["Data_Type"], CultureInfo.InvariantCulture),
                        Location = (AsusSensorLocation)Convert.ToUInt32(res["Location"], CultureInfo.InvariantCulture),
                        Source = (AsusSensorSource)Convert.ToUInt32(res["Source"], CultureInfo.InvariantCulture),
                        Type = (AsusSensorType)Convert.ToUInt32(res["Type"], CultureInfo.InvariantCulture),
                    };
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"ASUS WMI: sensor {index}: {ex.Message}");
                return null;
            }
        }

        private static bool TryInvoke(ManagementObject instance, string method, string inParamName, uint arg, out uint data)
        {
            data = 0;
            if (instance == null)
                return false;

            try
            {
                using (ManagementBaseObject res = WMI.InvokeMethod(instance, method, inParamName, arg))
                {
                    if (res == null)
                        return false;

                    data = Convert.ToUInt32(res["Data"], CultureInfo.InvariantCulture);
                    return true;
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"ASUS WMI: {method}: {ex.Message}");
                return false;
            }
        }
    }
}
