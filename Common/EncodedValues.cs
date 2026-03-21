using System.Collections.Generic;
using System.Globalization;

namespace ZenStates.Core
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
        protected override Dictionary<int, string> Lookup { get; } = EncodedValueDictionaries.ProcOdtImpedanceDict;
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

            if (this.RawValue > 0)
                return $"{value} ({240 / RawValue})";
            return $"{value}";
        }
    }

    public class Voltage
    {
        protected int Value;
        public Voltage(int value)
        {
            Value = value;
        }

        public override string ToString()
        {
            return string.Format(CultureInfo.GetCultureInfo("en-US"), "{0:F4}V", Value / 1000.0);
        }
    }
}
