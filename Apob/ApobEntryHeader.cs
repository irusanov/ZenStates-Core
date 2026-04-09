using System.Runtime.InteropServices;

namespace ZenStates.Core
{
    [StructLayout(LayoutKind.Explicit, Pack = 1)]
    public readonly struct ApobEntryHeader
    {
        [FieldOffset(0x0)]
        private readonly uint _type0;
        [FieldOffset(0x4)]
        private readonly uint _type1;
        [FieldOffset(0x8)]
        private readonly uint _unknown0;
        [FieldOffset(0xc)]
        private readonly uint _size; // seems to be total size, including header
        [FieldOffset(0x10), MarshalAs(UnmanagedType.ByValArray, SizeConst = 8)]
        private readonly uint[] _hash;

        // public
        public uint Type0 => _type0;
        public uint Type1 => _type1;
        public uint Unknown => _unknown0;
        public uint Size => _size;
        public uint[] Hash => _hash;
    }
}
