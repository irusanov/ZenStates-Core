using System.Collections.Generic;
using static ZenStates.Core.DRAM.MemoryConfig;

namespace ZenStates.Core.DRAM
{
    public class Ddr5Timings : BaseDramTimings
    {
        private static readonly Dictionary<uint, TimingDef[]> defs = new Dictionary<uint, TimingDef[]>
        {
            { 0x5012C, new[] {
                new TimingDef { Name = "PowerDown", HiBit = 28, LoBit = 28 },
            }},
            { 0x50200, new[] {
                new TimingDef { Name = "Cmd2T",     HiBit = 10, LoBit = 10 },
                new TimingDef { Name = "GDM",       HiBit = 11, LoBit = 11 },
            }},
            { 0x500D0, new[] {
                new TimingDef { Name = "BGSAlt0",   HiBit = 10, LoBit = 4 },
            }},
            { 0x500D4, new[] {
                new TimingDef { Name = "BGSAlt1",   HiBit = 10, LoBit = 4 },
            }},
            { 0x50050, new[] {
                new TimingDef { Name = "",   HiBit = 0, LoBit = 0 },
            }},
            { 0x50058, new[] {
                new TimingDef { Name = "",   HiBit = 0, LoBit = 0 },
            }},
            { 0x50204, new[] {
                new TimingDef { Name = "",   HiBit = 0, LoBit = 0 },
            }},
            { 0x50208, new[] {
                new TimingDef { Name = "",   HiBit = 0, LoBit = 0 },
            }},
            { 0x5020C, new[] {
                new TimingDef { Name = "",   HiBit = 0, LoBit = 0 },
            }},
            { 0x50210, new[] {
                new TimingDef { Name = "",   HiBit = 0, LoBit = 0 },
            }},
            { 0x50214, new[] {
                new TimingDef { Name = "",   HiBit = 0, LoBit = 0 },
            }},
            { 0x50218, new[] {
                new TimingDef { Name = "",   HiBit = 0, LoBit = 0 },
            }},
            { 0x5021C, new[] {
                new TimingDef { Name = "",   HiBit = 0, LoBit = 0 },
            }},
            { 0x50220, new[] {
                new TimingDef { Name = "",   HiBit = 0, LoBit = 0 },
            }},
            { 0x50224, new[] {
                new TimingDef { Name = "",   HiBit = 0, LoBit = 0 },
            }},
            { 0x50228, new[] {
                new TimingDef { Name = "",   HiBit = 0, LoBit = 0 },
            }},
            { 0x50230, new[] {
                new TimingDef { Name = "",   HiBit = 0, LoBit = 0 },
            }},
            { 0x50234, new[] {
                new TimingDef { Name = "",   HiBit = 0, LoBit = 0 },
            }},
            { 0x50250, new[] {
                new TimingDef { Name = "",   HiBit = 0, LoBit = 0 },
            }},
            { 0x50254, new[] {
                new TimingDef { Name = "",   HiBit = 0, LoBit = 0 },
            }},
            { 0x50258, new[] {
                new TimingDef { Name = "",   HiBit = 0, LoBit = 0 },
            }},
            { 0x50260, new[] {
                new TimingDef { Name = "",   HiBit = 0, LoBit = 0 },
            }},
            { 0x50264, new[] {
                new TimingDef { Name = "",   HiBit = 0, LoBit = 0 },
            }},
            { 0x50268, new[] {
                new TimingDef { Name = "",   HiBit = 0, LoBit = 0 },
            }},
            { 0x5026C, new[] {
                new TimingDef { Name = "",   HiBit = 0, LoBit = 0 },
            }},
            { 0x5028C, new[] {
                new TimingDef { Name = "",   HiBit = 0, LoBit = 0 },
            }},
        };

        public Ddr5Timings(Cpu cpu) : base(cpu)
        {
            this.Type = MemType.DDR5;
            this.Dict = defs;
        }

        public uint RFCsb { get; set; }
    }
}
