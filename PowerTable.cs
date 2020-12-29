using System;
using System.ComponentModel;
using System.Runtime.InteropServices;
using ZenStates;

namespace ZenStates.Core
{
    public class PowerTable : INotifyPropertyChanged
    {
        public const uint tableSize = 0x7E4;
        private uint[] table = new uint[tableSize];

        public event PropertyChangedEventHandler PropertyChanged;

        protected bool SetProperty<T>(ref T storage, T value, PropertyChangedEventArgs args)
        {
            if (Equals(storage, value) || value == null) return false;
            storage = value;
            OnPropertyChanged(args);
            return true;
        }

        [Serializable]
        [StructLayout(LayoutKind.Explicit)]
        private struct PowerTableAPU0
        {
            [FieldOffset(0x0F0)] public uint CldoVddp;
            [FieldOffset(0x104)] public uint VddcrSoc;
            [FieldOffset(0x298)] public uint Fclk;
            [FieldOffset(0x29C)] public uint Uclk;
            [FieldOffset(0x2A0)] public uint Mclk;
        };

        [Serializable]
        [StructLayout(LayoutKind.Explicit)]
        private struct PowerTableAPU1
        {
            [FieldOffset(0x144)] public uint Fclk;
            [FieldOffset(0x154)] public uint Uclk;
            [FieldOffset(0x164)] public uint Mclk;
            [FieldOffset(0x198)] public uint VddcrSoc;
        };

        [Serializable]
        [StructLayout(LayoutKind.Explicit)]
        private struct PowerTableCPU0
        {
            [FieldOffset(0x044)] public uint CldoVddp;
            [FieldOffset(0x068)] public uint VddcrSoc;
            [FieldOffset(0x084)] public uint Fclk;
            [FieldOffset(0x084)] public uint Uclk;
            [FieldOffset(0x084)] public uint Mclk;
        };

        [Serializable]
        [StructLayout(LayoutKind.Explicit)]
        private struct PowerTableCPU1
        {
            [FieldOffset(0x03C)] public uint CldoVddp;
            [FieldOffset(0x060)] public uint VddcrSoc;
            [FieldOffset(0x084)] public uint Fclk;
            [FieldOffset(0x084)] public uint Uclk;
            [FieldOffset(0x084)] public uint Mclk;
        };

        [Serializable]
        [StructLayout(LayoutKind.Explicit)]
        private struct PowerTableCPU2
        {
            [FieldOffset(0x0B4)] public uint VddcrSoc;
            [FieldOffset(0x0C0)] public uint Fclk;
            [FieldOffset(0x0C8)] public uint Uclk;
            [FieldOffset(0x0CC)] public uint Mclk;
            [FieldOffset(0x1F4)] public uint CldoVddp;
            [FieldOffset(0x1F8)] public uint CldoVddgIod;
        };

        [Serializable]
        [StructLayout(LayoutKind.Explicit)]
        private struct PowerTableCPU3
        {
            [FieldOffset(0x0B4)] public uint VddcrSoc;
            [FieldOffset(0x0C0)] public uint Fclk;
            [FieldOffset(0x0C8)] public uint Uclk;
            [FieldOffset(0x0CC)] public uint Mclk;
            [FieldOffset(0x224)] public uint CldoVddp;
            [FieldOffset(0x228)] public uint CldoVddgIod;
            [FieldOffset(0x22C)] public uint CldoVddgCcd;
        };

        // SMU version 56.27.00
        [Serializable]
        [StructLayout(LayoutKind.Explicit)]
        private struct PowerTableCPU_0x2D0903
        {
            [FieldOffset(0x0B0)] public uint VddcrSoc;
            [FieldOffset(0x0BC)] public uint Fclk;
            [FieldOffset(0x0C4)] public uint Uclk;
            [FieldOffset(0x0C8)] public uint Mclk;
            [FieldOffset(0x21C)] public uint CldoVddp;
            [FieldOffset(0x220)] public uint CldoVddgIod;
            [FieldOffset(0x224)] public uint CldoVddgCcd;
        };

        public PowerTable(uint version, SMU.SmuType smutype)
        {
            SmuType = smutype;
            TableVersion = version;
        }

        private static T ReadUsingMarshalSafe<T>(uint[] data) where T : struct
        {
            GCHandle handle = GCHandle.Alloc(data, GCHandleType.Pinned);
            try
            {
                return (T)Marshal.PtrToStructure(handle.AddrOfPinnedObject(), typeof(T));
            }
            finally
            {
                handle.Free();
            }
        }

