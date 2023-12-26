using System;
using static ZenStates.Core.DRAM.MemoryConfig;

namespace ZenStates.Core.DRAM
{
    public class Capacity
    {
        public CapacityUnit Unit;
        public ulong SizeInBytes;

        public Capacity()
        {
            Unit = CapacityUnit.GB;
            SizeInBytes = 0;
        }

        public Capacity(ulong sizeInBytes, CapacityUnit unit = CapacityUnit.GB)
        {
            Unit = unit;
            SizeInBytes = sizeInBytes;
        }

        public override string ToString()
        {
            return $"{SizeInBytes / Math.Pow(1024, (int)Unit)}{Enum.GetName(typeof(CapacityUnit), Unit)}";
        }
    }
}
