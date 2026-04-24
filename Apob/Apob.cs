using System;
using System.Collections.Generic;
using System.Diagnostics;
using ZenStates.Core.Drivers;

namespace ZenStates.Core
{
    /// <summary>
    /// Reads and parses the AGESA PSP Output Block (APOB) from physical memory.
    /// </summary>
    public sealed class Apob
    {
        private const uint APOB_SIGNATURE = 0x424F5041; // "APOB"
        private const uint HASH_SIZE = 32;
        private const int CONFIG_LIST_START = 0x30;
        private const int ENTRY_SIZE_OFFSET = 0x0C;
        private const int DATA_MIN_SIZE = 4;
        private const int DATA_PARSE_LEAD_BYTES = 48;
        private const int RTT_BLOCK_SIZE = 5;

        // Expected first-byte signatures for each config block type
        private const byte MAIN_CONFIG_BYTE0 = 0x01;
        private const byte MAIN_CONFIG_BYTE4 = 0x19;
        private const byte EXT_CONFIG_BYTE0 = 0x07;
        private const byte EXT_CONFIG_BYTE4 = 0x03;

        private static readonly IODriver io = IODriver.Instance;
        private static readonly uint[] KnownAddresses = new uint[] { 0xA200000, 0x9F00000, 0x4000000 };

        private readonly Cpu.CodeName _codeName;

        /// <summary>Gets a value indicating whether a valid APOB was located in physical memory.</summary>
        public bool IsAvailable { get { return Address != 0; } }

        /// <summary>Physical base address of the APOB table.</summary>
        public uint Address { get; private set; }
        public uint DataOffset { get; private set; }
        public uint DataSize { get; private set; }
        public uint ExtendedDataOffset { get; private set; }
        public uint ExtendedDataSize { get; private set; }

        public ApobHeader Header { get; private set; }
        public ApobData Data { get; private set; }
        public ApobData ExtendedData { get; private set; }

        /// <summary>Offsets of all non-zero config entries found inside the header region.</summary>
        public List<uint> ConfigOffsets { get; private set; }

        /// <summary>Raw bytes of the entire APOB table.</summary>
        public byte[] RawTable { get; private set; }

        public byte[] RawData
        {
            get { return SliceRawTable(DataOffset, DataSize); }
        }

        public byte[] RawExtendedData
        {
            get { return SliceRawTable(ExtendedDataOffset, ExtendedDataSize); }
        }

        public Apob(Cpu.CodeName codeName)
        {
            _codeName = codeName;

            if (io == null)
            {
                Debug.WriteLine("IODriver instance is not available.");
                return;
            }

            // 1. Scan known physical addresses for the "APOB" signature.
            Address = FindApobAddress();
            if (!IsAvailable)
                return;

            // 2. Read the table header.
            if (!TryParseHeader(Address, out ApobHeader header))
                return;
            Header = header;

            // 3. Read the entire table
            RawTable = io.ReadMemory(new IntPtr(Address), unchecked((int)Header.TableSize));
            if (RawTable == null || RawTable.Length == 0)
                return;

            // 4. Collect non-zero config entry offsets from the header region.
            ConfigOffsets = GetConfigOffsets(RawTable, Header);
            if (ConfigOffsets.Count == 0)
                return;

            // 5. Locate and validate the primary config block.
            if (!TryGetMainConfig())
                return;

            // 6. Optionally locate the extended config block, which may contain more data on some SKUs
            TryGetExtendedConfig();

            // 7. Parse data
            ParseRawData();
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

            int regionEnd = CONFIG_LIST_START + regionLength;

            for (int i = CONFIG_LIST_START; i + 3 < regionEnd && i + 3 < table.Length; i += 4)
            {
                uint offset = Utils.ReadUInt32(table, i);
                if (offset != 0 && offset + ENTRY_SIZE_OFFSET + 4 < table.Length)
                    list.Add(offset);
            }

            return list;
        }

        private bool TryGetMainConfig()
        {
            uint firstOffset = ConfigOffsets[0];
            if (firstOffset + ENTRY_SIZE_OFFSET + 4 >= RawTable.Length)
                return false;

            uint firstEntrySize = Utils.ReadUInt32(RawTable, (int)(firstOffset + ENTRY_SIZE_OFFSET));
            uint secondOffset = firstOffset + firstEntrySize;

            if (secondOffset + ENTRY_SIZE_OFFSET + 4 >= RawTable.Length)
                return false;
            if (secondOffset + 5 >= RawTable.Length)
                return false;

            if (RawTable[secondOffset] != MAIN_CONFIG_BYTE0 ||
                RawTable[secondOffset + 4] != MAIN_CONFIG_BYTE4)
                return false;

            uint secondSize = Utils.ReadUInt32(RawTable, (int)(secondOffset + ENTRY_SIZE_OFFSET));
            if (secondSize <= DATA_MIN_SIZE)
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

                if (RawTable[offset] == EXT_CONFIG_BYTE0 &&
                    RawTable[offset + 4] == EXT_CONFIG_BYTE4)
                {
                    if (offset + ENTRY_SIZE_OFFSET + 4 >= RawTable.Length)
                        return false;

                    ExtendedDataOffset = offset;
                    ExtendedDataSize = Utils.ReadUInt32(RawTable, (int)(offset + ENTRY_SIZE_OFFSET));
                    return true;
                }
            }

            return false;
        }

        private void ParseRawData()
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

                // Need at least 6 more bytes for RTT block extraction.
                if (i + 6 >= end)
                    return;

                Data = ApobDataReader.Read(RawTable, _codeName, i);

                byte[] rttBlock = new byte[RTT_BLOCK_SIZE];
                Buffer.BlockCopy(RawTable, (int)i + 2, rttBlock, 0, RTT_BLOCK_SIZE);

                if (Utils.AllZero(rttBlock))
                    return;

                // Locate the same sequence inside the extended data block.
                int extendedMatch = Utils.FindSequence(RawTable, (int)ExtendedDataOffset, rttBlock);
                if (extendedMatch < 2)
                    return;

                ExtendedData = ApobDataReader.Read(RawTable, _codeName, (uint)(extendedMatch - 2));
                return;
            }
        }
    }
}