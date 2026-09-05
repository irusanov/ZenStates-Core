using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using ZenStates.Core.Drivers;
using static ZenStates.Core.Cpu;

namespace ZenStates.Core.Hardware.Apob
{
    public readonly struct CcdlData
    {
        public uint Tccdl { get; }
        public uint Tccdlwr { get; }
        public uint Tccdlwr2 { get; }

        public CcdlData(uint tccdl, uint tccdlwr, uint tccdlwr2)
        {
            Tccdl = tccdl;
            Tccdlwr = tccdlwr;
            Tccdlwr2 = tccdlwr2;
        }
    }

    /// <summary>
    /// Reads and parses the AGESA PSP Output Block (APOB) from physical memory.
    /// </summary>
    public sealed class Apob
    {
        private const uint APOB_SIGNATURE = 0x424F5041; // "APOB"
        private const uint HASH_SIZE = 32;
        private const uint CONFIG_LIST_START = 0x30;
        private const uint ENTRY_SIZE_OFFSET = 0x0C;
        private const uint DATA_PARSE_LEAD_BYTES = 48;
        private const uint RTT_BLOCK_SIZE = 5;

        private static readonly uint[] KnownAddresses = new uint[] { 0xA200000, 0x9F00000, 0x4000000 };
        private static readonly IODriver io = IODriver.Instance;

        private readonly CPUInfo _cpuInfo;
        private readonly ApobProfile _profile;

        /// <summary>Gets a value indicating whether a valid APOB was located in physical memory.</summary>
        public bool IsAvailable { get { return Address != 0; } }

        /// <summary>Human-readable reason why APOB initialisation failed, or <c>null</c> on success.</summary>
        public string ErrorReason { get; private set; }

        /// <summary>Physical base address of the APOB table.</summary>
        public uint Address { get; private set; }
        public uint DataOffset { get; private set; }
        public uint DataSize { get; private set; }
        public int MainLayoutDataOffset { get; private set; } = -1;
        public int MainLayoutDataRelativeOffset { get; private set; } = -1;
        public uint ExtendedDataOffset { get; private set; }
        public uint ExtendedDataSize { get; private set; }
        public int ExtendedLayoutDataOffset { get; private set; } = -1;
        public int ExtendedLayoutDataRelativeOffset { get; private set; } = -1;

        public ApobHeader Header { get; private set; }
        public ApobData Data { get; private set; }
        public ApobData ExtendedData { get; private set; }

        public CcdlData CcdlData { get; private set; } = new CcdlData();

        /// <summary>Offsets of all non-zero config entries found inside the header region.</summary>
        public List<uint> ConfigOffsets { get; private set; } = new List<uint>();

        /// <summary>Raw bytes of the entire APOB table.</summary>
        public byte[] RawTable { get; private set; }

        public byte[] RawHeader
        {
            get { return SliceRawTable(0, Header.HeaderSize); }
        }

        public byte[] RawData
        {
            get { return SliceRawTable(DataOffset, DataSize); }
        }

        public byte[] RawExtendedData
        {
            get { return SliceRawTable(ExtendedDataOffset, ExtendedDataSize); }
        }

        public Apob(CPUInfo cpuInfo)
        {
            if (io == null)
            {
                ErrorReason = "IODriver instance is not available.";
                Debug.WriteLine(ErrorReason);
                return;
            }

            _cpuInfo = cpuInfo;
            _profile = ApobProfiles.Resolve(_cpuInfo);

            Address = FindApobAddress();
            if (!IsAvailable)
            {
                ErrorReason = "APOB signature not found at any known physical address.";
                return;
            }

            if (!TryParseHeader(Address, out ApobHeader header))
            {
                ErrorReason = string.Format("Failed to read or parse APOB header at address 0x{0:X8}.", Address);
                return;
            }
            Header = header;

            RawTable = io.ReadMemory(new IntPtr(Address), unchecked((int)Header.TableSize));
            if (RawTable == null || RawTable.Length == 0)
            {
                ErrorReason = string.Format("Failed to read APOB table body ({0} bytes) at address 0x{1:X8}.", Header.TableSize, Address);
                return;
            }

            ConfigOffsets = GetConfigOffsets(RawTable, Header);
            if (ConfigOffsets.Count == 0)
            {
                ErrorReason = "No valid config entry offsets found in APOB header region.";
                return;
            }

            if (!TryGetMainConfig())
            {
                ErrorReason = "Failed to locate or validate the primary APOB config block.";
                return;
            }

            TryGetExtendedConfig();
            TryGetCcdlBlock();
            ParseDataBlocks();
        }

