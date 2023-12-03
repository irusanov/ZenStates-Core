using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using static ZenStates.Core.ACPI;

namespace ZenStates.Core
{
    public class AOD
    {
        internal readonly IOModule io;
        internal readonly ACPI acpi;
        internal readonly Cpu.CodeName codeName;
        public AodTable Table;

        private static string GetByKey(Dictionary<int, string> dict, int key)
        {
            return dict.TryGetValue(key, out string output) ? output : "N/A";
        }

        public class TProcODT
        {
            public override string ToString()
            {
                return base.ToString();
            }
        }

        public static string GetProcODTString(int key) => GetByKey(AodDictionaries.ProcOdtDict, key);
        public static string GetProcDataDrvStrenString(int key) => GetByKey(AodDictionaries.ProcOdtDict, key);
        public static string GetDramDataDrvStrenString(int key) => GetByKey(AodDictionaries.DramDataDrvStrenDict, key);
        public static string GetCadBusDrvStrenString(int key) => GetByKey(AodDictionaries.CadBusDrvStrenDict, key);
        public static string GetRttString(int key) => GetByKey(AodDictionaries.RttDict, key);

        [Serializable]
        public class AodTable
        {
            public readonly uint Signature;
            public ulong OemTableId;
            // public readonly byte[] RegionSignature;
            public uint BaseAddress;
            public int Length;
            public ACPITable? AcpiTable;
            public AodData Data;
            public byte[] RawAodTable;

            public AodTable()
            {
                this.Signature = Signature(TableSignature.SSDT);
                this.OemTableId = SignatureUL(TableSignature.AOD_);
                //this.RegionSignature = ByteSignature(TableSignature.AODE);
            }
        }

        public AOD(IOModule io, Cpu.CodeName codeName)
        {
            this.io = io;
            this.codeName = codeName;
            this.acpi = new ACPI(io);
            this.Table = new AodTable();
            this.Init();
        }

        private ACPITable? GetAcpiTable()
        {
            try
            {
                RSDT rsdt = acpi.GetRSDT();

                foreach (uint addr in rsdt.Data)
                {
                    if (addr != 0)
                    {
                        try
                        {
                            SDTHeader hdr = acpi.GetHeader<SDTHeader>(addr);
                            if (
                                hdr.Signature == this.Table.Signature
                                && (hdr.OEMTableID == this.Table.OemTableId || hdr.OEMTableID == SignatureUL(TableSignature.AAOD))
                            ) {
                                return ParseSdtTable(io.ReadMemory(new IntPtr(addr), (int)hdr.Length));
                            }
                        }
                        catch { }
                    }
                }
            }
            catch { }
            return null;
        }

        private void Init()
        {
            this.Table.AcpiTable = GetAcpiTable();

            if (this.Table.AcpiTable != null)
            {
                int regionIndex = Utils.FindSequence(this.Table.AcpiTable?.Data, 0, ByteSignature(TableSignature.AODE));
                if (regionIndex == -1)
                    regionIndex = Utils.FindSequence(this.Table.AcpiTable?.Data, 0, ByteSignature(TableSignature.AODT));
                if (regionIndex == -1)
                    return;
                byte[] region = new byte[16];
                Buffer.BlockCopy(this.Table.AcpiTable?.Data, regionIndex, region, 0, 16);
                // OperationRegion(AODE, SystemMemory, Offset, Length)
                OperationRegion opRegion = Utils.ByteArrayToStructure<OperationRegion>(region);
                this.Table.BaseAddress = opRegion.Offset;
                this.Table.Length = opRegion.Length[1] << 8 | opRegion.Length[0];
            }

            this.Refresh();
        }

        private static Dictionary<string, int> GetAodDataDictionary(Cpu.CodeName codeName)
        {
            switch (codeName)
            {
                case Cpu.CodeName.StormPeak:
                case Cpu.CodeName.Genoa:
                case Cpu.CodeName.DragonRange:
                    return AodDictionaries.AodDataNewDictionary;
                default:
                    return AodDictionaries.AodDataDefaultDictionary;
            }
        }

        public bool Refresh()
        {
            try
            {
                this.Table.RawAodTable = this.io.ReadMemory(new IntPtr(this.Table.BaseAddress), this.Table.Length);
                // this.Table.Data = Utils.ByteArrayToStructure<AodData>(this.Table.rawAodTable);
                // int test = Utils.FindSequence(rawTable, 0, BitConverter.GetBytes(0x3ae));
                this.Table.Data = AodData.CreateFromByteArray(this.Table.RawAodTable, GetAodDataDictionary(this.codeName));
                return true;
            }
            catch { }

            return false;
        }
    }
}
