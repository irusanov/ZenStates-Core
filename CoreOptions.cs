using System;
using ZenStates.Core.Drivers;

namespace ZenStates.Core
{
    public class CoreOptions
    {
        public static CoreOptions Current { get; internal set; } = new CoreOptions();

        public struct ModuleSettings
        {
            public bool? Enabled { get; set; }
            public bool? Autorefresh { get; set; }
            public int? AutorefreshInterval { get; set; }
        }

        public const int DefaultAutorefreshIntervalMs = 1000;

        public bool PrintSerialNumbers { get; set; }

        /// <summary>
        /// Read the SPD, the hub / PMIC telemetry and the DDR4 thermal sensors on a background thread, so
        /// <see cref="Cpu"/> starts without waiting for hundreds of ms of SMBus traffic. Off by default: then
        /// <see cref="Hardware.DRAM.MemoryConfig.SpdInfo"/>, <see cref="Hardware.DRAM.MemoryConfig.Ddr4Spd"/>,
        /// <see cref="Hardware.DRAM.MemoryConfig.Ddr4ThermalSensors"/> and the module manufacturers are complete
        /// when the constructor returns. When on, they are empty until <see cref="Hardware.DRAM.MemoryConfig.SpdInfoLoaded"/>
        /// is raised (or <see cref="Hardware.DRAM.MemoryConfig.WaitForSpdInfo"/> returns true), and the consumer must
        /// handle that.
        /// </summary>
        public bool SpdLoadInBackground { get; set; } = true;
        public ModuleSettings IoModule { get; set; }
        public ModuleSettings Timings { get; set; }
        public ModuleSettings Aod { get; set; }
        public ModuleSettings Wmi { get; set; }
        public ModuleSettings Sensors { get; set; }

        /// <summary>
        /// The driver that reads the APOB: <see cref="ApobDriverType.Auto"/> (default) takes the PawnIO AMDAPOB
        /// module when it loads and InpOut otherwise; <see cref="ApobDriverType.PawnIo"/> or
        /// <see cref="ApobDriverType.InpOut"/> use only that one.
        /// </summary>
        public ApobDriverType ApobDriver { get; set; } = ApobDriverType.Auto;

        /// <summary>
        /// Measure the reference clock of the memory side on a background thread after start
        /// (<see cref="Hardware.Bclk.RefreshMemoryBclk"/>), so MEMCLK, UCLK, FCLK and the timings in ns follow the real clock.
        /// On by default; needs InpOut. When off, or until the measurement is done, the PLL register is used.
        /// </summary>
        public bool MeasureMemoryBclk { get; set; } = true;

        /// <summary>
        /// How often the memory side reference clock is measured again after start, in ms, as some boards let tools change
        /// it while running. 0 measures only at start and after <see cref="Hardware.Bclk.SetBclk"/>. A measurement takes about 0.4 s
        /// and holds the PCI bus lock only for a few short reads.
        /// </summary>
        public int MemoryBclkRefreshIntervalMs { get; set; } = DefaultMemoryBclkRefreshIntervalMs;

        public const int DefaultMemoryBclkRefreshIntervalMs = 10000;

        /// <summary>
        /// Called with a short description of each initialization stage as <see cref="Cpu"/> starts it,
        /// e.g. to show progress on a splash screen. Runs on the constructing thread; exceptions are ignored.
        /// </summary>
        public Action<string> InitProgress { get; set; }

        public CoreOptions()
        {
            IoModule = new ModuleSettings
            {
                Enabled = true
            };
            Timings = new ModuleSettings
            {
                Enabled = true,
                Autorefresh = true,
                AutorefreshInterval = DefaultAutorefreshIntervalMs
            };
            Aod = new ModuleSettings
            {
                Enabled = true
            };
            Wmi = new ModuleSettings
            {
                Enabled = true
            };
            Sensors = new ModuleSettings
            {
                Enabled = true,
                Autorefresh = true,
                AutorefreshInterval = DefaultAutorefreshIntervalMs
            };
        }
    }
}