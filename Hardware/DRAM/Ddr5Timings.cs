using System;
using ZenStates.Core.Dictionaries;

namespace ZenStates.Core.Hardware.DRAM
{
    [Serializable]
    public class Ddr5Timings : BaseDramTimings
    {
        public readonly struct NitroSettings
        {
            public byte RxDatChnDlyMode { get; }
            public byte TxDatChnDlyMode { get; }
            public byte CtrlLineDlyMode { get; }

            public byte RxData { get; }
            public byte TxData { get; }
            public byte CtrlLine { get; }

            public NitroSettings(uint registerValue)
            {
                CtrlLine = (byte)(registerValue & 0x3);
                TxData = (byte)((registerValue >> 4) & 0x3);
                RxData = (byte)((registerValue >> 8) & 0x3);
                CtrlLineDlyMode = (byte)((registerValue >> 16) & 0x1);
                TxDatChnDlyMode = (byte)((registerValue >> 17) & 0x1);
                RxDatChnDlyMode = (byte)((registerValue >> 18) & 0x1);
            }

            public override string ToString()
            {
                return $"{RxData}/{TxData}/{CtrlLine}";
            }
        }

        public Ddr5Timings(Cpu cpu) : base(cpu)
        {
            this.Dict = DDR5Dictionary.defs;
        }

        // 0x50100
        public uint DimmEccEn { get; internal set; }
        public uint BurstCtrl { get; internal set; }
        public uint BurstLength { get; internal set; }

        // 0x5012C
        public uint AggrPwrDownEn { get; internal set; }
        public uint PowerDownMode { get; internal set; }

        // 0x50130
        public uint OdtsIncRefEn { get; internal set; }
        public uint OdtsEn { get; internal set; }
        public uint ForcePwrDownThrotEn { get; internal set; }
        public uint OdtsCmdThrotEn { get; internal set; }
        public uint I2CThermEvent { get; internal set; }
        public uint OdtsCmdThrotCyc { get; internal set; }
        public uint RollWindowDepth { get; internal set; }

        // 0x50200
        public uint UclkGtFclk { get; internal set; }
        public uint WckRatioMode { get; internal set; }
        public uint BankGroupEn { get; internal set; }

        // 0x50208
        public uint RPpb { get; internal set; }
        public uint RCpb { get; internal set; }

        // 0x5021C
        public uint PPD { get; internal set; }

        // 0x50220
        public uint RDRDBan { get; internal set; }

        // 0x50224
        public uint WRWRBan { get; internal set; }

        // 0x50228
        public uint MW { get; internal set; }

        // 0x5022C
        public uint ShortInit { get; internal set; }
        public uint ZqcsInterval { get; internal set; }
        public uint Tzqcs { get; internal set; }

        // 0x50230
        public uint OdtsReadInterval { get; internal set; }

        // 0x50238
        public uint DLLK { get; internal set; }
        public uint XS { get; internal set; }

        // 0x5023C
        public uint RankBusyDly { get; internal set; }
        public uint CmdParLatency { get; internal set; }
        public uint AlertParDly { get; internal set; }
        public uint AlertCrcDly { get; internal set; }

        // 0x50240
        public uint MRRI { get; internal set; }
        public uint MRR { get; internal set; }
        public uint MRW { get; internal set; }
        public uint CtrlSwitchClks { get; internal set; }

        // 0x50244
        public uint AggrPwrDownDly { get; internal set; }
        public uint CSH { get; internal set; }
        public uint PwrDownDly { get; internal set; }
        public uint PD { get; internal set; }

        // 0x50248
        public uint SRX2SRX { get; internal set; }
        public uint STAB { get; internal set; }

        // 0x5024C
        public uint AlertCrcPulse { get; internal set; }
        public uint AlertParPulse { get; internal set; }

        // 0x50254
        public uint CPDED { get; internal set; }
        public uint CACSH { get; internal set; }

        // 0x50258
        public uint PARINL { get; internal set; }
        public uint RDDATAEN { get; internal set; }

        // 0x5025C
        public uint LpExitDly { get; internal set; }
        public uint LpDly { get; internal set; }

        //// 0x50278
        //public uint CombinationalBypass_Master { get; internal set; }
        //public uint SkipToFreqAdjAckForBypass { get; internal set; }
        //public uint ReadLookAhead_Master { get; internal set; }
        //public uint ReadSync_Master { get; internal set; }
        //public uint WriteLookAhead_Master { get; internal set; }
        //public uint WriteSync_Master { get; internal set; }

        //// 0x5027C
        //public uint CombinationalBypass_Slave { get; internal set; }
        //public uint ReadLookAhead_Slave { get; internal set; }
        //public uint ReadSync_Slave { get; internal set; }
        //public uint WriteLookAhead_Slave { get; internal set; }
        //public uint WriteSync_Slave { get; internal set; }

