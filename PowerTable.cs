using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Runtime.InteropServices;

namespace ZenStates.Core
{
    public class PowerTable : INotifyPropertyChanged
    {
        public int tableSize;
        private uint[] table;
        private PTDef tableDef;

        public event PropertyChangedEventHandler PropertyChanged;

        protected bool SetProperty<T>(ref T storage, T value, PropertyChangedEventArgs args)
        {
            if (Equals(storage, value) || value == null) return false;
            storage = value;
            OnPropertyChanged(args);
            return true;
        }

        private struct PTDef
        {
            public int tableVersion;
            public int tableSize;
            public int offsetFclk;
            public int offsetUclk;
            public int offsetMclk;
            public int offsetVddcrSoc;
            public int offsetCldoVddp;
            public int offsetCldoVddgIod;
            public int offsetCldoVddgCcd;

            public PTDef (int tableVersion, int tableSize, int offsetFclk, int offsetUclk, int offsetMclk,
                int offsetVddcrSoc, int offsetCldoVddp, int offsetCldoVddgIod, int offsetCldoVddgCcd)
            {
                this.tableVersion = tableVersion;
                this.tableSize = tableSize;
                this.offsetFclk = offsetFclk;
                this.offsetUclk = offsetUclk;
                this.offsetMclk = offsetMclk;
                this.offsetVddcrSoc = offsetVddcrSoc;
                this.offsetCldoVddp = offsetCldoVddp;
                this.offsetCldoVddgIod = offsetCldoVddgIod;
                this.offsetCldoVddgCcd = offsetCldoVddgCcd;
            }
        }

        [Serializable]
        private struct PT
        {
            public uint Fclk;
            public uint Uclk;
            public uint Mclk;
            public uint VddcrSoc;
            public uint CldoVddp;
            public uint CldoVddgIod;
            public uint CldoVddgCcd;
        }

        private class PowerTableDef : List<PTDef>
        {
            public void Add(int tableVersion, int tableSize, int offsetFclk, int offsetUclk, int offsetMclk,
                int offsetVddcrSoc, int offsetCldoVddp, int offsetCldoVddgIod, int offsetCldoVddgCcd)
            {
                Add(new PTDef(tableVersion, tableSize,  offsetFclk, offsetUclk, offsetMclk,
                    offsetVddcrSoc, offsetCldoVddp, offsetCldoVddgIod, offsetCldoVddgCcd));
            }
        }

        private static readonly PowerTableDef powerTables = new PowerTableDef()
        {
            // Zen and Zen+ APU
            { 0x1E0001, 0x570, 0x460, 0x464, 0x468, 0x10C, 0xF8, -1, -1 },
            { 0x1E0002, 0x570, 0x474, 0x478, 0x47C, 0x10C, 0xF8, -1, -1 },
            { 0x1E0003, 0x610, 0x298, 0x29C, 0x2A0, 0x104, 0xF0, -1, -1 },
            // Generic (latest known)
            { 0x10, 0x610, 0x298, 0x29C, 0x2A0, 0x104, 0xF0, -1, -1 },

            // FireFlight
            { 0x260001, 0x610, 0x28, 0x2C, 0x30, 0x10, -1, -1, -1 },

            // Zen2 APU (Renoir)
            { 0x370000, 0x79C, 0x4B4, 0x4B8, 0x4BC, 0x190, 0x72C, -1, -1 },
            { 0x370001, 0x88C, 0x5A4, 0x5A8, 0x5AC, 0x190, 0x81C, -1, -1 },
            { 0x370002, 0x894, 0x5AC, 0x5B0, 0x5B4, 0x198, 0x824, -1, -1 },
            { 0x370003, 0x8B4, 0x5CC, 0x5D0, 0x5D4, 0x198, 0x844, -1, -1 },
            { 0x370005, 0x8D0, 0x5E8, 0x5EC, 0x5F0, 0x198, 0x860, -1, -1 },
            // Generic Zen2 APU (latest known)
            { 0x11, 0x8D0, 0x5E8, 0x5EC, 0x5F0, 0x198, 0x860, -1, -1 },

            // Zen CPU
            { 0x100, 0x7E4, 0x84, 0x84, 0x84, 0x68, 0x44, -1, -1 },
            // Zen+ CPU
            { 0x101, 0x7E4, 0x84, 0x84, 0x84, 0x60, 0x3C, -1, -1 },

            // Zen2 CPU combined versions
            // version from 0x240000 to 0x240900 ?
            { 0x200, 0x7E4, 0xB0, 0xB8, 0xBC, 0xA4, 0x1E4, 0x1E8, -1 },
            // version from 0x240001 to 0x240901 ?
            // version from 0x240002 to 0x240902 ?
            // version from 0x240004 to 0x240904 ?
            { 0x202, 0x7E4, 0xBC, 0xC4, 0xC8, 0xB0, 0x1F0, 0x1F4, -1 },
            // version from 0x240003 to 0x240903 ?
            // Generic Zen2 CPU (latest known)
            { 0x203, 0x7E4, 0xC0, 0xC8, 0xCC, 0xB4, 0x224, 0x228, -1 },

            // Zen3 CPU
            // This table is found in some early beta bioses for Vermeer (SMU version 56.27.00)
            { 0x2D0903, 0x7E4, 0xBC, 0xC4, 0xC8, 0xB0, 0x220, 0x224, -1 },
            // Generic Zen 3 CPU (latest known)
            { 0x300, 0x7E4, 0xC0, 0xC8, 0xCC, 0xB4, 0x224, 0x228, 0x22C },
        };
        /*
        // Zen and Zen+ APU
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

        // Renoir
        [Serializable]
        [StructLayout(LayoutKind.Explicit)]
        private struct PowerTableAPU1
        {
            [FieldOffset(0x5E8)] public uint Fclk; // 5E8
            [FieldOffset(0x5EC)] public uint Uclk; // 5EC
            [FieldOffset(0x5F0)] public uint Mclk; // 5F0
            [FieldOffset(0x198)] public uint VddcrSoc;
            //[FieldOffset(0x860)] public uint CldoVddp;
        };

        // Zen
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

        // Zen+
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

        // Zen2
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

        // Zen3
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

        // Zen 3
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
        */
        private PTDef GetDefByVersion(uint version)
        {
            return powerTables.Find(x => x.tableVersion == version);
        }

