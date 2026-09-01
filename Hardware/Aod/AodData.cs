using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Text;
using ZenStates.Core.Common;

namespace ZenStates.Core.Hardware.Aod
{
    // [Serializable]
    public class AodData
    {
        public int SMTEn { get; set; }
        public int MemClk { get; set; }
        public int Tcl { get; set; }
        public int Trcd { get; set; }
        public int TrcdWr { get; set; }
        public int TrcdRd { get; set; }
        public int Trp { get; set; }
        public int Tras { get; set; }
        public int Trc { get; set; }
        public int Twr { get; set; }
        public int Trfc { get; set; }
        public int Trfc2 { get; set; }
        public int Trfcsb { get; set; }
        public int Trtp { get; set; }
        public int TrrdL { get; set; }
        public int TrrdS { get; set; }
        public int Tfaw { get; set; }
        public int TwtrL { get; set; }
        public int TwtrS { get; set; }
        public int TrdrdScL { get; set; }
        public int TrdrdSc { get; set; }
        public int TrdrdSd { get; set; }
        public int TrdrdDd { get; set; }
        public int TwrwrScL { get; set; }
        public int TwrwrSc { get; set; }
        public int TwrwrSd { get; set; }
        public int TwrwrDd { get; set; }
        public int Twrrd { get; set; }
        public int Trdwr { get; set; }
        public CadBusDrvStren CadBusDrvStren { get; set; }
        public ProcDataDrvStren ProcDataDrvStren { get; set; }
        public ProcOdt ProcOdt { get; set; }
        public ProcOdt ProcOdtPullUp { get; set; }
        public ProcOdt ProcOdtPullDown { get; set; }
        // Phoenix
        public ProcOdtImpedance ProcCaOdt { get; set; }
        public ProcOdtImpedance ProcCkOdt { get; set; }
        public ProcOdtImpedance ProcDqOdt { get; set; }
        public ProcOdtImpedance ProcDqsOdt { get; set; }
        public CadBusDrvStren ProcDataDrvStrenApu { get; set; }
        // Phoenix: END
        public ProcOdtImpedance ProcCsDs { get; set; }
        public ProcOdtImpedance ProcCkDs { get; set; }
        public ProcOdt ProcDqDsPullUp { get; set; }
        public ProcOdt ProcDqDsPullDown { get; set; }
        public DramDataDrvStren DramDataDrvStren { get; set; }
        public DramDataDrvStren DramDqDsPullUp { get; set; }
        public DramDataDrvStren DramDqDsPullDown { get; set; }
        public Rtt RttNomWr { get; set; }
        public Rtt RttNomRd { get; set; }
        public Rtt RttWr { get; set; }
        public Rtt RttPark { get; set; }
        public Rtt RttParkDqs { get; set; }
        public Voltage MemVddio { get; set; }
        public Voltage MemVddq { get; set; }
        public Voltage MemVpp { get; set; }
        public Voltage ApuVddio { get; set; }

#if NET8_0_OR_GREATER
        [RequiresUnreferencedCode(
            "Forwards to Utils.CreateFromByteArray<AodData>, which uses reflection " +
            "(Type.GetProperty by name and Activator.CreateInstance) to populate AodData; " +
            "AodData's properties and their types must not be trimmed.")]
#endif
        public static AodData CreateFromByteArray(byte[] byteArray, Dictionary<string, int> fieldDictionary)
        {
            return Utils.CreateFromByteArray<AodData>(byteArray, fieldDictionary);
        }