        /// <summary>
        /// Bypasses physical-memory access entirely. Used only by <see cref="CreateFromDebugReport"/>
        /// to build a mock instance from previously captured debug report text.
        /// </summary>
        private Apob(CPUInfo cpuInfo, ApobProfile profile)
        {
            _cpuInfo = cpuInfo;
            _profile = profile;
        }

        /// <summary>Returns a copy of the requested region, or <c>null</c> when unavailable.</summary>
        private byte[] SliceRawTable(uint offset, uint size)
        {
            if (RawTable == null || size == 0)
                return null;

            long end = (long)offset + size;
            if (end > RawTable.Length)
                return null;

            byte[] buffer = new byte[size];
            Buffer.BlockCopy(RawTable, (int)offset, buffer, 0, (int)size);
            return buffer;
        }

        private static uint FindApobAddress()
        {
            for (int i = 0; i < KnownAddresses.Length; i++)
            {
                if (io.GetPhysLong(new UIntPtr(KnownAddresses[i]), out uint data) && data == APOB_SIGNATURE)
                    return KnownAddresses[i];
            }

            return 0;
        }

        /// <summary>
        /// Reads the header size from offset 0xC, then reads and deserialises the full header.
        /// </summary>
        private static bool TryParseHeader(uint address, out ApobHeader header)
        {
            header = default;
            try
            {
                if (!io.GetPhysLong(new UIntPtr(address + ENTRY_SIZE_OFFSET), out uint headerSize) || headerSize == 0)
                    return false;

                byte[] headerData = io.ReadMemory(new IntPtr(address), (int)headerSize);
                if (headerData == null || headerData.Length < (int)headerSize)
                    return false;

                header = Utils.ByteArrayToStructure<ApobHeader>(headerData);
                return true;
            }
            catch (Exception ex)
            {
                Debug.WriteLine(ex.Message);
                return false;
            }
        }

        private static List<uint> GetConfigOffsets(byte[] table, ApobHeader header)
        {
            var list = new List<uint>();

            if (table == null || header.HeaderSize == 0 || header.TableSize == 0)
                return list;

            int regionLength = (int)(header.HeaderSize - CONFIG_LIST_START - HASH_SIZE);
            if (regionLength <= 0)
                return list;

            uint regionEnd = CONFIG_LIST_START + (uint)regionLength;

            for (uint i = CONFIG_LIST_START; i + 3 < regionEnd && i + 3 < table.Length; i += 4)
            {
                uint offset = Utils.ReadUInt32(table, i);
                if (offset != 0 && offset + ENTRY_SIZE_OFFSET + 4 < table.Length)
                    list.Add(offset);
            }

            return list;
        }

        private bool TryGetMainConfig()
        {
            if (ConfigOffsets == null || ConfigOffsets.Count == 0)
                return false;

            uint firstOffset = ConfigOffsets[0];
            if (firstOffset + ENTRY_SIZE_OFFSET + 4 >= RawTable.Length)
                return false;

            uint firstEntrySize = Utils.ReadUInt32(RawTable, firstOffset + ENTRY_SIZE_OFFSET);
            uint secondOffset = firstOffset + firstEntrySize;

            if (secondOffset + ENTRY_SIZE_OFFSET + 4 >= RawTable.Length)
                return false;
            if (secondOffset + 5 >= RawTable.Length)
                return false;

            if (RawTable[secondOffset] != 0x01 || RawTable[secondOffset + 4] != 0x19)
                return false;

            uint secondSize = Utils.ReadUInt32(RawTable, secondOffset + ENTRY_SIZE_OFFSET);
            if (secondSize < (uint)_profile.MainLayout.BlockSize)
                return false;

            DataOffset = secondOffset;
            DataSize = secondSize;
            return true;
        }

