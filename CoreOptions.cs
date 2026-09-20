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
        public ModuleSettings IoModule { get; set; }
        public ModuleSettings Timings { get; set; }
        public ModuleSettings Aod { get; set; }
        public ModuleSettings Wmi { get; set; }
        public ModuleSettings Sensors { get; set; }

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