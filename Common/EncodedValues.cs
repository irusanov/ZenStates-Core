using System.Collections.Generic;
using System.Globalization;
using ZenStates.Core.Dictionaries;

namespace ZenStates.Core.Common
{
    public class ProcOdt : EncodedValueBase
    {
        public ProcOdt(int value) : base(value) { }
        protected override Dictionary<int, string> Lookup { get; } = EncodedValueDictionaries.ProcOdtDict;
    }

    public class ProcDataDrvStren : EncodedValueBase
    {
        public ProcDataDrvStren(int value) : base(value) { }
        protected override Dictionary<int, string> Lookup { get; } = EncodedValueDictionaries.ProcDataDrvStrenDict;
    }

    public class DramDataDrvStren : EncodedValueBase
    {
        public DramDataDrvStren(int value) : base(value) { }
        protected override Dictionary<int, string> Lookup { get; } = EncodedValueDictionaries.DramDataDrvStrenDict;
    }

    public class CadBusDrvStren : EncodedValueBase
    {
        public CadBusDrvStren(int value) : base(value) { }
        protected override Dictionary<int, string> Lookup { get; } = EncodedValueDictionaries.CadBusDrvStrenDict;
    }

    public class ProcOdtImpedance : EncodedValueBase
    {
        public ProcOdtImpedance(int value) : base(value) { }
        protected override Dictionary<int, string> Lookup { get; } = EncodedValueDictionaries.ProcImpedanceDict;
    }

    public class GroupOdtImpedance : EncodedValueBase
    {
        public GroupOdtImpedance(int value) : base(value) { }
        protected override Dictionary<int, string> Lookup { get; } = EncodedValueDictionaries.GroupOdtImpedanceDict;
    }

    public class Rtt : EncodedValueBase
    {
        public Rtt(int value) : base(value) { }
        protected override Dictionary<int, string> Lookup { get; } = EncodedValueDictionaries.RttDict;

        public override string ToString()
        {
            string value = base.ToString();

            if (this.RawValue > 0 && Lookup.ContainsKey(this.RawValue.Value))
                return $"{value} ({240 / RawValue})";
            return $"{value}";
        }
    }

    public class Ddr4ProcOdt : EncodedValueBase
    {
        public Ddr4ProcOdt(int value) : base(value) { }
        protected override Dictionary<int, string> Lookup { get; } = EncodedValueDictionaries.Ddr4ProcOdtDict;
    }

    public class Ddr4DrvStren : EncodedValueBase
    {
        public Ddr4DrvStren(int value) : base(value) { }
        protected override Dictionary<int, string> Lookup { get; } = EncodedValueDictionaries.Ddr4DrvStrenDict;
    }

    public class Ddr4Rtt : EncodedValueBase
    {
        // RZQ divisor of each encoding, 0 when off
        private static readonly int[] Divisors = { 0, 4, 2, 6, 1, 5, 3, 7 };

        public Ddr4Rtt(int value) : base(value) { }
        protected override Dictionary<int, string> Lookup { get; } = EncodedValueDictionaries.Ddr4RttDict;

        public override string ToString()
        {
            return Ddr4RttFormat.Format(base.ToString(), RawValue, Divisors);
        }
    }

    public class Ddr4RttWr : EncodedValueBase
    {
        // RZQ divisor of each encoding, 0 when off or Hi-Z
        private static readonly int[] Divisors = { 0, 2, 1, 0, 3 };

        public Ddr4RttWr(int value) : base(value) { }
        protected override Dictionary<int, string> Lookup { get; } = EncodedValueDictionaries.Ddr4RttWrDict;

        public override string ToString()
        {
            return Ddr4RttFormat.Format(base.ToString(), RawValue, Divisors);
        }
    }

    internal static class Ddr4RttFormat
    {
        // DDR4 RZQ = 240 ohm
        public static string Format(string value, int? rawValue, int[] divisors)
        {
            if (rawValue >= 0 && rawValue < divisors.Length && divisors[rawValue.Value] > 0)
                return $"{value} ({240 / divisors[rawValue.Value]})";
            return value;
        }
    }

    /// <summary>
    /// DDR4 AddrCmd / CsOdt / Cke setup time: the raw value as entered in the BIOS, with the PHY coarse (1/2 MEMCLK)
    /// and fine (1/64 MEMCLK) delay, e.g. "56 (1/24)".
    /// </summary>
    public class Ddr4Setup : EncodedValueBase
    {
        private static readonly Dictionary<int, string> Empty = new Dictionary<int, string>();

        public Ddr4Setup(int value) : base(value) { }
        protected override Dictionary<int, string> Lookup { get; } = Empty;

        public override string ToString()
        {
            if (IsNull)
                return "N/A";
            int value = RawValue.Value;
            if (value == 0)
                return "0";
            return $"{value} ({value / 32}/{value % 32})";
        }
    }

    /// <summary>
    /// DRAM Vref as the APCB / APOB hold it, the mode register code: DDR5 MR10 / MR11 / MR12 (VrefDQ / VrefCA /
    /// VrefCS) 97.5% of VDDQ minus 0.5% per step, codes above 0x7D reserved; LPDDR5 MR12 / MR14 / MR15 10% plus 0.5%
    /// per step. Shown as a percentage of VDDQ with the raw code, e.g. "71.5% (0x34)".
    /// </summary>
    public class DramVref : EncodedValueBase
    {
        private static readonly Dictionary<int, string> Empty = new Dictionary<int, string>();
        private readonly bool lpddr5;

        public DramVref(int value, bool lpddr5 = false) : base(value)
        {
            this.lpddr5 = lpddr5;
        }

        protected override Dictionary<int, string> Lookup { get; } = Empty;

        /// <summary>Vref in % of VDDQ, null for a reserved code.</summary>
        public double? Percent
        {
            get
            {
                if (IsNull)
                    return null;
                int code = RawValue.Value;
                if (lpddr5)
                    return code <= 0x7F ? 10.0 + code * 0.5 : (double?)null;
                return code <= 0x7D ? 97.5 - code * 0.5 : (double?)null;
            }
        }

        public override string ToString()
        {
            if (IsNull)
                return "N/A";
            double? percent = Percent;
            string raw = "0x" + RawValue.Value.ToString("X2", CultureInfo.InvariantCulture);
            return percent.HasValue ? percent.Value.ToString("F1", CultureInfo.InvariantCulture) + "% (" + raw + ")" : "N/A (" + raw + ")";
        }
    }

    /// <summary>
    /// The receiver Vref of the memory PHY (PMU PhyVref): VDDQ x code / 128, shown as a percentage of VDDQ with the
    /// raw code, e.g. "72.7% (0x5D)".
    /// </summary>
    public class PhyVref : EncodedValueBase
    {
        private static readonly Dictionary<int, string> Empty = new Dictionary<int, string>();

        public PhyVref(int value) : base(value) { }

        protected override Dictionary<int, string> Lookup { get; } = Empty;

        public double? Percent
        {
            get { return IsNull ? (double?)null : RawValue.Value * 100.0 / 128; }
        }

        public override string ToString()
        {
            if (IsNull)
                return "N/A";
            return Percent.Value.ToString("F1", CultureInfo.InvariantCulture) + "% (0x" +
                RawValue.Value.ToString("X2", CultureInfo.InvariantCulture) + ")";
        }
    }

    public class Voltage
    {
        protected int Value;
        public Voltage(int value)
        {
            Value = value;
        }

        public int RawValue { get { return Value; } }

        public override string ToString()
        {
            return string.Format(CultureInfo.GetCultureInfo("en-US"), "{0:F4}V", Value / 1000.0);
        }
    }
}
