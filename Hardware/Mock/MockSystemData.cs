using System;
using System.Collections.Generic;
using ZenStates.Core.Hardware.DRAM;
using static ZenStates.Core.Cpu;
// Namespace and class share the name "Apob" (ZenStates.Core.Hardware.Apob.Apob), so it's aliased
// here rather than imported with a plain "using" to avoid "Apob.Apob" ambiguity below.
using ApobTable = ZenStates.Core.Hardware.Apob.Apob;

namespace ZenStates.Core.Hardware.Mock
{
    /// <summary>
    /// Aggregated, hardware-free snapshot of everything a UI needs to render a "mock" view of a
    /// system, reconstructed entirely from a previously captured ZenTimings debug report.
    /// </summary>
    public sealed class MockSystemData
    {
        public CPUInfo CpuInfo { get; private set; }

        public MemType MemoryType { get; private set; } = MemType.UNKNOWN;

        public List<MemoryModule> Modules { get; private set; } = new List<MemoryModule>();

        public List<KeyValuePair<uint, BaseDramTimings>> Timings { get; private set; } = new List<KeyValuePair<uint, BaseDramTimings>>();

        public VirtualRegisters UmcRegisters { get; private set; } = new VirtualRegisters();

        public ApobTable Apob { get; private set; }

        public PowerTable PowerTable { get; private set; }

        public Capacity TotalCapacity { get; private set; } = new Capacity();
        public string CpuName { get; private set; }
        public string MbVendor { get; private set; }
        public string MbName { get; private set; }
        public string BiosVersion { get; private set; }
        public string AgesaVersion { get; private set; }
        public string SmuVersion { get; private set; }

        public string ReportVersion { get; private set; }

        /// <summary>Non-fatal problems hit while parsing, e.g. sections that couldn't be located. Empty on a clean parse.</summary>
        public List<string> Warnings { get; } = new List<string>();

        private MockSystemData()
        {
        }

        /// <summary>
        /// Builds a complete mock system snapshot from the text of a ZenTimings debug report.
        /// </summary>
        public static MockSystemData CreateFromDebugReport(string debugReportText)
        {
            if (debugReportText == null)
                throw new ArgumentNullException(nameof(debugReportText));

            string text = DebugReportParser.NormalizeLineEndings(debugReportText);
            string[] lines = text.Split('\n');

            var data = new MockSystemData();

            data.ReadSystemIdentity(text);
            data.ReadModules(lines);

            // The power table is built before the timings because it carries MCLK, which is the
            // frequency the captured system was running at.
            data.PowerTable = PowerTable.CreateFromDebugReport(debugReportText);

            data.ReadTimings(lines);
            data.ReadApob(debugReportText);

            return data;
        }

        private void ReadSystemIdentity(string text)
        {
            CpuInfo = DebugReportParser.ParseCpuInfo(text);

            CpuName = DebugReportParser.ParseCpuName(text) ?? CpuInfo.cpuName;

            // Board vendor and model contain spaces ("Micro-Star International Co., Ltd.",
            // "MEG X870E UNIFY-X MAX (MS-7E73)"), so they need the whole line. The single-token
            // ParseLabelValue used previously truncated both at the first space.
            MbVendor = DebugReportParser.ParseLabelLine(text, "MbVendor");
            MbName = DebugReportParser.ParseLabelLine(text, "MbName");

            // These are single tokens, so the token-wise parser is correct for them.
            BiosVersion = DebugReportParser.ParseLabelValue(text, "BiosVersion:");
            SmuVersion = DebugReportParser.ParseLabelValue(text, "SmuVersion:");
            AgesaVersion = DebugReportParser.ParseAgesaVersion(text);
            ReportVersion = DebugReportParser.ParseReportVersion(text);
            MemoryType = DebugReportParser.ParseMemType(text);
        }

        private void ReadModules(string[] lines)
        {
            Modules = DebugReportParser.ParseModules(lines, MemoryType);
            if (Modules.Count == 0)
                Warnings.Add("Could not locate a 'Memory Modules' section in the debug report.");

            ulong totalBytes = 0;
            foreach (MemoryModule module in Modules)
                totalBytes += module.Capacity?.SizeInBytes ?? 0;

            TotalCapacity = new Capacity(totalBytes);
        }

        /// <summary>
        /// Decodes one set of timings per module from the captured UMC registers, in module order
        /// </summary>
        private void ReadTimings(string[] lines)
        {
            VirtualRegisters registers = DebugReportParser.ParseUmcRegisters(lines);
            if (registers.IsEmpty)
            {
                Warnings.Add("Could not locate a 'Memory Channels Info' register dump in the debug report; timings are unavailable.");
                return;
            }

            UmcRegisters = registers;

            float frequency = ResolveReportedFrequency(lines);

            foreach (MemoryModule module in Modules)
            {
                if (!MockDramTimings.HasChannel(registers, module.DctOffset))
                {
                    Warnings.Add($"No captured registers for the channel at DCT offset 0x{module.DctOffset:X} ({module.Slot}).");
                    continue;
                }

                BaseDramTimings timings = MockDramTimings.CreateAndRead(
                    MemoryType, registers, module.DctOffset, frequency);

                if (timings == null)
                {
                    Warnings.Add($"No mock timings implementation for memory type {MemoryType}.");
                    return;
                }

                Timings.Add(new KeyValuePair<uint, BaseDramTimings>(module.DctOffset, timings));
            }
        }

        /// <summary>
        /// Memory data rate of the captured system. The power table's MCLK is preferred because it
        /// is the same source the live path uses; the report's own "Frequency:" line is the
        /// fallback. Returning 0 leaves the mock timings to derive it from the decoded ratio.
        /// </summary>
        private float ResolveReportedFrequency(string[] lines)
        {
            float mclk = PowerTable?.MCLK ?? 0f;
            if (mclk > 0)
                return mclk * 2;

            return DebugReportParser.ParseReportedMemoryFrequency(lines);
        }

        private void ReadApob(string debugReportText)
        {
            Apob = ApobTable.CreateFromDebugReport(debugReportText);

            if (!string.IsNullOrEmpty(Apob?.ErrorReason))
                Warnings.Add("APOB: " + Apob.ErrorReason);
        }
    }
}
