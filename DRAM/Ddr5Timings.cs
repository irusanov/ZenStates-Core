using System;
using static ZenStates.Core.DRAM.MemoryConfig;

namespace ZenStates.Core.DRAM
{
    [Serializable]
    public class Ddr5Timings : BaseDramTimings
    {
        public readonly struct NitroSettings
        {
            public byte RxData { get; }
            public byte TxData { get; }
            public byte CtrlLine { get; }

            public NitroSettings(uint registerValue)
            {
                CtrlLine = (byte)(registerValue & 0x3);
                TxData = (byte)((registerValue >> 4) & 0x3);
                RxData = (byte)((registerValue >> 8) & 0x3);
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

        public uint RFCsb { get; private set; }

        public NitroSettings Nitro { get; private set; }

        public override void Read(uint offset = 0)
        {
            Ratio = Utils.BitSlice(cpu.ReadDword(offset | 0x50200), 15, 0) / 100.0f;

            base.Read(offset);

            // TRFC
            // define as separate variables to avoid false-positives on virus scans
            uint trfcTimings0 = cpu.ReadDword(offset | 0x50260);
            uint trfcTimings1 = cpu.ReadDword(offset | 0x50264);
            uint trfcTimings2 = cpu.ReadDword(offset | 0x50268);
            uint trfcTimings3 = cpu.ReadDword(offset | 0x5026C);
            uint trfcRegValue = 0;

            uint[] ddr5Regs = new[] { trfcTimings0, trfcTimings1, trfcTimings2, trfcTimings3 };
            foreach (uint reg in ddr5Regs)
            {
                if (reg != 0x00C00138)
                {
                    trfcRegValue = reg;
                    break;
                }
            }

            if (trfcRegValue != 0)
            {
                RFC = Utils.BitSlice(trfcRegValue, 15, 0);
                RFC2 = Utils.BitSlice(trfcRegValue, 31, 16);
            }

            // TRFCsb
            trfcTimings0 = Utils.BitSlice(cpu.ReadDword(offset | 0x502c0), 10, 0);
            trfcTimings1 = Utils.BitSlice(cpu.ReadDword(offset | 0x502c4), 10, 0);
            trfcTimings2 = Utils.BitSlice(cpu.ReadDword(offset | 0x502c8), 10, 0);
            trfcTimings3 = Utils.BitSlice(cpu.ReadDword(offset | 0x502cc), 10, 0);
            ddr5Regs = new[] { trfcTimings0, trfcTimings1, trfcTimings2, trfcTimings3 };

            foreach (uint value in ddr5Regs)
            {
                if (value != 0)
                {
                    RFCsb = value;
                    break;
                }
            }

            uint nitroSettings = Utils.BitSlice(cpu.ReadDword(offset | 0x50284), 11, 0);
            Nitro = new NitroSettings(nitroSettings);
        }
    }
}
