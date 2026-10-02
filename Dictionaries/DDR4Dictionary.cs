using System.Collections.Generic;
using static ZenStates.Core.Hardware.DRAM.MemoryConfig;

namespace ZenStates.Core.Dictionaries
{
    internal static class DDR4Dictionary
    {
        public static readonly Dictionary<uint, TimingDef[]> defs = new Dictionary<uint, TimingDef[]>
        {
            /*
            // BGS0
            { 0x50050, new[] {
                new TimingDef { Name = "BGS0",                          HiBit = 31  ,   LoBit = 0   },
            }},
            // BGS1
            { 0x50058, new[] {
                new TimingDef { Name = "BGS1",                          HiBit = 31  ,   LoBit = 0   },
            }},
            { 0x500D0, new[] {
                new TimingDef { Name = "BGSAlt0",                       HiBit = 10  ,   LoBit = 4   },
            }},
            { 0x500D4, new[] {
                new TimingDef { Name = "BGSAlt1",                       HiBit = 10  ,   LoBit = 4   },
            }},
            */
            { 0x50100, new[] {
                new TimingDef { Name = "DimmEccEn",                     HiBit = 12  ,   LoBit = 12  },
                new TimingDef { Name = "BurstCtrl",                     HiBit = 11  ,   LoBit = 10  },
                new TimingDef { Name = "BurstLength",                   HiBit = 9   ,   LoBit = 8   },
            }},
            { 0x5012C, new[] {
                new TimingDef { Name = "AggrPwrDownEn",                 HiBit = 30  ,   LoBit = 30  },
                new TimingDef { Name = "PowerDownMode",                 HiBit = 29  ,   LoBit = 29  },
                new TimingDef { Name = "PowerDown",                     HiBit = 28  ,   LoBit = 28  },
            }},
            { 0x50130, new[] {
                new TimingDef { Name = "OdtsIncRefEn",                  HiBit = 28  ,   LoBit = 28  },
                new TimingDef { Name = "OdtsEn",                        HiBit = 27  ,   LoBit = 27  },
                new TimingDef { Name = "ForcePwrDownThrotEn",           HiBit = 26  ,   LoBit = 26  },
                new TimingDef { Name = "OdtsCmdThrotEn",                HiBit = 25  ,   LoBit = 25  },
                new TimingDef { Name = "SwCmdThrotEn",                  HiBit = 24  ,   LoBit = 24  },
                new TimingDef { Name = "OdtsCmdThrotCyc",               HiBit = 23  ,   LoBit = 16  },
                new TimingDef { Name = "SwCmdThrotCyc",                 HiBit = 15  ,   LoBit = 8   },
                new TimingDef { Name = "RollWindowDepth",               HiBit = 7   ,   LoBit = 0   },
            }},
            { 0x50200, new[] {
                new TimingDef { Name = "Preamble2t",                    HiBit = 12  ,   LoBit = 12  },
                new TimingDef { Name = "GDM",                           HiBit = 11  ,   LoBit = 11  },
                new TimingDef { Name = "Cmd2T",                         HiBit = 10  ,   LoBit = 10  },
                new TimingDef { Name = "BankGroupEn",                   HiBit = 8   ,   LoBit = 8   },
                // new TimingDef { Name = "Ratio",     HiBit = 6   ,   LoBit = 0   },
            }},
            { 0x50204, new[] {
                new TimingDef { Name = "RCDWR",                         HiBit = 29  ,   LoBit = 24  },
                new TimingDef { Name = "RCDRD",                         HiBit = 21  ,   LoBit = 16  },
                new TimingDef { Name = "RAS",                           HiBit = 14  ,   LoBit = 8   },
                new TimingDef { Name = "CL",                            HiBit = 5   ,   LoBit = 0   },
            }},
            { 0x50208, new[] {
                new TimingDef { Name = "RC",                            HiBit = 7   ,   LoBit = 0   },
                new TimingDef { Name = "RP",                            HiBit = 21  ,   LoBit = 16  },
            }},
            { 0x5020C, new[] {
                new TimingDef { Name = "RTP",                           HiBit = 28  ,   LoBit = 24  },
                new TimingDef { Name = "RRDL",                          HiBit = 12  ,   LoBit = 8   },
                new TimingDef { Name = "RRDS",                          HiBit = 4   ,   LoBit = 0   },
            }},
            { 0x50210, new[] {
                new TimingDef { Name = "FAW",                           HiBit = 6   ,   LoBit = 0   },
            }},
            { 0x50214, new[] {
                new TimingDef { Name = "WTRL",                          HiBit = 22  ,   LoBit = 16  },
                new TimingDef { Name = "WTRS",                          HiBit = 12  ,   LoBit = 8   },
                new TimingDef { Name = "CWL",                           HiBit = 5   ,   LoBit = 0   },
            }},
            { 0x50218, new[] {
                new TimingDef { Name = "WR",                            HiBit = 6   ,   LoBit = 0   },
            }},
            { 0x5021C, new[] {
                new TimingDef { Name = "TRCPAGE",                       HiBit = 31  ,   LoBit = 20  },
            }},
            { 0x50220, new[] {
                new TimingDef { Name = "RDRDBan",                       HiBit = 31  ,   LoBit = 30  },
                new TimingDef { Name = "RDRDSCL",                       HiBit = 27  ,   LoBit = 24  },
                new TimingDef { Name = "RDRDSC",                        HiBit = 19  ,   LoBit = 16  },
                new TimingDef { Name = "RDRDSD",                        HiBit = 11  ,   LoBit = 8   },
                new TimingDef { Name = "RDRDDD",                        HiBit = 3   ,   LoBit = 0   },
            }},
            { 0x50224, new[] {
                new TimingDef { Name = "WRWRBan",                       HiBit = 31  ,   LoBit = 30  },
                new TimingDef { Name = "WRWRSCL",                       HiBit = 29  ,   LoBit = 24  },
                new TimingDef { Name = "WRWRSC",                        HiBit = 19  ,   LoBit = 16  },
                new TimingDef { Name = "WRWRSD",                        HiBit = 11  ,   LoBit = 8   },
                new TimingDef { Name = "WRWRDD",                        HiBit = 3   ,   LoBit = 0   },
            }},
            { 0x50228, new[] {
                new TimingDef { Name = "RDWR",                          HiBit = 12  ,   LoBit = 8   },
                new TimingDef { Name = "WRRD",                          HiBit = 3   ,   LoBit = 0   },
            }},
            { 0x5022C, new[] {
                new TimingDef { Name = "ShortInit",                     HiBit = 31  ,   LoBit = 31  }, // 1 = ZqcsInterval in 2^10 clocks, 0 = 2^20 clocks
                new TimingDef { Name = "ZqcsInterval",                  HiBit = 29  ,   LoBit = 20  },
                new TimingDef { Name = "TzqOperCal",                    HiBit = 19  ,   LoBit = 8   }, // tZQOPER
                new TimingDef { Name = "Tzqcs",                         HiBit = 7   ,   LoBit = 0   },
            }},
            { 0x50230, new[] {
                new TimingDef { Name = "REFI",                          HiBit = 15  ,   LoBit = 0   },
            }},
            { 0x50234, new[] {
                new TimingDef { Name = "MODPDA",                        HiBit = 29  ,   LoBit = 24  },
                new TimingDef { Name = "MRDPDA",                        HiBit = 21  ,   LoBit = 16  },
                new TimingDef { Name = "MOD",                           HiBit = 13  ,   LoBit = 8   },
                new TimingDef { Name = "MRD",                           HiBit = 5   ,   LoBit = 0   },
            }},
            { 0x50238, new[] {
                new TimingDef { Name = "DLLK",                          HiBit = 26  ,   LoBit = 16  },
                new TimingDef { Name = "XS",                            HiBit = 10  ,   LoBit = 0   },
            }},
            { 0x5023C, new[] {
                new TimingDef { Name = "RankBusyDly",                   HiBit = 30  ,   LoBit = 24  },
                new TimingDef { Name = "CmdParLatency",                 HiBit = 19  ,   LoBit = 16  }, // DDR4 MR5.PL
                new TimingDef { Name = "AlertParDly",                   HiBit = 14  ,   LoBit = 8   },
                new TimingDef { Name = "AlertCrcDly",                   HiBit = 6   ,   LoBit = 0   },
            }},
            { 0x50244, new[] {
                new TimingDef { Name = "AggrPwrDownDly",                HiBit = 30  ,   LoBit = 25  },
                new TimingDef { Name = "PwrDownDly",                    HiBit = 24  ,   LoBit = 17  },
                new TimingDef { Name = "PD",                            HiBit = 4   ,   LoBit = 0   },
            }},
            { 0x50250, new[] {
                new TimingDef { Name = "STAG",                          HiBit = 23  ,   LoBit = 16  },
            }},
            { 0x50254, new[] {
                new TimingDef { Name = "CKE",                           HiBit = 28  ,   LoBit = 24  },
                new TimingDef { Name = "CPDED",                         HiBit = 19  ,   LoBit = 16  },
                new TimingDef { Name = "XP",                            HiBit = 5   ,   LoBit = 0   },
            }},
            { 0x50258, new[] {
                new TimingDef { Name = "PARINL",                        HiBit = 29  ,   LoBit = 28  },
                new TimingDef { Name = "PHYWRD",                        HiBit = 26  ,   LoBit = 24  },
                new TimingDef { Name = "PHYRDL",                        HiBit = 21  ,   LoBit = 16  },
                new TimingDef { Name = "PHYWRL",                        HiBit = 12  ,   LoBit = 8   },
                new TimingDef { Name = "RDDATAEN",                      HiBit = 6   ,   LoBit = 0   },
            }},
            { 0x5025C, new[] {
                new TimingDef { Name = "TgearHold",                     HiBit = 30  ,   LoBit = 28  },
                new TimingDef { Name = "TgearSetup",                    HiBit = 26  ,   LoBit = 24  },
                new TimingDef { Name = "LpExitDly",                     HiBit = 13  ,   LoBit = 8   },
                new TimingDef { Name = "LpDly",                         HiBit = 5   ,   LoBit = 0   },
            }},
            { 0x5028C, new[] {
                new TimingDef { Name = "WRMPR",                         HiBit = 29  ,   LoBit = 24  },
                new TimingDef { Name = "CmdStgCnt",                     HiBit = 21  ,   LoBit = 11  },
                new TimingDef { Name = "RcvrWait",                      HiBit = 10  ,   LoBit = 0   },
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
            { 0x502A4, new[] {
                new TimingDef { Name = "WRPRE",                         HiBit = 10  ,   LoBit = 8   },
                new TimingDef { Name = "RDPRE",                         HiBit = 2   ,   LoBit = 0   },
            }},
            { 0x50DF0, new[] {
                new TimingDef { Name = "DdrMaxRate",                    HiBit = 7   ,   LoBit = 0   },
            }},
            { 0x50DF4, new[] {
                new TimingDef { Name = "DdrMaxRateEnf",                 HiBit = 7   ,   LoBit = 0   },
            }},
        };
    }
}
