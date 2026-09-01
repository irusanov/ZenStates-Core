using System.Globalization;
using System.Reflection;
using System.Text;
using ZenStates.Core.Common;

namespace ZenStates.Core.Hardware.Apob
{
    public sealed class ApobData : ApobBlockData
    {
        internal ApobData(byte[] data, uint offset, ApobBlockLayout layout) : base(data, offset, layout)
        {
        }

        private byte? RttNomRdRaw { get { return ReadRawValue(Layout.Offsets.RttNomRd); } }
        private byte? RttNomWrRaw { get { return ReadRawValue(Layout.Offsets.RttNomWr); } }
        private byte? RttWrRaw { get { return ReadRawValue(Layout.Offsets.RttWr); } }
        private byte? RttParkRaw { get { return ReadRawValue(Layout.Offsets.RttPark); } }
        private byte? RttParkDqsRaw { get { return ReadRawValue(Layout.Offsets.RttParkDqs); } }
        private byte? DramDataDsRaw { get { return ReadRawValue(Layout.Offsets.DramDataDs); } }

        private byte? CkOdtARaw { get { return ReadRawValue(Layout.Offsets.CkOdtA); } }
        private byte? CsOdtARaw { get { return ReadRawValue(Layout.Offsets.CsOdtA); } }
        private byte? CaOdtARaw { get { return ReadRawValue(Layout.Offsets.CaOdtA); } }
        private byte? CkOdtBRaw { get { return ReadRawValue(Layout.Offsets.CkOdtB); } }
        private byte? CsOdtBRaw { get { return ReadRawValue(Layout.Offsets.CsOdtB); } }
        private byte? CaOdtBRaw { get { return ReadRawValue(Layout.Offsets.CaOdtB); } }

        private byte? ProcOdtRaw { get { return ReadRawValue(Layout.Offsets.ProcOdt); } }
        private byte? ProcDqDsRaw { get { return ReadRawValue(Layout.Offsets.ProcDqDs); } }
        private byte? ProcCaDsRaw { get { return ReadRawValue(Layout.Offsets.ProcCaDs); } }
        private byte? ProcCkDsRaw { get { return ReadRawValue(Layout.Offsets.ProcCkDs); } }
        private byte? ProcCsDsRaw { get { return ReadRawValue(Layout.Offsets.ProcCsDs); } }

        private byte? RttNomRdP0Raw { get { return ReadRawValue(Layout.Offsets.RttNomRdP0); } }
        private byte? RttNomWrP0Raw { get { return ReadRawValue(Layout.Offsets.RttNomWrP0); } }
        private byte? RttWrP0Raw { get { return ReadRawValue(Layout.Offsets.RttWrP0); } }
        private byte? RttParkP0Raw { get { return ReadRawValue(Layout.Offsets.RttParkP0); } }
        private byte? RttParkDqsP0Raw { get { return ReadRawValue(Layout.Offsets.RttParkDqsP0); } }

        private byte? DramDqDsPullUpP0Raw { get { return ReadRawValue(Layout.Offsets.DramDqDsPullUpP0); } }
        private byte? DramDqDsPullDownP0Raw { get { return ReadRawValue(Layout.Offsets.DramDqDsPullDownP0); } }

        private byte? ProcOdtPullUpP0Raw { get { return ReadRawValue(Layout.Offsets.ProcOdtPullUpP0); } }
        private byte? ProcOdtPullDownP0Raw { get { return ReadRawValue(Layout.Offsets.ProcOdtPullDownP0); } }
        private byte? ProcDqDsPullUpP0Raw { get { return ReadRawValue(Layout.Offsets.ProcDqDsPullUpP0); } }
        private byte? ProcDqDsPullDownP0Raw { get { return ReadRawValue(Layout.Offsets.ProcDqDsPullDownP0); } }

        private byte? ProcCaOdtRaw { get { return ReadRawValue(Layout.Offsets.ProcCaOdt); } }
        private byte? ProcCkOdtRaw { get { return ReadRawValue(Layout.Offsets.ProcCkOdt); } }
        private byte? ProcDqOdtRaw { get { return ReadRawValue(Layout.Offsets.ProcDqOdt); } }
        private byte? ProcDqsOdtRaw { get { return ReadRawValue(Layout.Offsets.ProcDqsOdt); } }
        private byte? ProcDataDsApuRaw { get { return ReadRawValue(Layout.Offsets.ProcDataDsApu); } }

        public Rtt RttNomRd { get { return RttNomRdRaw.HasValue ? new Rtt(RttNomRdRaw.Value) : null; } }
        public Rtt RttNomWr { get { return RttNomWrRaw.HasValue ? new Rtt(RttNomWrRaw.Value) : null; } }
        public Rtt RttWr { get { return RttWrRaw.HasValue ? new Rtt(RttWrRaw.Value) : null; } }
        public Rtt RttPark { get { return RttParkRaw.HasValue ? new Rtt(RttParkRaw.Value) : null; } }
        public Rtt RttParkDqs { get { return RttParkDqsRaw.HasValue ? new Rtt(RttParkDqsRaw.Value) : null; } }

        public DramDataDrvStren DramDataDs { get { return DramDataDsRaw.HasValue ? new DramDataDrvStren(DramDataDsRaw.Value) : null; } }