        private void ParseTable(uint[] pt)
        {
            if (pt == null)
                return;

            dynamic powerTable = null;

            switch (SmuType)
            {
                case SMU.SmuType.TYPE_CPU0:
                    powerTable = ReadUsingMarshalSafe<PowerTableCPU0>(pt);
                    break;

                case SMU.SmuType.TYPE_CPU1:
                    powerTable = ReadUsingMarshalSafe<PowerTableCPU1>(pt);
                    break;

                case SMU.SmuType.TYPE_CPU2:
                    powerTable = ReadUsingMarshalSafe<PowerTableCPU2>(pt);
                    break;

                case SMU.SmuType.TYPE_CPU3:
                    if (TableVersion == 0x2D0903)
                        powerTable = ReadUsingMarshalSafe<PowerTableCPU_0x2D0903>(pt);
                    else
                        powerTable = ReadUsingMarshalSafe<PowerTableCPU3>(pt);
                    break;

                case SMU.SmuType.TYPE_APU0:
                    powerTable = ReadUsingMarshalSafe<PowerTableAPU0>(pt);
                    break;

                case SMU.SmuType.TYPE_APU1:
                    powerTable = ReadUsingMarshalSafe<PowerTableAPU1>(pt);
                    break;

                default:
                    return;
            }

            float bclkCorrection = 1.00f;
            byte[] bytes;

            try
            {
                bytes = BitConverter.GetBytes(powerTable.Mclk);
                float mclkFreq = BitConverter.ToSingle(bytes, 0);

                // Compensate for lack of BCLK detection, based on configuredClockSpeed
                float dramFreq = ConfiguredClockSpeed / 2;
                //if ((dramFreq + 1) / mclkFreq > 1 && dramFreq % mclkFreq > 1)
                bclkCorrection = dramFreq / mclkFreq;

                MCLK = mclkFreq * bclkCorrection;
            }
            catch { }

            try
            {
                bytes = BitConverter.GetBytes(powerTable.Fclk);
                float fclkFreq = BitConverter.ToSingle(bytes, 0);
                FCLK = fclkFreq * bclkCorrection;
            }
            catch { }

            try
            {
                bytes = BitConverter.GetBytes(powerTable.Uclk);
                float uclkFreq = BitConverter.ToSingle(bytes, 0);
                UCLK = uclkFreq * bclkCorrection;
            }
            catch { }

            try
            {
                bytes = BitConverter.GetBytes(powerTable.VddcrSoc);
                VDDCR_SOC = BitConverter.ToSingle(bytes, 0);
            }
            catch { }

            try
            {
                bytes = BitConverter.GetBytes(powerTable.CldoVddp);
                CLDO_VDDP = BitConverter.ToSingle(bytes, 0);
            }
            catch { }

            try
            {
                bytes = BitConverter.GetBytes(powerTable.CldoVddgIod);
                CLDO_VDDG_IOD = BitConverter.ToSingle(bytes, 0);
            }
            catch { }

            try
            {
                bytes = BitConverter.GetBytes(powerTable.CldoVddgCcd);
                CLDO_VDDG_CCD = BitConverter.ToSingle(bytes, 0);
            }
            catch { }
        }

        public SMU.SmuType SmuType { get; protected set; }
        public uint TableVersion { get; protected set; }

        public uint[] Table
        {
            get => table;
            set
            {
                if (value != null)
                {
                    table = value;
                    ParseTable(value);
                }
            }
        }

        float fclk;
        public float FCLK
        {
            get => fclk;
            set => SetProperty(ref fclk, value, InternalEventArgsCache.FCLK);
        }

        float mclk;
        public float MCLK
        {
            get => mclk;
            set => SetProperty(ref mclk, value, InternalEventArgsCache.MCLK);
        }

        float uclk;
        public float UCLK
        {
            get => uclk;
            set => SetProperty(ref uclk, value, InternalEventArgsCache.UCLK);
        }

        float vddcr_soc;
        public float VDDCR_SOC
        {
            get => vddcr_soc;
            set => SetProperty(ref vddcr_soc, value, InternalEventArgsCache.VDDCR_SOC);
        }

        float cldo_vddp;
        public float CLDO_VDDP
        {
            get => cldo_vddp;
            set => SetProperty(ref cldo_vddp, value, InternalEventArgsCache.CLDO_VDDP);
        }

        float cldo_vddg_iod;
        public float CLDO_VDDG_IOD
        {
            get => cldo_vddg_iod;
            set => SetProperty(ref cldo_vddg_iod, value, InternalEventArgsCache.CLDO_VDDG_IOD);
        }

        float cldo_vddg_ccd;
        public float CLDO_VDDG_CCD
        {
            get => cldo_vddg_ccd;
            set => SetProperty(ref cldo_vddg_ccd, value, InternalEventArgsCache.CLDO_VDDG_CCD);
        }

        protected void OnPropertyChanged(PropertyChangedEventArgs eventArgs)
        {
            PropertyChanged?.Invoke(this, eventArgs);
        }

        public float ConfiguredClockSpeed { get; set; }
    }

    internal static class InternalEventArgsCache
    {
        internal static PropertyChangedEventArgs FCLK = new PropertyChangedEventArgs("FCLK");
        internal static PropertyChangedEventArgs MCLK = new PropertyChangedEventArgs("MCLK");
        internal static PropertyChangedEventArgs UCLK = new PropertyChangedEventArgs("UCLK");

        internal static PropertyChangedEventArgs VDDCR_SOC = new PropertyChangedEventArgs("VDDCR_SOC");
        internal static PropertyChangedEventArgs CLDO_VDDP = new PropertyChangedEventArgs("CLDO_VDDP");
        internal static PropertyChangedEventArgs CLDO_VDDG_IOD = new PropertyChangedEventArgs("CLDO_VDDG_IOD");
        internal static PropertyChangedEventArgs CLDO_VDDG_CCD = new PropertyChangedEventArgs("CLDO_VDDG_CCD");
    }
}
