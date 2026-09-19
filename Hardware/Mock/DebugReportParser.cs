using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text.RegularExpressions;
using ZenStates.Core.Hardware.DRAM;
using static ZenStates.Core.Cpu;
using static ZenStates.Core.Hardware.DRAM.MemoryConfig;
using ApobTable = ZenStates.Core.Hardware.Apob.Apob;

namespace ZenStates.Core.Hardware.Mock
{
    /// <summary>
    /// Turns the text of a ZenTimings debug report into data.
    /// </summary>
    public static class DebugReportParser
    {
        private static readonly Regex RegisterPairRegex =
            new Regex(@"^\s*0x(?<address>[0-9A-Fa-f]{8}):\s*0x(?<value>[0-9A-Fa-f]{8})\s*$");

        private static readonly Regex ModulePartRegex = new Regex(
            @"^(?<part>.+?)\s+(?<cap>\d+(?:\.\d+)?)(?<unit>[KMGT]?B)\s+(?<clk>\d+)MHz$",
            RegexOptions.IgnoreCase);

        private static readonly Regex CpuNameRegex =
            new Regex(@"^CpuName:[ \t]*(.+)$", RegexOptions.IgnoreCase | RegexOptions.Multiline);

        private static readonly Regex AgesaVersionRegex =
            new Regex(@"^AgesaVersion:[ \t]*(.+)$", RegexOptions.IgnoreCase | RegexOptions.Multiline);

        private static readonly Regex ReportVersionRegex =
            new Regex(@"^\S+[ \t]+(\S+)[ \t]+Debug Report[ \t]*$", RegexOptions.Multiline);

        private static readonly Regex Ddr5SmbusModuleRegex =
            new Regex(@"^DIMM at I2C address 0x[0-9A-Fa-f]+", RegexOptions.Multiline);

        private static readonly Regex MemoryFrequencyRegex =
            new Regex(@"^Frequency:\s*(\d+(?:\.\d+)?)\s*$", RegexOptions.Multiline);

        #region Section location

        /// <summary>Normalises CRLF/CR line endings so the line-oriented helpers below see only '\n'.</summary>
        public static string NormalizeLineEndings(string text)
        {
            return text == null ? null : text.Replace("\r\n", "\n").Replace("\r", "\n");
        }

        /// <summary>
        /// Finds the first content line of a "######\n{heading}\n######" section produced by
        /// DebugDialog's AddHeading, given the exact heading title.
        /// </summary>
        public static int FindSectionContentStart(string[] lines, string heading)
        {
            for (int i = 1; i < lines.Length - 1; i++)
            {
                if (lines[i].Trim() == heading &&
                    IsHashLine(lines[i - 1]) && IsHashLine(lines[i + 1]))
                {
                    return i + 2;
                }
            }
            return -1;
        }

        /// <summary>Scans forward from <paramref name="from"/> for the start of the next "######" heading block.</summary>
        public static int FindNextHeadingLine(string[] lines, int from)
        {
            for (int i = from; i < lines.Length; i++)
            {
                if (IsHashLine(lines[i]))
                    return i;
            }
            return lines.Length;
        }

        public static bool IsHashLine(string line)
        {
            string t = line.Trim();
            if (t.Length == 0)
                return false;

            for (int i = 0; i < t.Length; i++)
            {
                if (t[i] != '#')
                    return false;
            }
            return true;
        }

        /// <summary>Returns the lines of a named section, or an empty array when it is absent.</summary>
        public static string[] GetSectionLines(string[] lines, string heading)
        {
            int start = FindSectionContentStart(lines, heading);
            if (start < 0)
                return new string[0];

            int end = FindNextHeadingLine(lines, start);
            var section = new string[Math.Max(0, end - start)];
            Array.Copy(lines, start, section, 0, section.Length);
            return section;
        }

        #endregion

        #region Scalar labels

        public static string ParseLabelValue(string text, string label) => ApobTable.ParseLabelValue(text, label);

        /// <summary>Reads a label whose value may contain spaces, i.e. the rest of the line.</summary>
        public static string ParseLabelLine(string text, string label)
        {
            Match match = Regex.Match(
                text,
                @"^" + Regex.Escape(label) + @":[ \t]*(.+)$",
                RegexOptions.IgnoreCase | RegexOptions.Multiline);

            return match.Success ? match.Groups[1].Value.TrimEnd() : null;
        }

        public static string ParseCpuName(string text)
        {
            // "CpuName:  AMD Ryzen AI 7 350 w/ Radeon 860M" - the value contains spaces, so this
            // needs the rest of the line rather than the single-token ParseLabelValue.
            Match match = CpuNameRegex.Match(text);
            return match.Success ? match.Groups[1].Value.TrimEnd() : null;
        }

        public static string ParseAgesaVersion(string text)
        {
            Match match = AgesaVersionRegex.Match(text);
            return match.Success ? match.Groups[1].Value.TrimEnd() : null;
        }

