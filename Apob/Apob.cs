using System;
using System.Diagnostics;
using ZenStates.Core.Drivers;

namespace ZenStates.Core
{
    public sealed class Apob
    {
        private static readonly IODriver io = IODriver.Instance;
        private static readonly uint[] KnownAddresses = new uint[3] { 0xA200000, 0x9F00000, 0x4000000 };
        private const uint ApobSignature = 0x424f5041; // "APOB"
        //private static readonly byte[] DataOffsetPattern = new byte[8] { 0x01, 0x00, 0x00, 0x00, 0x19, 0x00, 0x00, 0x0 };
        //private static readonly byte[] EndPattern = new byte[6] { 0xff, 0xff, 0x01, 0x00, 0xff, 0xff };
        private readonly uint ApobAddress = 0;
        //private const int InitialHeaderSize = 16;
        private const int DefaultSizeToRead = 0x5000;

        public bool IsAvailable
        {
            get { return ApobAddress != 0; }
        }

        public uint Address
        {
            get { return ApobAddress; }
        }

        public int Offset { get; private set; }
        public int SecondOffset { get; private set; }
        public int LayoutVersion { get; private set; }
        public ApobHeader Header { get; private set; }
        public ApobData Data { get; private set; }
        public byte[] RawData { get; private set; }

        /**
         * 1. Find the APOB address by checking known addresses for the signature
         * 2. Read and parse the APOB header
         * 3. Read the raw data from CongigStartAddress to Config3StartAddress
         */
        public Apob()
        {
            Offset = -1;
            SecondOffset = -1;
            LayoutVersion = -1;

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
            if (!TryReadHeader(ApobAddress, out ApobHeader header))
                return;

            Header = header;
            RawData = io.ReadMemory(new IntPtr(ApobAddress), GetReadSize());

            if (RawData == null || RawData.Length == 0)
                return;

            ParseRawData();
        }

        private static uint FindApobAddress()
        {
            for (int i = 0; i < KnownAddresses.Length; i++)
            {
                if (io.GetPhysLong(new UIntPtr(KnownAddresses[i]), out uint data) && data == ApobSignature)
                    return KnownAddresses[i];
            }

            return 0;
        }

        /**
         * 1. Read the header size from a known offset 0xC
         * 2. Read the entire header based on the header size
         * 3. Convert the byte array to the ApobHeader structure
         */
        private static bool TryReadHeader(uint address, out ApobHeader header)
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

        private int GetReadSize()
        {
            int startAddress = (int)(ApobAddress);
            int endAddress = (int)(ApobAddress + Header.Config3StartOffset);
            int tableSize = endAddress - startAddress;
            return tableSize > 0 ? tableSize : DefaultSizeToRead;
        }

        private void ParseRawData()
        {
            // TODO: Find the offset and size of the block to read
            /**
             * 1. Find the data block offset from uint at offset 0xC from the start of the config data
             * 1. Find the (layout version?)
             * 3. From the start offset to Header.ConfigEndAddress, find first non-zero value
             * 4. Skip 2 bytes and take next 5 bytes, those are the Rtts
             * 5. Search for first occurence of Rtts after end sequence
             * 6. Rewind the index by 2 and parse the Apob data
             */

            uint dataOffset = BitConverter.ToUInt32(RawData, (int)Header.ConfigStartOffset + 0xC);
            if (dataOffset < 0)
                return;

            //Offset = Utils.FindSequence(RawData, 0, DataOffsetPattern);
            Offset = (int)(Header.ConfigStartOffset + dataOffset);

            // 1.
            ApobLayoutVersion layoutVersion;
            int layoutVersionIndex = Offset + 0xC;

            if (layoutVersionIndex >= RawData.Length)
                return;

            LayoutVersion = RawData[layoutVersionIndex];

            switch (LayoutVersion)
            {
                case 0x90:
                    layoutVersion = ApobLayoutVersion.V90;
                    break;
                case 0x60:
                    layoutVersion = ApobLayoutVersion.V60;
                    break;
                case 0xA4:
                    layoutVersion = ApobLayoutVersion.VA4;
                    break;
                default:
                    layoutVersion = ApobLayoutVersion.V60;
                    break;
            }

            // 2.
            int startOffset = Offset + 48;
            if (startOffset >= RawData.Length)
                return;

            //int endOffset = Utils.FindSequence(RawData, startOffset, EndPattern);
            int endOffset = (int)Header.ConfigEndOffset;
            if (endOffset < 0 || endOffset <= startOffset)
                return;

            // 3.
            byte[] rttBlock = new byte[5];
            bool foundRttBlock = false;

            int i;
            for (i = startOffset; i < endOffset; i++)
            {
                if (RawData[i] != 0)
                {
                    if (i + 2 + 5 > RawData.Length)
                        return;

                    if (layoutVersion == ApobLayoutVersion.V90)
                    {
                        Data = ApobDataReader.Read(RawData, layoutVersion, i);
                        // Skip additional RTT block parsing for V90 since the data is right there and the layout is different
                        break;
                    }

                    Buffer.BlockCopy(RawData, i + 2, rttBlock, 0, 5);
                    foundRttBlock = true;

                    break;
                }
            }

            if (!foundRttBlock || Utils.AllZero(rttBlock))
                return;

            // 4.
            SecondOffset = Utils.FindSequence(RawData, endOffset, rttBlock);
            if (SecondOffset > -1)
                SecondOffset -= 2;
            else
                return;

            if (SecondOffset < 0)
                return;

            Data = ApobDataReader.Read(RawData, layoutVersion, SecondOffset);
        }
    }
}