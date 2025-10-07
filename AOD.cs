using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Management;
using System.Text;
using static ZenStates.Core.ACPI;

namespace ZenStates.Core
{
    public class AOD
    {
        internal readonly IOModule io;
        internal readonly Cpu cpuInstance;
        public readonly ACPI acpi;
        internal readonly Cpu.CodeName codeName;
        internal readonly uint patchLevel;
        internal readonly bool hasRMP;
        public AodTable Table;

        public class AodEnumBase
        {
            protected int Value;

            public AodEnumBase(int value)
            {
                Value = value;
            }

            public override string ToString()
            {
                return GetByKey(ValueDictionary, Value);
            }

            protected virtual Dictionary<int, string> ValueDictionary { get; } = new Dictionary<int, string>();

            protected static string GetByKey(Dictionary<int, string> dictionary, int key)
            {
                return dictionary.TryGetValue(key, out string output) ? output : @"N/A";
            }
        }

        public class ProcOdt : AodEnumBase
        {
            public ProcOdt(int value) : base(value) { }
            protected override Dictionary<int, string> ValueDictionary { get; } = AodDictionaries.ProcOdtDict;
        }

        public class ProcDataDrvStren : AodEnumBase
        {
            public ProcDataDrvStren(int value) : base(value) { }
            protected override Dictionary<int, string> ValueDictionary { get; } = AodDictionaries.ProcDataDrvStrenDict;
        }

        public class DramDataDrvStren : AodEnumBase
        {
            public DramDataDrvStren(int value) : base(value) { }
            protected override Dictionary<int, string> ValueDictionary { get; } = AodDictionaries.DramDataDrvStrenDict;
        }

        public class CadBusDrvStren : AodEnumBase
        {
            public CadBusDrvStren(int value) : base(value) { }
            protected override Dictionary<int, string> ValueDictionary { get; } = AodDictionaries.CadBusDrvStrenDict;
        }

        public class ProcOdtImpedance : AodEnumBase
        {
            public ProcOdtImpedance(int value) : base(value) { }
            protected override Dictionary<int, string> ValueDictionary { get; } = AodDictionaries.ProcOdtImpedanceDict;
        }

        public class Rtt : AodEnumBase
        {
            public Rtt(int value) : base(value) { }
            protected override Dictionary<int, string> ValueDictionary { get; } = AodDictionaries.RttDict;

            public override string ToString()
            {
                string value = base.ToString();

                if (this.Value > 0)
                    return $"{value} ({240 / Value})";
                return $"{value}";
            }
        }

        public class Voltage
        {
            protected int Value;
            public Voltage(int value)
            {
                Value = value;
            }

            public override string ToString()
            {
                return string.Format(CultureInfo.GetCultureInfo("en-US"), "{0:F4}V", Value / 1000.0);
            }
        }

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
            public Dictionary<string, string> AcpiNames;

            public AodTable()
            {
                this.Signature = Signature(TableSignature.SSDT);
                this.OemTableId = SignatureUL(TableSignature.AOD_);
                //this.RegionSignature = ByteSignature(TableSignature.AODE);
            }
        }

        public AOD(IOModule io, Cpu cpuInstance)
        {
            this.io = io;
            this.cpuInstance = cpuInstance;
            this.codeName = this.cpuInstance.info.codeName;
            this.acpi = new ACPI(io);
            this.Table = new AodTable();
            this.patchLevel = this.cpuInstance.info.patchLevel;
            this.hasRMP = GetWmiFunctions().ContainsKey("Set RMP Profile");
            this.Init();
        }

        private ACPITable? GetAcpiTable()
        {
            // Try to get the table from RSDT first
            ACPITable? acpiTable = GetAcpiTableFromRsdt();
            if (acpiTable == null)
                return AOD.GetAcpiTableFromRegistry();
            return acpiTable;
        }

