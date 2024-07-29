using System;
using System.Collections.Generic;
using System.ComponentModel;

namespace ZenStates.Core
{
    public class PowerTable : INotifyPropertyChanged
    {
        private readonly IOModule io;
        private readonly SMU smu;
        private readonly AMD_MMIO mmio;
        private readonly PTDef tableDef;
        public readonly uint DramBaseAddressLo;
        public readonly uint DramBaseAddressHi;
        public readonly uint DramBaseAddress;
        public readonly int TableSize;
        private const int NUM_ELEMENTS_TO_COMPARE = 20;

        public event PropertyChangedEventHandler PropertyChanged;

        private void OnPropertyChanged(PropertyChangedEventArgs eventArgs)
        {
            PropertyChanged?.Invoke(this, eventArgs);
        }

        private bool SetProperty<T>(ref T storage, T value, PropertyChangedEventArgs args)
        {
            // Do not update if value is equal to the cached one
            if (Equals(storage, value))
                return false;

            // Cache the new value and notify the change
            storage = value;
            OnPropertyChanged(args);

            return true;
        }

        // Power table definition
        private struct PTDef
        {
            public int tableVersion;
            public int tableSize; // in bytes
            public int offsetFclk;
            public int offsetUclk;
            public int offsetMclk;
            public int offsetVddcrSoc;
            public int offsetCldoVddp;
            public int offsetCldoVddgIod;
            public int offsetCldoVddgCcd;
            public int offsetCoresPower;
            public int offsetVddMisc;
        }

        // @TODO: Rework to use struct or Dictionaries, this is not flexible at all
        private class PowerTableDef : List<PTDef>
        {
            public void Add
            (
                int tableVersion,
                int tableSize,
                int offsetFclk,
                int offsetUclk,
                int offsetMclk,
                int offsetVddcrSoc,
                int offsetCldoVddp,
                int offsetCldoVddgIod,
                int offsetCldoVddgCcd,
                int offsetCoresPower,
                int offsetVddMisc
            )
            {
                Add(new PTDef
                {
                    tableVersion = tableVersion,
                    tableSize = tableSize,
                    offsetFclk = offsetFclk,
                    offsetUclk = offsetUclk,
                    offsetMclk = offsetMclk,
                    offsetVddcrSoc = offsetVddcrSoc,
                    offsetCldoVddp = offsetCldoVddp,
                    offsetCldoVddgIod = offsetCldoVddgIod,
                    offsetCldoVddgCcd = offsetCldoVddgCcd,
                    offsetCoresPower = offsetCoresPower,
                    offsetVddMisc = offsetVddMisc
                });
            }
        }


