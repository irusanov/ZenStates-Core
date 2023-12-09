using System;
using System.Collections.Generic;
using System.Reflection;
using static ZenStates.Core.DRAM.MemoryConfig;

namespace ZenStates.Core.DRAM
{
    [Serializable]
    public abstract class BaseDramTimings
    {
        internal readonly Cpu cpu;
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
                catch
                {
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

        public virtual void ReadBankGroupSwap(uint offset = 0)
        {
            uint bgsa0 = cpu.ReadDword(offset | 0x500D0);
            uint bgsa1 = cpu.ReadDword(offset | 0x500D4);
            uint bgs0 = cpu.ReadDword(offset | 0x50050);
            uint bgs1 = cpu.ReadDword(offset | 0x50058);

            BGS = (bgs0 == 0x87654321 && bgs1 == 0x87654321) ? 0 : 1U;
            BGSAlt = (Utils.GetBits(bgsa0, 4, 7) > 0 || Utils.GetBits(bgsa1, 4, 7) > 0) ? 1U : 0;
        }

        public abstract void ReadUniqueTimings(uint offset = 0);

        public virtual void Read(uint offset = 0)
        {
            this.ReadBankGroupSwap(offset);
            this.ReadUniqueTimings();

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

        public MemType Type { get; internal set; }
        public float Frequency { get; private set; }
        private float ratio;
        public float Ratio
        {
            get => ratio;
            internal set
            {
                ratio = value;
                Frequency = ratio * 200;
            }
        }
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

        private uint rfc;
        public uint RFC
        {
            get => rfc;
            internal set
            {
                rfc = value;
                if (Frequency != 0)
                {
                    float rfcValue = Convert.ToSingle(rfc);
                    float trfcns = rfcValue * 2000.0f / Frequency;
                    if (trfcns > rfcValue) trfcns /= 2;
                    RFCns = trfcns;
                }
            }
        }
        public uint RFC2 { get; internal set; }

        private uint refi;
        public uint REFI
        {
            get => refi;
            set
            {
                refi = value;
                if (Frequency != 0)
                {
                    float refiValue = Convert.ToSingle(refi);
                    float trefins = 1000.0f / Frequency * 2 * refiValue;
                    if (trefins > refiValue) trefins /= 2;
                    REFIns = trefins;
                }
            }
        }
        public uint XP { get; private set; }
        public uint PHYWRD { get; private set; }
        public uint PHYWRL { get; private set; }
        public uint PHYRDL { get; private set; }
        public float RFCns { get; private set; }
        public float REFIns { get; private set; }
    }
}