        private bool TryGetExtendedConfig()
        {
            for (int i = 0; i < ConfigOffsets.Count; i++)
            {
                uint offset = ConfigOffsets[i];

                if (offset + 5 >= RawTable.Length)
                    continue;

                if (RawTable[offset] == 0x07 && RawTable[offset + 4] == 0x03)
                {
                    if (offset + ENTRY_SIZE_OFFSET + 4 >= RawTable.Length)
                        return false;

                    ExtendedDataOffset = offset;
                    ExtendedDataSize = Utils.ReadUInt32(RawTable, offset + ENTRY_SIZE_OFFSET);

                    if (ExtendedDataSize < (uint)_profile.ExtendedLayout.BlockSize)
                    {
                        ExtendedDataOffset = 0;
                        ExtendedDataSize = 0;
                        continue;
                    }

                    return true;
                }
            }

            return false;
        }

        private void TryGetCcdlBlock()
        {
            byte[] sourceData = _profile.CcdlLayout.SourceBlock == ApobBlockKind.Main ? RawData : RawExtendedData;
            if (sourceData == null)
                return;

            if (ApobDataReader.TryReadCcdl(sourceData, _profile.CcdlLayout, out uint ccdl, out uint ccdlrw, out uint ccdlrw2))
            {
                CcdlData = new CcdlData(ccdl, ccdlrw, ccdlrw2);
            }
        }

        private void ParseDataBlocks()
        {
            if (DataSize == 0)
                return;

            uint start = DataOffset + DATA_PARSE_LEAD_BYTES;
            uint end = DataOffset + DataSize;

            if (start >= end || end > RawTable.Length)
                return;

            for (uint i = start; i < end; i++)
            {
                if (RawTable[i] == 0)
                    continue;

                if (i + 6 >= end)
                    return;

                if (!ApobDataReader.TryRead(RawTable, i, _profile.MainLayout, out ApobData data))
                    return;

                Data = data;
                MainLayoutDataOffset = (int)i;
                MainLayoutDataRelativeOffset = (int)(i - DataOffset);

                byte[] rttBlock = new byte[RTT_BLOCK_SIZE];
                Buffer.BlockCopy(RawTable, (int)i + 2, rttBlock, 0, (int)RTT_BLOCK_SIZE);

                if (Utils.AllZero(rttBlock))
                    return;

                if (RawExtendedData == null)
                    return;

                // Locate the same sequence inside the extended data block.
                int extendedMatch = Utils.FindSequence(RawExtendedData, 0, rttBlock);
                if (extendedMatch < 2)
                    return;

                if (ApobDataReader.TryRead(RawExtendedData, (uint)(extendedMatch - 2), _profile.ExtendedLayout, out ApobData extendedData))
                {
                    ExtendedData = extendedData;
                    ExtendedLayoutDataRelativeOffset = (int)(extendedMatch - 2);
                    ExtendedLayoutDataOffset = (int)(ExtendedDataOffset + ExtendedLayoutDataRelativeOffset);
                }

                return;
            }
        }

        // ---------------------------------------------------------------------------------
        // Debug support: rebuild a mock Apob purely from the text of a previously captured
        // ZenTimings debug report (see DebugDialog / Apob.GetReport()), without touching
        // physical memory. Useful for diagnosing APOB parsing issues from a user-supplied
        // report on a machine that doesn't have the affected CPU.
        //
        // Nothing above this point is modified; everything below is purely additive and
        // reuses the existing private TryGetCcdlBlock()/ParseDataBlocks() methods so the mock
        // goes through the exact same block-scanning logic as real hardware.
        // ---------------------------------------------------------------------------------