        /// <summary>
        /// List of power table definitions for the different table versions found.
        /// If the sepcific detected version isn't found in the list, a generic one
        /// (usually the newest known) is selected.
        /// 
        /// Generic tables are defined with a faux version.
        /// APU faux versions start from 0x10
        /// CPU faux versions:
        /// Zen : 0x100
        /// Zen+: 0x101
        /// Zen2: 0x200
        /// Zen3: 0x300
        /// </summary>
        // version, size, FCLK, UCLK, MCLK, VDDCR_SOC, CLDO_VDDP, CLDO_VDDG_IOD, CLDO_VDDG_CCD, Cores Power Offset, MISC
        private static readonly PowerTableDef PowerTables = new PowerTableDef
        {
            // Zen and Zen+ APU
            { 0x1E0001, 0x570, 0x460, 0x464, 0x468, 0x10C, 0xF8, -1, -1, -1, -1 },
            { 0x1E0002, 0x570, 0x474, 0x478, 0x47C, 0x10C, 0xF8, -1, -1, -1, -1 },
            { 0x1E0003, 0x610, 0x298, 0x29C, 0x2A0, 0x104, 0xF0, -1, -1, -1, -1 },
            { 0x1E0004, 0x610, 0x298, 0x29C, 0x2A0, 0x104, 0xF0, -1, -1, -1, -1 },
            // Generic (latest known)
            { 0x000010, 0x610, 0x298, 0x29C, 0x2A0, 0x104, 0xF0, -1, -1, -1, -1 },

            // FireFlight
            { 0x260001, 0x610, 0x28, 0x2C, 0x30, 0x10, -1, -1, -1, -1, -1 },

            // Zen2 APU (Renoir)
            { 0x370000, 0x79C, 0x4B4, 0x4B8, 0x4BC, 0x190, 0x72C, -1, -1, -1, -1 },
            { 0x370001, 0x88C, 0x5A4, 0x5A8, 0x5AC, 0x190, 0x81C, -1, -1, -1, -1 },
            { 0x370002, 0x894, 0x5AC, 0x5B0, 0x5B4, 0x198, 0x824, -1, -1, -1, -1 },
            { 0x370003, 0x8B4, 0x5CC, 0x5D0, 0x5D4, 0x198, 0x844, -1, -1, -1, -1 },
            // 0x370004 missing
            { 0x370005, 0x8D0, 0x5E8, 0x5EC, 0x5F0, 0x198, 0x86C, -1, -1, -1, -1 },
            // Generic Zen2 APU (latest known)
            { 0x000011, 0x8D0, 0x5E8, 0x5EC, 0x5F0, 0x198, 0x86C, -1, -1, -1, -1 },

            // Zen3 APU (Cezanne)
            { 0x400001, 0x8D0, 0x624, 0x628, 0x62C, 0x19C, 0x89C, -1, -1, -1, -1 },
            { 0x400002, 0x8D0, 0x63C, 0x640, 0x644, 0x19C, 0x8B4, -1, -1, -1, -1 },
            { 0x400003, 0x944, 0x660, 0x664, 0x668, 0x19C, 0x8D0, -1, -1, -1, -1 },
            { 0x400004, 0x944, 0x664, 0x668, 0x66C, 0x19C, 0x8D4, -1, -1, -1, -1 },
            { 0x400005, 0x944, 0x664, 0x668, 0x66C, 0x19C, 0x8D4, -1, -1, -1, -1 },
            // Rembrandt (experimental)
            { 0x450004, 0xAA4, 0x664, 0x668, 0x66C, 0x19C, 0x8D4, -1, -1, -1, -1 },
            { 0x450005, 0xAA4, 0x6B0, 0x6B4, 0x6B8, 0x1C8, 0x8D4, -1, -1, -1, -1 },
            // Generic Zen3 APU
            { 0x000012, 0x948, 0x664, 0x668, 0x66C, 0x19C, 0x8D4, -1, -1, -1, -1 },

            // Zen CPU
            { 0x000100, 0x7E4, 0x84, 0x84, 0x84, 0x68, 0x44, -1, -1, -1, -1 },

            // Zen+ CPU
            { 0x000101, 0x7E4, 0x84, 0x84, 0x84, 0x60, 0x3C, -1, -1, -1, -1 },

            // Zen2 CPU combined versions
            // version from 0x240000 to 0x240900 ?
            { 0x000200, 0x7E4, 0xB0, 0xB8, 0xBC, 0xA4, 0x1E4, 0x1E8, -1, -1, -1 },
            // version from 0x240001 to 0x240901 ?
            // version from 0x240002 to 0x240902 ?
            // version from 0x240004 to 0x240904 ?
            { 0x000202, 0x7E4, 0xBC, 0xC4, 0xC8, 0xB0, 0x1F0, 0x1F4, -1, -1, -1 },
            // version from 0x240003 to 0x240903 ?
            // Generic Zen2 CPU (latest known)
            { 0x000203, 0x7E4, 0xC0, 0xC8, 0xCC, 0xB4, 0x1F4, 0x1F8, -1, 0x24C, -1 },

            // Zen3 CPU
            // This table is found in some early beta bioses for Vermeer (SMU version 56.27.00)
            { 0x2D0903, 0x7E4, 0xBC, 0xC4, 0xC8, 0xB0, 0x220, 0x224, -1, -1, -1 },
            { 0x380005, 0x1BB0, 0xC0, 0xC8, 0xCC, 0xB4, 0x224, 0x228, 0x22C, -1, -1 },
            { 0x380505, 0xF30, 0xC0, 0xC8, 0xCC, 0xB4, 0x224, 0x228, 0x22C, -1, -1 },
            { 0x380804, 0x8A4, 0xC0, 0xC8, 0xCC, 0xB4, 0x224, 0x228, 0x22C, 0x24C, -1 },
            { 0x380805, 0x8F0, 0xC0, 0xC8, 0xCC, 0xB4, 0x224, 0x228, 0x22C, 0x2B0, -1 },
            { 0x380904, 0x5A4, 0xC0, 0xC8, 0xCC, 0xB4, 0x224, 0x228, 0x22C, 0x2A4, -1 },
            { 0x380905, 0x5D0, 0xC0, 0xC8, 0xCC, 0xB4, 0x224, 0x228, 0x22C, 0x2B0, -1 },
            // Generic Zen 3 CPU (latest known)
            { 0x000300, 0x948, 0xC0, 0xC8, 0xCC, 0xB4, 0x224, 0x228, 0x22C, -1, -1 },

            // Zen4 (unverified): size should be correct, offsets are not verified yet
            { 0x540100, 0x618, 0x118, 0x128, 0x138, 0xD0, 0x430, -1, -1, -1, 0xE0 },
            { 0x540101, 0x61C, 0x118, 0x128, 0x138, 0xD0, 0x430, -1, -1, -1, 0xE0 },
            { 0x540102, 0x66C, 0x118, 0x128, 0x138, 0xD0, 0x430, -1, -1, -1, 0xE0 },
            { 0x540103, 0x68C, 0x118, 0x128, 0x138, 0xD0, 0x430, -1, -1, -1, 0xE0 },
            { 0x540104, 0x6A4, 0x118, 0x128, 0x138, 0xD0, 0x430, -1, -1, -1, 0xE0 },
            { 0x540000, 0x828, 0x118, 0x128, 0x138, 0xD0, 0x430, -1, -1, -1, 0xE0 },
            { 0x540001, 0x82C, 0x118, 0x128, 0x138, 0xD0, 0x430, -1, -1, -1, 0xE0 },
            { 0x540002, 0x87C, 0x118, 0x128, 0x138, 0xD0, 0x430, -1, -1, -1, 0xE0 },
            { 0x540003, 0x89C, 0x118, 0x128, 0x138, 0xD0, 0x430, -1, -1, -1, 0xE0 },
            { 0x540004, 0x8BC, 0x118, 0x128, 0x138, 0xD0, 0x430, -1, -1, -1, 0xE0 },
            { 0x540005, 0x8C4, 0x118, 0x128, 0x138, 0xD0, 0x430, -1, -1, -1, 0xE0 },

            { 0x540208, 0x8C4, 0x118, 0x128, 0x138, 0xD0, 0x430, -1, -1, -1, 0xE0 },
            
            // Storm Peak, cpuid 00A10F81
            { 0x5C0302, 0xD9C, 0x194, 0x1A8, 0x1BC, 0x11C, -1, -1, -1, -1, 0x130 },
            { 0x5C0303, 0xD9C, 0x19C, 0x1B0, 0x1C4, 0x124, -1, -1, -1, -1, 0x138 },

            // Phoenix SMU 4.76.15.205
            { 0x4C0006, 0xAFC, 0x174, 0x184, 0x194, 0x74, 0x768, -1, -1, -1, -1 },
            // Phoenix Desktop SMU 76.80.0
            { 0x4C0008, 0xAF0, 0x164, 0x174, 0x184, 0x194, 0x768, -1, -1, -1, -1 },
            { 0x4C0009, 0xAFC, 0x164, 0x174, 0x184, 0x194, 0x774, -1, -1, -1, -1 },

            // Generic Zen4 Threadripper
            { 0x0005C0, 0xD9C, 0x19C, 0x1B0, 0x1C4, 0x124, -1, -1, -1, -1, -1 },
            // Generic Zen4 Desktop
            { 0x000400, 0x948, 0x118, 0x128, 0x138, 0xD0, 0x430, -1, -1, -1, 0xE0 },
            // Generic Zen4 Phoenix
            { 0x0004C0, 0xAFC, 0x164, 0x174, 0x184, 0x194, 0x774, -1, -1, -1, -1 },
            
            // Zen5
            // GraniteRidge, table size unknown
            { 0x620205, 0x994, 0x11C, 0x12C, 0x13C, 0x14C, 0x434, -1, -1, -1, 0xE8 },
            // Generic Zen5
            { 0x000620, 0xAFC, 0x11C, 0x12C, 0x13C, 0x14C, 0x434, -1, -1, -1, 0xE8 },
        };