        private PTDef GetDefaultTableDef(uint tableVersion, SMU.SmuType smutype)
        {
            uint version = 0;

            switch (smutype)
            {
                case SMU.SmuType.TYPE_CPU0:
                    version = 0x100;
                    break;

                case SMU.SmuType.TYPE_CPU1:
                    version = 0x101;
                    break;

                case SMU.SmuType.TYPE_CPU2:
                    uint temp = tableVersion & 0x4;
                    if (temp == 0)
                        version = 0x200;
                    else if (temp == 1 || temp == 2 || temp == 4)
                        version = 0x202;
                    else
                        version = 0x203;
                    break;

                case SMU.SmuType.TYPE_CPU3:
                    version = 0x300;
                    break;

                case SMU.SmuType.TYPE_APU0:
                    version = 0x10;
                    break;

                case SMU.SmuType.TYPE_APU1:
                    version = 0x11;
                    break;

                default:
                    break;
            }

            return GetDefByVersion(version);
        }

        private PTDef GetPowerTableDef(uint tableVersion, SMU.SmuType smutype)
        {
            PTDef temp = GetDefByVersion(tableVersion);
            if (temp.tableSize != 0)
                return temp;
            else
                return GetDefaultTableDef(tableVersion, smutype);

            throw new Exception("Power Table not supported");
        }

        public PowerTable(uint version, SMU.SmuType smutype)
        {
            SmuType = smutype;
            TableVersion = version;
            tableDef = GetPowerTableDef(version, smutype);
            tableSize = tableDef.tableSize;
            table = new uint[tableSize / 4];
        }

        private uint GetDiscreteValue(uint[] pt, int index)
        {
            if (index > 0 && index < tableSize)
                return pt[index / 4];
            return 0;
        }

        private void ParseTable(uint[] pt)
        {
            if (pt == null)
                return;

            PT powerTable = new PT
            {
                Fclk = GetDiscreteValue(pt, tableDef.offsetFclk),
                Uclk = GetDiscreteValue(pt, tableDef.offsetUclk),
                Mclk = GetDiscreteValue(pt, tableDef.offsetMclk),
                VddcrSoc = GetDiscreteValue(pt, tableDef.offsetVddcrSoc),
                CldoVddp = GetDiscreteValue(pt, tableDef.offsetCldoVddp),
                CldoVddgIod = GetDiscreteValue(pt, tableDef.offsetCldoVddgIod),
                CldoVddgCcd = GetDiscreteValue(pt, tableDef.offsetCldoVddgCcd)
            };

            float bclkCorrection = 1.00f;
            byte[] bytes;

            try
            {
                bytes = BitConverter.GetBytes(powerTable.Mclk);
                float mclkFreq = BitConverter.ToSingle(bytes, 0);

                // Compensate for lack of BCLK detection, based on configuredClockSpeed
                if (ConfiguredClockSpeed > 0 && MemRatio > 0)
                    bclkCorrection = ConfiguredClockSpeed / (MemRatio * 200);

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
        public float MemRatio { get; set; }
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
