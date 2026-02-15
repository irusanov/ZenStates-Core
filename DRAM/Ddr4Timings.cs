using System;

namespace ZenStates.Core.DRAM
{
    [Serializable]
    public class Ddr4Timings : BaseDramTimings
    {
        public Ddr4Timings(Cpu cpu) : base(cpu)
        {
            this.Dict = DDR4Dictionary.defs;
        }

        // Specific DDR4 timings
        public uint RFC4 { get; set; }
        public new float RFCns { get; private set; }

        public override void Read(uint offset = 0)
        {
            Ratio = Utils.GetBits(cpu.ReadDword(offset | 0x50200), 0, 7) / 3.0f;

            base.Read(offset);

            uint trfcTimings0 = this.cpu.ReadDword(offset | 0x50260);
            uint trfcTimings1 = this.cpu.ReadDword(offset | 0x50264);
            uint trfcRegValue = trfcTimings0 != trfcTimings1 ? (trfcTimings0 != 0x21060138 ? trfcTimings0 : trfcTimings1) : trfcTimings0;

            if (trfcRegValue != 0)
            {
                RFC = Utils.BitSlice(trfcRegValue, 10, 0);
                RFC2 = Utils.BitSlice(trfcRegValue, 21, 11);
                RFC4 = Utils.BitSlice(trfcRegValue, 31, 22);
            }

            // Refresh mode
            uint refreshModeValue = cpu.ReadDword(offset | 0x5012C);
            FGR = Utils.BitSlice(refreshModeValue, 18, 16);
            //var allBankRefresh = Utils.GetBit(refreshModeValue, 19);

            if (FGR < 2)
            {
                RefreshMode = BankRefreshMode.NORMAL;
                RFCns = Utils.ToNanoseconds(RFC, Frequency);
            }
            else
            {
                RefreshMode = BankRefreshMode.FGR;
                if (FGR == 2)
                {
                    RFCns = Utils.ToNanoseconds(RFC2, Frequency);
                }
                else if (FGR == 4)
                {
                    RFCns = Utils.ToNanoseconds(RFC4, Frequency);
                }
            }
        }
    }
}