        /// <summary>
        /// Builds a mock <see cref="Apob"/> instance by re-parsing the "APOB" section of a
        /// ZenTimings debug report. The CPU codename, family, package type and SMU type are
        /// recovered from the report text and used to resolve the same <see cref="ApobProfile"/>
        /// that would have been used on the reporting machine; the raw "Data" and "Extended Data"
        /// byte blocks are then run through the normal block-scanning logic, so
        /// <see cref="GetReport"/> and the decoded <see cref="Data"/>/<see cref="ExtendedData"/>
        /// properties behave the same as they would have on the original machine.
        /// </summary>
        /// <param name="debugReportText">The full text of a ZenTimings debug report.</param>
        /// <returns>
        /// A non-null <see cref="Apob"/> instance. If the report's "Raw Data" section (the
        /// minimum required input) cannot be located, <see cref="IsAvailable"/> is <c>false</c>
        /// and <see cref="ErrorReason"/> explains why.
        /// </returns>
        /// <remarks>
        /// Family is read from an explicit "Family:" line when present, otherwise derived from
        /// the "CpuId:" (CPUID_Fn8000_0001_EAX) value using the same bit layout as
        /// <c>Cpu.GetCodeName</c>. PackageType is read from a "PackageType:" line when present;
        /// since it does not influence APOB profile resolution, it defaults to
        /// <see cref="PackageType.FPX"/> when the report predates that field. CodeName falls
        /// back to <see cref="CodeName.DEBUG"/> when it cannot be parsed.
        /// </remarks>
        public static Apob CreateFromDebugReport(string debugReportText)
        {
            if (debugReportText == null)
                throw new ArgumentNullException(nameof(debugReportText));

            string text = NormalizeLineEndings(debugReportText);

            CPUInfo mockCpuInfo = new CPUInfo
            {
                family = ParseFamily(text),
                codeName = ParseCodeName(text),
                packageType = ParsePackageType(text),
                smuType = ParseSmuType(text)
            };

            Apob apob = new Apob(mockCpuInfo, ApobProfiles.Resolve(mockCpuInfo));

            byte[] rawHeaderBytes = ParseRawSection(text, "-- Raw Header");
            byte[] rawDataBytes = ParseRawSection(text, "-- Raw Data");
            byte[] rawExtendedDataBytes = ParseRawSection(text, "-- Raw Extended Data");

            if (rawDataBytes == null || rawDataBytes.Length == 0)
            {
                apob.ErrorReason = "Could not locate an APOB 'Raw Data' section in the supplied debug report.";
                return apob;
            }

            ApobHeader header = default;
            if (rawHeaderBytes != null && rawHeaderBytes.Length > 0)
            {
                try
                {
                    header = Utils.ByteArrayToStructure<ApobHeader>(rawHeaderBytes);
                }
                catch (Exception ex)
                {
                    Debug.WriteLine(ex.Message);
                }
            }

            uint dataOffset = ParseHexValue(text, "-- Main Data Offset:") ?? (uint)(rawHeaderBytes?.Length ?? 0);
            uint dataSize = Math.Max(ParseHexValue(text, "-- Main Data Size:") ?? 0, (uint)rawDataBytes.Length);
            uint extendedDataOffset = ParseHexValue(text, "-- Ext. Data Offset:") ?? (dataOffset + dataSize);
            uint extendedDataSize = Math.Max(
                ParseHexValue(text, "-- Ext. Data Size:") ?? 0,
                (uint)(rawExtendedDataBytes?.Length ?? 0));

            // Sized from the actual extracted byte counts (not just the declared "Length:"/size
            // values) so a hand-edited or truncated report can't overrun the buffer below.
            long tableLength = Math.Max(
                header.HeaderSize,
                Math.Max((long)dataOffset + dataSize, (long)extendedDataOffset + extendedDataSize));

            byte[] rawTable = new byte[tableLength];
            if (rawHeaderBytes != null)
                Buffer.BlockCopy(rawHeaderBytes, 0, rawTable, 0, Math.Min(rawHeaderBytes.Length, rawTable.Length));

            Buffer.BlockCopy(rawDataBytes, 0, rawTable, (int)dataOffset,
                Math.Min(rawDataBytes.Length, rawTable.Length - (int)dataOffset));

            if (rawExtendedDataBytes != null && rawExtendedDataBytes.Length > 0)
                Buffer.BlockCopy(rawExtendedDataBytes, 0, rawTable, (int)extendedDataOffset,
                    Math.Min(rawExtendedDataBytes.Length, rawTable.Length - (int)extendedDataOffset));

            apob.Address = ParseHexValue(text, "-- Address:") ?? 0xFFFFFFFF; // sentinel: mock, no real physical address
            apob.Header = header;
            apob.RawTable = rawTable;
            apob.DataOffset = dataOffset;
            apob.DataSize = dataSize;
            apob.ExtendedDataOffset = extendedDataOffset;
            apob.ExtendedDataSize = extendedDataSize;
            apob.ConfigOffsets = ParseConfigOffsets(text);

            // Reuse the exact same block-scanning logic used for real hardware.
            apob.ParseDataBlocks();
            apob.TryGetCcdlBlock();

            return apob;
        }

