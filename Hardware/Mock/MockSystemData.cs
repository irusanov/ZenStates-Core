using System;
using System.Collections.Generic;
using System.Globalization;
using System.Reflection;
using System.Text.RegularExpressions;
using ZenStates.Core.Hardware.DRAM;
using static ZenStates.Core.Cpu;
using static ZenStates.Core.Hardware.DRAM.MemoryConfig;
// Namespace and class share the name "Apob" (ZenStates.Core.Hardware.Apob.Apob), so it's aliased
// here rather than imported with a plain "using" to avoid "Apob.Apob" ambiguity below.
using ApobTable = ZenStates.Core.Hardware.Apob.Apob;

namespace ZenStates.Core.Hardware.Mock
{
    /// <summary>
    /// Aggregated, hardware-free snapshot of everything a UI needs to render a "mock" view of a
    /// system, reconstructed entirely from a previously captured ZenTimings debug report.
    /// </summary>
    /// <remarks>
    /// This is the single entry point for turning a debug report back into data: it builds the
    /// <see cref="CPUInfo"/>, the DRAM <see cref="MemType"/>, the <see cref="MemoryModule"/> list,
    /// the parsed <see cref="BaseDramTimings"/>, the APOB instance
    /// (via <see cref="ApobTable.CreateFromDebugReport"/>), and the power table (FCLK/MCLK/UCLK,
    /// voltages) via <see cref="ZenStates.Core.PowerTable.CreateFromDebugReport"/>, so a caller
    /// such as ZenTimings' MainWindow doesn't need to hand-parse the report itself or touch any
    /// real hardware.
    /// </remarks>
    public sealed class MockSystemData
    {
        /// <summary>Mocked CPU identity info (family, codename, package/smu type), as consumed by APOB profile resolution.</summary>
        public CPUInfo CpuInfo { get; private set; }

        /// <summary>DRAM type. Read from an explicit "MemType:" line when present (new reports), otherwise inferred (see <see cref="ParseMemType"/>).</summary>
        public MemType MemoryType { get; private set; } = MemType.UNKNOWN;

        /// <summary>Memory modules parsed from the "Memory Modules" section, in report order.</summary>
        public List<MemoryModule> Modules { get; private set; } = new List<MemoryModule>();

        /// <summary>Parsed timings, keyed by DCT offset, mirroring <see cref="MemoryConfig.Timings"/>.</summary>
        public List<KeyValuePair<uint, BaseDramTimings>> Timings { get; private set; } =
            new List<KeyValuePair<uint, BaseDramTimings>>();

        /// <summary>Mock APOB instance built from the report's "APOB" section.</summary>
        public ApobTable Apob { get; private set; }

        /// <summary>Mock power table (FCLK/MCLK/UCLK/voltages) built from the report's "SMU: Power Table Detected Values" section.</summary>
        public PowerTable PowerTable { get; private set; }

        public Capacity TotalCapacity { get; private set; } = new Capacity();

        public string CpuName { get; private set; }
        public string MbVendor { get; private set; }
        public string MbName { get; private set; }
        public string BiosVersion { get; private set; }
        public string AgesaVersion { get; private set; }
        public string SmuVersion { get; private set; }

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

            string text = ApobTable.NormalizeLineEndings(debugReportText);
            var data = new MockSystemData();

            // -- CPU info (reuses the same tolerant parsing Apob.CreateFromDebugReport relies on) --
            data.CpuInfo = new CPUInfo
            {
                family = ApobTable.ParseFamily(text),
                codeName = ApobTable.ParseCodeName(text),
                packageType = ApobTable.ParsePackageType(text),
                smuType = ApobTable.ParseSmuType(text),
                cpuName = ApobTable.ParseLabelValue(text, "CpuName:")?.Replace('_', ' '),
                vendor = ApobTable.ParseLabelValue(text, "Vendor:"),
            };