        public GroupOdtImpedance CkOdtA { get { return CkOdtARaw.HasValue ? new GroupOdtImpedance(CkOdtARaw.Value) : null; } }
        public GroupOdtImpedance CsOdtA { get { return CsOdtARaw.HasValue ? new GroupOdtImpedance(CsOdtARaw.Value) : null; } }
        public GroupOdtImpedance CaOdtA { get { return CaOdtARaw.HasValue ? new GroupOdtImpedance(CaOdtARaw.Value) : null; } }
        public GroupOdtImpedance CkOdtB { get { return CkOdtBRaw.HasValue ? new GroupOdtImpedance(CkOdtBRaw.Value) : null; } }
        public GroupOdtImpedance CsOdtB { get { return CsOdtBRaw.HasValue ? new GroupOdtImpedance(CsOdtBRaw.Value) : null; } }
        public GroupOdtImpedance CaOdtB { get { return CaOdtBRaw.HasValue ? new GroupOdtImpedance(CaOdtBRaw.Value) : null; } }

        public ProcOdt ProcOdt { get { return ProcOdtRaw.HasValue ? new ProcOdt(ProcOdtRaw.Value) : null; } }
        public ProcOdt ProcDqDs { get { return ProcDqDsRaw.HasValue ? new ProcOdt(ProcDqDsRaw.Value) : null; } }
        public ProcOdtImpedance ProcCaDs { get { return ProcCaDsRaw.HasValue ? new ProcOdtImpedance(ProcCaDsRaw.Value) : null; } }
        public ProcOdtImpedance ProcCkDs { get { return ProcCkDsRaw.HasValue ? new ProcOdtImpedance(ProcCkDsRaw.Value) : null; } }
        public ProcOdtImpedance ProcCsDs { get { return ProcCsDsRaw.HasValue ? new ProcOdtImpedance(ProcCsDsRaw.Value) : null; } }

        public Rtt RttNomRdP0 { get { return RttNomRdP0Raw.HasValue ? new Rtt(RttNomRdP0Raw.Value) : null; } }
        public Rtt RttNomWrP0 { get { return RttNomWrP0Raw.HasValue ? new Rtt(RttNomWrP0Raw.Value) : null; } }
        public Rtt RttWrP0 { get { return RttWrP0Raw.HasValue ? new Rtt(RttWrP0Raw.Value) : null; } }
        public Rtt RttParkP0 { get { return RttParkP0Raw.HasValue ? new Rtt(RttParkP0Raw.Value) : null; } }
        public Rtt RttParkDqsP0 { get { return RttParkDqsP0Raw.HasValue ? new Rtt(RttParkDqsP0Raw.Value) : null; } }

        public DramDataDrvStren DramDqDsPullUpP0 { get { return DramDqDsPullUpP0Raw.HasValue ? new DramDataDrvStren(DramDqDsPullUpP0Raw.Value) : null; } }
        public DramDataDrvStren DramDqDsPullDownP0 { get { return DramDqDsPullDownP0Raw.HasValue ? new DramDataDrvStren(DramDqDsPullDownP0Raw.Value) : null; } }

        public ProcOdt ProcOdtPullUpP0 { get { return ProcOdtPullUpP0Raw.HasValue ? new ProcOdt(ProcOdtPullUpP0Raw.Value) : null; } }
        public ProcOdt ProcOdtPullDownP0 { get { return ProcOdtPullDownP0Raw.HasValue ? new ProcOdt(ProcOdtPullDownP0Raw.Value) : null; } }
        public ProcOdt ProcDqDsPullUpP0 { get { return ProcDqDsPullUpP0Raw.HasValue ? new ProcOdt(ProcDqDsPullUpP0Raw.Value) : null; } }
        public ProcOdt ProcDqDsPullDownP0 { get { return ProcDqDsPullDownP0Raw.HasValue ? new ProcOdt(ProcDqDsPullDownP0Raw.Value) : null; } }

        public ProcOdtImpedance ProcCaOdt { get { return ProcCaOdtRaw.HasValue ? new ProcOdtImpedance(ProcCaOdtRaw.Value) : null; } }
        public ProcOdtImpedance ProcCkOdt { get { return ProcCkOdtRaw.HasValue ? new ProcOdtImpedance(ProcCkOdtRaw.Value) : null; } }
        public ProcOdtImpedance ProcDqOdt { get { return ProcDqOdtRaw.HasValue ? new ProcOdtImpedance(ProcDqOdtRaw.Value) : null; } }
        public ProcOdtImpedance ProcDqsOdt { get { return ProcDqsOdtRaw.HasValue ? new ProcOdtImpedance(ProcDqsOdtRaw.Value) : null; } }
        public CadBusDrvStren ProcDataDsApu { get { return ProcDataDsApuRaw.HasValue ? new CadBusDrvStren(ProcDataDsApuRaw.Value) : null; } }

        public string GetReport()
        {
            StringBuilder sb = new StringBuilder();
            PropertyInfo[] properties = GetType().GetProperties(BindingFlags.Public | BindingFlags.Instance);

            for (int i = 0; i < properties.Length; i++)
            {
                PropertyInfo property = properties[i];
                object value = property.GetValue(this, null);
                string rawValue = "null";

                if (value is EncodedValueBase encodedValue)
                {
                    rawValue = encodedValue.RawValue.HasValue
                        ? encodedValue.RawValue.Value.ToString(CultureInfo.InvariantCulture)
                        : "null";
                }

                sb.AppendLine(string.Format("{0,-20}{1,-20}({2})", property.Name + ":", value ?? "N/A", rawValue));
            }

            return sb.ToString();
        }
    }
}
