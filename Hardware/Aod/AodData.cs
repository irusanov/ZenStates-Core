using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
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

        /// <summary>Label column width used throughout this block's report.</summary>
        private const int TimingLabelWidth = 19;

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
            ReportBuilder report = new ReportBuilder();

            report.AppendValue("SMTEn", SMTEn, TimingLabelWidth);
            report.AppendValue("MemClk", MemClk, TimingLabelWidth);
            report.AppendValue("Tcl", Tcl, TimingLabelWidth);
            report.AppendValue("Trcd", Trcd, TimingLabelWidth);
            report.AppendValue("TrcdWr", TrcdWr, TimingLabelWidth);
            report.AppendValue("TrcdRd", TrcdRd, TimingLabelWidth);
            report.AppendValue("Trp", Trp, TimingLabelWidth);
            report.AppendValue("Tras", Tras, TimingLabelWidth);
            report.AppendValue("Trc", Trc, TimingLabelWidth);
            report.AppendValue("Twr", Twr, TimingLabelWidth);
            report.AppendValue("Trfc", Trfc, TimingLabelWidth);
            report.AppendValue("Trfc2", Trfc2, TimingLabelWidth);
            report.AppendValue("Trfcsb", Trfcsb, TimingLabelWidth);
            report.AppendValue("Trtp", Trtp, TimingLabelWidth);
            report.AppendValue("TrrdL", TrrdL, TimingLabelWidth);
            report.AppendValue("TrrdS", TrrdS, TimingLabelWidth);
            report.AppendValue("Tfaw", Tfaw, TimingLabelWidth);
            report.AppendValue("TwtrL", TwtrL, TimingLabelWidth);
            report.AppendValue("TwtrS", TwtrS, TimingLabelWidth);
            report.AppendValue("TrdrdScL", TrdrdScL, TimingLabelWidth);
            report.AppendValue("TrdrdSc", TrdrdSc, TimingLabelWidth);
            report.AppendValue("TrdrdSd", TrdrdSd, TimingLabelWidth);
            report.AppendValue("TrdrdDd", TrdrdDd, TimingLabelWidth);
            report.AppendValue("TwrwrScL", TwrwrScL, TimingLabelWidth);
            report.AppendValue("TwrwrSc", TwrwrSc, TimingLabelWidth);
            report.AppendValue("TwrwrSd", TwrwrSd, TimingLabelWidth);
            report.AppendValue("TwrwrDd", TwrwrDd, TimingLabelWidth);
            report.AppendValue("Twrrd", Twrrd, TimingLabelWidth);
            report.AppendValue("Trdwr", Trdwr, TimingLabelWidth);

            report.AppendEncodedValue("CadBusDrvStren", CadBusDrvStren);
            report.AppendEncodedValue("ProcDataDrvStren", ProcDataDrvStren);
            report.AppendEncodedValue("ProcOdt", ProcOdt);
            report.AppendEncodedValue("ProcOdtPullUp", ProcOdtPullUp);
            report.AppendEncodedValue("ProcOdtPullDown", ProcOdtPullDown);
            report.AppendEncodedValue("ProcCaOdt", ProcCaOdt);
            report.AppendEncodedValue("ProcCkOdt", ProcCkOdt);
            report.AppendEncodedValue("ProcDqOdt", ProcDqOdt);
            report.AppendEncodedValue("ProcDqsOdt", ProcDqsOdt);
            report.AppendEncodedValue("ProcDataDrvStrenApu", ProcDataDrvStrenApu);
            report.AppendEncodedValue("ProcCsDs", ProcCsDs);
            report.AppendEncodedValue("ProcCkDs", ProcCkDs);
            report.AppendEncodedValue("ProcDqDsPullUp", ProcDqDsPullUp);
            report.AppendEncodedValue("ProcDqDsPullDown", ProcDqDsPullDown);
            report.AppendEncodedValue("DramDataDrvStren", DramDataDrvStren);
            report.AppendEncodedValue("DramDqDsPullUp", DramDqDsPullUp);
            report.AppendEncodedValue("DramDqDsPullDown", DramDqDsPullDown);
            report.AppendEncodedValue("RttNomWr", RttNomWr);
            report.AppendEncodedValue("RttNomRd", RttNomRd);
            report.AppendEncodedValue("RttWr", RttWr);
            report.AppendEncodedValue("RttPark", RttPark);
            report.AppendEncodedValue("RttParkDqs", RttParkDqs);

            report.AppendValue("MemVddq", MemVddq, TimingLabelWidth);
            report.AppendValue("MemVpp", MemVpp, TimingLabelWidth);
            report.AppendValue("ApuVddio", ApuVddio, TimingLabelWidth);

            return report.ToString();
        }
    }
}