        private static string NormalizeLineEndings(string text)
        {
            return text.Replace("\r\n", "\n").Replace("\r", "\n");
        }

        private static Family ParseFamily(string text)
        {
            string raw = ParseLabelValue(text, "Family:");
            if (raw != null)
            {
                if (Utils.TryParseEnum(raw, out Family family))
                    return family;

                if (TryParseNumeric(raw, out uint numericFamily))
                    return (Family)numericFamily;
            }

            // Fall back to deriving it from CPUID_Fn8000_0001_EAX (same formula as Cpu.GetCodeName),
            // since older debug reports don't print an explicit "Family:" line.
            string cpuIdRaw = ParseLabelValue(text, "CpuId:");
            if (cpuIdRaw != null &&
                uint.TryParse(cpuIdRaw, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out uint eax))
            {
                return (Family)(((eax & 0xf00) >> 8) + ((eax & 0xff00000) >> 20));
            }

            return Family.UNSUPPORTED;
        }

        private static CodeName ParseCodeName(string text)
        {
            string raw = ParseLabelValue(text, "CodeName:");
            if (raw != null && Utils.TryParseEnum(raw, out CodeName codeName))
                return codeName;

            // CodeName.DEBUG exists specifically for mocked/synthetic scenarios like this one.
            return CodeName.DEBUG;
        }

        private static PackageType ParsePackageType(string text)
        {
            string raw = ParseLabelValue(text, "PackageType:");
            if (raw != null)
            {
                if (Utils.TryParseEnum(raw, out PackageType packageType))
                    return packageType;

                if (TryParseNumeric(raw, out uint numericPackageType))
                    return (PackageType)numericPackageType;
            }

            // Not present in older reports, and not used by ApobProfiles.Resolve, so any
            // reasonable default is fine here.
            return PackageType.FPX;
        }

        private static SMU.SmuType ParseSmuType(string text)
        {
            string raw = ParseLabelValue(text, "SmuType:");
            if (raw != null && Utils.TryParseEnum(raw, out SMU.SmuType smuType))
                return smuType;

            return SMU.SmuType.TYPE_UNSUPPORTED;
        }

        private static bool TryParseNumeric(string raw, out uint value)
        {
            raw = raw.Trim();
            if (raw.StartsWith("0x", StringComparison.OrdinalIgnoreCase))
                return uint.TryParse(raw.Substring(2), NumberStyles.HexNumber, CultureInfo.InvariantCulture, out value);

            return uint.TryParse(raw, NumberStyles.Integer, CultureInfo.InvariantCulture, out value);
        }

        // Matches a "Label:value" line anchored at the start of a line (as produced by
        // DebugDialog's fixed-width report formatting) and returns the first whitespace-delimited
        // token after the label, or null if the label isn't present.
        private static string ParseLabelValue(string text, string label)
        {
            Match match = Regex.Match(
                text,
                "^" + Regex.Escape(label) + @"[ \t]*(\S+)",
                RegexOptions.IgnoreCase | RegexOptions.Multiline);

            return match.Success ? match.Groups[1].Value : null;
        }

        // Same as ParseLabelValue, but for "Label: 0xHEXVALUE" lines such as
        // "-- Main Data Offset: 0x00001DB4".
        private static uint? ParseHexValue(string text, string label)
        {
            Match match = Regex.Match(
                text,
                "^" + Regex.Escape(label) + @"[ \t]*0x([0-9A-Fa-f]+)",
                RegexOptions.IgnoreCase | RegexOptions.Multiline);

            if (match.Success &&
                uint.TryParse(match.Groups[1].Value, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out uint value))
                return value;

            return null;
        }

        private static List<uint> ParseConfigOffsets(string text)
        {
            var list = new List<uint>();
            foreach (Match match in Regex.Matches(
                text,
                @"^Config Offset\[\d+\]:[ \t]*0x([0-9A-Fa-f]+)",
                RegexOptions.IgnoreCase | RegexOptions.Multiline))
            {
                if (uint.TryParse(match.Groups[1].Value, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out uint offset))
                    list.Add(offset);
            }

            return list;
        }