        /// <summary>Version of the ZenTimings build that wrote the report, e.g. "140.594+7facabf".</summary>
        public static string ParseReportVersion(string text)
        {
            Match match = ReportVersionRegex.Match(text);
            return match.Success ? match.Groups[1].Value : null;
        }

        /// <summary>
        /// Builds the mocked CPU identity. The individual field parsers are APOB's, reused rather
        /// than duplicated.
        /// </summary>
        public static CPUInfo ParseCpuInfo(string text)
        {
            return new CPUInfo
            {
                family = ApobTable.ParseFamily(text),
                codeName = ApobTable.ParseCodeName(text),
                packageType = ApobTable.ParsePackageType(text),
                smuType = ApobTable.ParseSmuType(text),
                cpuName = ApobTable.ParseLabelValue(text, "CpuName:")?.Replace('_', ' '),
                vendor = ApobTable.ParseLabelValue(text, "Vendor:"),
                topology = new CpuTopology
                {
                    cores = ParseUInt(ApobTable.ParseLabelValue(text, "FusedCoreCount:")),
                    logicalCores = ParseUInt(ApobTable.ParseLabelValue(text, "Threads:")),
                },
            };
        }

        /// <summary>
        /// Determines DRAM type. Prefers an explicit "MemType:" line (emitted by newer ZenTimings
        /// builds); falls back to inferring DDR5 vs DDR4 from the presence of a populated "SMBUS
        /// Memory Modules" section for older reports, since that section is only ever printed for
        /// DDR5 systems. The LPDDR4/LPDDR5 distinction can't be recovered from older reports.
        /// </summary>
        public static MemType ParseMemType(string text)
        {
            string raw = ApobTable.ParseLabelValue(text, "MemType:");
            if (raw != null && Utils.TryParseEnum(raw, out MemType memType))
                return memType;

            return Ddr5SmbusModuleRegex.IsMatch(text) ? MemType.DDR5 : MemType.DDR4;
        }

        /// <summary>
        /// Reads the reported memory data rate in MT/s from the "Memory Config" section's
        /// "Frequency:" line. Returns 0 when absent, letting the caller fall back to the power
        /// table or to the ratio decoded from the registers.
        /// </summary>
        public static float ParseReportedMemoryFrequency(string[] lines)
        {
            foreach (string line in GetSectionLines(lines, "Memory Config"))
            {
                Match m = MemoryFrequencyRegex.Match(line.Trim());
                if (m.Success &&
                    float.TryParse(m.Groups[1].Value, NumberStyles.Float, CultureInfo.InvariantCulture, out float value))
                {
                    return value;
                }
            }

            return 0f;
        }

        #endregion

        #region Register dump

        /// <summary>
        /// Section headings of the report's register dumps. Every one of these prints plain
        /// "0xADDRESS: 0xVALUE" lines and can be read by <see cref="ParseRegisters"/>.
        /// </summary>
        public static class RegisterSections
        {
            /// <summary>UMC blocks for every enabled memory channel, each at its own DCT offset.</summary>
            public const string MemoryChannels = "Memory Channels Info";

            /// <summary>SVI2 telemetry PCI range.</summary>
            public const string Svi2PciRange = "SVI2: PCI Range";

            /// <summary>SMU fuse block read through NBSMNIND.</summary>
            public const string SmuFuse = "SMU: SMUFUSE NBSMNIND";

            /// <summary>The AMD MMIO window. This section also carries decoded text above its raw dump.</summary>
            public const string Mmio = "MMIO";
        }

        /// <summary>
        /// Reads the "0xADDRESS: 0xVALUE" pairs of a named section into a register set. Lines that
        /// are not address/value pairs are skipped, so sections that print decoded text alongside
        /// their raw dump (MMIO, for instance) parse correctly.
        /// </summary>
        /// <returns>
        /// The captured registers, empty when the section is absent. Each section is returned as
        /// its own set rather than merged, since an address only means something within one space.
        /// </returns>
        public static VirtualRegisters ParseRegisters(string[] lines, string sectionHeading)
        {
            var registers = new VirtualRegisters(sectionHeading);

            int start = FindSectionContentStart(lines, sectionHeading);
            if (start < 0)
                return registers;

            for (int i = start, end = FindNextHeadingLine(lines, start); i < end; i++)
            {
                Match m = RegisterPairRegex.Match(lines[i]);
                if (m.Success)
                    registers.Set(ParseHex(m.Groups["address"].Value), ParseHex(m.Groups["value"].Value));
            }

            return registers;
        }

        /// <summary>
        /// The UMC register dump, i.e. <see cref="ParseRegisters"/> over
        /// <see cref="RegisterSections.MemoryChannels"/>.
        /// </summary>
        public static VirtualRegisters ParseUmcRegisters(string[] lines)
        {
            return ParseRegisters(lines, RegisterSections.MemoryChannels);
        }

