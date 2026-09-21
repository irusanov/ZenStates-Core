using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace ZenStates.Core.Hardware
{
    /// <summary>An SVI3 voltage rail reported in the power table.</summary>
    public enum Svi3Rail
    {
        Vdd,
        Vdd1,
        Soc,
        Misc,
    }

    /// <summary>A reading of an SVI3 rail as the power table carries it.</summary>
    public enum Svi3Reading
    {
        Vid,
        Voltage,
        Current,
        VrmTemperature,
    }

    /// <summary>
    /// The SVI3 VRM telemetry the SMU copies into the power table, exposed as sensors in the same
    /// way as a SuperIO chip: for each rail the measured voltage, VID, current, power (voltage x
    /// current) and VRM temperature. Rails and readings the table doesn't carry are left out.
    /// <para>
    /// The values come from <see cref="PowerTable"/>, so <see cref="Update"/> reflects the last
    /// <see cref="PowerTable.Refresh"/>.
    /// </para>
    /// </summary>
    public sealed class Svi3Hardware : IHardware
    {
        public const string GroupName = "SVI3 Telemetry";

        private static readonly Svi3Rail[] Rails = { Svi3Rail.Vdd, Svi3Rail.Vdd1, Svi3Rail.Soc, Svi3Rail.Misc };

        private sealed class SensorSource
        {
            public Sensor Sensor;
            public Svi3Rail Rail;
            public Svi3Reading Reading;
            public bool IsPower;
        }

        private readonly PowerTable _powerTable;
        private readonly object _createLock = new object();

        // Replaced as a whole once the sensors exist, never modified in place, so readers on other
        // threads can enumerate them without locking.
        private volatile Sensor[] _sensors = new Sensor[0];
        private volatile SensorSource[] _sources = new SensorSource[0];

        public Svi3Hardware(PowerTable powerTable)
        {
            _powerTable = powerTable ?? throw new ArgumentNullException(nameof(powerTable));
        }

        public HardwareType HardwareType => HardwareType.Svi3;

        public string Name => GroupName;

        public IEnumerable<Sensor> Sensors
        {
            get
            {
                EnsureSensors();
                return _sensors;
            }
        }

        public bool HasSensors
        {
            get
            {
                EnsureSensors();
                return _sensors.Length > 0;
            }
        }

        public void Update()
        {
            EnsureSensors();

            foreach (SensorSource source in _sources)
                source.Sensor.Value = Read(source);
        }

        public string GetReport()
        {
            StringBuilder report = new StringBuilder();
            report.AppendLine(GroupName);

            foreach (Sensor sensor in Sensors)
            {
                string value = sensor.Value.HasValue
                    ? sensor.Value.Value.ToString("F4", CultureInfo.InvariantCulture)
                    : "N/A";
                report.AppendLine($"{sensor.Name}: {value}");
            }

            return report.ToString();
        }

        public static string GetRailName(Svi3Rail rail)
        {
            switch (rail)
            {
                case Svi3Rail.Vdd: return "VDDCR_VDD";
                case Svi3Rail.Vdd1: return "VDDCR_VDD1";
                case Svi3Rail.Soc: return "VDDCR_SOC";
                case Svi3Rail.Misc: return "VDD_MISC";
                default: return rail.ToString();
            }
        }

        private float Read(SensorSource source)
        {
            if (source.IsPower)
            {
                return _powerTable.GetSvi3Reading(source.Rail, Svi3Reading.Voltage) *
                       _powerTable.GetSvi3Reading(source.Rail, Svi3Reading.Current);
            }

            return _powerTable.GetSvi3Reading(source.Rail, source.Reading);
        }

        // The rails are known once the table layout is (live) or its values are (debug report). A
        // live table whose layout was resolved late gets its sensors on the first update after that.
        private void EnsureSensors()
        {
            if (_sensors.Length > 0)
                return;

            lock (_createLock)
            {
                if (_sensors.Length > 0)
                    return;

                List<Sensor> sensors = new List<Sensor>();
                List<SensorSource> sources = new List<SensorSource>();

                foreach (Svi3Rail rail in Rails)
                {
                    bool hasVoltage = _powerTable.HasSvi3Reading(rail, Svi3Reading.Voltage);
                    bool hasCurrent = _powerTable.HasSvi3Reading(rail, Svi3Reading.Current);
                    if (!hasVoltage && !hasCurrent)
                        continue;

                    string railName = GetRailName(rail);

                    if (hasVoltage)
                        Add(sensors, sources, railName + " Voltage", SensorType.Voltage, rail, Svi3Reading.Voltage, false);
                    if (_powerTable.HasSvi3Reading(rail, Svi3Reading.Vid))
                        Add(sensors, sources, railName + " VID", SensorType.Voltage, rail, Svi3Reading.Vid, false);
                    if (hasCurrent)
                        Add(sensors, sources, railName + " Current", SensorType.Current, rail, Svi3Reading.Current, false);
                    if (hasVoltage && hasCurrent)
                        Add(sensors, sources, railName + " Power", SensorType.Power, rail, Svi3Reading.Current, true);
                    if (_powerTable.HasSvi3Reading(rail, Svi3Reading.VrmTemperature))
                        Add(sensors, sources, railName + " VRM Temp", SensorType.Temperature, rail, Svi3Reading.VrmTemperature, false);
                }

                if (sensors.Count == 0)
                    return;

                _sources = sources.ToArray();
                _sensors = sensors.ToArray();
            }
        }

        private static void Add(List<Sensor> sensors, List<SensorSource> sources, string name, SensorType type,
            Svi3Rail rail, Svi3Reading reading, bool isPower)
        {
            Sensor sensor = new Sensor(name, sensors.Count, type);
            sensors.Add(sensor);
            sources.Add(new SensorSource { Sensor = sensor, Rail = rail, Reading = reading, IsPower = isPower });
        }
    }
}
