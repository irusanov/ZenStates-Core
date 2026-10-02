using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using ZenStates.Core.Common;

namespace ZenStates.Core.Hardware.Apob
{
    /// <summary>
    /// The LPDDR5 mode registers MR0 to MR41 the ABL programs for a memory P-state, from the timing blocks of the GEN
    /// configuration info entry (Rembrandt). The read-only registers (MR0, MR4 to MR9) are 0. The fields follow
    /// JESD209-5:
    /// <list type="bullet">
    /// <item>MR3: OP[2:0] PDDS, OP[4:3] bank organisation, OP[5] WLS, OP[6] DBI-RD, OP[7] DBI-WR.</item>
    /// <item>MR11: OP[2:0] DQ ODT, OP[3] NT-ODT enable, OP[6:4] CA ODT.</item>
    /// <item>MR12 / MR14 / MR15: VREF(CA) / VREF(DQ[7:0]) / VREF(DQ[15:8]), OP[6:0] 10% of VDDQ + 0.5% per step.</item>
    /// <item>MR17: OP[2:0] SoC ODT, OP[3] ODTD-CK, OP[4] ODTD-CS, OP[5] ODTD-CA (1 = that ODT disabled).</item>
    /// <item>MR18: OP[2:0] WCK ODT, OP[3] WCK FM, OP[4] WCK ON, OP[7] CKR (0 = WCK:CK 4:1, 1 = 2:1).</item>
    /// <item>MR41: OP[7:5] NT DQ ODT.</item>
    /// </list>
    /// The impedances are RZQ / code with RZQ = 240 ohm, code 0 is off. Worked out on a 6800HS with LPDDR5-6400: MR18
    /// CKR matches the WCK ratio of every block (4:1 at 6400, 2:1 at 3200 and 1600), MR3 switches to 16 bank mode below
    /// 3200 MT/s and the CA ODT is off (MR11) where MR17 disables it at 1600. The Vref values are the ones the ABL
    /// starts training with, equal on all channels.
    /// </summary>
    public sealed class ApobLpddr5ModeRegisters
    {
        /// <summary>MR0 to MR41.</summary>
        public const int Count = 42;

        private readonly byte[] values;

        internal ApobLpddr5ModeRegisters(byte[] buffer, uint offset)
        {
            values = new byte[Count];
            Buffer.BlockCopy(buffer, (int)offset, values, 0, Count);
        }

        /// <summary>The value of MR<paramref name="index"/>.</summary>
        public byte this[int index]
        {
            get { return values[index]; }
        }

        public byte[] RawBytes
        {
            get { return (byte[])values.Clone(); }
        }

        /// <summary>MR3 OP[2:0]: DRAM pull-down drive strength.</summary>
        public Rtt Pdds { get { return new Rtt(values[3] & 0x07); } }

        /// <summary>MR3 OP[4:3]: BG (bank groups), 8B or 16B mode.</summary>
        public string BankMode
        {
            get
            {
                switch ((values[3] >> 3) & 0x03)
                {
                    case 0: return "BG";
                    case 1: return "8B";
                    case 2: return "16B";
                    default: return "RFU";
                }
            }
        }

        public bool ReadDbi { get { return (values[3] & 0x40) != 0; } }
        public bool WriteDbi { get { return (values[3] & 0x80) != 0; } }

        /// <summary>MR11 OP[2:0]: DQ ODT of the DRAM.</summary>
        public Rtt DqOdt { get { return new Rtt(values[11] & 0x07); } }

        /// <summary>MR11 OP[3]: non-target ODT enabled.</summary>
        public bool NtOdtEnabled { get { return (values[11] & 0x08) != 0; } }

        /// <summary>MR41 OP[7:5]: non-target DQ ODT, off when MR11 does not enable it.</summary>
        public Rtt NtDqOdt { get { return new Rtt(NtOdtEnabled ? (values[41] >> 5) & 0x07 : 0); } }

        /// <summary>MR11 OP[6:4]: CA ODT of the DRAM.</summary>
        public Rtt CaOdt { get { return new Rtt((values[11] >> 4) & 0x07); } }

        /// <summary>MR17 OP[2:0]: the ODT of the SoC (memory controller) the DRAM is told about.</summary>
        public Rtt SocOdt { get { return new Rtt(values[17] & 0x07); } }

        /// <summary>MR17 OP[3] / OP[4] / OP[5]: the CK / CS / CA ODT of the DRAM is disabled.</summary>
        public bool CkOdtDisabled { get { return (values[17] & 0x08) != 0; } }
        public bool CsOdtDisabled { get { return (values[17] & 0x10) != 0; } }
        public bool CaOdtDisabled { get { return (values[17] & 0x20) != 0; } }

