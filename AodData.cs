using System;
using System.Collections.Generic;
using System.Reflection;
using static ZenStates.Core.AOD;

namespace ZenStates.Core
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
        public CadBusDrvStren ProcCaOdt { get; set; }
        public CadBusDrvStren ProcCkOdt { get; set; }
        public CadBusDrvStren ProcDqOdt { get; set; }
        public CadBusDrvStren ProcDqsOdt { get; set; }
        public DramDataDrvStren DramDataDrvStren { get; set; }
        public Rtt RttNomWr { get; set; }
        public Rtt RttNomRd { get; set; }
        public Rtt RttWr { get; set; }
        public Rtt RttPark { get; set; }
        public Rtt RttParkDqs { get; set; }
        public Voltage MemVddio { get; set; }
        public Voltage MemVddq { get; set; }
        public Voltage MemVpp { get; set; }
        public Voltage ApuVddio { get; set; }

        public static AodData CreateFromByteArray(byte[] byteArray, Dictionary<string, int> fieldDictionary)
        {
            AodData data = new AodData();

            foreach (var entry in fieldDictionary)
            {
                try
                {
                    string fieldName = entry.Key;
                    int fieldOffset = entry.Value;

                    PropertyInfo property = typeof(AodData).GetProperty(fieldName);
                    if (property != null)
                    {
                        Type propertyType = property.PropertyType;
                        object fieldValue;

                        if (propertyType.IsClass && propertyType != typeof(string))
                        {
                            fieldValue = Activator.CreateInstance(propertyType, BitConverter.ToInt32(byteArray, fieldOffset));
                        }
                        else
                        {
                            fieldValue = Convert.ChangeType(BitConverter.ToInt32(byteArray, fieldOffset), propertyType);
                        }

                        property.SetValue(data, fieldValue, null);
                    }
                }
                catch (Exception e)
                {
                    Console.WriteLine(e.ToString(), e.Message);
                }
            }

            return data;
        }

    }
}
