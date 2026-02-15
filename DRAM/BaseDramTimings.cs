using System;
using System.Collections.Generic;
using System.Reflection;
using static ZenStates.Core.DRAM.MemoryConfig;

namespace ZenStates.Core.DRAM
{
    public readonly struct BooleanProp
    {
        private readonly uint value;

        public BooleanProp(uint value)
        {
            this.value = value;
        }

        public override string ToString()
        {
            if (value == 1) return "Enabled";
            if (value == 0) return "Disabled";
            return "Unknown";
        }

        // Allow implicit conversion both ways
        public static implicit operator BooleanProp(uint value) => new BooleanProp(value);
        public static implicit operator uint(BooleanProp flag) => flag.value;
    }

    public readonly struct CommandRateProp
    {
        private readonly uint value;

        public CommandRateProp(uint value)
        {
            this.value = value;
        }

        public override string ToString()
        {
            if (value == 1) return "2T";
            if (value == 0) return "1T";
            return "Unknown";
        }

        // Allow implicit conversion both ways
        public static implicit operator CommandRateProp(uint value) => new CommandRateProp(value);
        public static implicit operator uint(CommandRateProp flag) => flag.value;
    }

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
                        if (propertyInfo != null)
                        {
                            object converted = Utils.ConvertValue(value, propertyInfo.PropertyType);
                            //if (converted != null)
                            {
                                propertyInfo.SetValue(this, converted, null);
                            }
                        }
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

        //public MemType Type { get; set; } = MemType.UNKNOWN;
        public float Frequency => Ratio * 200;
        public float Ratio { get; internal set; }
        // public string TotalCapacity { get; internal set; }
        public BooleanProp BGS { get; internal set; }
        public BooleanProp BGSAlt { get; internal set; }
        public BooleanProp GDM { get; internal set; }
        public BooleanProp PowerDown { get; internal set; }
        public CommandRateProp Cmd2T { get; internal set; }
        public uint CL { get; internal set; }
        public uint RCDWR { get; internal set; }
        public uint RCDRD { get; internal set; }
        public uint RP { get; internal set; }
        public uint RAS { get; internal set; }
        public uint RC { get; internal set; }
        public uint RRDS { get; internal set; }
        public uint RRDL { get; internal set; }
        public uint FAW { get; internal set; }
        public uint WTRS { get; internal set; }
        public uint WTRL { get; internal set; }
        public uint WR { get; internal set; }
        public uint RDRDSCL { get; internal set; }
        public uint WRWRSCL { get; internal set; }
        public uint CWL { get; internal set; }
        public uint RTP { get; internal set; }
        public uint RDWR { get; internal set; }
        public uint WRRD { get; internal set; }
        public uint RDRDSC { get; internal set; }
        public uint RDRDSD { get; internal set; }
        public uint RDRDDD { get; internal set; }
        public uint WRWRSC { get; internal set; }
        public uint WRWRSD { get; internal set; }
        public uint WRWRDD { get; internal set; }
        public uint TRCPAGE { get; internal set; }
        public uint CKE { get; internal set; }
        public uint STAG { get; internal set; }
        public uint STAGsb { get; internal set; }
        public uint MOD { get; internal set; }
        public uint MODPDA { get; internal set; }
        public uint MRD { get; internal set; }
        public uint MRDPDA { get; internal set; }
        public uint RFC { get; internal set; }
        public uint RFC2 { get; internal set; }
        public uint REFI { get; internal set; }
        public uint XP { get; internal set; }
        public uint PHYWRD { get; internal set; }
        public uint PHYWRL { get; internal set; }
        public uint PHYRDL { get; internal set; }
        // WRPRE seems to be zero-based in the register and off by one
        private uint _wrpre;
        public uint WRPRE
        {
            get => _wrpre + 1;
            internal set => _wrpre = value;
        }
        public uint RDPRE { get; internal set; }
        public float RFCns { get => Utils.ToNanoseconds(RFC, Frequency); }
        public float REFIns { get => Utils.ToNanoseconds(REFI, Frequency); }
        public uint FGR { get; internal set; }
        public BankRefreshMode RefreshMode { get; internal set; } = BankRefreshMode.UNKNOWN;

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