        private ACPITable? GetAcpiTableFromRsdt()
        {
            try
            {
                RSDT rsdt = acpi.GetRsdt();

                foreach (uint addr in rsdt.Data)
                {
                    if (addr != 0)
                    {
                        try
                        {
                            SDTHeader hdr = acpi.GetHeader<SDTHeader>(addr);
                            if (
                                hdr.Signature == this.Table.Signature
                                && (hdr.OEMTableID == this.Table.OemTableId ||
                                hdr.OEMTableID == SignatureUL(TableSignature.AAOD) ||
                                hdr.OEMTableID == SignatureUL(TableSignature.LENOVO_AOD))
                            )
                            {
                                return ParseSdtTable(io.ReadMemory(new IntPtr(addr), (int)hdr.Length));
                            }
                        }
                        catch { }
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error getting SSDT ACPI table from RSDT: {ex.Message}");
            }

            return null;
        }

        private static ACPITable? GetAcpiTableFromRegistry()
        {
            string acpiRegistryPath = @"HARDWARE\ACPI";

            RegistryKey acpiKey = null;

            try
            {
                acpiKey = Registry.LocalMachine.OpenSubKey(acpiRegistryPath);

                if (acpiKey != null)
                {
                    string[] subkeyNames = acpiKey.GetSubKeyNames();
                    foreach (string subkeyName in subkeyNames)
                    {
                        Console.WriteLine($"Subkey: {subkeyName}");

                        if (subkeyName.StartsWith("SSD"))
                        {
                            byte[] acpiTableData = GetRawTableFromSubkeys(acpiKey, subkeyName);
                            if (acpiTableData != null)
                            {
                                if (GetAodRegionIndex(acpiTableData) == -1)
                                    continue;

                                return ParseSdtTable(acpiTableData);
                            }
                        }
                    }
                }
                else
                {
                    Console.WriteLine("ACPI registry key not found.");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error accessing ACPI registry: {ex.Message}");
            }
            finally
            {
                acpiKey?.Close();
            }

            return null;
        }

        private static int GetAodRegionIndex(byte[] rawTable)
        {
            if (rawTable == null)
                return -1;

            int regionIndex = Utils.FindSequence(rawTable, 0, ByteSignature(TableSignature.AODE));
            if (regionIndex == -1)
                regionIndex = Utils.FindSequence(rawTable, 0, ByteSignature(TableSignature.AODT));
            return regionIndex;
        }

        private static byte[] GetRawTableFromSubkeys(RegistryKey parentKey, string subkeyName)
        {
            RegistryKey subkey = null;
            try
            {
                subkey = parentKey.OpenSubKey(subkeyName);
                if (subkey != null)
                {
                    string[] subkeyNames = subkey.GetSubKeyNames();
                    if (subkeyNames.Length == 0)
                    {
                        // Found a key without further subkeys, retrieve the first REG_BINARY value
                        foreach (string valueName in subkey.GetValueNames())
                        {
                            object value = subkey.GetValue(valueName);

                            if (value is byte[] rawTable)
                            {
                                return rawTable;
                            }
                        }

                        Console.WriteLine($"No REG_BINARY value found in {subkeyName}.");
                    }
                    else
                    {
                        // Continue drilling down if there are more subkeys
                        foreach (string nestedSubkeyName in subkeyNames)
                        {
                            Console.WriteLine($"Nested Subkey: {nestedSubkeyName}");
                            byte[] rawTable = GetRawTableFromSubkeys(subkey, nestedSubkeyName);
                            if (rawTable != null)
                            {
                                return rawTable;
                            }
                        }
                    }
                }
            }
            finally
            {
                subkey?.Close();
            }

            return null;
        }

        private void Init()
        {
            //Table.AcpiTable = GetAcpiTable();
            Table.AcpiTable = AOD.GetAcpiTableFromRegistry();

            if (Table.AcpiTable != null && Table?.AcpiTable.Value.Data != null)
            {
                int regionIndex = GetAodRegionIndex(Table.AcpiTable.Value.Data);
                if (regionIndex == -1)
                    return;

                byte[] region = new byte[16];
                Buffer.BlockCopy(this.Table.AcpiTable.Value.Data, regionIndex, region, 0, 16);
                // OperationRegion(AODE, SystemMemory, Offset, Length)
                OperationRegion opRegion = Utils.ByteArrayToStructure<OperationRegion>(region);
                this.Table.BaseAddress = opRegion.Offset;
                this.Table.Length = (opRegion.Length[1] << 8) | opRegion.Length[0];

                this.Refresh();
            }
        }

        struct BaseDictionary { public Dictionary<string, int> Dict; public int LastOffset; }

        // TODO: Make generic for all CPUs
        private BaseDictionary GetBaseDictionaryByFrequency()
        {
            var frequency = (cpuInstance.memoryConfig?.Timings[0].Value as DRAM.BaseDramTimings)?.Frequency ?? 0;
            if (frequency > 0)
            {
                var tableIndex = Utils.FindLastSequence(this.Table.RawAodTable, 0, Utils.ToBytes2(frequency / 2));
                if (tableIndex > -1)
                {
                    return new BaseDictionary()
                    {
                        Dict = new Dictionary<string, int>()
                        {
                            { "SMTEn", tableIndex - 4 },
                            { "MemClk", tableIndex },
                            { "Tcl", tableIndex + 4 },
                            { "Trcd", tableIndex + 8 },
                            { "Trp", tableIndex + 12},
                            { "Tras", tableIndex + 16 },
                            { "Trc", tableIndex + 20 },
                            { "Twr", tableIndex + 24 },
                            { "Trfc", tableIndex + 28 },
                            { "Trfc2", tableIndex + 32 },
                            { "Trfcsb", tableIndex + 36 },
                            { "Trtp", tableIndex + 40 },
                            { "TrrdL", tableIndex + 44 },
                            { "TrrdS", tableIndex + 48 },
                            { "Tfaw", tableIndex + 52 },
                            { "TwtrL", tableIndex + 56 },
                            { "TwtrS", tableIndex + 60 },
                            { "TrdrdScL", tableIndex + 64 },
                            { "TrdrdSc", tableIndex + 68 },
                            { "TrdrdSd", tableIndex + 72 },
                            { "TrdrdDd", tableIndex + 76 },
                            { "TwrwrScL", tableIndex + 80 },
                            { "TwrwrSc", tableIndex + 84 },
                            { "TwrwrSd", tableIndex + 88 },
                            { "TwrwrDd", tableIndex + 92 },
                            { "Twrrd", tableIndex + 96},
                            { "Trdwr", tableIndex + 100},
                            { "CadBusDrvStren", tableIndex + 104 },
                        },
                        LastOffset = tableIndex + 104
                    };
                }
            }
            return new BaseDictionary { Dict = null, LastOffset = -1 };
        }

        private Dictionary<string, int> GetAodDataDictionary(Cpu.CodeName codeName, uint patchLevel)
        {
            if (Table.AcpiTable.Value.Header.OEMTableID == TableSignature.LENOVO_AOD)
                return AodDictionaries.AodDataDictionaryV3;

            var baseDictionary = GetBaseDictionaryByFrequency();
            var lastOffset = baseDictionary.LastOffset;
            var memModule = cpuInstance.memoryConfig?.Modules[0];
            var isMDie = memModule?.Rank == DRAM.MemRank.SR && memModule.AddressConfig.NumRow > 16;

            switch (codeName)
            {
                case Cpu.CodeName.StormPeak:
                case Cpu.CodeName.Genoa:
                case Cpu.CodeName.DragonRange:
                    return AodDictionaries.AodDataDictionaryV2;
                case Cpu.CodeName.Phoenix:
                case Cpu.CodeName.Phoenix2:
                case Cpu.CodeName.HawkPoint:
                    if (baseDictionary.Dict != null)
                    {
                        return new Dictionary<string, int>(baseDictionary.Dict)
                        {
                            { "ProcDataDrvStren", lastOffset + 4},
                            { "ProcCaOdt", lastOffset + 8 },
                            { "ProcCkOdt", lastOffset + 12 },
                            { "ProcDqOdt", lastOffset + 16 },
                            { "ProcDqsOdt", lastOffset + 20 },
                            { "DramDataDrvStren", lastOffset + 24 },
                            { "RttNomWr", lastOffset + 28 },
                            { "RttNomRd", lastOffset + 32 },
                            { "RttWr", lastOffset + 36 },
                            { "RttPark", lastOffset + 40 },
                            { "RttParkDqs", lastOffset + 44 },

                            { "MemVddio", lastOffset + 88 },
                            { "MemVddq", lastOffset + 92 },
                            { "MemVpp", lastOffset + 96 },
                            { "ApuVddio", lastOffset + 100 }
                        };
                    }
                    return AodDictionaries.AodDataDictionaryV4;
                case Cpu.CodeName.GraniteRidge:
                case Cpu.CodeName.Turin:
                case Cpu.CodeName.TurinD:
                case Cpu.CodeName.ShimadaPeak:
                    if (baseDictionary.Dict != null)
                    {
                        Dictionary<string, int> dict;

                        if (patchLevel > 0xB404022)
                        {
                            dict = new Dictionary<string, int>(baseDictionary.Dict)
                            {
                                { "RttNomWr", lastOffset + 4 },
                                { "RttNomRd", lastOffset + 8 },
                                { "RttWr", lastOffset + 12 },
                                { "RttPark", lastOffset + 16 },
                                { "RttParkDqs", lastOffset + 20 },

                                { "MemVddio", lastOffset + 56 },
                                { "MemVddq", lastOffset + 60 },
                                { "MemVpp", lastOffset + 64 },
                                { "ApuVddio", lastOffset + 68 },

                                { "ProcOdt", lastOffset + 144 },
                                { "ProcOdtPullUp", lastOffset + 144 },
                                { "ProcOdtPullDown", lastOffset + 148 },
                                { "DramDataDrvStren", lastOffset + 152 },
                                { "DramDqDsPullUp", lastOffset + 152 },
                                { "DramDqDsPullDown", lastOffset + 156 },
                                { "ProcCsDs", lastOffset + 160 },
                                { "ProcCkDs", lastOffset + 164 },
                                { "ProcDataDrvStren", lastOffset + 168 },
                                { "ProcDqDsPullUp", lastOffset + 172 },
                                { "ProcDqDsPullDown", lastOffset +  176 },
                            };

                            // Why?
                            if (isMDie && !hasRMP)
                            {
                                dict["ProcOdt"] = lastOffset + 132;
                                dict["ProcOdtPullUp"] = lastOffset + 132;
                                dict["ProcOdtPullDown"] = lastOffset + 136;
                                dict["DramDataDrvStren"] = lastOffset + 140;
                                dict["DramDqDsPullUp"] = lastOffset + 140;
                                dict["DramDqDsPullDown"] = lastOffset + 140;
                                dict["ProcCsDs"] = lastOffset + 144;
                                dict["ProcCkDs"] = lastOffset + 148;
                                dict["ProcDataDrvStren"] = lastOffset + 152;
                                dict["ProcDqDsPullUp"] = lastOffset + 156;
                                dict["ProcDqDsPullDown"] = lastOffset + 160;
                            }

                            return dict;
                        }
                        else
                        {
                            dict = new Dictionary<string, int>(baseDictionary.Dict)
                            {
                                { "ProcDataDrvStren", lastOffset + 4 },

                                { "RttNomWr", lastOffset + 8 },
                                { "RttNomRd", lastOffset + 12 },
                                { "RttWr", lastOffset + 16 },
                                { "RttPark", lastOffset + 20 },
                                { "RttParkDqs", lastOffset + 24 },

                                { "MemVddio", lastOffset + 60 },
                                { "MemVddq", lastOffset + 64 },
                                { "MemVpp", lastOffset + 68 },
                                { "ApuVddio", lastOffset + 72 },

                                { "ProcOdt", lastOffset + 136 },
                                { "ProcOdtPullUp", lastOffset + 136 },
                                { "ProcOdtPullDown", lastOffset + 140 },
                                { "DramDataDrvStren", lastOffset + 144 }
                            };

                            
                            if (isMDie && hasRMP)
                            {
                                dict["ProcOdt"] = lastOffset + 148;
                                dict["ProcOdtPullUp"] = lastOffset + 148;
                                dict["ProcOdtPullDown"] = lastOffset + 152;
                                dict["DramDataDrvStren"] = lastOffset + 156;
                            }

                            return dict;
                        }
                    }

                    return AodDictionaries.AodDataDictionary_1Ah_B404023;
                default:
                    return AodDictionaries.AodDataDictionaryV1;
            }
        }

        public static Dictionary<string, uint> GetWmiFunctions()
        {
            Dictionary<string, uint> dict = new Dictionary<string, uint>();

            try
            {
                string wmiAMDACPI = "AMD_ACPI";
                string wmiScope = "root\\wmi";
                ManagementBaseObject pack;

                string instanceName = WMI.GetInstanceName(wmiScope, wmiAMDACPI);

                if (String.IsNullOrEmpty(instanceName))
                    return dict;

                ManagementObject classInstance = new ManagementObject(wmiScope,
                    $"{wmiAMDACPI}.InstanceName='{instanceName}'",
                    null);

                // Get function names with their IDs
                string[] functionObjects = { "GetObjectID", "GetObjectID2" };

                foreach (var functionObject in functionObjects)
                {
                    try
                    {
                        pack = WMI.InvokeMethodAndGetValue(classInstance, functionObject, "pack", null, 0);

                        if (pack != null)
                        {
                            var ID = (uint[])pack.GetPropertyValue("ID");
                            var IDString = (string[])pack.GetPropertyValue("IDString");
                            var Length = (byte)pack.GetPropertyValue("Length");

                            for (var i = 0; i < Length; ++i)
                            {
                                if (IDString[i] == "")
                                    break;

                                dict.Add(IDString[i], ID[i]);
                            }
                        }
                    }
                    catch
                    {
                        // ignored
                    }
                }
            }
            catch
            {
                // ignored
            }

            return dict;
        }

        public bool Refresh()
        {
            try
            {
                this.Table.RawAodTable = this.io.ReadMemory(new IntPtr(this.Table.BaseAddress), this.Table.Length);
                // this.Table.Data = Utils.ByteArrayToStructure<AodData>(this.Table.rawAodTable);
                // int test = Utils.FindSequence(rawTable, 0, BitConverter.GetBytes(0x3ae));
                this.Table.Data = AodData.CreateFromByteArray(this.Table.RawAodTable, GetAodDataDictionary(this.codeName, this.patchLevel));
                //if (this.Table?.AcpiTable != null)
                //    this.Table.AcpiNames = GetAcpiNames(this.Table.AcpiTable.Value.Data);
                return true;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error refreshing AOD data: {ex.Message}");
            }

            return false;
        }

        private static Dictionary<string, string> GetAcpiNames(byte[] table)
        {
            // ACPI AML Opcode for "Name"
            const byte AML_NAME_OP = 0x08;
            Dictionary<string, string> list = new Dictionary<string, string>();

            if (table == null)
                return list;

            for (int i = 0; i < table.Length; i++)
            {
                // Check for the Name opcode
                if (table[i] == AML_NAME_OP)
                {
                    // Parse the NameString (4 ASCII characters)
                    string name = Encoding.ASCII.GetString(table, i + 1, 4);
                    byte value = table[i + 5];
                    list.Add(name, value.ToString());
                }
            }
            return list;
        }
    }
}
