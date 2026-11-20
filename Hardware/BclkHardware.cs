using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace ZenStates.Core.Hardware
{
    /// <summary>
    /// The measured reference clocks of <see cref="Bclk"/> as a sensor group named after the CPU: the CPU side
    /// (<see cref="Bclk.CpuBclk"/>, TSC against the ACPI timer) and the memory side (<see cref="Bclk.MemoryBclk"/>, the UMC
    /// clock counter, or the TSC where there is no counter). Both are measured on <see cref="Bclk"/>'s background thread;
    /// <see cref="Update"/> only copies the last values, so it costs nothing. A value not measured (yet) is null.
    /// </summary>
    public sealed class BclkHardware : IHardware
    {
        private readonly Bclk _bclk;
        private readonly Sensor _cpuBclk;
        private readonly Sensor _memoryBclk;
        private readonly Sensor[] _sensors;

        public BclkHardware(Bclk bclk, string cpuName)
        {
            _bclk = bclk;
            Name = string.IsNullOrEmpty(cpuName) ? "CPU" : cpuName.Trim();
            _cpuBclk = new Sensor("CPU BCLK", 0, SensorType.Clock);
            _memoryBclk = new Sensor("Memory BCLK", 1, SensorType.Clock);
            _sensors = new[] { _cpuBclk, _memoryBclk };
            Update();
        }

        public HardwareType HardwareType => HardwareType.Cpu;

        /// <summary>The CPU name, e.g. "AMD Ryzen 9 9950X3D 16-Core Processor".</summary>
        public string Name { get; }

        public IEnumerable<Sensor> Sensors => _sensors;

        public bool HasSensors => _bclk != null && _bclk.IsAvailable;

        public void Update()
        {
            if (_bclk == null)
                return;

            _cpuBclk.Value = ToSensorValue(_bclk.CpuBclk);
            _memoryBclk.Value = ToSensorValue(_bclk.MemoryBclk);
        }

        public string GetReport()
        {
            StringBuilder report = new StringBuilder();
            report.AppendLine(Name);

            foreach (Sensor sensor in _sensors)
            {
                string value = sensor.Value.HasValue
                    ? sensor.Value.Value.ToString("F2", CultureInfo.InvariantCulture) + " MHz"
                    : "N/A";
                report.AppendLine($"{sensor.Name}: {value}");
            }

            return report.ToString();
        }

        private static float? ToSensorValue(double? mhz) => mhz.HasValue ? (float)mhz.Value : (float?)null;
    }
}
