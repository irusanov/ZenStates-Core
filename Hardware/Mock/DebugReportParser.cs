using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text.RegularExpressions;
using ZenStates.Core.Hardware.Aod;
using ZenStates.Core.Hardware.DRAM;
using ZenStates.Core.Hardware.DRAM.DDR5.Pmic;
using ZenStates.Core.Hardware.DRAM.DDR5.Spd;
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

        // The part number is optional: a module with a blank SPD part number prints "--  16GB 8000MHz".
        private static readonly Regex ModulePartRegex = new Regex(
            @"^(?:(?<part>.+?)\s+)?(?<cap>\d{1,9}(?:[.,]\d{1,6})?)(?<unit>[KMGT]?B)\s+(?<clk>\d{1,6})MHz$",
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
            new Regex(@"^Frequency:\s*(\d+(?:[.,]\d+)?)\s*$", RegexOptions.Multiline);

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
        /// DRAM type from the explicit "MemType:" line newer ZenTimings builds emit, or
        /// <see cref="MemType.UNKNOWN"/> when the report doesn't state it. Older reports are resolved
        /// from the captured UMC registers instead, see <see cref="MockDramTimings.TryReadMemType(IRegisterSource, out MemType)"/>.
        /// </summary>
        public static MemType ParseMemType(string text)
        {
            string raw = ApobTable.ParseLabelValue(text, "MemType:");
            if (raw != null && Utils.TryParseEnum(raw, out MemType memType))
                return memType;

            return MemType.UNKNOWN;
        }

        /// <summary>
        /// True when the report has a populated "SMBUS Memory Modules" section, which is only ever
        /// printed for DDR5 systems. A last-resort hint for reports without a UMC register dump.
        /// </summary>
        public static bool HasDdr5SmbusModules(string text)
        {
            return Ddr5SmbusModuleRegex.IsMatch(text);
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
                if (m.Success && TryParseReportFloat(m.Groups[1].Value, out float value))
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
                if (uint.TryParse(hex, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out uint channelIndex) &&
                    channelIndex <= 0xFFF)
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

            if (!double.TryParse(m.Groups["cap"].Value.Replace(',', '.'), NumberStyles.Float, CultureInfo.InvariantCulture, out double capValue))
                return;

            CapacityUnit unit;
            switch (m.Groups["unit"].Value.ToUpperInvariant())
            {
                case "KB": unit = CapacityUnit.KB; break;
                case "MB": unit = CapacityUnit.MB; break;
                default: unit = CapacityUnit.GB; break; // GB, or bare "B"/"TB" fall back to GB-scale storage
            }

            double scaled = Math.Round(capValue * Math.Pow(1024, (int)unit));
            if (double.IsNaN(scaled) || scaled < 0 || scaled >= ulong.MaxValue)
                return;

            ulong bytes = (ulong)scaled;
            module.Capacity = new Capacity(bytes, unit);
        }

        #endregion

        #region SMBUS SPD / PMIC

        /// <summary>Heading of the section holding one decoded SPD dump per populated DIMM.</summary>
        public const string SmbusModulesSection = "SMBUS Memory Modules";

        private static readonly Regex DimmAddressRegex =
            new Regex(@"^DIMM at I2C address 0x(?<addr>[0-9A-Fa-f]{1,2})\s*$");

        // Mirrors the "  Label             : value" layout every decoded dump uses.
        private static readonly Regex SpdLabelRegex =
            new Regex(@"^[ \t]+(?<label>[^:]+?)[ \t]*:[ \t]*(?<value>.*?)[ \t]*$");

        // Bounded so a malformed value can't overflow the int/float it's parsed into.
        private static readonly Regex LeadingIntRegex = new Regex(@"^(?<value>\d{1,9})(?!\d)");
        private static readonly Regex LeadingFloatRegex = new Regex(@"^(?<value>\d{1,9}(?:[.,]\d{1,9})?)(?!\d|[.,]\d)");
        private static readonly Regex HexValueRegex = new Regex(@"0x(?<hex>[0-9A-Fa-f]{1,8})(?![0-9A-Fa-f])");

        private const string SpdInvalidMarker = "*** INVALID OR UNSUPPORTED SPD DATA ***";

        /// <summary>
        /// Parses the "SMBUS Memory Modules" section into the same shape
        /// <see cref="MemoryConfig.SpdInfo"/> carries on a live machine: one entry per DIMM, keyed
        /// by the SPD hub's SMBus address.
        /// <para>
        /// The report holds decoded text rather than the raw 1024-byte SPD image, so the entries are
        /// partial (<see cref="Ddr5SpdInfo.IsPartial"/>): identity, the headline JEDEC numbers and -
        /// the point of this - the PMIC block. Returns an empty dictionary for reports without the
        /// section, i.e. every DDR4 report and DDR5 reports written before it existed.
        /// </para>
        /// </summary>
        public static Dictionary<byte, Ddr5SpdInfo> ParseSpdInfo(string[] lines)
        {
            var result = new Dictionary<byte, Ddr5SpdInfo>();

            int start = FindSectionContentStart(lines, SmbusModulesSection);
            if (start < 0)
                return result;

            int end = FindNextHeadingLine(lines, start);

            int blockStart = -1;
            byte blockAddress = 0;

            for (int i = start; i <= end; i++)
            {
                Match m = i < end ? DimmAddressRegex.Match(lines[i].Trim()) : Match.Empty;

                // A new "DIMM at ..." line, or the end of the section, closes the block before it.
                if (!m.Success && i < end)
                    continue;

                if (blockStart >= 0)
                {
                    Ddr5SpdInfo info = ParseSpdInfoBlock(lines, blockStart, i, blockAddress);
                    if (info != null && !result.ContainsKey(blockAddress))
                        result.Add(blockAddress, info);
                }

                if (i < end)
                {
                    // The regex allows at most two hex digits, so this always fits a byte.
                    uint parsedAddress;
                    blockAddress = TryParseHex(m.Groups["addr"].Value, out parsedAddress) && parsedAddress <= 0xFF
                        ? (byte)parsedAddress
                        : (byte)0;
                    blockStart = i + 1;
                }
            }

            return result;
        }

        /// <summary>
        /// Reads the PMIC block of one decoded SPD dump, i.e. everything under
        /// "-- PMIC (Power Management IC) ---" up to the next sub-heading. Returns null when the
        /// dump has no PMIC block.
        /// </summary>
        public static Ddr5PmicData ParsePmicData(string[] lines, int start, int end)
        {
            int pmicStart = -1;

            for (int i = start; i < end; i++)
            {
                if (lines[i].Trim().StartsWith(Ddr5PmicDecoder.PmicDumpHeading, StringComparison.Ordinal))
                {
                    pmicStart = i + 1;
                    break;
                }
            }

            if (pmicStart < 0)
                return null;

            int pmicEnd = pmicStart;
            while (pmicEnd < end && !IsDumpSubHeading(lines[pmicEnd]))
                pmicEnd++;

            var block = new string[pmicEnd - pmicStart];
            Array.Copy(lines, pmicStart, block, 0, block.Length);

            return Ddr5PmicDecoder.DecodeFromDump(block);
        }

        /// <summary>
        /// A "-- Section ---" line or the "======" rule that closes a dump. Value lines are indented,
        /// so a line starting at column zero with either marker ends the current block.
        /// </summary>
        private static bool IsDumpSubHeading(string line)
        {
            return line.StartsWith("--", StringComparison.Ordinal) ||
                   line.StartsWith("==", StringComparison.Ordinal);
        }

        private static Ddr5SpdInfo ParseSpdInfoBlock(string[] lines, int start, int end, byte address)
        {
            var info = new Ddr5SpdInfo
            {
                // Text-derived: the raw image is not in the report, and neither are most SPD bytes.
                IsPartial = true,
                RawSpd = null,
            };

            bool invalid = false;
            for (int i = start; i < end; i++)
            {
                if (lines[i].IndexOf(SpdInvalidMarker, StringComparison.Ordinal) >= 0)
                {
                    invalid = true;
                    break;
                }
            }

            info.IsValid = !invalid;

            info.SpdRevision = FindBlockValue(lines, start, end, "SPD Revision");
            info.DeviceTypeString = FindBlockValue(lines, start, end, "Device Type");
            info.MemoryFamily = FindBlockValue(lines, start, end, "Memory Family");
            info.ModuleTypeString = FindBlockValue(lines, start, end, "Module Type");
            info.BytesTotal = FindBlockInt(lines, start, end, "SPD Bytes Total");
            info.TotalCapacityMB = FindBlockInt(lines, start, end, "Total Capacity");

            // Base JEDEC speed. The EXPO/XMP blocks further down repeat these labels, so only the
            // first occurrence - the JEDEC one - is taken.
            info.SpeedGrade = FindBlockValue(lines, start, end, "Speed Grade");
            info.SpeedMTs = FindBlockInt(lines, start, end, "Data Rate");
            info.ClockMHz = FindBlockFloat(lines, start, end, "Clock Frequency");
            info.TimingString = FindBlockValue(lines, start, end, "Timing");
            info.VddString = FindBlockValue(lines, start, end, "VDD");

            info.HasThermalSensor = string.Equals(
                FindBlockValue(lines, start, end, "Thermal Sensor"), "Present", StringComparison.OrdinalIgnoreCase);

            string spdDevice = FindBlockValue(lines, start, end, "SPD Device");
            info.SpdDevicePresent = IsListed(spdDevice);
            info.SpdDeviceTypeString = info.SpdDevicePresent ? spdDevice : null;

            string pmic0 = FindBlockValue(lines, start, end, "PMIC0");
            info.Pmic0Present = IsListed(pmic0);
            info.Pmic0TypeString = info.Pmic0Present ? pmic0 : null;

            info.ModuleManufacturer = FindBlockValue(lines, start, end, "Module Manufacturer");
            info.ModulePartNumber = FindBlockValue(lines, start, end, "Module Part Number");
            info.ModuleSerialNumber = FindBlockValue(lines, start, end, "Module Serial");
            info.ModuleMfgDate = FindBlockValue(lines, start, end, "Module Date");
            info.DramManufacturer = FindBlockValue(lines, start, end, "DRAM Manufacturer");
            info.DramStepping = FindBlockHex(lines, start, end, "DRAM Stepping");

            info.IsLpddr5 = info.MemoryFamily != null &&
                            info.MemoryFamily.IndexOf("LPDDR5", StringComparison.OrdinalIgnoreCase) >= 0;

            info.PmicData = ParsePmicData(lines, start, end);

            // The PMIC prints its own I2C address, but fall back to the SPD hub's when it doesn't.
            if (info.PmicData != null && info.PmicData.IsValid && info.PmicData.SpdHubAddress == 0)
                info.PmicData.SpdHubAddress = address;

            return info;
        }

        /// <summary>"Not listed" is what the dump prints for a support device the SPD doesn't declare.</summary>
        private static bool IsListed(string value)
        {
            return !string.IsNullOrEmpty(value) &&
                   !value.Equals("Not listed", StringComparison.OrdinalIgnoreCase);
        }

        /// <summary>
        /// First value printed for a label within a block. First, not last: the EXPO and XMP profile
        /// blocks repeat several of the base SPD labels.
        /// </summary>
        private static string FindBlockValue(string[] lines, int start, int end, string label)
        {
            for (int i = start; i < end; i++)
            {
                Match m = SpdLabelRegex.Match(lines[i]);
                if (m.Success && m.Groups["label"].Value.Trim() == label)
                    return m.Groups["value"].Value;
            }

            return null;
        }

        private static int FindBlockInt(string[] lines, int start, int end, string label)
        {
            string value = FindBlockValue(lines, start, end, label);
            if (value == null)
                return 0;

            Match m = LeadingIntRegex.Match(value);
            int result;
            return m.Success &&
                   int.TryParse(m.Groups["value"].Value, NumberStyles.Integer, CultureInfo.InvariantCulture, out result)
                ? result
                : 0;
        }

        private static double FindBlockFloat(string[] lines, int start, int end, string label)
        {
            string value = FindBlockValue(lines, start, end, label);
            if (value == null)
                return 0;

            Match m = LeadingFloatRegex.Match(value);
            float result;
            return m.Success && TryParseReportFloat(m.Groups["value"].Value, out result)
                ? result
                : 0;
        }

        private static int FindBlockHex(string[] lines, int start, int end, string label)
        {
            string value = FindBlockValue(lines, start, end, label);
            if (value == null)
                return 0;

            Match m = HexValueRegex.Match(value);
            int result;
            return m.Success &&
                   int.TryParse(m.Groups["hex"].Value, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out result)
                ? result
                : 0;
        }

        #endregion

        #region AOD

        /// <summary>Heading of the section written by <see cref="AOD.GetReport"/>.</summary>
        public const string AodSection = "ACPI: AOD Table";

        /// <summary>
        /// Heading some older builds used instead, with the header split off into its own
        /// "ACPI: AOD Table Header" section.
        /// </summary>
        public const string AodDataSection = "ACPI: AOD Table Data";

        /// <summary>
        /// Heading a few 140.5xx builds used, with "ACPI: AOD Table" demoted to a plain line inside
        /// and the same "-- Data --" block as the current layout.
        /// </summary>
        public const string AodShortSection = "AOD";

        // "Tcl:               34", "RttWr:             RZQ/6 (40)          (6)", "MemVddio:          1.5400V"
        private static readonly Regex AodValueRegex = new Regex(@"^(?<name>\w+):\s*(?<value>.*?)\s*$");
        private static readonly Regex AodRawSuffixRegex = new Regex(@"\((?<raw>-?\d+)\)$");
        private static readonly Regex AodTextSuffixRegex = new Regex(@"^(?<text>.*?)\s*\(\d+\)$");
        // Either decimal separator: the report prints values in the capturing machine's culture.
        private static readonly Regex AodVoltageRegex = new Regex(@"^(?<volts>\d{1,3}(?:[.,]\d{1,6})?)V$");

        /// <summary>
        /// Reads the decoded AOD fields of the AOD section back into an <see cref="AodData"/>, using
        /// the printed values rather than re-decoding the raw table, whose layout depends on the
        /// captured machine's CPU, microcode, memory clock and BIOS.
        /// <para>
        /// Newer reports put the fields in a "-- Data --" block and print each encoded value's raw
        /// code after its text: "RttWr:  RZQ/6 (40)  (6)". Older ones list the fields straight under
        /// the heading - "ACPI: AOD Table" after the ACPI header, or a separate "ACPI: AOD Table Data"
        /// section - and print the text alone: "RttWr:  RZQ/6 (40)", so the raw code is recovered
        /// from the value's lookup table.
        /// </para>
        /// Returns null when the section carries no AOD field.
        /// </summary>
        public static AodData ParseAodData(string[] lines)
        {
            string[] section = GetSectionLines(lines, AodSection);
            if (section.Length == 0)
                section = GetSectionLines(lines, AodDataSection);
            if (section.Length == 0)
                section = GetSectionLines(lines, AodShortSection);

            int dataStart = -1;
            for (int i = 0; i < section.Length; i++)
            {
                if (section[i].Trim().StartsWith("-- Data", StringComparison.Ordinal))
                {
                    dataStart = i + 1;
                    break;
                }
            }

            bool printsRawCodes = dataStart >= 0;

            var data = new AodData();
            var seen = new Dictionary<string, bool>();

            for (int i = printsRawCodes ? dataStart : 0; i < section.Length; i++)
            {
                string trimmed = section[i].Trim();
                if (printsRawCodes && trimmed.StartsWith("--", StringComparison.Ordinal))
                    break;

                Match m = AodValueRegex.Match(trimmed);
                if (!m.Success)
                    continue;

                string name = m.Groups["name"].Value;
                string value = m.Groups["value"].Value;

                // An empty or "N/A" value is a field the layout doesn't have; it stays null/0.
                if (value.Length == 0 || value == "N/A" || seen.ContainsKey(name))
                    continue;

                if (ApplyAodField(data, name, value, printsRawCodes))
                    seen[name] = true;
            }

            return seen.Count > 0 ? data : null;
        }

        /// <summary>
        /// Sets one printed field. The text becomes the raw value AodData stores: millivolts for a
        /// voltage ("1.5400V"), the code of an encoded value, or the plain integer. Names that aren't
        /// AodData fields - the ACPI header lines of older reports, for instance - yield false.
        /// </summary>
        private static bool ApplyAodField(AodData data, string name, string value, bool printsRawCodes)
        {
            Dictionary<int, string> lookup;
            if (!AodData.IsField(name, out lookup))
                return false;

            int raw;
            bool parsed = TryParseAodMillivolts(value, out raw) ||
                          (lookup != null
                              ? TryParseAodCode(value, lookup, printsRawCodes, out raw)
                              : int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out raw));

            return parsed && data.TrySetRaw(name, raw);
        }

        /// <summary>
        /// Code of a printed encoded value: the trailing "(N)" in reports that print it, otherwise a
        /// reverse lookup of the text in <paramref name="lookup"/>.
        /// </summary>
        private static bool TryParseAodCode(string value, Dictionary<int, string> lookup, bool printsRawCodes, out int code)
        {
            code = 0;

            if (printsRawCodes)
            {
                Match raw = AodRawSuffixRegex.Match(value);
                return raw.Success &&
                       int.TryParse(raw.Groups["raw"].Value, NumberStyles.Integer, CultureInfo.InvariantCulture, out code);
            }

            if (TryFindCode(lookup, value, out code))
                return true;

            // Rtt prints its divider after the text: "RZQ/6 (40)".
            Match text = AodTextSuffixRegex.Match(value);
            return text.Success && TryFindCode(lookup, text.Groups["text"].Value, out code);
        }

        private static bool TryParseAodMillivolts(string value, out int millivolts)
        {
            millivolts = 0;

            Match voltage = AodVoltageRegex.Match(value);
            float volts;
            if (!voltage.Success || !TryParseReportFloat(voltage.Groups["volts"].Value, out volts) ||
                float.IsNaN(volts) || volts < 0 || volts > 1000)
                return false;

            millivolts = (int)Math.Round(volts * 1000);
            return true;
        }

        /// <summary>Reverse lookup of a printed text. The texts are unique within each table.</summary>
        private static bool TryFindCode(Dictionary<int, string> lookup, string text, out int code)
        {
            foreach (KeyValuePair<int, string> entry in lookup)
            {
                if (entry.Value == text)
                {
                    code = entry.Key;
                    return true;
                }
            }

            code = 0;
            return false;
        }

        #endregion

        #region SuperIO

        /// <summary>Heading of the section holding each SuperIO chip's report.</summary>
        public const string SuperIoSection = "SuperIO";

        private static readonly Regex SuperIoChipRegex = new Regex(@"^LPC\s+(?<class>\w+)\s*$");
        private static readonly Regex SuperIoHexLabelRegex =
            new Regex(@"^(?<label>Chip Id|Chip Revision|Chip Version|Base Address|GPIO Address):\s*0x(?<hex>[0-9A-Fa-f]{1,8})\s*$", RegexOptions.IgnoreCase);
        private static readonly Regex SuperIoBankRegex = new Regex(@"Registers Bank\s+(?<bank>\d{1,3})(?!\d)", RegexOptions.IgnoreCase);

        // " 0480   80 E4 D6 ..." (Nuvoton, bank in the address) or " 20   3F 7A ?? ..." (ITE, per bank).
        private static readonly Regex SuperIoRowRegex =
            new Regex(@"^(?<address>[0-9A-Fa-f]{2,4})\s{2,}(?<bytes>(?:(?:[0-9A-Fa-f]{2}|\?\?)\s*){1,16})$");

        /// <summary>
        /// Reads the "SuperIO" section into one <see cref="SuperIoDump"/> per chip, in report order,
        /// which is the chip index the live board configuration uses. Empty for reports without the
        /// section or with no chip detected.
        /// </summary>
        public static List<SuperIoDump> ParseSuperIo(string[] lines)
        {
            var dumps = new List<SuperIoDump>();
            SuperIoDump current = null;
            int bank = 0;

            foreach (string line in GetSectionLines(lines, SuperIoSection))
            {
                string trimmed = line.Trim();

                Match chip = SuperIoChipRegex.Match(trimmed);
                if (chip.Success)
                {
                    current = new SuperIoDump { ChipClass = chip.Groups["class"].Value };
                    dumps.Add(current);
                    bank = 0;
                    continue;
                }

                if (current == null)
                    continue;

                Match label = SuperIoHexLabelRegex.Match(trimmed);
                if (label.Success)
                {
                    // Out-of-range values are malformed: the field keeps its default.
                    uint value;
                    if (!TryParseHex(label.Groups["hex"].Value, out value))
                        continue;

                    switch (label.Groups["label"].Value.ToLowerInvariant())
                    {
                        case "chip id":
                            if (value <= ushort.MaxValue)
                                current.ChipId = (ushort)value;
                            break;
                        case "base address":
                            if (value <= ushort.MaxValue)
                                current.BaseAddress = (ushort)value;
                            break;
                        case "gpio address":
                            if (value <= ushort.MaxValue)
                                current.GpioAddress = (ushort)value;
                            break;
                        default: // Chip Revision / Chip Version
                            if (value <= byte.MaxValue)
                                current.Revision = (byte)value;
                            break;
                    }
                    continue;
                }

                Match bankHeading = SuperIoBankRegex.Match(trimmed);
                if (bankHeading.Success)
                {
                    // Registers are keyed (bank << 8) | register, so a bank must fit a byte.
                    uint parsedBank = ParseUInt(bankHeading.Groups["bank"].Value);
                    bank = parsedBank <= byte.MaxValue ? (int)parsedBank : 0;
                    continue;
                }

                Match row = SuperIoRowRegex.Match(trimmed);
                if (!row.Success)
                    continue;

                // ITE prints two-digit per-bank rows; Nuvoton's four-digit rows already hold the bank.
                string address = row.Groups["address"].Value;
                uint rowAddress;
                if (!TryParseHex(address, out rowAddress))
                    continue;

                uint rowBase = rowAddress | (address.Length == 2 ? (uint)(bank << 8) : 0);

                string[] cells = row.Groups["bytes"].Value.Split(new[] { ' ', '\t' }, StringSplitOptions.RemoveEmptyEntries);
                for (int j = 0; j < cells.Length && j < 16; j++)
                {
                    uint cell;
                    if (cells[j] != "??" && TryParseHex(cells[j], out cell))
                        current.Registers.Set(rowBase + (uint)j, cell);
                }
            }

            return dumps;
        }

        #endregion

        #region Byte dumps

        /// <summary>
        /// The DDR4 BIOS memory controller config (APCB) ZenTimings reads over WMI - ProcODT, RTT,
        /// drive strengths, setup times and the DRAM rails.
        /// </summary>
        public const string BiosMemControllerSection = "BIOS: Memory Controller Config";

        // "Index 033: 3A (58)", written by ReportBuilder.AppendIndexedBytes. D3 pads the index to at
        // least three digits, longer blocks print more.
        private static readonly Regex IndexedByteRegex =
            new Regex(@"^Index\s+(?<index>\d{1,6}):\s*(?<hex>[0-9A-Fa-f]{2})\b");

        /// <summary>
        /// Largest index <see cref="ParseIndexedBytes"/> accepts. The dumps it reads are a few
        /// hundred bytes; the cap keeps a malformed index from sizing a huge allocation.
        /// </summary>
        private const int MaxIndexedByteIndex = 0xFFFF;

        /// <summary>
        /// Reads an "Index NNN: XX (d)" byte dump back into its bytes. Returns null when the section
        /// is absent or holds no dump, e.g. "&lt;FAILED&gt;".
        /// </summary>
        public static byte[] ParseIndexedBytes(string[] lines, string sectionHeading)
        {
            var bytes = new Dictionary<int, byte>();
            int maxIndex = -1;

            foreach (string line in GetSectionLines(lines, sectionHeading))
            {
                Match m = IndexedByteRegex.Match(line.Trim());
                int index;
                uint value;
                if (!m.Success ||
                    !int.TryParse(m.Groups["index"].Value, NumberStyles.Integer, CultureInfo.InvariantCulture, out index) ||
                    index < 0 || index > MaxIndexedByteIndex ||
                    !TryParseHex(m.Groups["hex"].Value, out value) || value > byte.MaxValue)
                {
                    continue;
                }

                bytes[index] = (byte)value;
                if (index > maxIndex)
                    maxIndex = index;
            }

            if (maxIndex < 0)
                return null;

            var result = new byte[maxIndex + 1];
            foreach (KeyValuePair<int, byte> entry in bytes)
                result[entry.Key] = entry.Value;

            return result;
        }

        #endregion

        #region Primitives

        public static uint ParseHex(string value)
        {
            return uint.Parse(value, NumberStyles.HexNumber, CultureInfo.InvariantCulture);
        }

        /// <summary>Non-throwing <see cref="ParseHex"/>: false for empty, non-hex or over-32-bit input.</summary>
        public static bool TryParseHex(string value, out uint result)
        {
            result = 0;
            return value != null &&
                   uint.TryParse(value, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out result);
        }

        /// <summary>
        /// Parses a decimal the report printed with the capturing machine's culture: many locales
        /// write "0,9687" rather than "0.9687". Report values never carry thousands separators, so a
        /// comma is always the decimal one.
        /// </summary>
        public static bool TryParseReportFloat(string value, out float result)
        {
            result = 0;
            return value != null &&
                   float.TryParse(value.Trim().Replace(',', '.'), NumberStyles.Float, CultureInfo.InvariantCulture, out result);
        }

        public static uint ParseUInt(string value)
        {
            return uint.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out uint result) ? result : 0;
        }

        #endregion
    }
}
