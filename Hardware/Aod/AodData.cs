using System;
using System.Collections.Generic;
using ZenStates.Core.Common;
using ZenStates.Core.Dictionaries;

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

        private delegate void RawSetter(AodData data, int raw);

        private struct Field
        {
            public RawSetter Set;
            public Dictionary<int, string> Lookup; // encoded values only: the table their text prints from
        }

        private static Field F(RawSetter set, Dictionary<int, string> lookup = null) => new Field { Set = set, Lookup = lookup };

        // Every field by name and how its raw Int32 is stored. Explicit rather than reflected, so AodData
        // is safe to trim and to compile with Native AOT.
        private static readonly Dictionary<string, Field> Fields = new Dictionary<string, Field>
        {
            { "SMTEn",                F((d, v) => d.SMTEn = v) },
            { "MemClk",               F((d, v) => d.MemClk = v) },
            { "Tcl",                  F((d, v) => d.Tcl = v) },
            { "Trcd",                 F((d, v) => d.Trcd = v) },
            { "TrcdWr",               F((d, v) => d.TrcdWr = v) },
            { "TrcdRd",               F((d, v) => d.TrcdRd = v) },
            { "Trp",                  F((d, v) => d.Trp = v) },
            { "Tras",                 F((d, v) => d.Tras = v) },
            { "Trc",                  F((d, v) => d.Trc = v) },
            { "Twr",                  F((d, v) => d.Twr = v) },
            { "Trfc",                 F((d, v) => d.Trfc = v) },
            { "Trfc2",                F((d, v) => d.Trfc2 = v) },
            { "Trfcsb",               F((d, v) => d.Trfcsb = v) },
            { "Trtp",                 F((d, v) => d.Trtp = v) },
            { "TrrdL",                F((d, v) => d.TrrdL = v) },
            { "TrrdS",                F((d, v) => d.TrrdS = v) },
            { "Tfaw",                 F((d, v) => d.Tfaw = v) },
            { "TwtrL",                F((d, v) => d.TwtrL = v) },
            { "TwtrS",                F((d, v) => d.TwtrS = v) },
            { "TrdrdScL",             F((d, v) => d.TrdrdScL = v) },
            { "TrdrdSc",              F((d, v) => d.TrdrdSc = v) },
            { "TrdrdSd",              F((d, v) => d.TrdrdSd = v) },
            { "TrdrdDd",              F((d, v) => d.TrdrdDd = v) },
            { "TwrwrScL",             F((d, v) => d.TwrwrScL = v) },
            { "TwrwrSc",              F((d, v) => d.TwrwrSc = v) },
            { "TwrwrSd",              F((d, v) => d.TwrwrSd = v) },
            { "TwrwrDd",              F((d, v) => d.TwrwrDd = v) },
            { "Twrrd",                F((d, v) => d.Twrrd = v) },
            { "Trdwr",                F((d, v) => d.Trdwr = v) },
            { "CadBusDrvStren",       F((d, v) => d.CadBusDrvStren = new CadBusDrvStren(v), EncodedValueDictionaries.CadBusDrvStrenDict) },
            { "ProcDataDrvStren",     F((d, v) => d.ProcDataDrvStren = new ProcDataDrvStren(v), EncodedValueDictionaries.ProcDataDrvStrenDict) },
            { "ProcOdt",              F((d, v) => d.ProcOdt = new ProcOdt(v), EncodedValueDictionaries.ProcOdtDict) },
            { "ProcOdtPullUp",        F((d, v) => d.ProcOdtPullUp = new ProcOdt(v), EncodedValueDictionaries.ProcOdtDict) },
            { "ProcOdtPullDown",      F((d, v) => d.ProcOdtPullDown = new ProcOdt(v), EncodedValueDictionaries.ProcOdtDict) },
            { "ProcCaOdt",            F((d, v) => d.ProcCaOdt = new ProcOdtImpedance(v), EncodedValueDictionaries.ProcImpedanceDict) },
            { "ProcCkOdt",            F((d, v) => d.ProcCkOdt = new ProcOdtImpedance(v), EncodedValueDictionaries.ProcImpedanceDict) },
            { "ProcDqOdt",            F((d, v) => d.ProcDqOdt = new ProcOdtImpedance(v), EncodedValueDictionaries.ProcImpedanceDict) },
            { "ProcDqsOdt",           F((d, v) => d.ProcDqsOdt = new ProcOdtImpedance(v), EncodedValueDictionaries.ProcImpedanceDict) },
            { "ProcDataDrvStrenApu",  F((d, v) => d.ProcDataDrvStrenApu = new CadBusDrvStren(v), EncodedValueDictionaries.CadBusDrvStrenDict) },
            { "ProcCsDs",             F((d, v) => d.ProcCsDs = new ProcOdtImpedance(v), EncodedValueDictionaries.ProcImpedanceDict) },
            { "ProcCkDs",             F((d, v) => d.ProcCkDs = new ProcOdtImpedance(v), EncodedValueDictionaries.ProcImpedanceDict) },
            { "ProcDqDsPullUp",       F((d, v) => d.ProcDqDsPullUp = new ProcOdt(v), EncodedValueDictionaries.ProcOdtDict) },
            { "ProcDqDsPullDown",     F((d, v) => d.ProcDqDsPullDown = new ProcOdt(v), EncodedValueDictionaries.ProcOdtDict) },
            { "DramDataDrvStren",     F((d, v) => d.DramDataDrvStren = new DramDataDrvStren(v), EncodedValueDictionaries.DramDataDrvStrenDict) },
            { "DramDqDsPullUp",       F((d, v) => d.DramDqDsPullUp = new DramDataDrvStren(v), EncodedValueDictionaries.DramDataDrvStrenDict) },
            { "DramDqDsPullDown",     F((d, v) => d.DramDqDsPullDown = new DramDataDrvStren(v), EncodedValueDictionaries.DramDataDrvStrenDict) },
            { "RttNomWr",             F((d, v) => d.RttNomWr = new Rtt(v), EncodedValueDictionaries.RttDict) },
            { "RttNomRd",             F((d, v) => d.RttNomRd = new Rtt(v), EncodedValueDictionaries.RttDict) },
            { "RttWr",                F((d, v) => d.RttWr = new Rtt(v), EncodedValueDictionaries.RttDict) },
            { "RttPark",              F((d, v) => d.RttPark = new Rtt(v), EncodedValueDictionaries.RttDict) },
            { "RttParkDqs",           F((d, v) => d.RttParkDqs = new Rtt(v), EncodedValueDictionaries.RttDict) },
            { "MemVddio",             F((d, v) => d.MemVddio = new Voltage(v)) },
            { "MemVddq",              F((d, v) => d.MemVddq = new Voltage(v)) },
            { "MemVpp",               F((d, v) => d.MemVpp = new Voltage(v)) },
            { "ApuVddio",             F((d, v) => d.ApuVddio = new Voltage(v)) },
        };

        /// <summary>
        /// Whether <paramref name="name"/> is an AodData field. <paramref name="lookup"/> is the
        /// code-to-text table of an encoded field, null for integers and voltages.
        /// </summary>
        internal static bool IsField(string name, out Dictionary<int, string> lookup)
        {
            Field field;
            bool found = Fields.TryGetValue(name, out field);
            lookup = field.Lookup;
            return found;
        }

        /// <summary>Stores a raw value in the named field. False for a name that isn't an AodData field.</summary>
        public bool TrySetRaw(string name, int raw)
        {
            Field field;
            if (!Fields.TryGetValue(name, out field))
                return false;

            field.Set(this, raw);
            return true;
        }

        /// <summary>
        /// Builds AodData from the raw AOD region, reading each field as an Int32 at its offset.
        /// Offsets outside the table are skipped.
        /// </summary>
        public static AodData CreateFromByteArray(byte[] byteArray, Dictionary<string, int> fieldDictionary)
        {
            var data = new AodData();

            if (byteArray != null)
            {
                foreach (KeyValuePair<string, int> entry in fieldDictionary)
                {
                    if (entry.Value >= 0 && entry.Value <= byteArray.Length - sizeof(int))
                        data.TrySetRaw(entry.Key, BitConverter.ToInt32(byteArray, entry.Value));
                }
            }

            return data;
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

            report.AppendValue("MemVddio", MemVddio, TimingLabelWidth);
            report.AppendValue("MemVddq", MemVddq, TimingLabelWidth);
            report.AppendValue("MemVpp", MemVpp, TimingLabelWidth);
            report.AppendValue("ApuVddio", ApuVddio, TimingLabelWidth);

            return report.ToString();
        }
    }
}
