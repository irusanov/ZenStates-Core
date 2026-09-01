using System;
using System.Collections.Generic;
using System.Diagnostics;
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
        public uint ExtendedDataOffset { get; private set; }
        public uint ExtendedDataSize { get; private set; }

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
            get { return Data.RawBytes; }
        }

        public byte[] RawExtendedData
        {
            get { return ExtendedData.RawBytes; }
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
            header = default(ApobHeader);
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

                    ApobData extendedData;
                    if (ApobDataReader.TryRead(RawExtendedData, 0, _profile.ExtendedLayout, out extendedData))
                    {
                        ExtendedData = extendedData;
                    }
                    else
                    {
                        Debug.WriteLine("APOB extended block was found, but the configured layout did not fit the block size.");
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

            uint ccdl;
            uint ccdlrw;
            uint ccdlrw2;

            if (ApobDataReader.TryReadCcdl(sourceData, _profile.CcdlLayout, out ccdl, out ccdlrw, out ccdlrw2))
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

                ApobData data;
                if (!ApobDataReader.TryRead(RawTable, i, _profile.MainLayout, out data))
                    return;

                Data = data;

                byte[] rttBlock = new byte[RTT_BLOCK_SIZE];
                Buffer.BlockCopy(RawTable, (int)i + 2, rttBlock, 0, (int)RTT_BLOCK_SIZE);

                if (Utils.AllZero(rttBlock))
                    return;

                if (RawExtendedData == null)
                    return;

                int extendedMatch = Utils.FindSequence(RawExtendedData, 0, rttBlock);
                if (extendedMatch < 2)
                    return;

                ApobData extendedData;
                if (ApobDataReader.TryRead(RawExtendedData, (uint)(extendedMatch - 2), _profile.ExtendedLayout, out extendedData))
                {
                    ExtendedData = extendedData;
                }

                return;
            }
        }
    }
}
