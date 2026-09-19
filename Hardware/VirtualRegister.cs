using System;
using System.Globalization;

namespace ZenStates.Core.Hardware
{
    /// <summary>
    /// A single register captured by address and value
    /// </summary>
    [Serializable]
    public readonly struct VirtualRegister
    {
        public uint Address { get; }
        public uint Value { get; }

        public VirtualRegister(uint address, uint value)
        {
            Address = address;
            Value = value;
        }

        // Helper methods to extract bits from the register value
        public uint GetBits(int offset, int n) => Utils.GetBits(Value, offset, n);

        public uint BitSlice(int hi, int lo) => Utils.BitSlice(Value, hi, lo);

        public uint GetBit(int offset) => Utils.GetBit(Value, offset);

        public override string ToString()
        {
            return string.Format(CultureInfo.InvariantCulture, "0x{0:X8}: 0x{1:X8}", Address, Value);
        }
    }
}
