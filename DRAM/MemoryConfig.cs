using System.Collections.Generic;

namespace ZenStates.Core.DRAM
{
    public class MemoryConfig
    {
        private const int DRAM_TYPE_BIT_MASK = 0x1;

        private const uint DRAM_TYPE_REG_ADDR = 0x50100;

        private readonly Cpu cpu;

        public struct TimingDef
        {
            public string Name;

            public int HiBit;

            public int LoBit;
        }

        public enum MemType
        {
            DDR4 = 0,
            DDR5 = 1,
        }

        public BaseDramTimings Timings;

        // @TODO: either read all offsets or expose DCT offset
        public MemoryConfig(Cpu cpuInstance)
        {
            cpu = cpuInstance;

            MemType type = (MemType)(cpu.ReadDword(0 | DRAM_TYPE_REG_ADDR) & DRAM_TYPE_BIT_MASK);
            if (type == MemType.DDR4)
            {
                Timings = new Ddr4Timings(cpu);
            }
            else if (type == MemType.DDR5)
            {
                Timings = new Ddr5Timings(cpu);
            }

            ReadTimings();
        }

        public void ReadTimings(uint offset = 0)
        {
            Timings?.Read();
        }
    }
}