        /// <summary>MR18 OP[2:0]: WCK ODT of the DRAM.</summary>
        public Rtt WckOdt { get { return new Rtt(values[18] & 0x07); } }

        /// <summary>MR18 OP[7]: WCK:CK 2 (1) or 4 (0).</summary>
        public int WckCkRatio { get { return (values[18] & 0x80) != 0 ? 2 : 4; } }

        /// <summary>MR12: VREF(CA) in % of VDDQ.</summary>
        public double VrefCa { get { return VrefPercent(values[12]); } }

        /// <summary>MR14 / MR15: VREF(DQ) of the lower / upper byte in % of VDDQ.</summary>
        public double VrefDqLower { get { return VrefPercent(values[14]); } }
        public double VrefDqUpper { get { return VrefPercent(values[15]); } }

        private static double VrefPercent(byte value)
        {
            return 10.0 + (value & 0x7F) * 0.5;
        }

        /// <summary>The decoded settings in display order, the register in brackets.</summary>
        public List<KeyValuePair<string, string>> GetSettings()
        {
            return new List<KeyValuePair<string, string>>
            {
                Setting("DQ ODT (MR11)", DqOdt.ToString()),
                Setting("NT-ODT (MR11/41)", NtDqOdt.ToString()),
                Setting("CA ODT (MR11)", CaOdtDisabled ? "Off" : CaOdt.ToString()),
                Setting("WCK ODT (MR18)", WckOdt.ToString()),
                Setting("CK/CS ODT (MR17)", string.Format("{0} / {1}", CkOdtDisabled ? "Off" : "On", CsOdtDisabled ? "Off" : "On")),
                Setting("SoC ODT (MR17)", SocOdt.ToString()),
                Setting("PDDS (MR3)", Pdds.ToString()),
                Setting("VrefCA (MR12)", Vref(12)),
                Setting("VrefDQ (MR14/15)", values[14] == values[15] ? Vref(14) : Vref(14) + " / " + Vref(15)),
                Setting("WCK:CK (MR18)", WckCkRatio + ":1"),
                Setting("Bank mode (MR3)", BankMode),
                Setting("DBI RD/WR (MR3)", string.Format("{0} / {1}", ReadDbi ? "On" : "Off", WriteDbi ? "On" : "Off")),
            };
        }

        private static KeyValuePair<string, string> Setting(string name, string value)
        {
            return new KeyValuePair<string, string>(name, value);
        }

        // The Vref of MR<index> in % of VDDQ with the raw value, e.g. "38.0% (0x38)"
        private string Vref(int index)
        {
            return VrefPercent(values[index]).ToString("F1", CultureInfo.InvariantCulture) + "% (0x" +
                values[index].ToString("X2", CultureInfo.InvariantCulture) + ")";
        }

        /// <summary>The registers as hex bytes, MR0 first.</summary>
        public string ToHexString()
        {
            var sb = new StringBuilder(Count * 3);
            for (int i = 0; i < Count; i++)
            {
                if (i > 0)
                    sb.Append(' ');
                sb.Append(values[i].ToString("X2", CultureInfo.InvariantCulture));
            }
            return sb.ToString();
        }

        internal bool SameAs(ApobLpddr5ModeRegisters other)
        {
            if (other == null)
                return false;
            for (int i = 0; i < Count; i++)
            {
                if (values[i] != other.values[i])
                    return false;
            }
            return true;
        }

        /// <summary>
        /// The registers at <paramref name="offset"/>, null when they do not look like mode registers of a block at
        /// WCK:CK <paramref name="wckRatio"/>: MR0 (read only) not 0, MR18 CKR not the ratio, or PDDS / ODT codes
        /// that JESD209-5 does not define.
        /// </summary>
        internal static ApobLpddr5ModeRegisters TryRead(byte[] buffer, uint offset, ulong end, int wckRatio)
        {
            if ((ulong)offset + Count > end)
                return null;

            var registers = new ApobLpddr5ModeRegisters(buffer, offset);
            int pdds = registers[3] & 0x07;
            bool valid = registers[0] == 0 && registers.WckCkRatio == wckRatio && pdds >= 1 && pdds <= 6 &&
                (registers[11] & 0x07) <= 6 && ((registers[11] >> 4) & 0x07) <= 6 &&
                (registers[17] & 0x07) <= 6 && (registers[18] & 0x07) <= 6;
            return valid ? registers : null;
        }
    }
}
