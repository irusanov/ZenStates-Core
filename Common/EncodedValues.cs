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
