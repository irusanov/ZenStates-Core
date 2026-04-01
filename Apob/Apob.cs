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
        private static readonly byte[] DataOffsetPattern = new byte[8] { 0x01, 0x00, 0x00, 0x00, 0x19, 0x00, 0x00, 0x0 };
        private static readonly byte[] EndPattern = new byte[6] { 0xff, 0xff, 0x01, 0x00, 0xff, 0xff };
        private readonly uint apobAddress = 0;
        //private const int InitialHeaderSize = 16;
        private const int SizeToRead = 0x5000;

        public bool IsAvailable
        {
            get { return apobAddress != 0; }
        }

        public uint Address
        {
            get { return apobAddress; }
        }

        public int Offset { get; private set; }
        public int DataBlockOffset { get; private set; }
        public int LayoutVersion { get; private set; }
        public ApobHeader Header { get; private set; }
        public ApobData Data { get; private set; }
        public byte[] RawData { get; private set; }

        public Apob()
        {
            Offset = -1;
            DataBlockOffset = -1;
            LayoutVersion = -1;

            if (io == null)
            {
                Debug.WriteLine("IOModule instance is not available.");
                return;
            }

            apobAddress = FindApobAddress();

            if (!IsAvailable)
                return;

            if (!TryReadHeader(apobAddress, out ApobHeader header))
                return;

            Header = header;

            int sizeToRead = GetReadSize(header);
            RawData = io.ReadMemory(new IntPtr(apobAddress), sizeToRead);

            if (RawData == null || RawData.Length == 0)
                return;

            ParseRawData();
        }

        private static uint FindApobAddress()
        {
            int i;
            uint data;

            for (i = 0; i < KnownAddresses.Length; i++)
            {
                if (io.GetPhysLong(new UIntPtr(KnownAddresses[i]), out data) && data == ApobSignature)
                    return KnownAddresses[i];
            }

            return 0;
        }

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

        private static int GetReadSize(ApobHeader header)
        {
            int tableSize = unchecked((int)header.TableSize);
            return tableSize > 0 ? tableSize : SizeToRead;
        }

        private void ParseRawData()
        {
            // TODO: Find the offset and size of the block to read
            /**
             * 1. Find the start and (layout version?)
             * 2. Find end sequence
             * 3. From the start offset to end offset, find first non-zero value
             * 4. Skip 2 bytes and take next 5 bytes, those are the Rtts
             * 5. Search for first occurence of Rtts after end sequence
             * 6. Rewind the index by 2 and parse the Apob data
             */
            byte[] buffer = new byte[2];
            Buffer.BlockCopy(RawData, (int)(Header.ConfigStartAddress + 0xC), buffer, 0, buffer.Length);

            //Offset = Utils.FindSequence(RawData, 0, DataOffsetPattern);
            Offset = (int)Header.ConfigStartAddress + (buffer[1] << 8 | buffer[0]);
            if (Offset < 0)
                return;

            // 1.
            ApobLayoutVersion layoutVersion;
            int layoutVersionIndex = Offset + DataOffsetPattern.Length + 4;

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

            int endOffset = Utils.FindSequence(RawData, startOffset, EndPattern);

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
            DataBlockOffset = Utils.FindSequence(RawData, endOffset, rttBlock);
            if (DataBlockOffset > -1)
                DataBlockOffset -= 2;
            else
                return;

            if (DataBlockOffset < 0)
                return;

            Data = ApobDataReader.Read(RawData, layoutVersion, DataBlockOffset);
        }
    }
}