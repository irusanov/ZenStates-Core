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
        [FieldOffset(0x30)]
        private readonly uint _configStartOffet;
        [FieldOffset(0x34)]
        private readonly uint _configEndOffset;
        [FieldOffset(0x38)]
        private readonly uint _config2StartOffset;
        [FieldOffset(0x3C)]
        private readonly uint _config3StartOffset;

        // public
        public uint Signature => _signature;
        public uint Version => _version;
        public uint TableSize => _tableSize;
        public uint HeaderSize => _headerSize;

        public uint ConfigStartOffset => _configStartOffet;
        public uint ConfigEndOffset => _configEndOffset;
        public uint Config2StartOffset => _config2StartOffset;
        public uint Config3StartOffset => _config3StartOffset;
    }
}
