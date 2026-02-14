using OpenHardwareMonitor.Hardware;
using System;
using static ZenStates.Core.Cpu;

namespace ZenStates.Core
{
    [Serializable]
    public class SystemInfo
    {
        private static CPUInfo cpuInfo;
        public SystemInfo(CPUInfo info, SMU smu, string agesaVersion)
        {
            cpuInfo = info;
            SmuVersion = smu.Version;
            SmuTableVersion = smu.TableVersion;
            AgesaVersion = agesaVersion;
            MbVendor = SMBiosSingleton.Instance.Board.ManufacturerName;
            MbName = SMBiosSingleton.Instance.Board.ProductName;
            BiosVersion = SMBiosSingleton.Instance.Bios.Version;
        }

        public string CpuName => cpuInfo.cpuName ?? "N/A";
        public string CodeName => cpuInfo.codeName.ToString();
        public uint CpuId => cpuInfo.cpuid;
        public uint BaseModel => cpuInfo.baseModel;
        public uint ExtendedModel => cpuInfo.extModel;
        public uint Model => cpuInfo.model;
        public uint Stepping => cpuInfo.stepping;
        // This is not working correctly, it needs mappings for each generation
        // public string PackageType => $"{cpuInfo.packageType} ({(int)cpuInfo.packageType})";
        public int FusedCoreCount => (int)cpuInfo.topology.cores;
        public int PhysicalCoreCount => (int)cpuInfo.topology.physicalCores;
        public int NodesPerProcessor => (int)cpuInfo.topology.cpuNodes;
        public int Threads => (int)cpuInfo.topology.logicalCores;
        public bool SMT => (int)cpuInfo.topology.threadsPerCore > 1;
        // Disable for now, need revising
        // public int CCDCount => (int)cpuInfo.topology.ccds;
        // public int CCXCount => (int)cpuInfo.topology.ccxs;
        // public int NumCoresInCCX => (int)cpuInfo.topology.coresPerCcx;
        public string MbVendor { get; private set; }
        public string MbName { get; private set; }
        public string BiosVersion { get; private set; }
        public string AgesaVersion { get; set; }
        public uint SmuVersion { get; private set; }
        public uint SmuTableVersion { get; private set; }
        public uint PatchLevel => cpuInfo.patchLevel;
        public string GetSmuVersionString() => SmuVersionToString(SmuVersion);

        public string GetCpuIdString() => CpuId.ToString("X8").TrimStart('0');

        public static SMBios SMBios => SMBiosSingleton.Instance;

        private static string SmuVersionToString(uint ver)
        {
            if (ver.Equals(0))
            {
                return "Unknown";
            }

            if ((ver & 0xFF000000) > 0)
            {
                return $"{(ver >> 24) & 0xff}.{(ver >> 16) & 0xff}.{(ver >> 8) & 0xff}.{ver & 0xff}";
            }

            return $"{(ver >> 16) & 0xff}.{(ver >> 8) & 0xff}.{ver & 0xff}";
        }
    }
}