        #endregion

        #region Memory modules

        /// <summary>
        /// Parses the "Memory Modules" section, e.g.:
        /// <code>
        /// P0 CHANNEL A | DIMM 0
        /// -- Slot: A1
        /// -- Single Rank
        /// -- DCT Offset: 0x0
        /// -- Manufacturer: Ramaxel Technology
        /// -- RMSB3410MD88IBF-5600 16GB 5600MHz
        /// -- 32 Banks (5 bit), Col: 10, Row: 16, No RM (0 bit), 8 Bank Groups (3 bit)
        /// </code>
        /// </summary>
        public static List<MemoryModule> ParseModules(string[] lines, MemType memType)
        {
            var modules = new List<MemoryModule>();

            int start = FindSectionContentStart(lines, "Memory Modules");
            if (start < 0)
                return modules;

            int end = FindNextHeadingLine(lines, start);
            MemoryModule current = null;

            for (int i = start; i < end; i++)
            {
                string trimmed = lines[i].Trim();

                if (trimmed.Length == 0)
                {
                    if (current != null)
                    {
                        modules.Add(current);
                        current = null;
                    }
                    continue;
                }

                if (!trimmed.StartsWith("--"))
                {
                    // New module header, e.g. "P0 CHANNEL A | DIMM 0"
                    if (current != null)
                        modules.Add(current);

                    current = new MemoryModule { Type = memType };
                    string[] parts = trimmed.Split(new[] { '|' }, 2);
                    current.BankLabel = parts[0].Trim();
                    current.DeviceLocator = parts.Length > 1 ? parts[1].Trim() : string.Empty;
                    continue;
                }

                if (current == null)
                    continue;

                ApplyModuleAttribute(current, trimmed.TrimStart('-', ' ').Trim());
            }

            if (current != null)
                modules.Add(current);

            return modules;
        }

        private static void ApplyModuleAttribute(MemoryModule module, string attr)
        {
            if (attr.StartsWith("Slot:", StringComparison.OrdinalIgnoreCase))
            {
                module.Slot = attr.Substring("Slot:".Length).Trim();
            }
            else if (attr.Equals("Single Rank", StringComparison.OrdinalIgnoreCase))
            {
                module.Rank = MemRank.SR;
            }
            else if (attr.Equals("Dual Rank", StringComparison.OrdinalIgnoreCase))
            {
                module.Rank = MemRank.DR;
            }
            else if (attr.Equals("Quad Rank", StringComparison.OrdinalIgnoreCase))
            {
                module.Rank = MemRank.QR;
            }
            else if (attr.StartsWith("DCT Offset:", StringComparison.OrdinalIgnoreCase))
            {
                string hex = attr.Substring("DCT Offset:".Length).Trim();
                if (hex.StartsWith("0x", StringComparison.OrdinalIgnoreCase))
                    hex = hex.Substring(2);

                // The report prints the channel index; the register addresses put it at bit 20.
                if (uint.TryParse(hex, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out uint channelIndex))
                    module.DctOffset = channelIndex << 20;
            }
            else if (attr.StartsWith("Manufacturer:", StringComparison.OrdinalIgnoreCase))
            {
                module.Manufacturer = attr.Substring("Manufacturer:".Length).Trim();
            }
            else
            {
                ApplyModulePartNumber(module, attr);
            }

            // Bank/row/col/RM/bank-group description lines aren't needed to populate the main
            // timings UI and are intentionally not parsed here.
        }

        private static void ApplyModulePartNumber(MemoryModule module, string attr)
        {
            Match m = ModulePartRegex.Match(attr);
            if (!m.Success)
                return;

            module.PartNumber = m.Groups["part"].Value.Trim();

            if (uint.TryParse(m.Groups["clk"].Value, NumberStyles.Integer, CultureInfo.InvariantCulture, out uint clock))
                module.ClockSpeed = clock;

            if (!double.TryParse(m.Groups["cap"].Value, NumberStyles.Float, CultureInfo.InvariantCulture, out double capValue))
                return;

            CapacityUnit unit;
            switch (m.Groups["unit"].Value.ToUpperInvariant())
            {
                case "KB": unit = CapacityUnit.KB; break;
                case "MB": unit = CapacityUnit.MB; break;
                default: unit = CapacityUnit.GB; break; // GB, or bare "B"/"TB" fall back to GB-scale storage
            }

            ulong bytes = (ulong)Math.Round(capValue * Math.Pow(1024, (int)unit));
            module.Capacity = new Capacity(bytes, unit);
        }

        #endregion

        #region Primitives

        public static uint ParseHex(string value)
        {
            return uint.Parse(value, NumberStyles.HexNumber, CultureInfo.InvariantCulture);
        }

        public static uint ParseUInt(string value)
        {
            return uint.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out uint result) ? result : 0;
        }

        #endregion
    }
}
