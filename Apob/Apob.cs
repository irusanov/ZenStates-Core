using System;

namespace ZenStates.Core
{
    public class Apob
    {
        private static readonly IOModule io = IOModule.Instance;
        private static readonly uint[] KnownAddresses = new uint[2] { 0xA200000, 0x400000 };
        private const uint ApobSignature = 0x424f5041; // "APOB"
        private readonly uint ApobAddress = 0;
        private const int size = 0x2000;

        public bool IsAvailable => ApobAddress != 0;

        public ApobData Data { get; private set; }

        public Apob()
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
                byte[] data = io.ReadMemory(new IntPtr(ApobAddress), size);
                Data = Utils.ByteArrayToStructure<ApobData>(data);
            }
        }
    }
}
