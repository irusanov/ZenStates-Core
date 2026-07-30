using System.Collections.Generic;
using static ZenStates.Core.DRAM.MemoryConfig;

namespace ZenStates.Core
{
    internal static class DDR5Dictionary
    {
        public static readonly Dictionary<uint, TimingDef[]> defs = new Dictionary<uint, TimingDef[]>
        {
            /*
            { 0x50050, new[] {
                new TimingDef { Name = "BGS0",   HiBit = 31, LoBit = 0 },
            }},
            { 0x50058, new[] {
                new TimingDef { Name = "BGS1",   HiBit = 31, LoBit = 0 },
            }},
            { 0x500D0, new[] {
                new TimingDef { Name = "BGSAlt0",   HiBit = 10, LoBit = 4 },
            }},
            { 0x500D4, new[] {
                new TimingDef { Name = "BGSAlt1",   HiBit = 10, LoBit = 4 },
            }},
            */
            { 0x50100, new[] {
                new TimingDef { Name = "DimmEccEn",                     HiBit = 12  ,   LoBit = 12  },
                new TimingDef { Name = "BurstCtrl",                     HiBit = 11  ,   LoBit = 10  },
                new TimingDef { Name = "BurstLength",                   HiBit = 9   ,   LoBit = 8   },
            }},
            { 0x5012C, new[] {
                new TimingDef { Name = "AggrPwrDownEn",                 HiBit = 30  ,   LoBit = 30  }, // Aggressive Power Down Enable
                new TimingDef { Name = "PowerDownMode",                 HiBit = 29  ,   LoBit = 29  }, // 0 = Full, 1 = Partial ?
                new TimingDef { Name = "PowerDown",                     HiBit = 28  ,   LoBit = 28  }, // PowerDown Enable
            }},
            { 0x50130, new[] {
                new TimingDef { Name = "OdtsIncRefEn",                  HiBit = 28  ,   LoBit = 28  },
                new TimingDef { Name = "OdtsEn",                        HiBit = 27  ,   LoBit = 27  },
                new TimingDef { Name = "ForcePwrDownThrotEn",           HiBit = 26  ,   LoBit = 26  },
                new TimingDef { Name = "OdtsCmdThrotEn",                HiBit = 25  ,   LoBit = 25  },
                new TimingDef { Name = "I2CThermEvent",                 HiBit = 24  ,   LoBit = 24  },
                new TimingDef { Name = "OdtsCmdThrotCyc",               HiBit = 10  ,   LoBit = 19  },
                new TimingDef { Name = "RollWindowDepth",               HiBit = 9   ,   LoBit = 0   },
            }},
            { 0x50200, new[] {
                new TimingDef { Name = "UclkGtFclk",                    HiBit = 31  ,   LoBit = 31  },
                new TimingDef { Name = "WckRatioMode",                  HiBit = 27  ,   LoBit = 26  },
                new TimingDef { Name = "GDM",                           HiBit = 18  ,   LoBit = 18  },
                new TimingDef { Name = "Cmd2T",                         HiBit = 17  ,   LoBit = 17  },
                new TimingDef { Name = "BankGroupEn",                   HiBit = 16  ,   LoBit = 16  },
                // new TimingDef { Name = "Ratio",     HiBit = 15, LoBit = 0  },
            }},
            { 0x50204, new[] {
                new TimingDef { Name = "RCDWR",                         HiBit = 29  ,   LoBit = 24  },
                new TimingDef { Name = "RCDRD",                         HiBit = 21  ,   LoBit = 16  },
                new TimingDef { Name = "RAS",                           HiBit = 14  ,   LoBit = 8   },
                new TimingDef { Name = "CL",                            HiBit = 5   ,   LoBit = 0   },
            }},
            { 0x50208, new[] {
                new TimingDef { Name = "RPpb",                          HiBit = 29  ,   LoBit = 24  },
                new TimingDef { Name = "RP",                            HiBit = 21  ,   LoBit = 16  },
                new TimingDef { Name = "RCpb",                          HiBit = 15  ,   LoBit = 8   },
                new TimingDef { Name = "RC",                            HiBit = 7   ,   LoBit = 0   },
            }},
            { 0x5020C, new[] {
                new TimingDef { Name = "RTP",                           HiBit = 28  ,   LoBit = 24  },
                new TimingDef { Name = "RRDL",                          HiBit = 12  ,   LoBit = 8   },
                new TimingDef { Name = "RRDS",                          HiBit = 4   ,   LoBit = 0   },
            }},
            { 0x50210, new[] {
                new TimingDef { Name = "FAW",                           HiBit = 7   ,   LoBit = 0   },
            }},
            { 0x50214, new[] {
                new TimingDef { Name = "WTRL",                          HiBit = 22  ,   LoBit = 16  },
                new TimingDef { Name = "WTRS",                          HiBit = 12  ,   LoBit = 8   },
                new TimingDef { Name = "CWL",                           HiBit = 5   ,   LoBit = 0   },
            }},
            { 0x50218, new[] {
                new TimingDef { Name = "WR",                            HiBit = 7   ,   LoBit = 0   },
            }},
            { 0x5021C, new[] {
                new TimingDef { Name = "TRCPAGE",                       HiBit = 31  ,   LoBit = 20  },
                new TimingDef { Name = "PPD",                           HiBit = 2   ,   LoBit = 0   },
            }},
            { 0x50220, new[] {
                new TimingDef { Name = "RDRDBan",                       HiBit = 31  ,   LoBit = 30  },
                new TimingDef { Name = "RDRDSCL",                       HiBit = 29  ,   LoBit = 24  },
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
                new TimingDef { Name = "MW",                            HiBit = 24  ,   LoBit = 28  },
                new TimingDef { Name = "RDWR",                          HiBit = 13  ,   LoBit = 8   },
                new TimingDef { Name = "WRRD",                          HiBit = 3   ,   LoBit = 0   },
            }},
            { 0x5022c, new[] {
                new TimingDef { Name = "ShortInit",                     HiBit = 29  ,   LoBit = 20  },
                new TimingDef { Name = "ZqcsInterval",                  HiBit = 19  ,   LoBit = 8   },
                new TimingDef { Name = "Tzqcs",                         HiBit = 7   ,   LoBit = 0   },
            }},
            { 0x50230, new[] {
                new TimingDef { Name = "OdtsReadInterval",              HiBit = 29  ,   LoBit = 20  },
                new TimingDef { Name = "REFI",                          HiBit = 15  ,   LoBit = 0   },
            }},
            { 0x50234, new[] {
                new TimingDef { Name = "MODPDA",                        HiBit = 29  ,   LoBit = 24  },
                new TimingDef { Name = "MRDPDA",                        HiBit = 21  ,   LoBit = 16  },
                new TimingDef { Name = "MOD",                           HiBit = 13  ,   LoBit = 8   },
                new TimingDef { Name = "MRD",                           HiBit = 5   ,   LoBit = 0   },
            }},
            { 0x50238, new[] {
                new TimingDef { Name = "DLLK",                          HiBit = 27  ,   LoBit = 16  },
                new TimingDef { Name = "XS",                            HiBit = 11  ,   LoBit = 0   },
            }},
            { 0x5023c, new[] {
                new TimingDef { Name = "RankBusyDly",                   HiBit = 31  ,   LoBit = 24  },
                new TimingDef { Name = "CmdParLatency",                 HiBit = 19  ,   LoBit = 16  },
                new TimingDef { Name = "AlertParDly",                   HiBit = 14  ,   LoBit = 8   },
                new TimingDef { Name = "AlertCrcDly",                   HiBit = 0   ,   LoBit = 7   },
            }},
            { 0x50240, new[] {
                new TimingDef { Name = "MRRI",                          HiBit = 26  ,   LoBit = 24  },
                new TimingDef { Name = "MRR",                           HiBit = 21  ,   LoBit = 16  },
                new TimingDef { Name = "MRW",                           HiBit = 13  ,   LoBit = 8   },
                new TimingDef { Name = "CtrlSwitchClks",                HiBit = 7   ,   LoBit = 0   },
            }},
            { 0x50244, new[] {
                new TimingDef { Name = "AggrPwrDownDly",                HiBit = 31  ,   LoBit = 26  },
                new TimingDef { Name = "CSH",                           HiBit = 25  ,   LoBit = 20  },
                new TimingDef { Name = "PwrDownDly",                    HiBit = 8  ,    LoBit = 19  },
                new TimingDef { Name = "PD",                            HiBit = 4  ,    LoBit = 0   },
            }},
            { 0x50248, new[] {
                new TimingDef { Name = "SRX2SRX",                       HiBit = 15  ,   LoBit = 10  },
                new TimingDef { Name = "STAB",                          HiBit = 9   ,   LoBit = 0   },
            }},
            { 0x5024c, new[] {
                new TimingDef { Name = "AlertCrcPulse",                 HiBit = 10  ,   LoBit = 8   },
                new TimingDef { Name = "AlertParPulse",                 HiBit = 6   ,   LoBit = 0   },
            }},
            { 0x50250, new[] {
                new TimingDef { Name = "STAG",                          HiBit = 26  ,   LoBit = 16  },
                new TimingDef { Name = "STAGsb",                        HiBit = 8   ,   LoBit = 0   },
            }},
            { 0x50254, new[] {
                new TimingDef { Name = "CKE",                           HiBit = 28  ,   LoBit = 24  },
                new TimingDef { Name = "CPDED",                         HiBit = 20  ,   LoBit = 16  },
                new TimingDef { Name = "CACSH",                         HiBit = 13  ,   LoBit = 8   },
                new TimingDef { Name = "XP",                            HiBit = 5   ,   LoBit = 0   },
            }},
            { 0x50258, new[] {
                new TimingDef { Name = "PARINL",                        HiBit = 29  ,   LoBit = 28  },
                new TimingDef { Name = "PHYWRD",                        HiBit = 26  ,   LoBit = 24  },
                new TimingDef { Name = "PHYRDL",                        HiBit = 23  ,   LoBit = 16  },
                new TimingDef { Name = "PHYWRL",                        HiBit = 15  ,   LoBit = 8   },
                new TimingDef { Name = "RDDATAEN",                      HiBit = 6   ,   LoBit = 0   },
            }},
            { 0x5025c, new[] {
                new TimingDef { Name = "LpExitDly",                     HiBit = 13  ,   LoBit = 8   },
                new TimingDef { Name = "LpDly",                         HiBit = 5   ,   LoBit = 0   },
            }},
            // TRFC and TRFC2 regs, one of [0x50260, 0x50264, 0x50268, 0x5026C] should be != 0x00C00138
            /*{ 0x50260, new[] {
                new TimingDef { Name = "RFC",                   HiBit = 15  ,   LoBit = 0   },
                new TimingDef { Name = "RFC2",                  HiBit = 31  ,   LoBit = 16  },
            }},*/
            /*{ 0x50278, new[] {
                new TimingDef { Name = "CombinationalBypass_Master",    HiBit = 31  ,   LoBit = 31  },
                new TimingDef { Name = "SkipToFreqAdjAckForBypass",     HiBit = 29  ,   LoBit = 29  },
                new TimingDef { Name = "ReadLookAhead_Master",          HiBit = 17  ,   LoBit = 11  },
                new TimingDef { Name = "ReadSync_Master",               HiBit = 10  ,   LoBit = 9   },
                new TimingDef { Name = "WriteLookAhead_Master",         HiBit = 8   ,   LoBit = 2   },
                new TimingDef { Name = "WriteSync_Master",              HiBit = 1   ,   LoBit = 0   },
            }},
            { 0x5027c, new[] {
                new TimingDef { Name = "CombinationalBypass_Slave",     HiBit = 31  ,   LoBit = 31  },
                new TimingDef { Name = "SkipToFreqAdjAckForBypass",     HiBit = 29  ,   LoBit = 29  },
                new TimingDef { Name = "ReadLookAhead_Slave",           HiBit = 17  ,   LoBit = 11  },
                new TimingDef { Name = "ReadSync_Slave",                HiBit = 10  ,   LoBit = 9   },
                new TimingDef { Name = "WriteLookAhead_Slave",          HiBit = 8   ,   LoBit = 2   },
                new TimingDef { Name = "WriteSync_Slave",               HiBit = 1   ,   LoBit = 0   },
            }},*/
            // Nitro settings handled separately in Ddr5Timings.cs
            /*{ 0x50284, new[] {
                new TimingDef { Name = "RxDatChnDlyMode",               HiBit = 18  ,   LoBit = 18  },
                new TimingDef { Name = "TxDatChnDlyMode",               HiBit = 17  ,   LoBit = 17  },
                new TimingDef { Name = "TxCtrlChnDlyMode",              HiBit = 16  ,   LoBit = 16  },
                new TimingDef { Name = "RxData",                        HiBit = 10  ,   LoBit = 8   },
                new TimingDef { Name = "TxData",                        HiBit = 6   ,   LoBit = 4   },
                new TimingDef { Name = "CtrlLine",                      HiBit = 2   ,   LoBit = 0   },
            }},*/
            { 0x50288, new[] {
                new TimingDef { Name = "PHYUPD_CmdDly",                 HiBit = 11  ,   LoBit = 8   },
                new TimingDef { Name = "PHYUPD_WrDatDly",               HiBit = 7   ,   LoBit = 4   },
                new TimingDef { Name = "PHYUPD_resp",                   HiBit = 3   ,   LoBit = 0   },
            }},
            { 0x5028c, new[] {
                new TimingDef { Name = "WRMPR",                         HiBit = 29  ,   LoBit = 24  },
                new TimingDef { Name = "CmdStgCnt",                     HiBit = 21  ,   LoBit = 11  },
                new TimingDef { Name = "RcvrWait",                      HiBit = 10  ,   LoBit = 0   },
            }},
            // WCK is LPDDR5/6 only
            { 0x50294, new[] {
                new TimingDef { Name = "WCK_en_fs",                     HiBit = 23  ,   LoBit = 16  },
                new TimingDef { Name = "WCK_en_rd",                     HiBit = 15  ,   LoBit = 8   },
                new TimingDef { Name = "WCK_en_wr",                     HiBit = 7   ,   LoBit = 0   },
            }},
            { 0x50298, new[] {
                new TimingDef { Name = "WCK_dis",                       HiBit = 23  ,   LoBit = 16  },
                new TimingDef { Name = "WCK_toggle_post",               HiBit = 12  ,   LoBit = 8   },
                new TimingDef { Name = "WCK_toggle",                    HiBit = 4   ,   LoBit = 0   },
            }},
            { 0x5029c, new[] {
                new TimingDef { Name = "WCK_toggle_rd",                 HiBit = 15  ,   LoBit = 8   },
                new TimingDef { Name = "WCK_toggle_wr",                 HiBit = 7   ,   LoBit = 0   },
            }},
            { 0x502a0, new[] {
                new TimingDef { Name = "DRAM_clk_enable",               HiBit = 12  ,   LoBit = 8   },
                new TimingDef { Name = "DRAM_clk_disable",              HiBit = 4   ,   LoBit = 0   },
            }},
            { 0x502a4, new[] {
                new TimingDef { Name = "WRPOST",                        HiBit = 14  ,   LoBit = 12  },
                new TimingDef { Name = "WRPRE",                         HiBit = 10  ,   LoBit = 8   },
                new TimingDef { Name = "RDPOST",                        HiBit = 6   ,   LoBit = 4   },
                new TimingDef { Name = "RDPRE",                         HiBit = 2   ,   LoBit = 0   },
            }},
            { 0x502a8, new[] {
                new TimingDef { Name = "ECSc",                          HiBit = 8   ,   LoBit = 0   },
            }},
            // RFCsb regs, one of [0x502c0, 0x502c4, 0x502c8, 0x502cc] should be != 0
            /*{ 0x502c0, new[] {
                new TimingDef { Name = "REFSBRD",               HiBit = 23  ,   LoBit = 16  },
                new TimingDef { Name = "RFCsb",                 HiBit = 10  ,   LoBit = 0   },
            }},*/
            { 0x50df0, new[] {
                new TimingDef { Name = "DdrMaxRate",                    HiBit = 7   ,   LoBit = 0   },
            }},
            { 0x50df4, new[] {
                new TimingDef { Name = "DdrMaxRateEnf",                 HiBit = 7   ,   LoBit = 0   },
            }},
        };
    }
}