        private PTDef GetDefByVersion(uint version)
        {
            return PowerTables.Find(x => x.tableVersion == version);
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
                    uint temp = tableVersion & 0x7;
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

                case SMU.SmuType.TYPE_CPU4:
                    if ((tableVersion >> 16) == 0x5c)
                        version = 0x5c0;
                    else if ((tableVersion >> 16) == 0x62)
                        version = 0x620;
                    else
                        version = 0x400;
                    break;

                case SMU.SmuType.TYPE_APU0:
                    version = 0x10;
                    break;

                case SMU.SmuType.TYPE_APU1:
                case SMU.SmuType.TYPE_APU2:
                    if ((tableVersion >> 16) == 0x37)
                        version = 0x11;
                    else if ((tableVersion >> 16) == 0x4c)
                        version = 0x4c0;
                    else
                        version = 0x12;
                    break;
            }

            return GetDefByVersion(version);
        }

        private PTDef GetPowerTableDef(uint tableVersion, SMU.SmuType smutype)
        {
            PTDef temp = GetDefByVersion(tableVersion);
            if (temp.tableSize != 0)
                return temp;
            return GetDefaultTableDef(tableVersion, smutype);
        }

        public PowerTable(SMU smuInstance, IOModule ioInstance, AMD_MMIO mmio)
        {
            this.smu = smuInstance ?? throw new ArgumentNullException(nameof(smuInstance));
            this.io = ioInstance ?? throw new ArgumentNullException(nameof(ioInstance));
            this.mmio = mmio ?? throw new ArgumentNullException(nameof(mmio));
            SMUCommands.CmdResult result = new SMUCommands.GetDramAddress(smu).Execute();
            DramBaseAddressLo = DramBaseAddress = result.args[0];
            DramBaseAddressHi = result.args[1];

            if (DramBaseAddress == 0)
                throw new ApplicationException("Could not get DRAM base address.");

            if (!Utils.Is64Bit)
            {
                var status = new SMUCommands.SetToolsDramAddress(smu).Execute(DramBaseAddress);
                if (!status.Success)
                    throw new ApplicationException("Could not set DRAM base address.");
            }

            tableDef = GetPowerTableDef(smu.TableVersion, smu.SMU_TYPE);
            TableSize = tableDef.tableSize;
            Table = new float[TableSize / 4];
            this.Refresh();
        }