        // 0x50288
        public uint PHYUPD_CmdDly { get; internal set; }
        public uint PHYUPD_WrDatDly { get; internal set; }
        public uint PHYUPD_resp { get; internal set; }

        // 0x5028C
        public uint WRMPR { get; internal set; }
        public uint CmdStgCnt { get; internal set; }
        public uint RcvrWait { get; internal set; }

        // 0x50294
        public uint WCK_en_fs { get; internal set; }
        public uint WCK_en_rd { get; internal set; }
        public uint WCK_en_wr { get; internal set; }

        // 0x50298
        public uint WCK_dis { get; internal set; }
        public uint WCK_toggle_post { get; internal set; }
        public uint WCK_toggle { get; internal set; }

        // 0x5029C
        public uint WCK_toggle_rd { get; internal set; }
        public uint WCK_toggle_wr { get; internal set; }

        // 0x502A0
        public uint DRAM_clk_enable { get; internal set; }
        public uint DRAM_clk_disable { get; internal set; }

        // 0x502A8
        public uint ECSc { get; internal set; }

        // 0x50DF0
        public uint DdrMaxRate { get; internal set; }
        public uint DdrMaxRateEnf { get; internal set; }

        public uint RFCsb { get; private set; }

        public NitroSettings Nitro { get; private set; }

        public new float RFCns
        {
            get
            {
                if (RefreshMode == BankRefreshMode.NORMAL)
                {
                    return Utils.ToNanoseconds(RFC, Frequency);
                }

                return Utils.ToNanoseconds(RFC2, Frequency);
            }
        }

        public override void ReadRatio(uint offset = 0)
        {
            if (TryReadRegister(offset | 0x50200, out uint ratioReg))
            {
                Ratio = Utils.BitSlice(ratioReg, 15, 0) / 100.0f;
            }
        }

        public override void Read(uint offset = 0)
        {
            base.Read(offset);

            // TRFC
            // define as separate variables to avoid false-positives on virus scans
            TryReadRegister(offset | 0x50260, out uint trfcTimings0);
            TryReadRegister(offset | 0x50264, out uint trfcTimings1);
            TryReadRegister(offset | 0x50268, out uint trfcTimings2);
            TryReadRegister(offset | 0x5026C, out uint trfcTimings3);

            uint trfcRegValue = 0;
            bool trfcFound = false;
            uint[] ddr5Regs = new[] { trfcTimings0, trfcTimings1, trfcTimings2, trfcTimings3 };

            foreach (uint reg in ddr5Regs)
            {
                if (reg > 0 && reg != 0x00C00138)
                {
                    trfcRegValue = reg;
                    trfcFound = true;
                    break;
                }
            }

            if (trfcFound)
            {
                RFC = Utils.BitSlice(trfcRegValue, 15, 0);
                RFC2 = Utils.BitSlice(trfcRegValue, 31, 16);
            }

            // TRFCsb
            // Failed read on any of these would result in a 0 value,
            // so we can just read them all and take the first non-zero value
            TryReadRegister(offset | 0x502c0, out uint trfcsbTimings0);
            TryReadRegister(offset | 0x502c4, out uint trfcsbTimings1);
            TryReadRegister(offset | 0x502c8, out uint trfcsbTimings2);
            TryReadRegister(offset | 0x502cc, out uint trfcsbTimings3);

            ddr5Regs = new[] {
                Utils.BitSlice(trfcsbTimings0, 10, 0),
                Utils.BitSlice(trfcsbTimings1, 10, 0),
                Utils.BitSlice(trfcsbTimings2, 10, 0),
                Utils.BitSlice(trfcsbTimings3, 10, 0)
            };

            foreach (uint value in ddr5Regs)
            {
                if (value != 0)
                {
                    RFCsb = value;
                    break;
                }
            }

            if (TryReadRegister(offset | 0x50284, out uint nitroReg))
            {
                Nitro = new NitroSettings(nitroReg);
            }

            // Refresh mode
            if (TryReadRegister(offset | 0x5012C, out uint refreshModeValue))
            {
                FGR = Utils.BitSlice(refreshModeValue, 18, 16);
                //var allBankRefresh = Utils.GetBit(refreshModeValue, 19);
                var perBankRefresh = Utils.GetBit(refreshModeValue, 1);


                if (/*allBankRefresh == 1 && */perBankRefresh == 0)
                {
                    if (FGR == 0)
                        RefreshMode = BankRefreshMode.NORMAL;
                    else
                        RefreshMode = BankRefreshMode.FGR;
                }
                else if (/*allBankRefresh == 1 && */perBankRefresh == 1)
                {
                    if (FGR != 0)
                        RefreshMode = BankRefreshMode.MIXED;
                    else
                        RefreshMode = BankRefreshMode.PBONLY;
                }
            }
        }
    }
}
