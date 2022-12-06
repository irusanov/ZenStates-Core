using System;
using System.Runtime.InteropServices;
using static ZenStates.Core.ACPI;

namespace ZenStates.Core
{
    public class AOD
    {
        internal readonly IOModule io;
        internal readonly ACPI acpi;
        public AodTable Table;

        [Serializable]
        [StructLayout(LayoutKind.Explicit, Pack = 1)]
        public struct AodData
        {
            [FieldOffset(9028)] public byte CadBusDrvStren;
            [FieldOffset(9036)] public byte ProcODT;
        }

        public class AodTable
        {
            public readonly uint Signature;
            public readonly ulong OemTableId;
            public readonly byte[] RegionSignature;
            public uint BaseAddress;
            public int Length;
            public ACPITable? acpiTable;
            public AodData Data;

            public AodTable()
            {
                this.Signature = Signature(TableSignature.SSDT);
                this.OemTableId = SignatureUL(TableSignature.AOD_);
                this.RegionSignature = ByteSignature(TableSignature.AODE);
            }
        }

        public AOD(IOModule io)
        {
            this.io = io;
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
                        SDTHeader hdr = acpi.GetHeader<SDTHeader>(addr);
                        if (hdr.Signature == this.Table.Signature && hdr.OEMTableID == this.Table.OemTableId)
                            return ParseSdtTable(io.ReadMemory(new IntPtr(addr), (int)hdr.Length));
                    }
                }
            }
            catch { }
            return null;
        }

        private void Init()
        {
            this.Table.acpiTable = GetAcpiTable();

            if (this.Table.acpiTable != null)
            {
                int regionIndex = Utils.FindSequence(this.Table.acpiTable?.Data, 0, this.Table.RegionSignature);
                byte[] region = new byte[16];
                Buffer.BlockCopy(this.Table.acpiTable?.Data, regionIndex, region, 0, 16);
                // OperationRegion(AODE, SystemMemory, Offset, Length)
                OperationRegion opRegion = Utils.ByteArrayToStructure<OperationRegion>(region);
                this.Table.BaseAddress = opRegion.Offset;
                this.Table.Length = opRegion.Length[1] << 8 | opRegion.Length[0];
            }

            this.Refresh();
        }

        public bool Refresh()
        {
            try
            {
                byte[] rawTable = this.io.ReadMemory(new IntPtr(this.Table.BaseAddress), this.Table.Length);
                this.Table.Data = Utils.ByteArrayToStructure<AodData>(rawTable);
                return true;
            }
            catch { }

            return false;
        }
    }
}
