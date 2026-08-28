using System;
using System.Collections.Generic;
using System.Diagnostics;
using ZenStates.Core.Drivers;

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
        private const uint DATA_MIN_SIZE = 4;
        private const uint DATA_PARSE_LEAD_BYTES = 48;
        private const uint RTT_BLOCK_SIZE = 5;
        private const uint CCDL_BLOCK_OFFSET_ZEN4 = 0x28;
        private const uint CCDL_BLOCK_OFFSET_ZEN5 = 0x0E;

        // Expected first-byte signatures for each config block type
        private const byte MAIN_CONFIG_BYTE0 = 0x01;
        private const byte MAIN_CONFIG_BYTE4 = 0x19;
        private const byte EXT_CONFIG_BYTE0 = 0x07;
        private const byte EXT_CONFIG_BYTE4 = 0x03;

        private static readonly uint[] KnownAddresses = new uint[] { 0xA200000, 0x9F00000, 0x4000000 };

        private static readonly byte[] CCDL_BLOCK_MAGIC_ZEN4 = new byte[] { 0x00, 0x43, 0x30, 0x00 };
        private static readonly byte[] CCDL_BLOCK_MAGIC_ZEN5 = new byte[] { 0x00, 0x50, 0xC3, 0x00 };

        private static readonly IODriver io = IODriver.Instance;

        private readonly Cpu.CodeName _codeName;

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
        public List<uint> ConfigOffsets { get; private set; }

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

        public Apob(Cpu.CodeName codeName)
        {
            _codeName = codeName;

            if (io == null)
            {
                ErrorReason = "IODriver instance is not available.";
                Debug.WriteLine(ErrorReason);
                return;
            }

            // 1. Scan known physical addresses for the "APOB" signature.
            Address = FindApobAddress();
            if (!IsAvailable)
            {
                ErrorReason = "APOB signature not found at any known physical address.";
                return;
            }

            // 2. Read the table header.
            if (!TryParseHeader(Address, out ApobHeader header))
            {
                ErrorReason = $"Failed to read or parse APOB header at address 0x{Address:X8}.";
                return;
            }
            Header = header;

            // 3. Read the entire table
            RawTable = io.ReadMemory(new IntPtr(Address), unchecked((int)Header.TableSize));
            if (RawTable == null || RawTable.Length == 0)
            {
                ErrorReason = $"Failed to read APOB table body ({Header.TableSize} bytes) at address 0x{Address:X8}.";
                return;
            }

            // 4. Collect non-zero config entry offsets from the header region.
            ConfigOffsets = GetConfigOffsets(RawTable, Header);
            if (ConfigOffsets.Count == 0)
            {
                ErrorReason = "No valid config entry offsets found in APOB header region.";
                return;
            }

            // 5. Locate and validate the primary config block.
            if (!TryGetMainConfig())
            {
                ErrorReason = "Failed to locate or validate the primary APOB config block.";
                return;
            }

            // 6. Optionally locate the extended config block, which may contain more data on some SKUs
            TryGetExtendedConfig();

            // 7. Locate the channel start offset inside the extended data block
            if (TryFindCcdlBlock(out uint ccdl, out uint ccdlrw, out uint ccdlrw2))
            {
                CcdlData = new CcdlData(ccdl, ccdlrw, ccdlrw2);
            }

            // 8. Parse data
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
            uint firstOffset = ConfigOffsets[0];
            if (firstOffset + ENTRY_SIZE_OFFSET + 4 >= RawTable.Length)
                return false;

            uint firstEntrySize = Utils.ReadUInt32(RawTable, firstOffset + ENTRY_SIZE_OFFSET);
            uint secondOffset = firstOffset + firstEntrySize;

            if (secondOffset + ENTRY_SIZE_OFFSET + 4 >= RawTable.Length)
                return false;
            if (secondOffset + 5 >= RawTable.Length)
                return false;

            if (RawTable[secondOffset] != MAIN_CONFIG_BYTE0 ||
                RawTable[secondOffset + 4] != MAIN_CONFIG_BYTE4)
                return false;

            uint secondSize = Utils.ReadUInt32(RawTable, secondOffset + ENTRY_SIZE_OFFSET);
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
                    ExtendedDataSize = Utils.ReadUInt32(RawTable, offset + ENTRY_SIZE_OFFSET);
                    return true;
                }
            }

            return false;
        }

        private bool TryFindCcdlBlock(out uint ccdl, out uint ccdlrw, out uint ccdrw2)
        {
            ccdl = 0;
            ccdlrw = 0;
            ccdrw2 = 0;

            if (ExtendedDataOffset == 0 || ExtendedDataSize == 0 || RawExtendedData == null)
            {
                return false;
            }

            bool isDefault;
            switch (_codeName)
            {
                // TODO: Do not use codename, maybe use family
                case Cpu.CodeName.Turin:
                case Cpu.CodeName.TurinD:
                case Cpu.CodeName.ShimadaPeak:
                case Cpu.CodeName.StrixPoint:
                case Cpu.CodeName.StrixHalo:
                case Cpu.CodeName.KrackanPoint:
                case Cpu.CodeName.KrackanPoint2:
                case Cpu.CodeName.GraniteRidge:
                case Cpu.CodeName.Bergamo:
                    isDefault = true;
                    break;
                default:
                    isDefault = false;
                    break;
            }

            byte[] magic = isDefault ? CCDL_BLOCK_MAGIC_ZEN5 : CCDL_BLOCK_MAGIC_ZEN4;
            uint extraOffset = isDefault ? CCDL_BLOCK_OFFSET_ZEN5 : CCDL_BLOCK_OFFSET_ZEN4;

            int matchIndex = Utils.FindSequence(RawExtendedData, 0, magic);
            if (matchIndex < 0)
            {
                return false;
            }

            uint offset = (uint)(matchIndex + magic.Length + extraOffset);

            if (isDefault)
            {
                ccdl = Utils.ReadUInt16(RawExtendedData, offset);
                ccdlrw = Utils.ReadUInt16(RawExtendedData, offset + 2);
                ccdrw2 = Utils.ReadUInt16(RawExtendedData, offset + 4);
            }
            else
            {
                ccdl = Utils.ReadUInt32(RawExtendedData, offset);
                ccdlrw = Utils.ReadUInt32(RawExtendedData, offset + 4);
                ccdrw2 = Utils.ReadUInt32(RawExtendedData, offset + 8);
            }

            return true;
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
                Buffer.BlockCopy(RawTable, (int)i + 2, rttBlock, 0, (int)RTT_BLOCK_SIZE);

                if (Utils.AllZero(rttBlock))
                    return;

                // Locate the same sequence inside the extended data block.
                int extendedMatch = Utils.FindSequence(RawExtendedData, 0, rttBlock);
                if (extendedMatch < 2)
                    return;

                ExtendedData = ApobDataReader.Read(RawExtendedData, _codeName, (uint)(extendedMatch - 2));
                return;
            }
        }
    }
}