        // Parses one of the "-- Raw Header/Raw Data/Raw Extended Data --" sections produced by
        // GetReport()/AppendRawBinaryData: a "Length: N" line followed by N bytes, formatted as
        // space-separated hex pairs, 16 per line.
        private static byte[] ParseRawSection(string text, string sectionHeaderPrefix)
        {
            string[] lines = text.Split('\n');

            int headerLine = -1;
            for (int i = 0; i < lines.Length; i++)
            {
                if (lines[i].TrimEnd().StartsWith(sectionHeaderPrefix, StringComparison.OrdinalIgnoreCase))
                {
                    headerLine = i;
                    break;
                }
            }

            if (headerLine < 0)
                return null;

            int lengthLine = -1;
            int expectedLength = -1;
            for (int i = headerLine + 1; i < lines.Length && i < headerLine + 4; i++)
            {
                Match lengthMatch = Regex.Match(lines[i], @"Length:\s*(\d+)", RegexOptions.IgnoreCase);
                if (lengthMatch.Success)
                {
                    lengthLine = i;
                    expectedLength = int.Parse(lengthMatch.Groups[1].Value, CultureInfo.InvariantCulture);
                    break;
                }
            }

            if (lengthLine < 0 || expectedLength <= 0)
                return null;

            var bytes = new List<byte>(expectedLength);
            for (int i = lengthLine + 1; i < lines.Length && bytes.Count < expectedLength; i++)
            {
                string line = lines[i].Trim();
                if (line.Length == 0)
                    break;

                string[] tokens = line.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
                bool isHexLine = true;
                foreach (string token in tokens)
                {
                    if (token.Length != 2 || !byte.TryParse(token, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out _))
                    {
                        isHexLine = false;
                        break;
                    }
                }

                if (!isHexLine)
                    break;

                foreach (string token in tokens)
                {
                    if (bytes.Count >= expectedLength)
                        break;
                    bytes.Add(byte.Parse(token, NumberStyles.HexNumber, CultureInfo.InvariantCulture));
                }
            }

            return bytes.Count > 0 ? bytes.ToArray() : null;
        }

