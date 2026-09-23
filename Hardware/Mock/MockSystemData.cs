using System;
using System.Collections.Generic;
using ZenStates.Core.Hardware.Aod;
using ZenStates.Core.Hardware.DRAM;
using ZenStates.Core.Hardware.DRAM.DDR5.Pmic;
using ZenStates.Core.Hardware.DRAM.DDR5.Spd;
using ZenStates.Core.Hardware.Motherboard;
using ZenStates.Core.Hardware.Motherboard.Lpc;
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

        public Dictionary<byte, Ddr5SpdInfo> SpdInfo { get; private set; } = new Dictionary<byte, Ddr5SpdInfo>();

        /// <summary>
        /// PMIC of the first DIMM that has one, i.e. what the main window shows when no particular
        /// module is selected. Null when the report carries no readable PMIC block.
        /// </summary>
        public Ddr5PmicData PmicData { get; private set; }

        /// <summary>Decoded AOD fields as printed in the report - the mock counterpart of <c>cpu.info.aod.Table.Data</c>. Null when unavailable.</summary>
        public AodData AodData { get; private set; }

        /// <summary>
        /// DDR4 BIOS memory controller config (APCB) bytes as the report dumped them - what the live
        /// window reads over WMI for ProcODT, RTT, drive strengths and setup times. Null when the
        /// report has no such dump.
        /// </summary>
        public byte[] BiosMemControllerTable { get; private set; }

        public PowerTable PowerTable { get; private set; }

        /// <summary>
        /// The SVI3 telemetry of the report's power table, followed by the SuperIO sensors replayed from
        /// its register dumps, decoded with the same board configuration - the mock counterpart of
        /// <c>cpu.systemInfo.SensorGroups</c>. Values are the captured ones and don't change. Empty
        /// when the report has neither.
        /// </summary>
        public List<SensorGroup> SensorGroups { get; } = new List<SensorGroup>();

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

            // Each section is parsed on its own: a malformed one becomes a warning and leaves its
            // data unavailable, it never aborts loading the rest of the report.
            try
            {
                data.ReadSystemIdentity(text);
            }
            catch (Exception ex)
            {
                data.AddSectionWarning("system identity", ex);
            }

            // Parsed up front: besides the timings, they give the DRAM type older reports don't print.
            VirtualRegisters registers;
            try
            {
                registers = DebugReportParser.ParseUmcRegisters(lines);
            }
            catch (Exception ex)
            {
                registers = new VirtualRegisters();
                data.AddSectionWarning("UMC registers", ex);
            }

            try
            {
                data.ResolveMemoryType(text, registers);
            }
            catch (Exception ex)
            {
                data.AddSectionWarning("memory type", ex);
            }

            try
            {
                data.ReadModules(lines);
            }
            catch (Exception ex)
            {
                data.AddSectionWarning("memory modules", ex);
            }

            // The power table is built before the timings because it carries MCLK, which is the
            // frequency the captured system was running at.
            try
            {
                data.PowerTable = PowerTable.CreateFromDebugReport(debugReportText);
            }
            catch (Exception ex)
            {
                data.AddSectionWarning("power table", ex);
            }

            try
            {
                data.ReadTimings(registers, lines);
            }
            catch (Exception ex)
            {
                data.AddSectionWarning("timings", ex);
            }

            try
            {
                data.ReadSpdInfo(lines);
            }
            catch (Exception ex)
            {
                data.AddSectionWarning("SPD", ex);
            }

            try
            {
                data.ReadAod(lines);
            }
            catch (Exception ex)
            {
                data.AddSectionWarning("AOD", ex);
            }

            try
            {
                data.ReadSuperIo(lines);
            }
            catch (Exception ex)
            {
                data.AddSectionWarning("SuperIO", ex);
            }

            try
            {
                data.AddSvi3Sensors();
            }
            catch (Exception ex)
            {
                data.AddSectionWarning("SVI3 telemetry", ex);
            }

            try
            {
                data.BiosMemControllerTable = DebugReportParser.ParseIndexedBytes(lines, DebugReportParser.BiosMemControllerSection);
            }
            catch (Exception ex)
            {
                data.AddSectionWarning("BIOS memory controller table", ex);
            }

            try
            {
                data.ReadApob(debugReportText);
            }
            catch (Exception ex)
            {
                data.AddSectionWarning("APOB", ex);
            }

            return data;
        }

        private void AddSectionWarning(string section, Exception ex)
        {
            Warnings.Add($"Could not parse the {section} section of the debug report: {ex.Message}");
        }

        /// <summary>
        /// PMIC of the module at <paramref name="moduleIndex"/> in <see cref="Modules"/>. SPD entries
        /// line up with the modules by index, the same assumption the live path makes. Falls back to
        /// <see cref="PmicData"/> when that module has no entry of its own.
        /// </summary>
        public Ddr5PmicData GetPmicData(int moduleIndex)
        {
            if (moduleIndex >= 0 && moduleIndex < SpdInfo.Count)
            {
                int index = 0;
                foreach (Ddr5SpdInfo info in SpdInfo.Values)
                {
                    if (index++ != moduleIndex)
                        continue;

                    if (info.PmicData != null && info.PmicData.IsValid)
                        return info.PmicData;

                    break;
                }
            }

            return PmicData;
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

        /// <summary>
        /// Keeps the report's own "MemType:" line when it has one. Otherwise the type is read from
        /// the captured UMC registers, as the live path once did; failing that, a populated SMBUS
        /// section means DDR5, and anything else is taken as DDR4.
        /// </summary>
        private void ResolveMemoryType(string text, VirtualRegisters registers)
        {
            if (MemoryType != MemType.UNKNOWN)
                return;

            MemType fromRegisters;
            if (MockDramTimings.TryReadMemType(registers, out fromRegisters))
            {
                MemoryType = fromRegisters;
                return;
            }

            MemoryType = DebugReportParser.HasDdr5SmbusModules(text) ? MemType.DDR5 : MemType.DDR4;
            Warnings.Add($"The debug report states no memory type and has no UMC DRAM type register; assuming {MemoryType}.");
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
        private void ReadTimings(VirtualRegisters registers, string[] lines)
        {
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

        /// <summary>
        /// Reads the decoded SPD dumps, and with them the PMIC rails the main window shows. Only
        /// DDR5 reports carry the section, so its absence is a warning for DDR5 alone.
        /// </summary>
        private void ReadSpdInfo(string[] lines)
        {
            SpdInfo = DebugReportParser.ParseSpdInfo(lines);

            bool isDdr5 = MemoryType == MemType.DDR5 || MemoryType == MemType.LPDDR5;

            if (SpdInfo.Count == 0)
            {
                if (isDdr5)
                    Warnings.Add("Could not locate an 'SMBUS Memory Modules' section in the debug report; SPD and PMIC data are unavailable.");
                return;
            }

            foreach (Ddr5SpdInfo info in SpdInfo.Values)
            {
                if (info.PmicData != null && info.PmicData.IsValid)
                {
                    PmicData = info.PmicData;
                    break;
                }
            }

            if (PmicData == null && isDdr5)
                Warnings.Add("The debug report's SPD dumps carry no readable PMIC block; DIMM voltages are unavailable.");
        }

        /// <summary>
        /// Rebuilds each dumped SuperIO chip and runs it through the live board configuration. The
        /// chip's position in the report is its index, as on the live machine.
        /// </summary>
        private void ReadSuperIo(string[] lines)
        {
            List<SuperIoDump> dumps = DebugReportParser.ParseSuperIo(lines);
            if (dumps == null)
                return;

            for (int i = 0; i < dumps.Count; i++)
            {
                SuperIoDump dump = dumps[i];

                try
                {
                    ISuperIO chip = dump.CreateChip();
                    if (chip == null)
                    {
                        Warnings.Add($"SuperIO: no replay support for LPC {dump.ChipClass} ({dump.Chip}).");
                        continue;
                    }

                    var hardware = new SuperIOHardware(chip, MbVendor, MbName, i);
                    hardware.Update();
                    SensorGroups.Add(SensorGroup.FromSuperIo(hardware));
                }
                catch (Exception ex)
                {
                    Warnings.Add($"SuperIO: could not replay {dump.Chip}: {ex.Message}");
                }
            }
        }

        /// <summary>Exposes the power table's SVI3 readings as a sensor group, as the live SystemInfo does.</summary>
        private void AddSvi3Sensors()
        {
            if (PowerTable == null)
                return;

            var svi3 = new Svi3Hardware(PowerTable);
            svi3.Update();
            if (svi3.HasSensors)
                SensorGroups.Insert(0, SensorGroup.FromSvi3(svi3));
        }

        private void ReadAod(string[] lines)
        {
            AodData = DebugReportParser.ParseAodData(lines);
            if (AodData == null)
                Warnings.Add("Could not locate decoded AOD data in the debug report; AOD data is unavailable.");
        }

        private void ReadApob(string debugReportText)
        {
            Apob = ApobTable.CreateFromDebugReport(debugReportText, Timings.Count > 0 ? Timings[0].Value : null);

            if (!string.IsNullOrEmpty(Apob?.ErrorReason))
                Warnings.Add("APOB: " + Apob.ErrorReason);
        }
    }
}
