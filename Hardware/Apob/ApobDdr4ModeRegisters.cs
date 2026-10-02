using System.Collections.Generic;
using System.Globalization;
using System.Text;
using ZenStates.Core.Common;

namespace ZenStates.Core.Hardware.Apob
{
    /// <summary>
    /// The DDR4 mode registers MR0 to MR6 (u16 each) the ABL programs for a memory P-state, from the timing blocks of
    /// the GEN configuration info entry (Summit Ridge to Vermeer). They follow the RFC values at a program dependent
    /// distance (Matisse: 0x26 bytes after the clock bytes, Summit Ridge 0x2C, Cezanne 0x62) and are found by checking
    /// them against the timings of the block: MR0 CL / WR, MR2 CWL, MR6 tCCD_L and MR1 DLL enable.
    /// Fields (JESD79-4):
    /// <list type="bullet">
    /// <item>MR1: A2:1 output driver impedance (RZQ/7, RZQ/5), A10:8 RTT_NOM.</item>
    /// <item>MR2: A5:3 CWL, A11:9 RTT_WR.</item>
    /// <item>MR5: A8:6 RTT_PARK.</item>
    /// <item>MR6: A5:0 VrefDQ value, A6 range (0 = range 1 from 60%, 1 = range 2 from 45% of VDDQ, 0.65% steps),
    /// A12:10 tCCD_L.</item>
    /// </list>
    /// The VrefDQ is the value training starts from; the PMU trains it per device.
    /// https://github.com/oxidecomputer/amd-apcb
    /// </summary>
    public sealed class ApobDdr4ModeRegisters
    {
        /// <summary>MR0 to MR6.</summary>
        public const int Count = 7;

        // MR0 CL, index A12 A6 A5 A4 A2 (JESD79-4 table 3); 0 reserved
        private static readonly int[] ClCodes =
        {
            9, 10, 11, 12, 13, 14, 15, 16, 18, 20, 22, 24, 23, 17, 19, 21,
            25, 26, 27, 28, 0, 30, 0, 32
        };

        // MR0 WR, index A13 A11 A10 A9
        private static readonly int[] WrCodes = { 10, 12, 14, 16, 18, 20, 24, 22, 26, 28 };

        // MR2 CWL, index A5:3
        private static readonly int[] CwlCodes = { 9, 10, 11, 12, 14, 16, 18, 20 };

        private readonly ushort[] values;

        private ApobDdr4ModeRegisters(ushort[] values)
        {
            this.values = values;
        }

        /// <summary>The value of MR<paramref name="index"/>.</summary>
        public ushort this[int index]
        {
            get { return values[index]; }
        }

        /// <summary>MR0: CAS latency, 0 when reserved.</summary>
        public int Cl
        {
            get
            {
                int mr0 = values[0];
                int code = ((mr0 >> 12) & 1) << 4 | ((mr0 >> 4) & 7) << 1 | ((mr0 >> 2) & 1);
                return code < ClCodes.Length ? ClCodes[code] : 0;
            }
        }

        /// <summary>MR0: write recovery, 0 when reserved.</summary>
        public int Wr
        {
            get
            {
                int mr0 = values[0];
                int code = ((mr0 >> 13) & 1) << 3 | ((mr0 >> 9) & 7);
                return code < WrCodes.Length ? WrCodes[code] : 0;
            }
        }

        /// <summary>MR2: CAS write latency.</summary>
        public int Cwl { get { return CwlCodes[(values[2] >> 3) & 7]; } }

        /// <summary>MR6: tCCD_L, 0 when reserved.</summary>
        public int Tccdl
        {
            get
            {
                int code = (values[6] >> 10) & 7;
                return code <= 4 ? code + 4 : 0;
            }
        }

