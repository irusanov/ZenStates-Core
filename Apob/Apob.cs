using System;
using System.Collections.Generic;
using System.Diagnostics;
using ZenStates.Core.Drivers;

namespace ZenStates.Core
{
    public sealed class Apob
    {
        private static readonly IODriver io = IODriver.Instance;
        private static readonly uint[] KnownAddresses = new uint[3] { 0xA200000, 0x9F00000, 0x4000000 };
        private const uint APOB_SIGNATURE = 0x424f5041; // "APOB"
        private const uint HASH_SIZE = 32;
        private readonly uint ApobAddress = 0;
        private readonly Cpu.CodeName CodeName;

        private static readonly byte[] CONFIG1_PATTERN = new byte[8] { 0x01, 0x00, 0x00, 0x00, 0x19, 0x00, 0x00, 0x00 };
        private static readonly byte[] CONFIG2_PATTERN = new byte[8] { 0x07, 0x00, 0x00, 0x00, 0x03, 0x00, 0x00, 0x00 };

        public bool IsAvailable
        {
            get { return ApobAddress != 0; }
        }

        public uint Address
        {
            get { return ApobAddress; }
        }

        public uint DataOffset { get; private set; }
        public uint ExtendedDataOffset { get; private set; }
        public ApobHeader Header { get; private set; }
        public ApobData Data { get; private set; }
        public ApobData ExtendedData { get; private set; }
        public List<uint> ConfigOffsets { get; private set; }
        public byte[] RawTable { get; private set; }
        public byte[] RawData { get; private set; }
        public byte[] RawExtendedData { get; private set; }

        /**
         * 1. Find the APOB address by checking known addresses for the signature
         * 2. Read and parse the APOB header
         * 3. Read whole table?
         * 4. Get all config addresses starting from offset 0x30 up to the start of the hash (header size - hash size (32))
         * 5. First offset should be first config, get its size and jump to next offset, which should be matching pattern '01 00 00 00 19 00 00 00'
         * 6. (Optional) Find extended config by checking offsets or searching for pattern '07 00 00 00 03 00 00 00', on 7950X it contains additional values
         * 7. Parse the config
         */
        public Apob(Cpu.CodeName codeName)
        {
            DataOffset = 0;
            CodeName = codeName;

            if (io == null)
            {
                Debug.WriteLine("IOModule instance is not available.");
                return;
            }

            // 1.
            ApobAddress = FindApobAddress();

            if (!IsAvailable)
                return;

            // 2.
            if (!TryParseHeader(ApobAddress, out ApobHeader header))
                return;

            Header = header;

            // 3.
            RawTable = io.ReadMemory(new IntPtr(ApobAddress), unchecked((int)Header.TableSize));
            if (RawTable == null || RawTable.Length == 0)
                return;

            // 4.
            ConfigOffsets = GetConfigOffsets(RawTable, Header);
            if (ConfigOffsets.Count == 0)
                return;

            // 4.
            RawData = GetMainConfigData();
            if (RawData == null || RawData.Length == 0)
                return;

            RawExtendedData = GetExtendedConfigData();

            // 5.
            ParseRawData();
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

        /**
         * 1. Read the header size from a known offset 0xC
         * 2. Read the entire header based on the header size
         * 3. Convert the byte array to the ApobHeader structure
         */
        private static bool TryParseHeader(uint address, out ApobHeader header)
        {
            header = default;
            try
            {
                byte headerSize = io.ReadMemory(new IntPtr(address + 0xC), 1)[0];
                byte[] headerData = io.ReadMemory(new IntPtr(address), headerSize);

                if (headerData == null || headerData.Length < headerSize)
                {
                    return false;
                }

                header = Utils.ByteArrayToStructure<ApobHeader>(headerData);

                return true;
            }
            catch (Exception e)
            {
                Debug.WriteLine(e, e.Message);
            }
            return false;
        }

        private static List<uint> GetConfigOffsets(byte[] table, ApobHeader header)
        {
            var list = new List<uint>();

            if (table == null || header.TableSize == 0)
                return list;

            //var configs = io.ReadMemory(new IntPtr(ApobAddress + 0x30), unchecked((int)(Header.HeaderSize - 0x30 - HASH_SIZE)));
            var buffer = new byte[header.HeaderSize - 0x30 - HASH_SIZE];
            
            Buffer.BlockCopy(table, 0x30, buffer, 0, buffer.Length);

            if (!Utils.AllZero(buffer))
            {
                for (int i = 0; i < buffer.Length; i += 4)
                {
                    uint offset = BitConverter.ToUInt32(buffer, i);
                    if (offset == 0)
                        break;

                    list.Add(offset);
                }
            }

            return list;
        }

        private byte[] GetMainConfigData()
        {
            var buffer = new byte[4];
            Buffer.BlockCopy(RawTable, (int)(ConfigOffsets[0] + 0xC), buffer, 0, buffer.Length);
            uint size = BitConverter.ToUInt32(buffer, 0);
            DataOffset = ConfigOffsets[0] + size;

            buffer = new byte[4];
            Buffer.BlockCopy(RawTable, (int)(DataOffset + 0xC), buffer, 0, buffer.Length);
            size = BitConverter.ToUInt32(buffer, 0);

            var data = new byte[size];
            Buffer.BlockCopy(RawTable, (int)(DataOffset), data, 0, (int)size);

            if (Utils.FindSequence(data, 0, CONFIG1_PATTERN) != 0)
                return null;

            return data;
        }

        private byte[] GetExtendedConfigData()
        {
            var buffer = new byte[8];
            foreach (var offset in ConfigOffsets)
            {
                Buffer.BlockCopy(RawTable, (int)offset, buffer, 0, buffer.Length);
                if (Utils.FindSequence(buffer, 0, CONFIG2_PATTERN) == 0)
                {
                    ExtendedDataOffset = offset;
                    break;
                }
            }

            if (ExtendedDataOffset == 0)
                return null;

            buffer = new byte[4];
            Buffer.BlockCopy(RawTable, (int)(ExtendedDataOffset + 0xC), buffer, 0, buffer.Length);
            var size = BitConverter.ToUInt32(buffer, 0);

            buffer = new byte[size];
            Buffer.BlockCopy(RawTable, (int)ExtendedDataOffset, buffer, 0, buffer.Length);
            return buffer;
        }

        private void ParseRawData()
        {
            uint startOffset = 48;
            if (startOffset >= RawData.Length)
                return;

            byte[] rttBlock = new byte[5];
            bool foundRttBlock = false;

            uint i;
            for (i = startOffset; i < RawData.Length; i++)
            {
                if (RawData[i] != 0)
                {
                    // 4. Skip 2 bytes and take next 5 bytes, those are the Rtts
                    if (i + 2 + rttBlock.Length > RawData.Length)
                        return;

                    Data = ApobDataReader.Read(RawData, CodeName, i);

                    Buffer.BlockCopy(RawData, (int)i + 2, rttBlock, 0, rttBlock.Length);
                    foundRttBlock = true;

                    break;
                }
            }

            if (!foundRttBlock || Utils.AllZero(rttBlock))
                return;

            int extendedOffset = Utils.FindSequence(RawExtendedData, 0, rttBlock);
            if (extendedOffset > -1)
                extendedOffset -= 2;
            else
                return;

            if (extendedOffset < 0)
                return;

            ExtendedData = ApobDataReader.Read(RawExtendedData, CodeName, (uint)extendedOffset);
        }
    }
}