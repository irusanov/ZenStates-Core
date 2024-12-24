namespace ZenStates.Core
{
    public class CpuInitSettings
    {
        public const int DefaultAutorefreshIntervalMs = 1000;

        public static readonly CpuInitSettings defaultSetttings = new CpuInitSettings();
        public struct CpuInitModuleSettings
        {
            public bool? Enabled { get; set; }
            public bool? Autorefresh { get; set; }
            public int? AutorefreshInterval { get; set; }
        }

        public CpuInitModuleSettings IoModule { get; set; }
        public CpuInitModuleSettings Timings { get; set; }
        public CpuInitModuleSettings Aod { get; set; }
        public CpuInitModuleSettings Wmi { get; set; }
        public CpuInitModuleSettings Sensors { get; set; }

        public CpuInitSettings()
        {
            IoModule = new CpuInitModuleSettings()
            {
                Enabled = true
            };
            Timings = new CpuInitModuleSettings()
            {
                Enabled = true,
                Autorefresh = true,
                AutorefreshInterval = DefaultAutorefreshIntervalMs
            };
            Aod = new CpuInitModuleSettings()
            {
                Enabled = true
            };
            Wmi = new CpuInitModuleSettings()
            {
                Enabled = true
            };
            Sensors = new CpuInitModuleSettings()
            {
                Enabled = true,
                Autorefresh = true,
                AutorefreshInterval = DefaultAutorefreshIntervalMs
            };
        }
    }
}