        /// <summary>MR1 A2:1: DRAM output driver impedance in ohm, 0 when reserved.</summary>
        public int DriverImpedance
        {
            get
            {
                switch ((values[1] >> 1) & 3)
                {
                    case 0: return 34;
                    case 1: return 48;
                    default: return 0;
                }
            }
        }

        public Ddr4Rtt RttNom { get { return new Ddr4Rtt((values[1] >> 8) & 7); } }
        public Ddr4RttWr RttWr { get { return new Ddr4RttWr((values[2] >> 9) & 7); } }
        public Ddr4Rtt RttPark { get { return new Ddr4Rtt((values[5] >> 6) & 7); } }

        /// <summary>MR6 A6: VrefDQ range 2 (45% to 77.5%) instead of range 1 (60% to 92.5%).</summary>
        public bool VrefDqRange2 { get { return (values[6] & 0x40) != 0; } }

        /// <summary>MR6: VrefDQ in % of VDDQ, null for a reserved value.</summary>
        public double? VrefDq
        {
            get
            {
                int value = values[6] & 0x3F;
                if (value > 50)
                    return null;
                return (VrefDqRange2 ? 45.0 : 60.0) + value * 0.65;
            }
        }

        /// <summary>The decoded settings in display order, the register in brackets.</summary>
        public List<KeyValuePair<string, string>> GetSettings()
        {
            double? vref = VrefDq;
            return new List<KeyValuePair<string, string>>
            {
                Setting("VrefDQ (MR6)", (vref.HasValue ? vref.Value.ToString("F2", CultureInfo.InvariantCulture) + "%" : "N/A") +
                    " (0x" + (values[6] & 0x3F).ToString("X2", CultureInfo.InvariantCulture) + ", range " + (VrefDqRange2 ? "2" : "1") + ")"),
                Setting("RttNom (MR1)", RttNom.ToString()),
                Setting("RttWr (MR2)", RttWr.ToString()),
                Setting("RttPark (MR5)", RttPark.ToString()),
                Setting("DramDs (MR1)", DriverImpedance > 0 ? DriverImpedance + " ohm" : "N/A"),
            };
        }

        private static KeyValuePair<string, string> Setting(string name, string value)
        {
            return new KeyValuePair<string, string>(name, value);
        }

        /// <summary>The registers as hex words, MR0 first.</summary>
        public string ToHexString()
        {
            var sb = new StringBuilder(Count * 5);
            for (int i = 0; i < Count; i++)
            {
                if (i > 0)
                    sb.Append(' ');
                sb.Append(values[i].ToString("X4", CultureInfo.InvariantCulture));
            }
            return sb.ToString();
        }

        /// <summary>
        /// Looks for the registers between <paramref name="start"/> and <paramref name="end"/> (the last start
        /// offset tried): MR0 to MR6 whose CL, WR, CWL and tCCD_L are the timings of the block. Null when not found.
        /// </summary>
        internal static ApobDdr4ModeRegisters Find(byte[] buffer, uint start, uint end, ulong bufferEnd,
            uint? cl, uint? cwl, uint? wr, uint? tccdl)
        {
            if (!cl.HasValue || !cwl.HasValue || !wr.HasValue || !tccdl.HasValue)
                return null;

            for (uint o = start; o <= end && (ulong)o + Count * 2 <= bufferEnd; o++)
            {
                var words = new ushort[Count];
                for (int i = 0; i < Count; i++)
                    words[i] = (ushort)(buffer[o + i * 2] | buffer[o + i * 2 + 1] << 8);

                var registers = new ApobDdr4ModeRegisters(words);
                // WR and CWL have no code for every value, the next one up is programmed then
                if (registers.Cl == cl.Value && registers.Tccdl == tccdl.Value && (words[1] & 1) == 1 &&
                    registers.Wr >= wr.Value && registers.Wr <= wr.Value + 2 &&
                    registers.Cwl >= cwl.Value && registers.Cwl <= cwl.Value + 2)
                    return registers;
            }

            return null;
        }
    }
}
