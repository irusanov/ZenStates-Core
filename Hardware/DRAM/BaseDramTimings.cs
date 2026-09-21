using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Reflection;
using static ZenStates.Core.Hardware.DRAM.MemoryConfig;

namespace ZenStates.Core.Hardware.DRAM
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

        /// <summary>
        /// Reads one UMC register.
        /// </summary>
        /// <returns>False when the register could not be read; <paramref name="value"/> is then 0.</returns>
        protected virtual bool TryReadRegister(uint address, out uint value)
        {
            // A null Cpu is legitimate for subclasses that never touch hardware.
            if (cpu == null)
            {
                value = 0;
                return false;
            }

            return cpu.TryReadDwordNoLock(address, out value);
        }

        protected uint ReadRegister(uint address)
        {
            return TryReadRegister(address, out uint value) ? value : 0;
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
#pragma warning disable IL2026
                            object converted = Utils.ConvertValue(value, propertyInfo.PropertyType);
#pragma warning restore IL2026
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
            PropertyInfo info = GetType().GetProperty(propertyName);

            // Under a trimmed or NativeAOT build a missing property means the member was
            // trimmed, not that the name was wrong — and the callers' null checks turn that
            // into every timing silently reading as unset. Make it visible.
            if (info == null)
            {
                Debug.WriteLine(
                    $"{GetType().Name}: no property '{propertyName}'. In a trimmed/AOT build this " +
                    "usually means it was trimmed; check AotRoots.xml.");
            }

            return info;
        }

        public abstract void ReadRatio(uint offset = 0);

        public virtual void ReadBankGroupSwap(uint offset = 0)
        {
            bool ok = true;
            ok &= TryReadRegister(offset | 0x500D0, out uint bgsa0);
            ok &= TryReadRegister(offset | 0x500D4, out uint bgsa1);
            ok &= TryReadRegister(offset | 0x50050, out uint bgs0);
            ok &= TryReadRegister(offset | 0x50058, out uint bgs1);

            if (!ok)
            {
                return;
            }

            BGS = (bgs0 == 0x87654321 && bgs1 == 0x87654321) ? 0 : 1U;
            BGSAlt = (Utils.GetBits(bgsa0, 4, 7) > 0 || Utils.GetBits(bgsa1, 4, 7) > 0) ? 1U : 0;
        }

        public virtual void Read(uint offset = 0)
        {
            ReadRatio(offset);
            ReadBankGroupSwap(offset);

            foreach (KeyValuePair<uint, TimingDef[]> entry in Dict)
            {
                foreach (TimingDef def in entry.Value)
                {
                    if (this[def.Name] != null)
                    {
                        if (TryReadRegister(offset | entry.Key, out uint data))
                        {
                            this[def.Name] = Utils.BitSlice(data, def.HiBit, def.LoBit);
                        }
                    }
                }
            }
        }

        //public MemType Type { get; set; } = MemType.UNKNOWN;

        /// <summary>
        /// Default reference clock used when the live BCLK cannot be read.
        /// </summary>
        protected const double DefaultBclk = 100.0;

        /// <summary>
        /// Effective memory data rate in MT/s. Several timings are reported in nanoseconds and are
        /// derived from this, so a subclass that decodes captured registers must override it too —
        /// otherwise it would read the clocks of whatever machine happens to be running the code.
        /// </summary>
        public virtual float Frequency
        {
            get
            {
                var mclk = PowerTable.Instance?.MCLK ?? 0;
                if (mclk > 0)
                {
                    return mclk * 2;
                }

                double bclk = Mmio.Instance?.GetBclk() ?? DefaultBclk;
                return Ratio * (float)bclk * 2;
            }
        }
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
        private uint _rdpre;
        public uint RDPRE
        {
            get
            {
                if (_rdpre < 2)
                    return _rdpre + 1;
                else
                    return _rdpre;
            }
            internal set => _rdpre = value;
        }
        public uint RDPOST { get; internal set; }
        public uint WRPOST { get; internal set; }
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