        private float GetDiscreteValue(float[] pt, int byteIndex)
        {
            if (byteIndex > -1 && byteIndex < TableSize)
                return pt[byteIndex / 4];
            return 0;
        }

        private void ParseTable(float[] pt)
        {
            if (pt == null)
                return;

            float bclkCorrection = 1.0f;
            double? bclk = mmio.GetBclk();

            if (bclk != null)
                bclkCorrection = (float)bclk / 100.0f;

            // Compensate for lack of BCLK detection, based on configuredClockSpeed
            /*if (ConfiguredClockSpeed > 0 && MemRatio > 0)
                bclkCorrection = ConfiguredClockSpeed / (MemRatio * 200);*/

            MCLK = GetDiscreteValue(pt, tableDef.offsetMclk) * bclkCorrection;
            FCLK = GetDiscreteValue(pt, tableDef.offsetFclk) * bclkCorrection;
            UCLK = GetDiscreteValue(pt, tableDef.offsetUclk) * bclkCorrection;
            VDDCR_SOC = GetDiscreteValue(pt, tableDef.offsetVddcrSoc);
            CLDO_VDDP = GetDiscreteValue(pt, tableDef.offsetCldoVddp);
            CLDO_VDDG_IOD = GetDiscreteValue(pt, tableDef.offsetCldoVddgIod);
            CLDO_VDDG_CCD = GetDiscreteValue(pt, tableDef.offsetCldoVddgCcd);
            VDD_MISC = GetDiscreteValue(pt, tableDef.offsetVddMisc);

            // Test
            /*if (tableDef.offsetCoresPower > 0)
            {
                Console.WriteLine();
                for (int i = 0; i < 16; i++)
                {
                    bytes = BitConverter.GetBytes(GetDiscreteValue(pt, tableDef.offsetCoresPower + i * 4));
                    float power = BitConverter.ToSingle(bytes, 0);
                    string status = power > 0 ? "Enabled" : "Disabled";
                    Console.WriteLine($"Core{i}: {power} -> {status}");
                }
            }*/
        }