            data.CpuName = ParseCpuNameLine(text) ?? data.CpuInfo.cpuName;
            data.MbVendor = ApobTable.ParseLabelValue(text, "MbVendor:");
            data.MbName = ApobTable.ParseLabelValue(text, "MbName:");
            data.BiosVersion = ApobTable.ParseLabelValue(text, "BiosVersion:");
            data.SmuVersion = ApobTable.ParseLabelValue(text, "SmuVersion:");
            data.AgesaVersion = ParseAgesaVersionLine(text);

            // -- Memory type --
            data.MemoryType = ParseMemType(text);

            // -- Memory modules --
            data.Modules = ParseModules(text, data.MemoryType);
            if (data.Modules.Count == 0)
                data.Warnings.Add("Could not locate a 'Memory Modules' section in the debug report.");

            ulong totalBytes = 0;
            foreach (MemoryModule module in data.Modules)
                totalBytes += module.Capacity?.SizeInBytes ?? 0;
            data.TotalCapacity = new Capacity(totalBytes);

            // -- Timings --
            uint dctOffset = data.Modules.Count > 0 ? data.Modules[0].DctOffset : 0;
            BaseDramTimings timings = CreateTimingsInstance(data.MemoryType);
            if (timings != null)
            {
                if (!ParseTimings(text, timings))
                    data.Warnings.Add("Could not locate a 'Memory Config' section in the debug report.");

                data.Timings.Add(new KeyValuePair<uint, BaseDramTimings>(dctOffset, timings));
            }

            // -- APOB (delegates to the existing, already-shipped parser) --
            data.Apob = ApobTable.CreateFromDebugReport(debugReportText);
            if (!data.Apob.IsAvailable)
                data.Warnings.Add("APOB: " + data.Apob.ErrorReason);

            // -- Power table (FCLK/MCLK/UCLK/voltages) --
            data.PowerTable = PowerTable.CreateFromDebugReport(debugReportText);

            return data;
        }

        private static string ParseCpuNameLine(string text)
        {
            // "CpuName:           AMD Ryzen AI 7 350 w/ Radeon 860M" - value can contain spaces,
            // so this needs the rest of the line rather than the single-token ParseLabelValue.
            Match match = Regex.Match(text, @"^CpuName:[ \t]*(.+)$", RegexOptions.IgnoreCase | RegexOptions.Multiline);
            return match.Success ? match.Groups[1].Value.TrimEnd() : null;
        }

        private static string ParseAgesaVersionLine(string text)
        {
            Match match = Regex.Match(text, @"^AgesaVersion:[ \t]*(.+)$", RegexOptions.IgnoreCase | RegexOptions.Multiline);
            return match.Success ? match.Groups[1].Value.TrimEnd() : null;
        }

        /// <summary>
        /// Determines DRAM type. Prefers an explicit "MemType:" line (emitted by newer ZenTimings
        /// builds); falls back to inferring DDR5 vs DDR4 from the presence of a populated "SMBUS
        /// Memory Modules" section for older reports, since that section is only ever printed for
        /// DDR5 systems. The LPDDR4/LPDDR5 distinction can't be recovered from older reports.
        /// </summary>
        private static MemType ParseMemType(string text)
        {
            string raw = ApobTable.ParseLabelValue(text, "MemType:");
            if (raw != null && Utils.TryParseEnum(raw, out MemType memType))
                return memType;

            bool hasSmbusDdr5Modules = Regex.IsMatch(text, @"^DIMM at I2C address 0x[0-9A-Fa-f]+", RegexOptions.Multiline);
            return hasSmbusDdr5Modules ? MemType.DDR5 : MemType.DDR4;
        }