        public string GetReport()
        {
            StringBuilder sb = new StringBuilder();

            sb.AppendLine("APOB");
            sb.AppendLine();

            try
            {
                if (!IsAvailable)
                {
                    sb.AppendLine("<APOB table not available>");
                    if (!string.IsNullOrEmpty(ErrorReason))
                        sb.AppendLine(ErrorReason);

                    sb.AppendLine();
                    return sb.ToString();
                }

                sb.AppendLine(string.Format("-- Address: 0x{0:X8}", Address));
                sb.AppendLine(string.Format("-- Main Data Offset: 0x{0:X8}", DataOffset));
                sb.AppendLine(string.Format("-- Main Data Size: 0x{0:X8} ({0})", DataSize));
                sb.AppendLine(string.Format("-- Main Layout Offset: 0x{0:X8}", MainLayoutDataOffset));
                sb.AppendLine(string.Format("-- Main Layout Rel. Offset: 0x{0:X8} ({0})", MainLayoutDataRelativeOffset));
                sb.AppendLine(string.Format("-- Ext. Data Offset: 0x{0:X8}", ExtendedDataOffset));
                sb.AppendLine(string.Format("-- Ext. Data Size: 0x{0:X8} ({0})", ExtendedDataSize));
                sb.AppendLine(string.Format("-- Ext. Layout Offset: 0x{0:X8}", ExtendedLayoutDataOffset));
                sb.AppendLine(string.Format("-- Ext. Layout Rel. Offset: 0x{0:X8} ({0})", ExtendedLayoutDataRelativeOffset));
                sb.AppendLine();
                sb.AppendLine("-- Metadata -------------------------------------");
                sb.AppendLine(string.Format("{0,-28}{1}", "Config Offsets Count:", ConfigOffsets != null ? ConfigOffsets.Count : 0));
                sb.AppendLine(string.Format("{0,-28}{1}", "Main Block Parsed:", Data != null));
                sb.AppendLine(string.Format("{0,-28}{1}", "Extended Block Parsed:", ExtendedData != null));
                sb.AppendLine(string.Format("{0,-28}{1}", "Raw Table Bytes:", RawTable != null ? RawTable.Length : 0));

                if (ConfigOffsets != null && ConfigOffsets.Count > 0)
                {
                    for (int i = 0; i < ConfigOffsets.Count; i++)
                    {
                        sb.AppendLine(string.Format("{0,-28}0x{1:X8}", "Config Offset[" + i + "]:", ConfigOffsets[i]));
                    }
                }

                sb.AppendLine();
                sb.AppendLine("-- Header ---------------------------------------");

                var headerProperties = Header.GetType().GetProperties();
                for (int i = 0; i < headerProperties.Length; i++)
                {
                    var property = headerProperties[i];
                    object value = property.GetValue(Header, null);
                    sb.AppendLine(string.Format("{0,-20}{1}", property.Name + ":", value));
                }

                sb.AppendLine();
                sb.AppendLine("-- Data -----------------------------------------");
                if (Data != null)
                {
                    sb.Append(Data.GetReport());
                }
                else
                {
                    sb.AppendLine("<APOB table data not available>");
                }

                sb.AppendLine();
                sb.AppendLine("-- Extended Data --------------------------------");
                if (ExtendedData != null)
                {
                    sb.Append(ExtendedData.GetReport());
                }
                else
                {
                    sb.AppendLine("<APOB extended data not available>");
                }

                sb.AppendLine();
                sb.AppendLine("-- CCDL Data ------------------------------------");
                var ccdlFields = CcdlData.GetType().GetFields();
                if (ccdlFields.Length == 0)
                {
                    sb.AppendLine("<APOB CCDL data not available>");
                }
                else
                {
                    for (int i = 0; i < ccdlFields.Length; i++)
                    {
                        var field = ccdlFields[i];
                        object value = field.GetValue(CcdlData);
                        sb.AppendLine(string.Format("{0,-20}{1}", field.Name + ":", value ?? "N/A"));
                    }
                }

                sb.AppendLine();
                sb.AppendLine("APOB: Raw");
                sb.AppendLine();

                sb.AppendLine("-- Raw Header -----------------------------------");
                sb.AppendLine(string.Format("Length: {0}", RawHeader != null ? RawHeader.Length : 0));
                try
                {
                    if (!AppendRawBinaryData(sb, RawHeader))
                        sb.AppendLine("<APOB raw header not available>");
                }
                catch (Exception ex)
                {
                    sb.AppendLine("<FAILED>");
                    sb.AppendLine(ex.Message);
                }

                sb.AppendLine();
                sb.AppendLine("-- Raw Data -------------------------------------");
                sb.AppendLine(string.Format("Length: {0}", RawData != null ? RawData.Length : 0));
                try
                {
                    if (!AppendRawBinaryData(sb, RawData))
                        sb.AppendLine("<APOB raw data not available>");
                }
                catch (Exception ex)
                {
                    sb.AppendLine("<FAILED>");
                    sb.AppendLine(ex.Message);
                }

                sb.AppendLine();
                sb.AppendLine("-- Raw Extended Data ----------------------------");
                sb.AppendLine(string.Format("Length: {0}", RawExtendedData != null ? RawExtendedData.Length : 0));
                try
                {
                    if (!AppendRawBinaryData(sb, RawExtendedData))
                        sb.AppendLine("<APOB raw extended data not available>");
                }
                catch (Exception ex)
                {
                    sb.AppendLine("<FAILED>");
                    sb.AppendLine(ex.Message);
                }

                sb.AppendLine();
            }
            catch (Exception ex)
            {
                sb.AppendLine("<FAILED>");
                sb.AppendLine(ex.Message);
                sb.AppendLine();
            }

            return sb.ToString();
        }

        private static bool AppendRawBinaryData(StringBuilder sb, byte[] data)
        {
            if (data == null || data.Length == 0)
                return false;

            for (int i = 0; i < data.Length; i += 16)
            {
                int length = Math.Min(16, data.Length - i);

                for (int j = 0; j < length; j++)
                {
                    if (j > 0)
                        sb.Append(' ');

                    sb.Append(data[i + j].ToString("X2"));
                }

                sb.AppendLine();
            }

            return true;
        }
    }
}