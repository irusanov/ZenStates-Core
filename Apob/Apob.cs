using System;
using static ZenStates.Core.Cpu;

namespace ZenStates.Core
{
    public class Apob
    {
        private static readonly IOModule io = IOModule.Instance;
        private static readonly uint[] KnownAddresses = new uint[2] { 0xA200000, 0x400000 };
        private const uint ApobSignature = 0x424f5041; // "APOB"
        private static readonly byte[] DataOffsetPattern = new byte[8] { 0x01, 0x00, 0x00, 0x00, 0x19, 0x00, 0x00, 0x0 };
        private readonly uint ApobAddress = 0;
        private const int SizeToRead = 0x2000;
        // TODO: Detect actual channel count, as otherwise it would read other data blocks as channel data, which will be garbage
        //private const int MAX_CHANNELS = 12;

        public bool IsAvailable => ApobAddress != 0;

        public ApobHeader Header { get; private set; }
        public ApobData Data { get; private set; }
        public byte[] RawData { get; private set; }

        public Apob(Family family)
        {
            if (io == null)
            {
                throw new InvalidOperationException("IOModule instance is not available.");
            }

            foreach (uint address in KnownAddresses)
            {
                if (io.GetPhysLong(new UIntPtr(address), out uint data) && data == ApobSignature)
                {
                    ApobAddress = address;
                    break;
                }
            }

            if (IsAvailable)
            {
                RawData = io.ReadMemory(new IntPtr(ApobAddress), SizeToRead);
                Header = Utils.ByteArrayToStructure<ApobHeader>(RawData);

                // Supposedly the pattern is always the same. Easiest way to find the channel info.
                var index = Utils.FindSequence(RawData, 0, DataOffsetPattern);
                if (index > -1)
                {
                    // TODO: Detect version in another way, if possible
                    var layoutVersion = family == Family.FAMILY_1AH ? ApobLayoutVersion.V2 : ApobLayoutVersion.V1;
                    Data = ApobDataReader.Read(RawData, layoutVersion, index + 48);
                }
            }
        }
    }
}