        private static BaseDramTimings CreateTimingsInstance(MemType memType)
        {
            switch (memType)
            {
                case MemType.DDR4:
                case MemType.LPDDR4:
                    return new Ddr4Timings(null);
                case MemType.DDR5:
                case MemType.LPDDR5:
                    return new Ddr5Timings(null);
                default:
                    return null;
            }
        }

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
        private static List<MemoryModule> ParseModules(string text, MemType memType)
        {
            var modules = new List<MemoryModule>();
            string[] lines = text.Split('\n');

            int start = FindSectionContentStart(lines, "Memory Modules");
            if (start < 0)
                return modules;

            int end = FindNextHeadingLine(lines, start);

            MemoryModule current = null;
            var partCapClockRegex = new Regex(
                @"^(?<part>.+?)\s+(?<cap>\d+(?:\.\d+)?)(?<unit>[KMGT]?B)\s+(?<clk>\d+)MHz$",
                RegexOptions.IgnoreCase);

            for (int i = start; i < end; i++)
            {
                string line = lines[i].TrimEnd();
                string trimmed = line.Trim();

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

                string attr = trimmed.TrimStart('-', ' ').Trim();

                if (attr.StartsWith("Slot:", StringComparison.OrdinalIgnoreCase))
                {
                    current.Slot = attr.Substring("Slot:".Length).Trim();
                }
                else if (attr.Equals("Single Rank", StringComparison.OrdinalIgnoreCase))
                {
                    current.Rank = MemRank.SR;
                }
                else if (attr.Equals("Dual Rank", StringComparison.OrdinalIgnoreCase))
                {
                    current.Rank = MemRank.DR;
                }
                else if (attr.Equals("Quad Rank", StringComparison.OrdinalIgnoreCase))
                {
                    current.Rank = MemRank.QR;
                }
                else if (attr.StartsWith("DCT Offset:", StringComparison.OrdinalIgnoreCase))
                {
                    string hex = attr.Substring("DCT Offset:".Length).Trim();
                    if (hex.StartsWith("0x", StringComparison.OrdinalIgnoreCase))
                        hex = hex.Substring(2);
                    if (uint.TryParse(hex, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out uint shifted))
                        current.DctOffset = shifted << 20;
                }
                else if (attr.StartsWith("Manufacturer:", StringComparison.OrdinalIgnoreCase))
                {
                    current.Manufacturer = attr.Substring("Manufacturer:".Length).Trim();
                }
                else
                {
                    Match m = partCapClockRegex.Match(attr);
                    if (m.Success)
                    {
                        current.PartNumber = m.Groups["part"].Value.Trim();
                        current.ClockSpeed = uint.Parse(m.Groups["clk"].Value, CultureInfo.InvariantCulture);

                        double capValue = double.Parse(m.Groups["cap"].Value, NumberStyles.Float, CultureInfo.InvariantCulture);
                        CapacityUnit unit;
                        switch (m.Groups["unit"].Value.ToUpperInvariant())
                        {
                            case "KB": unit = CapacityUnit.KB; break;
                            case "MB": unit = CapacityUnit.MB; break;
                            default: unit = CapacityUnit.GB; break; // GB, or bare "B"/"TB" fall back to GB-scale storage
                        }
                        ulong bytes = (ulong)Math.Round(capValue * Math.Pow(1024, (int)unit));
                        current.Capacity = new Capacity(bytes, unit);
                    }
                    // Bank/row/col/RM/bank-group description lines aren't needed to populate the
                    // main timings UI and are intentionally not parsed here.
                }
            }

            if (current != null)
                modules.Add(current);

            return modules;
        }

        /// <summary>
        /// Parses the "Memory Config" section's reflection-dumped "PropertyName:      value" lines
        /// straight onto a <see cref="BaseDramTimings"/> instance via its indexer, converting each
        /// raw text value to the type the target property actually needs first (the indexer's own
        /// conversion only handles same-typed values, not text).
        /// </summary>
        private static bool ParseTimings(string text, BaseDramTimings timings)
        {
            string[] lines = text.Split('\n');

            int start = FindSectionContentStart(lines, "Memory Config");
            if (start < 0)
                return false;

            int end = FindNextHeadingLine(lines, start);
            var lineRegex = new Regex(@"^(?<name>[A-Za-z0-9_]+):\s*(?<value>.*)$");

            for (int i = start; i < end; i++)
            {
                string line = lines[i].TrimEnd();
                if (line.Trim().Length == 0)
                    continue;

                Match m = lineRegex.Match(line);
                if (!m.Success)
                    continue;

                SetTimingProperty(timings, m.Groups["name"].Value, m.Groups["value"].Value.Trim());
            }

            return true;
        }

