using System;
using System.Collections.Generic;
using System.Reflection;
using static ZenStates.Core.DRAM.MemoryConfig;

namespace ZenStates.Core.DRAM
{
    [Serializable]
    public abstract class BaseDramTimings
    {
        private readonly Cpu cpu;
        public BaseDramTimings(Cpu cpuInstance)
        {
            this.cpu = cpuInstance;
        }

        public object this[string propertyName]
        {
            get
            {
                try
                {
                    if (propertyName.Length > 0)
                    {
                        Type myType = typeof(BaseDramTimings);
                        PropertyInfo myPropInfo = myType.GetProperty(propertyName);
                        return myPropInfo?.GetValue(this, null);
                    }
                    return null;
                }
                catch {
                    return null;
                }
            }
            set
            {
                try
                {
                    if (propertyName.Length > 0)
                    {
                        Type myType = typeof(BaseDramTimings);
                        PropertyInfo myPropInfo = myType.GetProperty(propertyName);
                        if (myPropInfo != null) myPropInfo.SetValue(this, value, null);
                    }
                }
                catch { }
            }
        }

        internal Dictionary<uint, TimingDef[]> Dict { get; set; }

        public void Read(uint offset = 0)
        {
            foreach (KeyValuePair<uint, TimingDef[]> entry in Dict)
            {
                foreach (TimingDef def in entry.Value) {
                    uint data = cpu.ReadDword(offset | entry.Key);
                    this[def.Name] = Utils.BitSlice(data, def.HiBit, def.LoBit);
                }
            }
        }

        public MemType Type { get; set; }
        public float Frequency { get; set; }
        public float Ratio { get; set; }
        public string TotalCapacity { get; set; }
        public string BGS { get; set; }
        public string BGSAlt { get; set; }
        public string GDM { get; set; }
        public string PowerDown { get; set; }
        public string Cmd2T { get; set; }
        public uint CL { get; set; }
        public uint RCDWR { get; set; }
        public uint RCDRD { get; set; }
        public uint RP { get; set; }
        public uint RAS { get; set; }
        public uint RC { get; set; }
        public uint RRDS { get; set; }
        public uint RRDL { get; set; }
        public uint FAW { get; set; }
        public uint WTRS { get; set; }
        public uint WTRL { get; set; }
        public uint WR { get; set; }
        public uint RDRDSCL { get; set; }
        public uint WRWRSCL { get; set; }
        public uint CWL { get; set; }
        public uint RTP { get; set; }
        public uint RDWR { get; set; }
        public uint WRRD { get; set; }
        public uint RDRDSC { get; set; }
        public uint RDRDSD { get; set; }
        public uint RDRDDD { get; set; }
        public uint WRWRSC { get; set; }
        public uint WRWRSD { get; set; }
        public uint WRWRDD { get; set; }
        public uint TRCPAGE { get; set; }
        public uint CKE { get; set; }
        public uint STAG { get; set; }
        public uint MOD { get; set; }
        public uint MODPDA { get; set; }
        public uint MRD { get; set; }
        public uint MRDPDA { get; set; }

        private uint rfc;
        public uint RFC
        {
            get => rfc;
            set
            {
                rfc = value;
                double rfcValue = Convert.ToDouble(rfc);
                double trfcns = rfcValue * 2000 / Frequency;
                if (trfcns > rfcValue) trfcns /= 2;
                RFCns = $"{trfcns:F4}".TrimEnd('0').TrimEnd('.', ',');
            }
        }
        public string RFCns { get; set; }
        public uint RFC2 { get; set; }

        private uint refi;
        public uint REFI
        {
            get => refi;
            set
            {
                refi = value;
                double refiValue = Convert.ToDouble(refi);
                double trefins = 1000 / Frequency * 2 * refiValue;
                if (trefins > refiValue) trefins /= 2;
                REFIns = $"{trefins:F3}".TrimEnd('0').TrimEnd('.', ',');
            }
        }
        public string REFIns { get; set; }
        public uint XP { get; set; }
        public uint PHYWRD { get; set; }
        public uint PHYWRL { get; set; }
        public uint PHYRDL { get; set; }
    }
}
