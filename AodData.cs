using System;
using System.Collections.Generic;
using System.Diagnostics;
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
        public ProcOdt ProcOdtPullUp { get; set; }
        public ProcOdt ProcOdtPullDown { get; set; }
        // Phoenix
        public ProcOdtImpedance ProcCaOdt { get; set; }
        public ProcOdtImpedance ProcCkOdt { get; set; }
        public ProcOdtImpedance ProcDqOdt { get; set; }
        public ProcOdtImpedance ProcDqsOdt { get; set; }
        // Phoenix: END
        public ProcOdtImpedance ProcCsDs { get; set; }
        public ProcOdtImpedance ProcCkDs { get; set; }
        public ProcDataDrvStren ProcDqDsPullUp { get; set; }
        public ProcDataDrvStren ProcDqDsPullDown { get; set; }
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

        public static AodData CreateFromByteArray(byte[] byteArray, Dictionary<string, int> fieldDictionary)
        {
            AodData data = new AodData();

            if (byteArray == null) return data;

            foreach (var entry in fieldDictionary)
            {
                try
                {
                    string fieldName = entry.Key;
                    int fieldOffset = entry.Value;

                    PropertyInfo property = typeof(AodData).GetProperty(fieldName);
                    if (fieldOffset > -1 && fieldOffset <= byteArray.Length - sizeof(int) && property != null)
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
                    Debug.WriteLine(e.ToString(), e.Message);
                }
            }

            return data;
        }

    }
}
