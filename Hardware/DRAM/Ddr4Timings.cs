using System;
using ZenStates.Core.Dictionaries;

namespace ZenStates.Core.Hardware.DRAM
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

        /// <summary>
        /// Refresh rate of the fine granularity refresh mode (<see cref="BaseDramTimings.FGR"/>, encoded like the DDR4 MR3
        /// A8:A6 field): 1 normal, 2 or 4 fixed or on the fly. 0 for a reserved code.
        /// </summary>
        public uint FgrMultiplier
        {
            get
            {
                switch (FGR)
                {
                    case 0: return 1;
                    case 1: case 5: return 2;
                    case 2: case 6: return 4;
                    default: return 0;
                }
            }
        }

        /// <summary>The refresh rate switches on the fly between 1x and <see cref="FgrMultiplier"/> (FGR 5 and 6).</summary>
        public bool FgrOnTheFly
        {
            get { return FGR == 5 || FGR == 6; }
        }

        /// <summary>tRFC of the refresh mode in use (tRFC, tRFC2 or tRFC4) in ns; 0 for a reserved mode.</summary>
        public new float RFCns
        {
            get
            {
                switch (FgrMultiplier)
                {
                    case 1: return Utils.ToNanoseconds(RFC, Frequency);
                    case 2: return Utils.ToNanoseconds(RFC2, Frequency);
                    case 4: return Utils.ToNanoseconds(RFC4, Frequency);
                    default: return 0;
                }
            }
        }

        // 0x50130
        public uint SwCmdThrotEn { get; internal set; }
        public uint SwCmdThrotCyc { get; internal set; }

        // 0x50200
        public uint Preamble2t { get; internal set; }

        // 0x5025C
        public uint TgearHold { get; internal set; }
        public uint TgearSetup { get; internal set; }

        public override void ReadRatio(uint offset = 0)
        {
            if (TryReadRegister(offset | 0x50200, out uint ratioReg))
            {
                Ratio = Utils.GetBits(ratioReg, 0, 7) / 3.0f;
            }
        }

        public override void Read(uint offset = 0)
        {
            base.Read(offset);

            uint trfcRegValue = ReadTrfcRegister(offset);

            if (trfcRegValue != 0)
            {
                RFC = Utils.BitSlice(trfcRegValue, 10, 0);
                RFC2 = Utils.BitSlice(trfcRegValue, 21, 11);
                RFC4 = Utils.BitSlice(trfcRegValue, 31, 22);
            }

            // Refresh mode: 0x5012C [18:16] is the fine granularity refresh mode, encoded like DDR4 MR3 A8:A6
            // (0 normal, 1 fixed 2x, 2 fixed 4x, 5 on the fly 1x/2x, 6 on the fly 1x/4x)
            uint refreshModeValue = ReadRegister(offset | 0x5012C);
            FGR = Utils.BitSlice(refreshModeValue, 18, 16);
            //var allBankRefresh = Utils.GetBit(refreshModeValue, 19);

            RefreshMode = FGR == 0 ? BankRefreshMode.NORMAL : BankRefreshMode.FGR;
        }

        private const uint TrfcDefaultValue = 0x21060138;

        /// <summary>
        /// Reads the tRFC register of the channel's populated DIMM.
        /// The tRFC timings are per DIMM: 0x50260 for DIMM 0 (CS0/CS1) and 0x50264 for DIMM 1 (CS2/CS3).
        /// The register of an empty slot is not reliably at its reset value: a manual tRFC can be written to both,
        /// leaving tRFC2/tRFC4 of the empty slot at the defaults (ryzen_smu issue #70), so the register is chosen
        /// by the enabled chip selects and the default-value comparison is only a fallback.
        /// </summary>
        private uint ReadTrfcRegister(uint offset)
        {
            uint trfcTimings0 = ReadRegister(offset | 0x50260);
            uint trfcTimings1 = ReadRegister(offset | 0x50264);

            bool? dimm0 = IsDimmEnabled(offset, 0);
            bool? dimm1 = IsDimmEnabled(offset, 1);

            if (dimm0 == true && trfcTimings0 != 0)
                return trfcTimings0;
            if (dimm1 == true && trfcTimings1 != 0)
                return trfcTimings1;

            // Chip selects unknown (registers not readable, e.g. a debug report without them)
            return trfcTimings0 != trfcTimings1 && trfcTimings0 == TrfcDefaultValue ? trfcTimings1 : trfcTimings0;
        }

        /// <summary>Whether a chip select of the DIMM is enabled, DIMM n having CS 2n and 2n+1.</summary>
        /// <returns>Null when none of the chip select registers could be read.</returns>
        private bool? IsDimmEnabled(uint offset, int dimm)
        {
            bool? even = IsChipSelectEnabled(offset, 2 * dimm);
            bool? odd = IsChipSelectEnabled(offset, 2 * dimm + 1);

            if (even == true || odd == true)
                return true;

            return even == null && odd == null ? (bool?)null : false;
        }
    }
}