        private static void SetTimingProperty(BaseDramTimings timings, string name, string rawValue)
        {
            PropertyInfo pi = timings.GetType().GetProperty(name);
            if (pi == null || !pi.CanWrite || rawValue.Length == 0)
                return;

            Type targetType = pi.PropertyType;
            object value;

            if (targetType == typeof(BooleanProp))
            {
                uint v = rawValue.Equals("Enabled", StringComparison.OrdinalIgnoreCase) ? 1u
                    : rawValue.Equals("Disabled", StringComparison.OrdinalIgnoreCase) ? 0u
                    : 2u; // "Unknown" (or anything unrecognised) round-trips through BooleanProp's own "Unknown" case
                value = v;
            }
            else if (targetType == typeof(CommandRateProp))
            {
                uint v = rawValue == "1T" ? 0u : rawValue == "2T" ? 1u : 2u;
                value = v;
            }
            else if (targetType == typeof(Ddr5Timings.NitroSettings))
            {
                // NitroSettings.ToString() prints "RxData/TxData/CtrlLine" (its per-channel delay-mode
                // bits aren't included in that string, so they can't be recovered from a report and are
                // left at 0). Re-encode just those three fields into a raw register value so the
                // struct's own constructor decodes them back out identically.
                Match nm = Regex.Match(rawValue, @"^(?<rx>\d+)/(?<tx>\d+)/(?<ctrl>\d+)$");
                if (!nm.Success)
                    return;
                if (!byte.TryParse(nm.Groups["rx"].Value, out byte rx) ||
                    !byte.TryParse(nm.Groups["tx"].Value, out byte tx) ||
                    !byte.TryParse(nm.Groups["ctrl"].Value, out byte ctrl))
                    return;

                uint registerValue = (uint)(ctrl & 0x3) | ((uint)(tx & 0x3) << 4) | ((uint)(rx & 0x3) << 8);
                value = new Ddr5Timings.NitroSettings(registerValue);
            }
            else if (targetType == typeof(BankRefreshMode))
            {
                // BankRefreshMode is a smart-enum class (not a real enum), so it needs its own
                // text->instance mapping instead of Enum.Parse; values mirror BankRefreshMode.ToString().
                switch (rawValue)
                {
                    case "Normal": value = BankRefreshMode.NORMAL; break;
                    case "FGR": value = BankRefreshMode.FGR; break;
                    case "Mixed": value = BankRefreshMode.MIXED; break;
                    case "Per-Bank Only": value = BankRefreshMode.PBONLY; break;
                    default: value = BankRefreshMode.UNKNOWN; break;
                }
            }
            else if (targetType.IsEnum)
            {
                try
                {
                    object enumValue = Enum.Parse(targetType, rawValue, true);
                    if (!Enum.IsDefined(targetType, enumValue))
                        return;
                    value = enumValue;
                }
                catch (ArgumentException)
                {
                    return;
                }
            }
            else if (targetType == typeof(uint))
            {
                if (!uint.TryParse(rawValue, NumberStyles.Integer, CultureInfo.InvariantCulture, out uint v))
                    return;
                value = v;
            }
            else if (targetType == typeof(float))
            {
                if (!float.TryParse(rawValue, NumberStyles.Float, CultureInfo.InvariantCulture, out float v))
                    return;
                value = v;
            }
            else
            {
                // Computed/read-only properties (Frequency, RFCns, REFIns, ...) and anything else
                // unsupported are simply left alone.
                return;
            }

            timings[name] = value;
        }

        /// <summary>
        /// Finds the first content line of a "######\n{heading}\n######" section produced by
        /// DebugDialog's AddHeading, given the exact heading title.
        /// </summary>
        internal static int FindSectionContentStart(string[] lines, string heading)
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
        internal static int FindNextHeadingLine(string[] lines, int from)
        {
            for (int i = from; i < lines.Length; i++)
            {
                if (IsHashLine(lines[i]))
                    return i;
            }
            return lines.Length;
        }

        internal static bool IsHashLine(string line)
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
    }
}
