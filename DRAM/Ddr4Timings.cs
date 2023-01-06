using System.Collections.Generic;
using static ZenStates.Core.DRAM.MemoryConfig;

namespace ZenStates.Core.DRAM
{
    public class Ddr4Timings : BaseDramTimings
    {
        private static readonly Dictionary<uint, TimingDef[]> defs = new Dictionary<uint, TimingDef[]>
        {
            /*
            // BGS0
            { 0x50050, new[] {
                new TimingDef { Name = "BGS0",   HiBit = 31, LoBit = 0 },
            }},
            // BGS1
            { 0x50058, new[] {
                new TimingDef { Name = "BGS1",   HiBit = 31, LoBit = 0 },
            }},
            { 0x500D0, new[] {
                new TimingDef { Name = "BGSAlt0",   HiBit = 10  ,   LoBit = 4   },
            }},
            { 0x500D4, new[] {
                new TimingDef { Name = "BGSAlt1",   HiBit = 10  ,   LoBit = 4   },
            }},
            */
            { 0x5012C, new[] {
                new TimingDef { Name = "PowerDown", HiBit = 28  ,   LoBit = 28  },
            }},
            { 0x50200, new[] {
                new TimingDef { Name = "Cmd2T",     HiBit = 10  ,   LoBit = 10  },
                new TimingDef { Name = "GDM",       HiBit = 11  ,   LoBit = 11  },
                // new TimingDef { Name = "Ratio",     HiBit = 6   ,   LoBit = 0   },
            }},
            { 0x50204, new[] {
                new TimingDef { Name = "RCDWR",     HiBit = 29  ,   LoBit = 24  },
                new TimingDef { Name = "RCDRD",     HiBit = 21  ,   LoBit = 16  },
                new TimingDef { Name = "RAS",       HiBit = 14  ,   LoBit = 8   },
                new TimingDef { Name = "CL",        HiBit = 5   ,   LoBit = 0   },
            }},
            { 0x50208, new[] {
                new TimingDef { Name = "RC",        HiBit = 7   ,   LoBit = 0   },
                new TimingDef { Name = "RP",        HiBit = 21  ,   LoBit = 16  },
            }},
            { 0x5020C, new[] {
                new TimingDef { Name = "RTP",       HiBit = 28  ,   LoBit = 24  },
                new TimingDef { Name = "RRDL",      HiBit = 12  ,   LoBit = 8   },
                new TimingDef { Name = "RRDS",      HiBit = 4   ,   LoBit = 0   },
            }},
            { 0x50210, new[] {
                new TimingDef { Name = "FAW",       HiBit = 7   ,   LoBit = 0   },
            }},
            { 0x50214, new[] {
                new TimingDef { Name = "WTRL",      HiBit = 22  ,   LoBit = 16  },
                new TimingDef { Name = "WTRS",      HiBit = 12  ,   LoBit = 8   },
                new TimingDef { Name = "CWL",       HiBit = 5   ,   LoBit = 0   },
            }},
            { 0x50218, new[] {
                new TimingDef { Name = "WR",        HiBit = 7   ,   LoBit = 0   },
            }},
            { 0x5021C, new[] {
                new TimingDef { Name = "TRCPAGE",   HiBit = 31  ,   LoBit = 20  }, // ?
            }},
            { 0x50220, new[] {
                new TimingDef { Name = "RDRDSCL",   HiBit = 29  ,   LoBit = 24  }, // ?
                new TimingDef { Name = "RDRDSC",    HiBit = 19  ,   LoBit = 16  },
                new TimingDef { Name = "RDRDSD",    HiBit = 11  ,   LoBit = 8   },
                new TimingDef { Name = "RDRDDD",    HiBit = 3   ,   LoBit = 0   },
            }},
            { 0x50224, new[] {
                new TimingDef { Name = "WRWRSCL",   HiBit = 29  ,   LoBit = 24  }, // ?
                new TimingDef { Name = "WRWRSC",    HiBit = 19  ,   LoBit = 16  },
                new TimingDef { Name = "WRWRSD",    HiBit = 11  ,   LoBit = 8   },
                new TimingDef { Name = "WRWRDD",    HiBit = 3   ,   LoBit = 0   },
            }},
            { 0x50228, new[] {
                new TimingDef { Name = "RDWR",      HiBit = 13  ,   LoBit = 8   },
                new TimingDef { Name = "WRRD",      HiBit = 3   ,   LoBit = 0   },
            }},
            { 0x50230, new[] {
                new TimingDef { Name = "REFI",      HiBit = 15  ,   LoBit = 0   },
            }},
            { 0x50234, new[] {
                new TimingDef { Name = "MODPDA",    HiBit = 29  ,   LoBit = 24  },
                new TimingDef { Name = "MRDPDA",    HiBit = 21  ,   LoBit = 16  },
                new TimingDef { Name = "MOD",       HiBit = 13  ,   LoBit = 8   },
                new TimingDef { Name = "MRD",       HiBit = 5   ,   LoBit = 0   },
            }},
            { 0x50250, new[] {
                new TimingDef { Name = "STAG",      HiBit = 26  ,   LoBit = 16  },
            }},
            { 0x50254, new[] {
                new TimingDef { Name = "CKE",       HiBit = 28  ,   LoBit = 24  },
                new TimingDef { Name = "XP",        HiBit = 5   ,   LoBit = 0   },
            }},
            { 0x50258, new[] {
                new TimingDef { Name = "PHYWRD",    HiBit = 26  ,   LoBit = 24  },
                new TimingDef { Name = "PHYRDL",    HiBit = 23  ,   LoBit = 16  },
                new TimingDef { Name = "PHYWRL",    HiBit = 15  ,   LoBit = 8   },
            }},
            // TRFC registers
            /*
            { 0x50260, new[] {
                new TimingDef { Name = "",   HiBit = 0, LoBit = 0 },
            }},
            { 0x50264, new[] {
                new TimingDef { Name = "",   HiBit = 0, LoBit = 0 },
            }},
            */
        };

        public Ddr4Timings(Cpu cpu) : base(cpu)
        {
            this.Type = MemType.DDR4;
            this.Dict = defs;
        }

        // Specific DDR4 timings
        public uint RFC4 { get; set; }

        public override void ReadUniqueTimings(uint offset = 0)
        {
            Ratio = Utils.GetBits(cpu.ReadDword(offset | 0x50200), 0, 7) / 3.0f;

            uint trfcTimings0 = this.cpu.ReadDword(offset | 0x50260);
            uint trfcTimings1 = this.cpu.ReadDword(offset | 0x50264);
            uint trfcRegValue = trfcTimings0 != trfcTimings1 ? (trfcTimings0 != 0x21060138 ? trfcTimings0 : trfcTimings1) : trfcTimings0;

            RFC = Utils.BitSlice(trfcRegValue, 10, 0);
            RFC2 = Utils.BitSlice(trfcRegValue, 21, 11);
            RFC4 = Utils.GetBits(trfcRegValue, 31, 22);
        }
    }
}