        private float[] ReadTableFromMemory(int tableSizeInBytes)
        {
            float[] table = new float[tableSizeInBytes / 4];

            if (Utils.Is64Bit)
            {
                IntPtr dramBaseAddress = smu.SMU_TYPE >= SMU.SmuType.TYPE_CPU4 && smu.SMU_TYPE < SMU.SmuType.TYPE_CPU9 || smu.SMU_TYPE == SMU.SmuType.TYPE_APU2
                        ? new IntPtr((long)DramBaseAddressHi << 32 | DramBaseAddressLo)
                        : new IntPtr(DramBaseAddressLo);

                byte[] bytes = io.ReadMemory(dramBaseAddress, tableSizeInBytes);

                if (bytes != null && bytes.Length > 0)
                    Buffer.BlockCopy(bytes, 0, table, 0, bytes.Length);
            }
            else
            {
                try
                {
                    for (int i = 0; i < table.Length; ++i)
                    {
                        int offset = i * sizeof(float);
                        io.GetPhysLong((UIntPtr)(DramBaseAddress + offset), out uint data);
                        byte[] bytes = BitConverter.GetBytes(data);
                        Buffer.BlockCopy(bytes, 0, table, offset, bytes.Length);
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine("Error occurred while reading table: " + ex.Message);
                }
            }

            return table;
        }

        public SMU.Status Refresh()
        {
            if (DramBaseAddress == 0)
            {
                return SMU.Status.FAILED;
            }

            try
            {
                float[] tempTable = ReadTableFromMemory(NUM_ELEMENTS_TO_COMPARE * 4);

                // Issue a refresh command if the table is empty or the first {NUM_ELEMENTS_TO_COMPARE} elements of both tables are equal,
                // otherwise skip as some other app already refreshed the data.
                // Checking for empty Table should issue a refresh on first load.
                if (Utils.AllZero(Table) || Utils.AllZero(tempTable) || Utils.ArrayMembersEqual(Table, tempTable, NUM_ELEMENTS_TO_COMPARE))
                {
                    SMU.Status status = new SMUCommands.TransferTableToDram(smu).Execute().status;
                    if (status != SMU.Status.OK)
                        return status;
                }

                Buffer.BlockCopy(ReadTableFromMemory(TableSize), 0, Table, 0, TableSize);

                if (Utils.AllZero(Table))
                    return SMU.Status.FAILED;

                ParseTable(Table);
                return SMU.Status.OK;
            }
            catch (Exception ex)
            {
                Console.WriteLine("Error occurred while reading table: " + ex.Message);
                return SMU.Status.FAILED;
            }
        }

        // Static one-time properties
        public float ConfiguredClockSpeed { get; set; } = 0;
        public float MemRatio { get; set; } = 0;

        // Dynamic properties
        public float[] Table { get; private set; }

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

        float vdd_misc;

        public float VDD_MISC
        {
            get => vdd_misc;
            set => SetProperty(ref vdd_misc, value, InternalEventArgsCache.VDD_MISC);
        }
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
        internal static PropertyChangedEventArgs VDD_MISC = new PropertyChangedEventArgs("VDD_MISC");
    }
}
