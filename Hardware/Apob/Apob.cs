using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Text.RegularExpressions;
using ZenStates.Core.Common;
using ZenStates.Core.Drivers;
using ZenStates.Core.Hardware.DRAM;
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

        public override string ToString()
        {
            if (Tccdl != 0 && Tccdlwr != 0 && Tccdlwr2 != 0)
            {
                return $"{Tccdl}/{Tccdlwr}/{Tccdlwr2}";
            }
            return null;
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

        // APOB Group Definitons
        private const uint APOB_MEM      = 1;
        private const uint APOB_DF       = 2;
        private const uint APOB_CCX      = 3;
        private const uint APOB_GNB      = 4;
        private const uint APOB_FCH      = 5;
        private const uint APOB_PSP      = 6;
        private const uint APOB_GEN      = 7;
        private const uint APOB_SMBIOS   = 8;
        private const uint APOB_FABRIC   = 9;

        // APOB Type Definitons
        private const uint APOB_MEM_GENERAL_ERRORS_TYPE                             = 1;
        private const uint APOB_MEM_GENERAL_CONFIGURATION_INFO_TYPE                 = 2;
        private const uint APOB_GEN_CONFIGURATION_INFO_TYPE                         = 3;
        private const uint APOB_GEN_S3_REPLAY_BUFFER_INFO_TYPE                      = 4;
        private const uint APOB_MEM_PMU_SMB_TYPE                                    = 5;
        private const uint APOB_GEN_EVENT_LOG_TYPE                                  = 6;
        private const uint APOB_MEM_DIMM_SMBUS_INFO_TYPE                            = 7;
        private const uint APOB_MEM_SMBIOS_TYPE                                     = 8;
        private const uint APOB_SYS_MAP_INFO_TYPE                                   = 9;
        private const uint APOB_MEM_NVDIMM_INFO_TYPE                                = 15;
        private const uint APOB_APCB_BOOT_INFO_TYPE                                 = 16;
        private const uint APOB_MEM_DIMM_SPD_DATA_TYPE                              = 17;
        private const uint APOB_MEM_MBIST_RESULT_INFO_TYPE                          = 18;
        private const uint APOB_SYS_NPS_INFO_TYPE                                   = 19;
        private const uint APOB_SYS_SLINK_INFO_TYPE                                 = 20;
        private const uint APOB_DF_DXIO_PHY_FW_OVERRIDE_INFO_TYPE                   = 21;
        private const uint APOB_MEM_PMU_TRAINING_FAILURE_INFO_TYPE                  = 22;
        private const uint APOB_ENV_FLAGS_INFO_TYPE                                 = 23;
        private const uint APOB_SYS_CXL_INFO_TYPE                                   = 24;
        private const uint APOB_MEM_SYSTEM_CONFIGURATION_INFO_TYPE                  = 25;
        private const uint APOB_GEN_CONFIG_DATA_TYPE                                = 26;
        private const uint APOB_MEM_SOC_INIT_CONFIG_TYPE                            = 27;
        private const uint APOB_MEM_RMP_INFO                                        = 28;

        private const uint APOB_MEM_S3_DDR_PHY_REPLAY_PHASE0_BUFFER_INFO_TYPE       = 30;
        private const uint APOB_MEM_S3_DDR_PHY_REPLAY_MAX_ENTRIES                   = 10;

        private const uint APOB_MEM_S3_MOP_ARRAY_REPLAY_CHANNEL0_BUFFER_INFO_TYPE   = 40;
        //private const uint APOB_MEM_S3_MOP_ARRAY_REPLAY_MAX_ENTRIES                 = ABL_APOB_MAX_CHANNELS_PER_DIE;

        // APOB CCX Type Definitons
        private const uint APOB_CCX_LOGICAL_TO_PHYSICAL_MAP_TYPE  = 1;
        private const uint APOB_CCX_EDC_THROTTLE_THRESH_TYPE      = 2;
        private const uint APOB_CCD_LOGICAL_TO_PHYSICAL_MAP_TYPE  = 3;

        private static readonly uint[] KnownAddresses = new uint[] { 0xA200000, 0x9F00000, 0x4000000 };
        private static IODriver _ioDriver => IODriver.Instance;

        private readonly CPUInfo _cpuInfo;
        private readonly ApobProfile _profile;

        public delegate uint RegisterReader(uint address);

        private readonly MemoryConfig _memoryConfig;

        // The memory is LPDDR5: the DRAM Vrefs of the ODT records use the LPDDR5 encoding
        private bool _lpddr5;

        /// <summary>Gets a value indicating whether a valid APOB was located in physical memory.</summary>
        public bool IsAvailable { get { return Address != 0; } }

        /// <summary>
        /// Indicates whether the APOB was successfully read and parsed, and contains at least one valid data block.
        /// </summary>
        public bool IsValid { get { return IsAvailable && (Data != null || ExtendedData != null); } }

        /// <summary>Name of the layout profile used for this CPU (e.g. "Zen5 Desktop"), null when unsupported.</summary>
        public string ProfileName { get { return _profile?.Name; } }

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

        /// <summary>
        /// The CCD / CCX / core logical to physical maps (APOB_CCX group), one per instance (socket), empty when the
        /// APOB has none.
        /// </summary>
        public List<ApobCoreMap> CoreMaps { get; private set; } = new List<ApobCoreMap>();

        /// <summary>The core map of the first instance, or <c>null</c>.</summary>
        public ApobCoreMap CoreMap
        {
            get { return CoreMaps != null && CoreMaps.Count > 0 ? CoreMaps[0] : null; }
        }

        /// <summary>All the entries of the table, in table order (empty for an instance built from a debug report).</summary>
        public List<ApobEntry> Entries { get; private set; } = new List<ApobEntry>();

        /// <summary>APCB boot info (MEM group, type 16): DIMM fingerprints and the date of the last training.</summary>
        public ApobBootInfo BootInfo { get; private set; }

        /// <summary>Memory SMBIOS info (SMBIOS group, type 8): memory type, DIMM speed, voltage and address ranges.</summary>
        public ApobDmiInfo DmiInfo { get; private set; }

        /// <summary>
        /// The SPD of each populated slot as the ABL used it (MEM group, type 17); for soldered LPDDR5 the only copy.
        /// Empty when the APOB has none.
        /// </summary>
        public List<ApobDimmSpd> DimmSpd { get; private set; } = new List<ApobDimmSpd>();

        /// <summary>System memory map (FABRIC group, type 9): top of memory and the reserved holes.</summary>
        public ApobMemoryMap MemoryMap { get; private set; }

        /// <summary>
        /// Memory general configuration info (MEM group, type 2) of the DDR4 programs: the impedance / setup
        /// settings of each channel and the state of the memory options. Null on DDR5.
        /// </summary>
        public ApobMemGeneralConfig MemGeneralConfig { get; private set; }

        /// <summary>The ABL event log (GEN group, type 6).</summary>
        public ApobEventLog EventLog { get; private set; }

        /// <summary>The memory profile (MEM group, type 28), tentative.</summary>
        public ApobMemoryProfileInfo MemoryProfileInfo { get; private set; }

        /// <summary>
        /// The record of each channel of the main entry (MEM group, type 25), read with the main layout (its block size
        /// is the record size). Empty when the entry is not a whole number of records.
        /// </summary>
        public List<ApobData> ChannelData { get; private set; } = new List<ApobData>();

        /// <summary>The timings of each channel (GEN group, type 3), empty when the profile has no timing layout.</summary>
        public List<ApobChannelTimings> ChannelTimings { get; private set; } = new List<ApobChannelTimings>();

        /// <summary>
        /// LPDDR5 (Rembrandt): the mode registers of the first active timing block, or of the first block when the
        /// active one is not known. Null when the timing blocks have none.
        /// </summary>
        public ApobLpddr5ModeRegisters ActiveLpddr5ModeRegisters
        {
            get
            {
                if (ChannelTimings == null)
                    return null;

                ApobLpddr5ModeRegisters first = null;
                for (int i = 0; i < ChannelTimings.Count; i++)
                {
                    ApobLpddr5ModeRegisters registers = ChannelTimings[i].Lpddr5ModeRegisters;
                    if (registers == null)
                        continue;
                    if (ChannelTimings[i].IsActive)
                        return registers;
                    if (first == null)
                        first = registers;
                }
                return ActiveMemClk > 0 ? null : first;
            }
        }

        /// <summary>
        /// MEMCLK the memory runs at, from the timings ratio (ratio x 100, as for AOD), used to mark the active
        /// timing blocks. 0 when not known (e.g. a table parsed without timings).
        /// </summary>
        public uint ActiveMemClk { get; private set; }

        /// <summary>Non-zero data bytes of the PMU training failure info entry, null when the APOB has none.</summary>
        public int? TrainingFailureDataBytes { get; private set; }

        /// <summary>Non-zero data bytes of the MBIST result info entry, null when the APOB has none.</summary>
        public int? MbistResultDataBytes { get; private set; }

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

        public Apob(CPUInfo cpuInfo, MemoryConfig memoryConfig = null)
        {
            if (_ioDriver == null)
            {
                ErrorReason = "I/O Driver instance is not available.";
                Debug.WriteLine(ErrorReason);
                return;
            }

            _cpuInfo = cpuInfo;
            _memoryConfig = memoryConfig;
            // Might be not defined, but we still need to get raw data
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

            RawTable = _ioDriver.ReadMemory(new IntPtr(Address), unchecked((int)Header.TableSize));
            if (RawTable == null || RawTable.Length == 0)
            {
                ErrorReason = string.Format("Failed to read APOB table body ({0} bytes) at address 0x{1:X8}.", Header.TableSize, Address);
                return;
            }

            ParseTable();
        }

        /// <summary>
        /// Bypasses physical-memory access entirely. Used by <see cref="CreateFromDebugReport"/> and
        /// <see cref="CreateFromRawTable"/> to build an instance from captured data.
        /// </summary>
        private Apob(CPUInfo cpuInfo, ApobProfile profile, MemoryConfig memoryConfig = null)
        {
            _cpuInfo = cpuInfo;
            _profile = profile;
            _memoryConfig = memoryConfig;
        }

        /// <summary>
        /// Builds an instance from a raw APOB table (e.g. a dump of the table read from physical memory), parsed
        /// the same way as the table of the running system.
        /// </summary>
        /// <param name="rawTable">The table, from the "APOB" signature on.</param>
        /// <param name="cpuInfo">The CPU the table comes from (family, code name and SMU type select the layouts).</param>
        /// <param name="memoryConfig">Optional, used to pick the CCD_L block like on the running system.</param>
        public static Apob CreateFromRawTable(byte[] rawTable, CPUInfo cpuInfo, MemoryConfig memoryConfig = null)
        {
            if (rawTable == null)
                throw new ArgumentNullException(nameof(rawTable));

            Apob apob = new Apob(cpuInfo, ApobProfiles.Resolve(cpuInfo), memoryConfig);

            if (rawTable.Length < 16 || Utils.ReadUInt32(rawTable, 0) != APOB_SIGNATURE)
            {
                apob.ErrorReason = "The raw table does not start with the APOB signature.";
                return apob;
            }

            ApobHeader header = Utils.ByteArrayToStructure<ApobHeader>(rawTable);
            if (header.HeaderSize == 0 || header.HeaderSize > rawTable.Length)
            {
                apob.ErrorReason = "The raw table has an invalid header size.";
                return apob;
            }

            uint tableSize = header.TableSize > 0 && header.TableSize <= rawTable.Length ? header.TableSize : (uint)rawTable.Length;
            byte[] table = new byte[tableSize];
            Buffer.BlockCopy(rawTable, 0, table, 0, (int)tableSize);

            apob.Address = 0xFFFFFFFF; // sentinel: no physical address
            apob.Header = header;
            apob.RawTable = table;
            apob.ParseTable();
            return apob;
        }

        /// <summary>Parses <see cref="RawTable"/>, <see cref="Header"/> must be set.</summary>
        private void ParseTable()
        {
            ConfigOffsets = GetConfigOffsets(RawTable, Header);

            // Independent of the memory layouts and of the CPU profile
            TryParseCoreMap();
            TryParseEntries();

            if (Entries.Count == 0)
            {
                ErrorReason = "No valid entries found in the APOB table.";
                return;
            }

            bool hasMainConfig = TryGetConfigFromEntries(APOB_MEM, APOB_MEM_SYSTEM_CONFIGURATION_INFO_TYPE, true);
            TryGetConfigFromEntries(APOB_GEN, APOB_GEN_CONFIGURATION_INFO_TYPE, false);

            // Abort if profile is not defined for this CPU family
            if (_profile == null)
            {
                ErrorReason = string.Format("Unsupported CPU family ({0}) for APOB parsing; refusing to guess an offset layout.", _cpuInfo.family);
                Debug.WriteLine(ErrorReason);
                return;
            }

            // DDR4 tables have no system configuration info entry, their profiles have no main layout
            if (!hasMainConfig && _profile.MainLayout != null)
            {
                ErrorReason = "Failed to locate or validate the primary APOB config block.";
                return;
            }

            ParseDataBlocks();
            TryParseChannelBlocks();
            TryGetCcdlBlock();
            ApplyMemoryType();
        }

        // Tells the ODT records which DRAM Vref encoding applies
        private void ApplyMemoryType()
        {
            if (_memoryConfig != null && _memoryConfig.Type == MemType.LPDDR5)
                _lpddr5 = true;
            if (!_lpddr5)
                return;

            if (Data != null)
                Data.IsLpddr5 = true;
            if (ExtendedData != null)
                ExtendedData.IsLpddr5 = true;
            if (ChannelData != null)
            {
                for (int i = 0; i < ChannelData.Count; i++)
                    ChannelData[i].IsLpddr5 = true;
            }
            if (ChannelTimings != null)
            {
                for (int i = 0; i < ChannelTimings.Count; i++)
                {
                    if (ChannelTimings[i].ExtendedData != null)
                        ChannelTimings[i].ExtendedData.IsLpddr5 = true;
                }
            }
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
                if (_ioDriver.GetPhysLong(new UIntPtr(KnownAddresses[i]), out uint data) && data == APOB_SIGNATURE)
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
                if (!_ioDriver.GetPhysLong(new UIntPtr(address + ENTRY_SIZE_OFFSET), out uint headerSize) || headerSize == 0)
                    return false;

                byte[] headerData = _ioDriver.ReadMemory(new IntPtr(address), (int)headerSize);
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

            for (long i = CONFIG_LIST_START; i + 3 < regionEnd && i + 3 < table.Length; i += 4)
            {
                uint offset = Utils.ReadUInt32(table, (uint)i);
                if (offset != 0 && (long)offset + ENTRY_SIZE_OFFSET + 4 < table.Length)
                    list.Add(offset);
            }

            return list;
        }

        /// <summary>
        /// Takes the main (MEM type 25) or extended (GEN type 3) config entry from <see cref="Entries"/>, the first
        /// readable one. The entries include the ones at the header offsets (<see cref="ConfigOffsets"/>).
        /// </summary>
        private bool TryGetConfigFromEntries(uint groupId, uint dataTypeId, bool main)
        {
            ApobEntry entry = FindEntry(groupId, dataTypeId);
            if (entry == null)
                return false;

            ApobBlockLayout layout = main ? _profile?.MainLayout : _profile?.ExtendedLayout;
            if (layout != null && entry.Size < (uint)layout.BlockSize)
                return false;

            if (main)
            {
                DataOffset = entry.Offset;
                DataSize = entry.Size;
            }
            else
            {
                ExtendedDataOffset = entry.Offset;
                ExtendedDataSize = entry.Size;
            }

            return true;
        }

        /// <summary>The first readable entry of the given group and type, or null.</summary>
        public ApobEntry FindEntry(uint groupId, uint dataTypeId)
        {
            if (Entries == null)
                return null;

            for (int i = 0; i < Entries.Count; i++)
            {
                if (!Entries[i].IsEncrypted && Entries[i].Is(groupId, dataTypeId))
                    return Entries[i];
            }

            return null;
        }

        /// <summary>A copy of the first readable entry of the given group and type (header included), or null.</summary>
        public byte[] GetRawEntry(uint groupId, uint dataTypeId)
        {
            ApobEntry entry = FindEntry(groupId, dataTypeId);
            return entry != null ? entry.GetRawEntry(RawTable) : null;
        }

        /// <summary>Lists the entries of the table and decodes the ones with a known layout.</summary>
        private void TryParseEntries()
        {
            try
            {
                Entries = ApobEntry.Enumerate(RawTable, Header.HeaderSize, Header.TableSize, ConfigOffsets);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"APOB entries: {ex.Message}");
                Entries = new List<ApobEntry>();
                return;
            }

            DecodeEntries(GetRawEntry);

            ApobEntry trainingFailure = FindEntry(APOB_MEM, APOB_MEM_PMU_TRAINING_FAILURE_INFO_TYPE);
            if (trainingFailure != null)
                TrainingFailureDataBytes = trainingFailure.CountNonZeroDataBytes(RawTable);

            ApobEntry mbist = FindEntry(APOB_MEM, APOB_MEM_MBIST_RESULT_INFO_TYPE);
            if (mbist != null)
                MbistResultDataBytes = mbist.CountNonZeroDataBytes(RawTable);
        }

        private delegate byte[] RawEntryProvider(uint groupId, uint dataTypeId);

        // The decoded entries, also dumped raw in the report so CreateFromDebugReport can decode them again
        private static readonly uint[][] DecodedEntryTypes =
        {
            new[] { APOB_MEM, APOB_APCB_BOOT_INFO_TYPE },
            new[] { APOB_SMBIOS, APOB_MEM_SMBIOS_TYPE },
            new[] { APOB_FABRIC, APOB_SYS_MAP_INFO_TYPE },
            new[] { APOB_MEM, APOB_MEM_RMP_INFO },
            new[] { APOB_GEN, APOB_GEN_EVENT_LOG_TYPE },
            new[] { APOB_MEM, APOB_MEM_GENERAL_CONFIGURATION_INFO_TYPE },
            new[] { APOB_MEM, APOB_MEM_DIMM_SPD_DATA_TYPE },
        };

        /// <summary>
        /// Decodes the entries with a known layout, <paramref name="getRawEntry"/> gives a copy of an entry (header
        /// included), null when there is none.
        /// </summary>
        private void DecodeEntries(RawEntryProvider getRawEntry)
        {
            byte[] raw;

            try
            {
                if ((raw = getRawEntry(APOB_MEM, APOB_APCB_BOOT_INFO_TYPE)) != null)
                    BootInfo = ApobBootInfo.Decode(raw, 0);
                if ((raw = getRawEntry(APOB_SMBIOS, APOB_MEM_SMBIOS_TYPE)) != null)
                    DmiInfo = ApobDmiInfo.Decode(raw, 0);
                if ((raw = getRawEntry(APOB_FABRIC, APOB_SYS_MAP_INFO_TYPE)) != null)
                    MemoryMap = ApobMemoryMap.Decode(raw, 0);
                if ((raw = getRawEntry(APOB_MEM, APOB_MEM_RMP_INFO)) != null)
                    MemoryProfileInfo = ApobMemoryProfileInfo.Decode(raw, 0);
                if ((raw = getRawEntry(APOB_GEN, APOB_GEN_EVENT_LOG_TYPE)) != null)
                    EventLog = ApobEventLog.Decode(raw, 0);
                if ((raw = getRawEntry(APOB_MEM, APOB_MEM_GENERAL_CONFIGURATION_INFO_TYPE)) != null)
                    MemGeneralConfig = ApobMemGeneralConfig.Decode(raw, 0);
                if ((raw = getRawEntry(APOB_MEM, APOB_MEM_DIMM_SPD_DATA_TYPE)) != null)
                    DimmSpd = ApobDimmSpdParser.Decode(raw, 0);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"APOB entry decode: {ex.Message}");
            }
        }

        /// <summary>
        /// The per channel blocks: the main layout blocks of the type 25 entry and the timing blocks of the GEN
        /// configuration info entry. Read from the main and extended data, which are those entries.
        /// </summary>
        private void TryParseChannelBlocks(BaseDramTimings timingsOverride = null)
        {
            try
            {
                byte[] main = RawData;
                ApobBlockLayout layout = _profile?.MainLayout;
                uint stride = layout != null ? (uint)layout.BlockSize : 0;
                if (main != null && stride > 0 && ApobBytes.IsEntry(main, 0, APOB_MEM, APOB_MEM_SYSTEM_CONFIGURATION_INFO_TYPE) &&
                    ApobBytes.TryGetData(main, 0, out uint data, out uint size) && size > 0 && size % stride == 0)
                {
                    var blocks = new List<ApobData>();
                    for (uint o = data; o + stride <= data + size; o += stride)
                    {
                        if (ApobDataReader.TryRead(main, o, layout, out ApobData block))
                            blocks.Add(block);
                    }

                    // Unused records at the end
                    while (blocks.Count > 0 && Utils.AllZero(blocks[blocks.Count - 1].RawBytes))
                        blocks.RemoveAt(blocks.Count - 1);

                    ChannelData = blocks;
                }

                ApobChannelTimingLayout timingLayout = _profile?.ChannelTimingLayout;
                byte[] extended = RawExtendedData;
                if (timingLayout != null && extended != null &&
                    ApobBytes.IsEntry(extended, 0, timingLayout.SourceGroupId, timingLayout.SourceDataTypeId))
                {
                    ChannelTimings = ApobChannelTimings.Read(extended, 0, timingLayout);
                    ReadChannelExtendedData(extended, timingLayout);
                    MarkActiveChannelTimings(timingsOverride);
                    MapChannelsToDimms();
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"APOB channel blocks: {ex.Message}");
            }
        }

        /// <summary>
        /// Marks the timing blocks of the MEMCLK the memory runs at: the timings ratio x 100, the same MEMCLK the AOD
        /// table is searched with. The ratio comes from <paramref name="timingsOverride"/> (a debug report) or
        /// from the memory config.
        /// </summary>
        private void MarkActiveChannelTimings(BaseDramTimings timingsOverride)
        {
            BaseDramTimings timings = timingsOverride;
            bool lpddr5 = false;

            try
            {
                if (timings == null && _memoryConfig != null)
                {
                    var list = _memoryConfig.Timings;
                    timings = list != null && list.Count > 0 ? list[0].Value : null;
                    lpddr5 = _memoryConfig.Type == MemType.LPDDR5;
                }

                float ratio = timings != null ? timings.Ratio : 0;
                if (ratio <= 0 || ChannelTimings == null)
                    return;

                uint mclk = (uint)Math.Round(ratio * 100);
                ActiveMemClk = mclk;

                // LPDDR5 P-states can share MEMCLK and differ in the WCK ratio (Rembrandt: LPDDR5-6400 and 3200 both at
                // MEMCLK 800), so the data rate decides when the timings know it
                uint activeRate = timings.ClockToDataRate != 2 ? (uint)Math.Round(mclk * (double)timings.ClockToDataRate) : 0;

                for (int i = 0; i < ChannelTimings.Count; i++)
                {
                    uint clk = ChannelTimings[i].MemClk;
                    if (activeRate > 0 && ChannelTimings[i].DataRate > 0 && _profile?.ChannelTimingLayout?.WckOffset >= 0)
                    {
                        ChannelTimings[i].IsActive = NearlyEqual(clk, mclk) && NearlyEqual(ChannelTimings[i].DataRate, activeRate);
                        continue;
                    }

                    // AOD uses 4 x MEMCLK on LPDDR5; the Rembrandt LPDDR5 blocks hold MEMCLK itself
                    ChannelTimings[i].IsActive = NearlyEqual(clk, mclk) ||
                        (lpddr5 && (NearlyEqual(clk, mclk * 2) || NearlyEqual(clk, mclk * 4)));
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"APOB active timings: {ex.Message}");
            }
        }

        /// <summary>
        /// Numbers the channels of the timing blocks after the populated channels of the SMBIOS info, so a system
        /// with only channel B populated shows its block as channel B. Left in block order when the counts differ.
        /// </summary>
        private void MapChannelsToDimms()
        {
            if (DmiInfo == null || ChannelTimings == null || ChannelTimings.Count == 0)
                return;

            var populated = new List<int>();
            foreach (ApobDmiPhysicalDimm dimm in DmiInfo.PhysicalDimms)
            {
                if (dimm.Present && !populated.Contains(dimm.Channel))
                    populated.Add(dimm.Channel);
            }
            populated.Sort();

            int channels = 0;
            foreach (ApobChannelTimings t in ChannelTimings)
                channels = Math.Max(channels, t.Channel + 1);

            if (populated.Count != channels)
                return;

            foreach (ApobChannelTimings t in ChannelTimings)
                t.Channel = populated[t.Channel];
        }

        private static bool NearlyEqual(uint a, uint b)
        {
            return a >= b ? a - b <= 1 : b - a <= 1;
        }

        /// <summary>
        /// Reads the extended copy of the ODT / drive strength record of each timing block at its place in the
        /// channel, and makes the first one <see cref="ExtendedData"/> (in place of the one found by searching for
        /// the RTT values).
        /// </summary>
        private void ReadChannelExtendedData(byte[] extended, ApobChannelTimingLayout timingLayout)
        {
            ApobBlockLayout layout = _profile?.ExtendedLayout;
            if (layout == null || timingLayout.ExtendedRecordOffset < 0 || ChannelTimings == null)
                return;

            ApobChannelTimings channelStart = null;
            for (int i = 0; i < ChannelTimings.Count; i++)
            {
                ApobChannelTimings t = ChannelTimings[i];
                if (t.PState == 0)
                    channelStart = t;
                if (channelStart == null)
                    continue;

                long offset = (long)channelStart.EntryRelativeOffset + timingLayout.ExtendedRecordOffset +
                    (long)t.PState * timingLayout.ExtendedRecordStride;
                if (offset + layout.BlockSize > extended.Length)
                    continue;

                if (ApobBytes.CountNonZero(extended, (uint)offset, (uint)layout.BlockSize) == 0)
                    continue;

                if (ApobDataReader.TryRead(extended, (uint)offset, layout, out ApobData data))
                {
                    t.ExtendedData = data;
                    t.ExtendedDataOffset = (uint)offset;
                }
            }

            if (ChannelTimings.Count > 0 && ChannelTimings[0].ExtendedData != null)
            {
                ExtendedData = ChannelTimings[0].ExtendedData;
                ExtendedLayoutDataRelativeOffset = (int)ChannelTimings[0].ExtendedDataOffset;
                ExtendedLayoutDataOffset = (int)(ExtendedDataOffset + ChannelTimings[0].ExtendedDataOffset);
            }
        }

        private void TryParseCoreMap()
        {
            try
            {
                var entries = ApobCoreMapParser.EnumerateEntries(RawTable, Header.HeaderSize, Header.TableSize, ConfigOffsets);
                CoreMaps = ApobCoreMapParser.Parse(RawTable, entries, (int)_cpuInfo.topology.logicalCores);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"APOB core map: {ex.Message}");
                CoreMaps = new List<ApobCoreMap>();
            }
        }

        // timingsOverride lets CreateFromDebugReport supply the mock timings decoded from the report,
        // as memoryConfig is not available as a mock
        private void TryGetCcdlBlock(Ddr5Timings timingsOverride = null)
        {
            // The channel timing blocks hold the values at fixed offsets, the magic search is the fallback
            if (TryGetCcdlFromChannelTimings())
                return;

            if (_profile?.CcdlLayout == null)
                return;

            byte[] sourceData = _profile.CcdlLayout.SourceBlock == ApobBlockKind.Main ? RawData : RawExtendedData;
            if (sourceData == null)
                return;

            uint targetCcdlWr2 = 0;
            uint targetCcdl = 0;

            try
            {
                var timings = timingsOverride;
                if (timings == null && _memoryConfig != null && (_memoryConfig.Type == MemType.DDR5 || _memoryConfig.Type == MemType.LPDDR5))
                    timings = _memoryConfig.Timings[0].Value as Ddr5Timings;

                if (timings != null)
                {
                    targetCcdlWr2 = timings.IsCcdlWr2RawValid ? timings.CcdlWr2Raw + 7 : 0;
                    targetCcdl = timings.RdBrstGap + 5;
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine(ex);
            }

            if (ApobDataReader.TryReadCcdl(sourceData, _profile.CcdlLayout, out uint ccdl, out uint ccdlrw, out uint ccdlrw2, targetCcdl, targetCcdlWr2))
            {
                CcdlData = new CcdlData(ccdl, ccdlrw, ccdlrw2);
            }
        }

        /// <summary>
        /// CCD_L / CCD_L_WR / CCD_L_WR2 from the channel timing block of the running MEMCLK (the active block),
        /// otherwise from the fastest block (memory P-state 0).
        /// </summary>
        private bool TryGetCcdlFromChannelTimings()
        {
            if (ChannelTimings == null)
                return false;

            ApobChannelTimings best = null;
            for (int i = 0; i < ChannelTimings.Count; i++)
            {
                ApobChannelTimings t = ChannelTimings[i];
                uint? ccdl = t.Tccdl, ccdlWr = t.Tccdlwr, ccdlWr2 = t.Tccdlwr2;
                if (!ccdl.HasValue || !ccdlWr.HasValue || !ccdlWr2.HasValue ||
                    !ApobCcdlValidation.IsValid(ccdl.Value, ccdlWr.Value, ccdlWr2.Value))
                    continue;

                // The active block, otherwise the fastest one (P-state 0)
                if (best == null || (t.IsActive && !best.IsActive) || (t.IsActive == best.IsActive && t.MemClk > best.MemClk))
                    best = t;
            }

            if (best == null)
                return false;

            CcdlData = new CcdlData(best.Tccdl.Value, best.Tccdlwr.Value, best.Tccdlwr2.Value);
            return true;
        }

        private void ParseDataBlocks()
        {
            if (DataSize == 0 || _profile?.MainLayout == null)
                return;

            long start = (long)DataOffset + DATA_PARSE_LEAD_BYTES;
            long end = (long)DataOffset + DataSize;

            if (start >= end || end > RawTable.Length)
                return;

            for (long i = start; i < end; i++)
            {
                if (RawTable[i] == 0)
                    continue;

                if (i + 6 >= end)
                    return;

                if (!ApobDataReader.TryRead(RawTable, (uint)i, _profile.MainLayout, out ApobData data))
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
        public static Apob CreateFromDebugReport(string debugReportText, BaseDramTimings timings = null)
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

            var profile = ApobProfiles.Resolve(mockCpuInfo);
            Apob apob = new Apob(mockCpuInfo, profile);

            byte[] rawHeaderBytes = ParseRawSection(text, "-- Raw Header");
            byte[] rawDataBytes = ParseRawSection(text, "-- Raw Data");
            byte[] rawExtendedDataBytes = ParseRawSection(text, "-- Raw Extended Data");

            // DDR4 tables have no main data, only the extended data (the GEN configuration info entry)
            bool hasData = rawDataBytes != null && rawDataBytes.Length > 0;
            bool hasExtendedData = rawExtendedDataBytes != null && rawExtendedDataBytes.Length > 0;
            if (!hasData && !hasExtendedData)
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
            uint dataSize = hasData ? Math.Max(ParseHexValue(text, "-- Main Data Size:") ?? 0, (uint)rawDataBytes.Length) : 0;
            long defaultExtendedDataOffset = (long)dataOffset + dataSize;
            uint? declaredExtendedDataOffset = ParseHexValue(text, "-- Ext. Data Offset:");
            if (!declaredExtendedDataOffset.HasValue && defaultExtendedDataOffset > uint.MaxValue)
            {
                apob.ErrorReason = "The APOB data offsets exceed the supported range.";
                return apob;
            }
            uint extendedDataOffset = declaredExtendedDataOffset ?? (uint)defaultExtendedDataOffset;
            uint extendedDataSize = Math.Max(
                ParseHexValue(text, "-- Ext. Data Size:") ?? 0,
                (uint)(rawExtendedDataBytes?.Length ?? 0));

            // Sized from the actual extracted byte counts (not just the declared "Length:"/size
            // values) so a hand-edited or truncated report can't overrun the buffer below.
            long tableLength = Math.Max(
                header.HeaderSize,
                Math.Max((long)dataOffset + dataSize, (long)extendedDataOffset + extendedDataSize));

            if (tableLength > int.MaxValue)
            {
                apob.ErrorReason = "The APOB data offsets and sizes exceed the supported buffer length.";
                return apob;
            }

            byte[] rawTable = new byte[tableLength];
            if (rawHeaderBytes != null)
                Buffer.BlockCopy(rawHeaderBytes, 0, rawTable, 0, Math.Min(rawHeaderBytes.Length, rawTable.Length));

            if (hasData)
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

            for (int i = 0; ; i++)
            {
                byte[] rawCoreMap = ParseRawSection(text, "-- Raw Core Map [" + i + "]");
                if (rawCoreMap == null)
                    break;

                ApobCoreMap coreMap = ApobCoreMapParser.Decode(rawCoreMap, 0);
                if (coreMap != null)
                    apob.CoreMaps.Add(coreMap);
            }

            apob.DecodeEntries(delegate (uint groupId, uint dataTypeId)
            {
                // The trailing space keeps "1/2" from matching "1/28"
                return ParseRawSection(text, "-- " + RawEntryTitle(groupId, dataTypeId) + " ");
            });

            if (profile != null)
            {
                apob.ParseDataBlocks();
                apob.TryParseChannelBlocks(timings);
                // Only available for DDR5/LPDDR5 for now
                apob.TryGetCcdlBlock(timings as Ddr5Timings);
            }

            apob._lpddr5 = string.Equals(ParseLabelValue(text, "MemType:"), "LPDDR5", StringComparison.OrdinalIgnoreCase);
            apob.ApplyMemoryType();
            return apob;
        }

        internal static string NormalizeLineEndings(string text)
        {
            return text.Replace("\r\n", "\n").Replace("\r", "\n");
        }

        internal static Family ParseFamily(string text)
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

        internal static CodeName ParseCodeName(string text)
        {
            string raw = ParseLabelValue(text, "CodeName:");
            if (raw != null && Utils.TryParseEnum(raw, out CodeName codeName))
                return codeName;

            // CodeName.DEBUG exists specifically for mocked/synthetic scenarios like this one.
            return CodeName.DEBUG;
        }

        internal static PackageType ParsePackageType(string text)
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

        internal static SMU.SmuType ParseSmuType(string text)
        {
            string raw = ParseLabelValue(text, "SmuType:");
            if (raw != null && Utils.TryParseEnum(raw, out SMU.SmuType smuType))
                return smuType;

            return SMU.SmuType.TYPE_UNSUPPORTED;
        }

        internal static bool TryParseNumeric(string raw, out uint value)
        {
            raw = raw.Trim();
            if (raw.StartsWith("0x", StringComparison.OrdinalIgnoreCase))
                return uint.TryParse(raw.Substring(2), NumberStyles.HexNumber, CultureInfo.InvariantCulture, out value);

            return uint.TryParse(raw, NumberStyles.Integer, CultureInfo.InvariantCulture, out value);
        }

        // Matches a "Label:value" line anchored at the start of a line (as produced by
        // DebugDialog's fixed-width report formatting) and returns the first whitespace-delimited
        // token after the label, or null if the label isn't present.
        internal static string ParseLabelValue(string text, string label)
        {
            Match match = Regex.Match(
                text,
                "^" + Regex.Escape(label) + @"[ \t]*(\S+)",
                RegexOptions.IgnoreCase | RegexOptions.Multiline);

            return match.Success ? match.Groups[1].Value : null;
        }

        // Same as ParseLabelValue, but for "Label: 0xHEXVALUE" lines such as
        // "-- Main Data Offset: 0x00001DB4".
        internal static uint? ParseHexValue(string text, string label)
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
        // GetReport()/AppendRawBlock: a "Length: N" line followed by N bytes, formatted as
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
                    if (!int.TryParse(lengthMatch.Groups[1].Value, NumberStyles.Integer, CultureInfo.InvariantCulture, out expectedLength))
                        return null;
                    break;
                }
            }

            // Older reports have no "Length:" line, the bytes follow the section header
            if (lengthLine < 0)
            {
                lengthLine = headerLine;
                expectedLength = int.MaxValue;
            }
            else if (expectedLength <= 0)
            {
                return null;
            }

            var bytes = new List<byte>(Math.Min(expectedLength == int.MaxValue ? 4096 : expectedLength, text.Length / 2));
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
            ReportBuilder report = new ReportBuilder();

            report.AppendHeading("APOB");

            try
            {
                if (!IsAvailable)
                {
                    report.AppendLine("<APOB table not available>");
                    if (!string.IsNullOrEmpty(ErrorReason))
                        report.AppendLine(ErrorReason);

                    report.AppendLine();
                    return report.ToString();
                }

                report.AppendLine(string.Format("-- Address: 0x{0:X8}", Address));
                report.AppendLine(string.Format("-- Main Data Offset: 0x{0:X8}", DataOffset));
                report.AppendLine(string.Format("-- Main Data Size: 0x{0:X8} ({0})", DataSize));
                report.AppendLine(string.Format("-- Main Layout Offset: 0x{0:X8}", MainLayoutDataOffset));
                report.AppendLine(string.Format("-- Main Layout Rel. Offset: 0x{0:X8} ({0})", MainLayoutDataRelativeOffset));
                report.AppendLine(string.Format("-- Ext. Data Offset: 0x{0:X8}", ExtendedDataOffset));
                report.AppendLine(string.Format("-- Ext. Data Size: 0x{0:X8} ({0})", ExtendedDataSize));
                report.AppendLine(string.Format("-- Ext. Layout Offset: 0x{0:X8}", ExtendedLayoutDataOffset));
                report.AppendLine(string.Format("-- Ext. Layout Rel. Offset: 0x{0:X8} ({0})", ExtendedLayoutDataRelativeOffset));
                report.AppendLine();
                report.AppendSection("Metadata");
                report.AppendValue("Config Offsets Count", ConfigOffsets != null ? ConfigOffsets.Count : 0, 28);
                report.AppendValue("Main Block Parsed", Data != null, 28);
                report.AppendValue("Extended Block Parsed", ExtendedData != null, 28);
                report.AppendValue("Raw Table Bytes", RawTable != null ? RawTable.Length : 0, 28);

                if (ConfigOffsets != null)
                {
                    for (int i = 0; i < ConfigOffsets.Count; i++)
                        report.AppendHexValue("Config Offset[" + i + "]", ConfigOffsets[i], 8, 28);
                }

                report.AppendLine();
                report.AppendSection("Header");
                report.AppendValue("Signature", Header.Signature);
                report.AppendValue("Version", Header.Version);
                report.AppendValue("TableSize", Header.TableSize);
                report.AppendValue("HeaderSize", Header.HeaderSize);

                report.AppendLine();
                report.AppendSection("Data");
                report.Append(Data != null ? Data.GetReport() : "<APOB table data not available>" + Environment.NewLine);

                report.AppendLine();
                report.AppendSection("Extended Data");
                report.Append(ExtendedData != null ? ExtendedData.GetReport() : "<APOB extended data not available>" + Environment.NewLine);

                report.AppendLine();
                report.AppendSection("CCDL Data");
                report.AppendValue("Tccdl", CcdlData.Tccdl);
                report.AppendValue("Tccdlwr", CcdlData.Tccdlwr);
                report.AppendValue("Tccdlwr2", CcdlData.Tccdlwr2);

                report.AppendLine();
                report.AppendSection("Core Map");
                AppendCoreMaps(report);

                report.AppendLine();
                report.AppendSection("Entries");
                AppendEntries(report);

                report.AppendLine();
                report.AppendSection("APCB Boot Info");
                AppendBootInfo(report);

                report.AppendLine();
                report.AppendSection("SMBIOS Memory Info");
                AppendDmiInfo(report);

                report.AppendLine();
                report.AppendSection("DIMM SPD");
                AppendDimmSpd(report);

                report.AppendLine();
                report.AppendSection("Memory Map");
                AppendMemoryMap(report);

                report.AppendLine();
                report.AppendSection("Memory Profile (tentative)");
                AppendMemoryProfile(report);

                report.AppendLine();
                report.AppendSection("Channel Data");
                AppendChannelData(report);

                if (MemGeneralConfig != null)
                {
                    report.AppendLine();
                    report.AppendSection("Memory Configuration (DDR4)");
                    AppendMemGeneralConfig(report);
                }

                report.AppendLine();
                report.AppendSection("Channel Timings");
                AppendChannelTimings(report);

                if (HasLpddr5ModeRegisters())
                {
                    report.AppendLine();
                    report.AppendSection("LPDDR5 Mode Registers");
                    AppendLpddr5ModeRegisters(report);
                }

                if (HasDdr4ModeRegisters())
                {
                    report.AppendLine();
                    report.AppendSection("DDR4 Mode Registers");
                    AppendDdr4ModeRegisters(report);
                }

                report.AppendLine();
                report.AppendSection("Event Log");
                AppendEventLog(report);

                report.AppendLine();
                report.AppendLine("APOB: Raw");
                report.AppendLine();

                // Each of these properties slices a fresh copy out of the raw table on every
                // access, so each block is taken once.
                AppendRawBlock(report, "Raw Header", RawHeader, "<APOB raw header not available>");
                report.AppendLine();
                AppendRawBlock(report, "Raw Data", RawData, "<APOB raw data not available>");
                report.AppendLine();
                AppendRawBlock(report, "Raw Extended Data", RawExtendedData, "<APOB raw extended data not available>");

                if (CoreMaps != null)
                {
                    for (int i = 0; i < CoreMaps.Count; i++)
                    {
                        report.AppendLine();
                        AppendRawBlock(report, "Raw Core Map [" + i + "]", CoreMaps[i].RawEntry, "<APOB raw core map not available>");
                    }
                }

                for (int i = 0; i < DecodedEntryTypes.Length; i++)
                {
                    uint groupId = DecodedEntryTypes[i][0];
                    uint dataTypeId = DecodedEntryTypes[i][1];
                    ApobEntry entry = FindEntry(groupId, dataTypeId);
                    if (entry == null)
                        continue;

                    // An empty event log is 1 KB of zeros
                    if (groupId == APOB_GEN && dataTypeId == APOB_GEN_EVENT_LOG_TYPE && entry.CountNonZeroDataBytes(RawTable) == 0)
                        continue;

                    byte[] raw = entry.GetRawEntry(RawTable);
                    if (raw != null && groupId == APOB_MEM && dataTypeId == APOB_APCB_BOOT_INFO_TYPE && !CoreOptions.Current.PrintSerialNumbers)
                        ApobBootInfo.MaskSerialNumbers(raw);
                    if (raw != null && groupId == APOB_MEM && dataTypeId == APOB_MEM_DIMM_SPD_DATA_TYPE && !CoreOptions.Current.PrintSerialNumbers)
                        ApobDimmSpdParser.MaskSerialNumbers(raw);

                    report.AppendLine();
                    AppendRawBlock(report, RawEntryTitle(groupId, dataTypeId), raw, "<APOB raw entry not available>");
                }

                report.AppendLine();
            }
            catch (Exception ex)
            {
                report.AppendFailure(ex);
                report.AppendLine();
            }

            return report.ToString();
        }

        private void AppendCoreMaps(ReportBuilder report)
        {
            if (CoreMaps == null || CoreMaps.Count == 0)
            {
                report.AppendLine("<APOB core map not available>");
                return;
            }

            for (int m = 0; m < CoreMaps.Count; m++)
            {
                ApobCoreMap map = CoreMaps[m];
                report.AppendLine(map.ToString());
                report.AppendHexValue("Entry Offset", map.EntryOffset, 8, 28);
                report.AppendHexValue("Physical CCD Mask", map.PhysicalCcdMask, 4, 28);
                report.AppendValue("Structure Slots", map.SlotLayout, 28);

                for (int i = 0; i < map.Cores.Count; i++)
                {
                    ApobCoreMapCore core = map.Cores[i];
                    report.AppendLine(string.Format(CultureInfo.InvariantCulture,
                        "Core {0,2}: logical CCD {1} CCX {2} Core {3} -> physical CCD {4} CCX {5} Core {6}, {7} threads",
                        core.LogicalIndex, core.LogicalCcd, core.LogicalCcx, core.LogicalCore,
                        core.PhysicalCcd, core.PhysicalCcx, core.PhysicalCore, core.EnabledThreads));
                }
            }
        }

        private static string RawEntryTitle(uint groupId, uint dataTypeId)
        {
            return string.Format(CultureInfo.InvariantCulture, "Raw Entry {0}/{1}", groupId, dataTypeId);
        }

        private void AppendEntries(ReportBuilder report)
        {
            if (Entries == null || Entries.Count == 0)
            {
                report.AppendLine("<APOB entry list not available>");
                return;
            }

            for (int i = 0; i < Entries.Count; i++)
            {
                ApobEntry e = Entries[i];
                report.AppendLine(string.Format(CultureInfo.InvariantCulture,
                    "[{0,2}] 0x{1:X8} {2,6} {3,6} {4,5}  {5,-6} {6}{7}",
                    i, e.Offset, e.GroupId + "/" + e.DataTypeId, "I" + e.InstanceId, e.Size,
                    e.GroupName, e.Name, e.IsEncrypted ? " (encrypted)" : ""));
            }
        }

        private void AppendBootInfo(ReportBuilder report)
        {
            ApobBootInfo info = BootInfo;
            if (info == null)
            {
                report.AppendLine("<APOB boot info not available>");
                return;
            }

            DateTime? date = info.LastTrainingDate;
            report.AppendHexValue("Active APCB Instance", info.ApcbActiveInstance, 8, 28);
            report.AppendValue("Last Training Date", date.HasValue ? date.Value.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture) : "N/A", 28);
            report.AppendHexValue("Last Training Raw", info.LastTrainingTimeRaw, 8, 28);
            report.AppendValue("DIMM Config Updated", info.DimmConfigurationUpdated, 28);
            report.AppendValue("APCB Recovery Flag", info.ApcbRecoveryFlag, 28);
            report.AppendValue("Action On BIST Failure", info.ActionOnBistFailure, 28);
            report.AppendValue("Workload Profile", info.WorkloadProfile, 28);
            report.AppendLine(string.Format(CultureInfo.InvariantCulture, "Unknown:                    0x{0:X8} 0x{1:X8} 0x{2:X8}",
                info.Unknown0, info.Unknown1, info.Unknown2));

            for (int i = 0; i < info.Dimms.Count; i++)
            {
                ApobBootDimm dimm = info.Dimms[i];
                if (!dimm.Present)
                {
                    report.AppendLine(string.Format(CultureInfo.InvariantCulture, "Slot {0} ({1}): empty", dimm.Slot, dimm.SlotName));
                    continue;
                }

                report.AppendLine(string.Format(CultureInfo.InvariantCulture,
                    "Slot {0} ({1}): module {2} (0x{3:X4}), DRAM {4} (0x{5:X4}), serial {6}",
                    dimm.Slot, dimm.SlotName, dimm.ModuleManufacturer, dimm.ModuleManufacturerId,
                    dimm.DramManufacturer, dimm.DramManufacturerId,
                    CoreOptions.Current.PrintSerialNumbers ? dimm.SerialNumber.ToString("X8", CultureInfo.InvariantCulture) : "****"));
            }
        }

        // One line per slot with what its SPD decodes to; the SPD itself is in the raw 1/17 entry
        private void AppendDimmSpd(ReportBuilder report)
        {
            if (DimmSpd == null || DimmSpd.Count == 0)
            {
                report.AppendLine("<APOB DIMM SPD data not available>");
                return;
            }

            foreach (ApobDimmSpd slot in DimmSpd)
            {
                string decoded;
                try
                {
                    if (slot.DeviceType == 0x0C)
                    {
                        var ddr4 = DRAM.DDR4.Spd.Ddr4SpdDecoder.Decode(slot.Data, false);
                        decoded = string.Format("{0}, {1} MB, {2}", ddr4.ModulePartNumber, ddr4.TotalCapacityMB, ddr4.TimingString);
                    }
                    else if (DRAM.DDR5.Spd.Ddr5SpdDecoder.IsSupportedDeviceType(slot.DeviceType))
                    {
                        var ddr5 = DRAM.DDR5.Spd.Ddr5SpdDecoder.Decode(slot.Data, false);
                        decoded = string.Format("{0}, {1} MB, {2}", ddr5.ModulePartNumber, ddr5.TotalCapacityMB, ddr5.TimingString);
                    }
                    else
                    {
                        decoded = "not decoded";
                    }
                }
                catch (Exception ex)
                {
                    decoded = "<FAILED> " + ex.Message;
                }

                report.AppendLine(string.Format(CultureInfo.InvariantCulture, "{0}: {1}", slot, decoded));
            }
        }

        private void AppendDmiInfo(ReportBuilder report)
        {
            ApobDmiInfo info = DmiInfo;
            if (info == null)
            {
                report.AppendLine("<APOB SMBIOS memory info not available>");
                return;
            }

            report.AppendValue("Memory Type", info.MemoryTypeName, 28);
            report.AppendValue("ECC Capable", info.EccCapable, 28);
            report.AppendValue("Max Physical DIMMs", info.MaxPhysicalDimms, 28);
            report.AppendValue("Max Logical DIMMs", info.MaxLogicalDimms, 28);

            for (int i = 0; i < info.PhysicalDimms.Count; i++)
            {
                ApobDmiPhysicalDimm d = info.PhysicalDimms[i];
                if (!d.Present)
                    continue;

                report.AppendLine(string.Format(CultureInfo.InvariantCulture,
                    "Physical {0} (socket {1}): handle {2}, speed {3} MHz, voltage {4} mV, SPD address 0x{5:X2}",
                    d.SlotName, d.Socket, d.Handle, d.ConfiguredSpeed, d.ConfiguredVoltage, d.SpdAddress));
            }

            for (int i = 0; i < info.LogicalDimms.Count; i++)
            {
                ApobDmiLogicalDimm d = info.LogicalDimms[i];
                if (!d.Present)
                    continue;

                report.AppendLine(string.Format(CultureInfo.InvariantCulture,
                    "Logical {0} (socket {1}): handle {2}, 0x{3:X8}-0x{4:X8} KB ({5} MB){6}",
                    d.SlotName, d.Socket, d.Handle, d.StartingAddressKb, d.EndingAddressKb, d.SizeMB,
                    d.Interleaved ? ", interleaved" : ""));
            }
        }

        private void AppendMemoryMap(ReportBuilder report)
        {
            ApobMemoryMap map = MemoryMap;
            if (map == null)
            {
                report.AppendLine("<APOB memory map not available>");
                return;
            }

            report.AppendLine(string.Format(CultureInfo.InvariantCulture, "Top Of System Memory:       0x{0:X12} ({1} MB)",
                map.TopOfSystemMemory, map.TopOfSystemMemory >> 20));
            report.AppendValue("Holes", map.NumberOfHoles, 28);

            for (int i = 0; i < map.Holes.Count; i++)
            {
                ApobMemoryHole hole = map.Holes[i];
                report.AppendLine(string.Format(CultureInfo.InvariantCulture,
                    "0x{0:X12}-0x{1:X12} {2,10} KB  {3}",
                    hole.Base, hole.End, hole.Size >> 10, hole.TypeName));
            }

            report.AppendLine("(type names from openSIL MEMORY_HOLE_TYPES, client parts may differ)");
        }

        private void AppendMemoryProfile(ReportBuilder report)
        {
            ApobMemoryProfileInfo info = MemoryProfileInfo;
            if (info == null)
            {
                report.AppendLine("<APOB memory profile not available>");
                return;
            }

            report.AppendLine(string.Format(CultureInfo.InvariantCulture, "Flags:                      {0}, {1}, {2}",
                info.Flags0, info.Flags1, info.Flags2));
            report.AppendValue("MemClk", info.MemClk + " MHz", 28);
            report.AppendValue("VDD", info.VddMv + " mV", 28);
            report.AppendValue("CAS", info.Cas, 28);
            report.AppendValue("EXPO Block", info.HasExpoBlock, 28);

            if (info.HasExpoBlock)
            {
                report.AppendLine(string.Format(CultureInfo.InvariantCulture,
                    "EXPO:                       revision {0}.{1}, profiles 0x{2:X2}, profile 1 tCK {3} ps, tAA {4} ps",
                    info.ExpoRevision >> 4, info.ExpoRevision & 0xF, info.ExpoProfileBits, info.ExpoProfile1TckPs, info.ExpoProfile1TaaPs));
            }
        }

        private void AppendChannelData(ReportBuilder report)
        {
            if (ChannelData == null || ChannelData.Count == 0)
            {
                report.AppendLine("<APOB channel data not available>");
                return;
            }

            byte[] first = ChannelData[0].RawBytes;
            for (int i = 0; i < ChannelData.Count; i++)
            {
                byte[] raw = ChannelData[i].RawBytes;
                if (Utils.AllZero(raw))
                {
                    report.AppendLine("Channel " + i + ": empty");
                }
                else if (i > 0 && SameBytes(raw, first))
                {
                    report.AppendLine("Channel " + i + ": same as channel 0");
                }
                else if (i == 0 && Data != null && SameBytes(Data.RawBytes, raw))
                {
                    report.AppendLine("Channel 0: same as Data");
                }
                else
                {
                    report.AppendLine("Channel " + i + ":");
                    report.Append(ChannelData[i].GetReport());
                }
            }
        }

        private static bool SameBytes(byte[] a, byte[] b, int start = 0)
        {
            if (a == null || b == null || a.Length != b.Length)
                return false;

            for (int i = start; i < a.Length; i++)
            {
                if (a[i] != b[i])
                    return false;
            }

            return true;
        }

        private void AppendMemGeneralConfig(ReportBuilder report)
        {
            ApobMemGeneralConfig config = MemGeneralConfig;
            report.AppendValue("MemClkFreq", config.MemClkFreq + " MHz", 28);
            report.AppendValue("DdrMaxRate", config.DdrMaxRate, 28);
            report.AppendValue("Channel Interleave", config.ChannelInterleave, 28);
            report.AppendLine(string.Format(CultureInfo.InvariantCulture, "Interleave:                 mode 0x{0:X}, capability 0x{1:X}, size 0x{2:X}",
                config.InterleaveCurrentMode, config.InterleaveCapability, config.InterleaveSize));

            foreach (ApobDdr4ChannelConfig c in config.Channels)
            {
                if (!c.IsPopulated)
                    continue;

                report.AppendLine(string.Format(CultureInfo.InvariantCulture,
                    "Channel {0}: ProcOdt {1}, RttNom {2}, RttWr {3}, RttPark {4}",
                    (char)('A' + c.Channel), c.ProcOdt, c.RttNom, c.RttWr, c.RttPark));
                report.AppendLine(string.Format(CultureInfo.InvariantCulture,
                    "           Setup AddrCmd {0}, CsOdt {1}, Cke {2}; DrvStren Clk {3}, AddrCmd {4}, CsOdt {5}, Cke {6}",
                    c.AddrCmdSetup, c.CsOdtSetup, c.CkeSetup,
                    c.ClkDrvStren, c.AddrCmdDrvStren, c.CsOdtCmdDrvStren, c.CkeDrvStren));
            }

            foreach (ApobMemSetting setting in config.Settings)
            {
                if (setting.StatusCode != 0)
                    report.AppendLine(string.Format(CultureInfo.InvariantCulture, "{0,-28}{1} (0x{2:X4})", setting.Name + ":", setting.Value, setting.StatusCode));
            }
        }

        private void AppendChannelTimings(ReportBuilder report)
        {
            if (ChannelTimings == null || ChannelTimings.Count == 0)
            {
                report.AppendLine("<APOB channel timings not available>");
                return;
            }

            if (ActiveMemClk > 0)
                report.AppendValue("Active MEMCLK", ActiveMemClk + " MHz (timings ratio)", 28);

            var sb = new System.Text.StringBuilder();
            sb.Append(string.Format(CultureInfo.InvariantCulture, "{0,-12}", "Timing"));
            for (int c = 0; c < ChannelTimings.Count; c++)
                sb.Append(string.Format(CultureInfo.InvariantCulture, "{0,10}", "Blk" + ChannelTimings[c].Index));
            report.AppendLine(sb.ToString());

            AppendTimingRow(report, "Offset", delegate (ApobChannelTimings t) { return "0x" + t.EntryRelativeOffset.ToString("X", CultureInfo.InvariantCulture); });
            AppendTimingRow(report, "Channel", delegate (ApobChannelTimings t) { return t.Channel.ToString(CultureInfo.InvariantCulture); });
            AppendTimingRow(report, "P-state", delegate (ApobChannelTimings t) { return t.PState.ToString(CultureInfo.InvariantCulture); });
            if (ActiveMemClk > 0)
                AppendTimingRow(report, "Active", delegate (ApobChannelTimings t) { return t.IsActive ? "yes" : "-"; });
            bool anyExtended = false;
            foreach (ApobChannelTimings t in ChannelTimings)
                anyExtended |= t.ExtendedData != null;
            if (anyExtended)
                AppendTimingRow(report, "Ext. Offset", delegate (ApobChannelTimings t) { return t.ExtendedData != null ? "0x" + t.ExtendedDataOffset.ToString("X", CultureInfo.InvariantCulture) : "-"; });
            AppendTimingRow(report, "DataRate", delegate (ApobChannelTimings t) { return t.DataRate.ToString(CultureInfo.InvariantCulture); });
            AppendTimingRow(report, "MemClk", delegate (ApobChannelTimings t) { return t.MemClk.ToString(CultureInfo.InvariantCulture); });

            List<ApobTimingValue> first = ChannelTimings[0].Values;
            bool anyTentative = false;
            for (int f = 0; f < first.Count; f++)
            {
                int index = f;
                ApobTimingField field = first[f].Field;
                anyTentative |= field.Tentative;
                AppendTimingRow(report, field.Name + (field.Tentative ? " (?)" : ""), delegate (ApobChannelTimings t)
                {
                    return index < t.Values.Count ? t.Values[index].Value.ToString(CultureInfo.InvariantCulture) : "";
                });
            }

            if (anyTentative)
                report.AppendLine("(?) = placement not confirmed");
            report.AppendLine("One block per channel, per memory P-state on APUs");

            // The extended record of each block, Blk0 is Extended Data
            for (int c = 1; c < ChannelTimings.Count; c++)
            {
                ApobData ext = ChannelTimings[c].ExtendedData;
                if (ext == null)
                    continue;

                byte[] raw = ext.RawBytes;
                int same = -1;
                for (int k = 0; k < c && same < 0; k++)
                {
                    // Byte 0 is not a field (on Phoenix it is the last byte of the record before)
                    if (ChannelTimings[k].ExtendedData != null && SameBytes(ChannelTimings[k].ExtendedData.RawBytes, raw, 1))
                        same = k;
                }

                report.AppendLine();
                if (same >= 0)
                {
                    report.AppendLine(string.Format(CultureInfo.InvariantCulture, "Blk{0} extended: same as Blk{1}", c, same));
                }
                else
                {
                    report.AppendLine(string.Format(CultureInfo.InvariantCulture, "Blk{0} extended:", c));
                    report.Append(ext.GetReport());
                }
            }
        }

        private bool HasLpddr5ModeRegisters()
        {
            if (ChannelTimings == null)
                return false;
            for (int i = 0; i < ChannelTimings.Count; i++)
            {
                if (ChannelTimings[i].Lpddr5ModeRegisters != null)
                    return true;
            }
            return false;
        }

        private bool HasDdr4ModeRegisters()
        {
            if (ChannelTimings == null)
                return false;
            for (int i = 0; i < ChannelTimings.Count; i++)
            {
                if (ChannelTimings[i].Ddr4ModeRegisters != null)
                    return true;
            }
            return false;
        }

        // Each block's decoded DDR4 mode registers, a block equal to an earlier one only named
        private void AppendDdr4ModeRegisters(ReportBuilder report)
        {
            for (int c = 0; c < ChannelTimings.Count; c++)
            {
                ApobChannelTimings t = ChannelTimings[c];
                ApobDdr4ModeRegisters registers = t.Ddr4ModeRegisters;
                string title = string.Format(CultureInfo.InvariantCulture, "Blk{0} (channel {1}, P{2}, {3} MT/s{4})",
                    t.Index, t.Channel, t.PState, t.DataRate, t.IsActive ? ", active" : "");
                if (registers == null)
                {
                    report.AppendLine(title + ": not found");
                    continue;
                }

                int same = -1;
                for (int k = 0; k < c && same < 0; k++)
                {
                    ApobDdr4ModeRegisters other = ChannelTimings[k].Ddr4ModeRegisters;
                    if (other != null && other.ToHexString() == registers.ToHexString())
                        same = k;
                }

                if (same >= 0)
                {
                    report.AppendLine(string.Format(CultureInfo.InvariantCulture, "{0}: same as Blk{1}", title, ChannelTimings[same].Index));
                    continue;
                }

                report.AppendLine(title + ":");
                foreach (KeyValuePair<string, string> setting in registers.GetSettings())
                    report.AppendValue("  " + setting.Key, setting.Value, 22);
                report.AppendValue("  MR0-6", registers.ToHexString(), 22);
            }
        }

        // Each block's decoded mode registers, a block equal to an earlier one only named
        private void AppendLpddr5ModeRegisters(ReportBuilder report)
        {
            bool first = true;
            for (int c = 0; c < ChannelTimings.Count; c++)
            {
                ApobChannelTimings t = ChannelTimings[c];
                List<ApobLpddr5ModeRegisters> sets = t.Lpddr5ModeRegisterSets;
                if (sets.Count == 0)
                    continue;

                if (!first)
                    report.AppendLine();
                first = false;

                string title = string.Format(CultureInfo.InvariantCulture, "Blk{0} (channel {1}, P{2}, {3} MT/s{4})",
                    t.Index, t.Channel, t.PState, t.DataRate, t.IsActive ? ", active" : "");

                int same = -1;
                for (int k = 0; k < c && same < 0; k++)
                {
                    if (SameModeRegisterSets(ChannelTimings[k].Lpddr5ModeRegisterSets, sets))
                        same = k;
                }

                if (same >= 0)
                {
                    report.AppendLine(string.Format(CultureInfo.InvariantCulture, "{0}: same as Blk{1}", title, ChannelTimings[same].Index));
                    continue;
                }

                report.AppendLine(title + ":");
                foreach (KeyValuePair<string, string> setting in sets[0].GetSettings())
                    report.AppendValue("  " + setting.Key, setting.Value, 22);
                for (int s = 0; s < sets.Count; s++)
                {
                    bool copy = s > 0 && sets[s].SameAs(sets[0]);
                    report.AppendValue(string.Format(CultureInfo.InvariantCulture, "  MR0-41 copy {0}", s),
                        copy ? "same as copy 0" : sets[s].ToHexString(), 22);
                }
            }
        }

        private static bool SameModeRegisterSets(List<ApobLpddr5ModeRegisters> a, List<ApobLpddr5ModeRegisters> b)
        {
            if (a.Count != b.Count)
                return false;
            for (int i = 0; i < a.Count; i++)
            {
                if (!a[i].SameAs(b[i]))
                    return false;
            }
            return true;
        }

        private delegate string TimingCell(ApobChannelTimings timings);

        private void AppendTimingRow(ReportBuilder report, string name, TimingCell cell)
        {
            var sb = new System.Text.StringBuilder();
            sb.Append(string.Format(CultureInfo.InvariantCulture, "{0,-12}", name));
            for (int c = 0; c < ChannelTimings.Count; c++)
                sb.Append(string.Format(CultureInfo.InvariantCulture, "{0,10}", cell(ChannelTimings[c])));
            report.AppendLine(sb.ToString());
        }

        private void AppendEventLog(ReportBuilder report)
        {
            report.AppendValue("PMU Training Failure", StatusText(TrainingFailureDataBytes), 28);
            report.AppendValue("MBIST Result", StatusText(MbistResultDataBytes), 28);

            if (EventLog == null)
            {
                report.AppendLine("<APOB event log not available>");
                return;
            }

            report.AppendValue("Events", EventLog.Count, 28);
            for (int i = 0; i < EventLog.Events.Count; i++)
                report.AppendLine(string.Format(CultureInfo.InvariantCulture, "[{0,2}] {1}", i, EventLog.Events[i]));
        }

        private static string StatusText(int? nonZeroBytes)
        {
            if (!nonZeroBytes.HasValue)
                return "N/A";

            return nonZeroBytes.Value == 0 ? "no data (all zero)" : nonZeroBytes.Value + " non-zero bytes";
        }

        private static void AppendRawBlock(ReportBuilder report, string title, byte[] data, string unavailableText)
        {
            report.AppendSection(title);
            report.AppendLine(string.Format(CultureInfo.InvariantCulture, "Length: {0}", data != null ? data.Length : 0));

            try
            {
                if (!report.AppendHexDump(data))
                    report.AppendLine(unavailableText);
            }
            catch (Exception ex)
            {
                report.AppendFailure(ex);
            }
        }
    }
}