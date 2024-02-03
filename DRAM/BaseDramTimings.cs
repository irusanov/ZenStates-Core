using System;
using System.Collections.Generic;
using System.Reflection;
using static ZenStates.Core.DRAM.MemoryConfig;

namespace ZenStates.Core.DRAM
{
    [Serializable]
    public abstract class BaseDramTimings : IDramTimings, IDisposable
    {
        private bool disposedValue;

        internal readonly Cpu cpu;
        internal Dictionary<uint, TimingDef[]> Dict { get; set; }

        public BaseDramTimings(Cpu cpuInstance)
        {
            cpu = cpuInstance;
        }

        public object this[string propertyName]
        {
            get
            {
                try
                {
                    if (!string.IsNullOrEmpty(propertyName))
                    {
                        PropertyInfo propertyInfo = GetPropertyInfo(propertyName);
                        return propertyInfo?.GetValue(this, null);
                    }
                    return null;
                }
                catch
                {
                    return null;
                }
            }
            set
            {
                try
                {
                    if (!string.IsNullOrEmpty(propertyName))
                    {
                        PropertyInfo propertyInfo = GetPropertyInfo(propertyName);
                        propertyInfo?.SetValue(this, value, null);
                    }
                }
                catch
                {
                    // do nothing
                }
            }
        }

        private PropertyInfo GetPropertyInfo(string propertyName)
        {
            return GetType().GetProperty(propertyName);
        }

        public virtual void ReadBankGroupSwap(uint offset = 0)
        {
            uint bgsa0 = cpu.ReadDword(offset | 0x500D0);
            uint bgsa1 = cpu.ReadDword(offset | 0x500D4);
            uint bgs0 = cpu.ReadDword(offset | 0x50050);
            uint bgs1 = cpu.ReadDword(offset | 0x50058);

            BGS = (bgs0 == 0x87654321 && bgs1 == 0x87654321) ? 0 : 1U;
            BGSAlt = (Utils.GetBits(bgsa0, 4, 7) > 0 || Utils.GetBits(bgsa1, 4, 7) > 0) ? 1U : 0;
        }

        public virtual void Read(uint offset = 0)
        {
            ReadBankGroupSwap(offset);

            foreach (KeyValuePair<uint, TimingDef[]> entry in Dict)
            {
                foreach (TimingDef def in entry.Value)
                {
                    if (this[def.Name] != null)
                    {
                        uint data = cpu.ReadDword(offset | entry.Key);
                        this[def.Name] = Utils.BitSlice(data, def.HiBit, def.LoBit);
                    }
                }
            }
        }

        public MemType Type { get; set; }
        public float Frequency => Ratio * 200;
        public float Ratio { get; internal set; }
        // public string TotalCapacity { get; private set; }
        public uint BGS { get; private set; }
        public uint BGSAlt { get; private set; }
        public uint GDM { get; private set; }
        public uint PowerDown { get; private set; }
        public uint Cmd2T { get; private set; }
        public uint CL { get; private set; }
        public uint RCDWR { get; private set; }
        public uint RCDRD { get; private set; }
        public uint RP { get; private set; }
        public uint RAS { get; private set; }
        public uint RC { get; private set; }
        public uint RRDS { get; private set; }
        public uint RRDL { get; private set; }
        public uint FAW { get; private set; }
        public uint WTRS { get; private set; }
        public uint WTRL { get; private set; }
        public uint WR { get; private set; }
        public uint RDRDSCL { get; private set; }
        public uint WRWRSCL { get; private set; }
        public uint CWL { get; private set; }
        public uint RTP { get; private set; }
        public uint RDWR { get; private set; }
        public uint WRRD { get; private set; }
        public uint RDRDSC { get; private set; }
        public uint RDRDSD { get; private set; }
        public uint RDRDDD { get; private set; }
        public uint WRWRSC { get; private set; }
        public uint WRWRSD { get; private set; }
        public uint WRWRDD { get; private set; }
        public uint TRCPAGE { get; private set; }
        public uint CKE { get; private set; }
        public uint STAG { get; private set; }
        public uint MOD { get; private set; }
        public uint MODPDA { get; private set; }
        public uint MRD { get; private set; }
        public uint MRDPDA { get; private set; }
        public uint RFC { get; internal set; }
        public uint RFC2 { get; internal set; }
        public uint REFI { get; internal set; }
        public uint XP { get; private set; }
        public uint PHYWRD { get; private set; }
        public uint PHYWRL { get; private set; }
        public uint PHYRDL { get; private set; }
        public float RFCns { get => Utils.ToNanoseconds(RFC, Frequency); }
        public float REFIns { get => Utils.ToNanoseconds(REFI, Frequency); }

        protected virtual void Dispose(bool disposing)
        {
            if (!disposedValue)
            {
                Dict = null;
                disposedValue = true;
            }
        }

        public void Dispose()
        {
            Dispose(disposing: true);
            GC.SuppressFinalize(this);
        }
    }
}
