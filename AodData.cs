using System;
using System.Collections.Generic;

namespace ZenStates.Core
{
    // [Serializable]
    public class AodData
    {
        public int SMTEn { get; set; }
        public int MemClk { get; set; }
        public int Tcl { get; set; }
        public int Trcd { get; set; }
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
        public int CadBusDrvStren { get; set; }
        public int ProcDataDrvStren { get; set; }
        public int ProcODT { get; set; }
        public int DramDataDrvStren { get; set; }
        public int RttNomWr { get; set; }
        public int RttNomRd { get; set; }
        public int RttWr { get; set; }
        public int RttPark { get; set; }
        public int RttParkDqs { get; set; }
        public int MemVddio { get; set; }
        public int MemVddq { get; set; }
        public int MemVpp { get; set; }
        public int ApuVddio { get; set; }

        public static AodData CreateFromByteArray(byte[] byteArray, Dictionary<string, int> fieldDictionary)
        {
            AodData data = new AodData();

            foreach (var entry in fieldDictionary)
            {
                string fieldName = entry.Key;
                int fieldOffset = entry.Value;

                // Each property corresponds to 4 bytes in the byte array
                int fieldValue = BitConverter.ToInt32(byteArray, fieldOffset);

                typeof(AodData).GetProperty(fieldName)?.SetValue(data, fieldValue, null);
            }

            return data;
        }
    }
}
