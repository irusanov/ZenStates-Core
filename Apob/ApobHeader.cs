using System.Runtime.InteropServices;

namespace ZenStates.Core
{
    [StructLayout(LayoutKind.Explicit, Pack = 1)]
    public struct ApobHeader
    {
        [FieldOffset(0x0)]
        private readonly uint _signature; // "APOB"
        [FieldOffset(0x4)]
        private readonly uint _version;
        [FieldOffset(0x8)]
        private readonly uint _tableSize;
        [FieldOffset(0xc)]
        private readonly uint _headerSize; // First entry offset?

        // public
        public uint Signature => _signature;
        public uint Version => _version;
        public uint TableSize => _tableSize;
        public uint HeaderSize => _headerSize;
    }
}