        public string GetReport()
        {
            StringBuilder sb = new StringBuilder();

            AppendValue(sb, "SMTEn", SMTEn);
            AppendValue(sb, "MemClk", MemClk);
            AppendValue(sb, "Tcl", Tcl);
            AppendValue(sb, "Trcd", Trcd);
            AppendValue(sb, "TrcdWr", TrcdWr);
            AppendValue(sb, "TrcdRd", TrcdRd);
            AppendValue(sb, "Trp", Trp);
            AppendValue(sb, "Tras", Tras);
            AppendValue(sb, "Trc", Trc);
            AppendValue(sb, "Twr", Twr);
            AppendValue(sb, "Trfc", Trfc);
            AppendValue(sb, "Trfc2", Trfc2);
            AppendValue(sb, "Trfcsb", Trfcsb);
            AppendValue(sb, "Trtp", Trtp);
            AppendValue(sb, "TrrdL", TrrdL);
            AppendValue(sb, "TrrdS", TrrdS);
            AppendValue(sb, "Tfaw", Tfaw);
            AppendValue(sb, "TwtrL", TwtrL);
            AppendValue(sb, "TwtrS", TwtrS);
            AppendValue(sb, "TrdrdScL", TrdrdScL);
            AppendValue(sb, "TrdrdSc", TrdrdSc);
            AppendValue(sb, "TrdrdSd", TrdrdSd);
            AppendValue(sb, "TrdrdDd", TrdrdDd);
            AppendValue(sb, "TwrwrScL", TwrwrScL);
            AppendValue(sb, "TwrwrSc", TwrwrSc);
            AppendValue(sb, "TwrwrSd", TwrwrSd);
            AppendValue(sb, "TwrwrDd", TwrwrDd);
            AppendValue(sb, "Twrrd", Twrrd);
            AppendValue(sb, "Trdwr", Trdwr);

            AppendValue(sb, "CadBusDrvStren", CadBusDrvStren);
            AppendValue(sb, "ProcDataDrvStren", ProcDataDrvStren);
            AppendValue(sb, "ProcOdt", ProcOdt);
            AppendValue(sb, "ProcOdtPullUp", ProcOdtPullUp);
            AppendValue(sb, "ProcOdtPullDown", ProcOdtPullDown);
            AppendValue(sb, "ProcCaOdt", ProcCaOdt);
            AppendValue(sb, "ProcCkOdt", ProcCkOdt);
            AppendValue(sb, "ProcDqOdt", ProcDqOdt);
            AppendValue(sb, "ProcDqsOdt", ProcDqsOdt);
            AppendValue(sb, "ProcDataDrvStrenApu", ProcDataDrvStrenApu);
            AppendValue(sb, "ProcCsDs", ProcCsDs);
            AppendValue(sb, "ProcCkDs", ProcCkDs);
            AppendValue(sb, "ProcDqDsPullUp", ProcDqDsPullUp);
            AppendValue(sb, "ProcDqDsPullDown", ProcDqDsPullDown);
            AppendValue(sb, "DramDataDrvStren", DramDataDrvStren);
            AppendValue(sb, "DramDqDsPullUp", DramDqDsPullUp);
            AppendValue(sb, "DramDqDsPullDown", DramDqDsPullDown);
            AppendValue(sb, "RttNomWr", RttNomWr);
            AppendValue(sb, "RttNomRd", RttNomRd);
            AppendValue(sb, "RttWr", RttWr);
            AppendValue(sb, "RttPark", RttPark);
            AppendValue(sb, "RttParkDqs", RttParkDqs);
            AppendValue(sb, "MemVddio", MemVddio);
            AppendValue(sb, "MemVddq", MemVddq);
            AppendValue(sb, "MemVpp", MemVpp);
            AppendValue(sb, "ApuVddio", ApuVddio);

            return sb.ToString();
        }

        private static void AppendValue(StringBuilder sb, string name, object value)
        {
            if (value is EncodedValueBase encodedValue)
            {
                string rawValue = encodedValue.RawValue.HasValue
                    ? encodedValue.RawValue.Value.ToString(CultureInfo.InvariantCulture)
                    : "null";

                sb.AppendLine(string.Format("{0,-19}{1,-20}({2})", name + ":", value ?? "N/A", rawValue));
                return;
            }

            sb.AppendLine(string.Format("{0,-19}{1}", name + ":", value ?? "N/A"));
        }
    }
}
