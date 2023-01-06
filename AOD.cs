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
        public AodTable Table;

        private static readonly Dictionary<int, string> ProcOdtDict = new Dictionary<int, string>
        {
            {0, "Hi-Z"},
            {1, "480.0 Ω"},
            {2, "240.0 Ω"},
            {3, "160.0 Ω"},
            {4, "120.0 Ω"},
            {5, "96.0 Ω"},
            {6, "80.0 Ω"},
            {7, "68.6 Ω"},
            {12, "60.0 Ω"},
            {13, "53.3 Ω"},
            {14, "48.0 Ω"},
            {15, "43.6 Ω"},
            {28, "40.0 Ω"},
            {29, "36.9 Ω"},
            {30, "34.3 Ω"},
            {31, "32.0 Ω"},
            {60, "30.0 Ω"},
            {61, "28.2 Ω"},
            {62, "26.7 Ω"},
            {63, "25.3 Ω"},
        };

        private static readonly Dictionary<int, string> ProcDataDrvStrenDict = new Dictionary<int, string>
        {
            {2, "240.0 Ω"},
            {4, "120.0 Ω"},
            {6, "80.0 Ω"},
            {12, "60.0 Ω"},
            {14, "48.0 Ω"},
            {28, "40.0 Ω"},
            {30, "34.3 Ω"},
        };

        private static readonly Dictionary<int, string> DramDataDrvStrenDict = new Dictionary<int, string>
        {
            {0, "34.0 Ω"},
            {1, "40.0 Ω"},
            {2, "48.0 Ω"},
        };

        private static readonly Dictionary<int, string> CadBusDrvStrenDict = new Dictionary<int, string>
        {
            {30, "30.0 Ω"},
            {40, "40.0 Ω"},
            {60, "60.0 Ω"},
            {120, "120.0 Ω"},
        };

        // RttNom, RttPark
        private static readonly Dictionary<int, string> RttDict = new Dictionary<int, string>
        {
            {0, "Off"},
            {1, "RZQ/1"},
            {2, "RZQ/2"},
            {3, "RZQ/3"},
            {4, "RZQ/4"},
            {5, "RZQ/5"},
            {6, "RZQ/6"},
            {7, "RZQ/7"},
        };

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

        public static string GetProcODTString(int key) => GetByKey(ProcOdtDict, key);
        public static string GetProcDataDrvStrenString(int key) => GetByKey(ProcOdtDict, key);
        public static string GetDramDataDrvStrenString(int key) => GetByKey(DramDataDrvStrenDict, key);
        public static string GetCadBusDrvStrenString(int key) => GetByKey(CadBusDrvStrenDict, key);
        public static string GetRttString(int key) => GetByKey(RttDict, key);

        [Serializable]
        [StructLayout(LayoutKind.Explicit, Pack = 4)]
        public struct AodData
        {
            [FieldOffset(8920)] public int SMTEn;
            [FieldOffset(8924)] public int MemClk;
            [FieldOffset(8928)] public int Tcl;
            [FieldOffset(8932)] public int Trcd;
            [FieldOffset(8936)] public int Trp;
            [FieldOffset(8940)] public int Tras;
            [FieldOffset(8944)] public int Trc;
            [FieldOffset(8948)] public int Twr;
            [FieldOffset(8952)] public int Trfc;
            [FieldOffset(8956)] public int Trfc2;
            [FieldOffset(8960)] public int Trfcsb;
            [FieldOffset(8964)] public int Trtp;
            [FieldOffset(8968)] public int TrrdL;
            [FieldOffset(8972)] public int TrrdS;
            [FieldOffset(8976)] public int Tfaw;
            [FieldOffset(8980)] public int TwtrL;
            [FieldOffset(8984)] public int TwtrS;
            [FieldOffset(8988)] public int TrdrdScL;
            [FieldOffset(8992)] public int TrdrdSc;
            [FieldOffset(8996)] public int TrdrdSd;
            [FieldOffset(9000)] public int TrdrdDd;
            [FieldOffset(9004)] public int TwrwrScL;
            [FieldOffset(9008)] public int TwrwrSc;
            [FieldOffset(9012)] public int TwrwrSd;
            [FieldOffset(9016)] public int TwrwrDd;
            [FieldOffset(9020)] public int Twrrd;
            [FieldOffset(9024)] public int Trdwr;

            // DRAM Controller Configuration
            [FieldOffset(9028)] public int CadBusDrvStren; // AddrCmdDrvStren
            [FieldOffset(9032)] public int ProcDataDrvStren;
            [FieldOffset(9036)] public int ProcODT;
            [FieldOffset(9040)] public int DramDataDrvStren;
            [FieldOffset(9044)] public int RttNomWr;
            [FieldOffset(9048)] public int RttNomRd;

            // Data Bus Configuration
            [FieldOffset(9052)] public int RttWr;
            [FieldOffset(9056)] public int RttPark;
            [FieldOffset(9060)] public int RttParkDqs;

            // DRAM Voltages
            [FieldOffset(9096)] public int MemVddio;
            [FieldOffset(9100)] public int MemVddq;
            [FieldOffset(9104)] public int MemVpp;
        }

        [Serializable]
        public class AodTable
        {
            public readonly uint Signature;
            public ulong OemTableId;
            // public readonly byte[] RegionSignature;
            public uint BaseAddress;
            public int Length;
            public ACPITable? acpiTable;
            public AodData Data;
            public byte[] rawAodTable;

            public AodTable()
            {
                this.Signature = Signature(TableSignature.SSDT);
                this.OemTableId = SignatureUL(TableSignature.AOD_);
                //this.RegionSignature = ByteSignature(TableSignature.AODE);
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
            this.Table.acpiTable = GetAcpiTable();

            if (this.Table.acpiTable != null)
            {
                int regionIndex = Utils.FindSequence(this.Table.acpiTable?.Data, 0, ByteSignature(TableSignature.AODE));
                if (regionIndex == -1)
                    regionIndex = Utils.FindSequence(this.Table.acpiTable?.Data, 0, ByteSignature(TableSignature.AODT));
                if (regionIndex == -1)
                    return;
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
                this.Table.rawAodTable = this.io.ReadMemory(new IntPtr(this.Table.BaseAddress), this.Table.Length);
                this.Table.Data = Utils.ByteArrayToStructure<AodData>(this.Table.rawAodTable);
                // int test = Utils.FindSequence(rawTable, 0, BitConverter.GetBytes(0x3ae));
                return true;
            }
            catch { }

            return false;
        }
    }
}
