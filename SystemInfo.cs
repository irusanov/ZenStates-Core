using OpenHardwareMonitor.Hardware;
using System;
using static ZenStates.Core.Cpu;

namespace ZenStates.Core
{
    [Serializable]
    public class SystemInfo
    {
        private readonly CPUInfo _cpuInfo;

        public SystemInfo(CPUInfo info, SMU smu, string agesaVersion)
        {
            _cpuInfo = info;
            SmuVersion = smu.Version;
            SmuTableVersion = smu.TableVersion;
            SmuType = smu.SMU_TYPE.ToString();
            AgesaVersion = agesaVersion;

            SMBios smbios = SMBiosSingleton.Instance;
            MbVendor = smbios?.Board?.ManufacturerName ?? "N/A";
            MbName = smbios?.Board?.ProductName ?? "N/A";
            BiosVersion = smbios?.Bios?.Version ?? "N/A";
        }

        // CPU identity
        public string CpuName => _cpuInfo.cpuName ?? "N/A";
        public string Vendor => _cpuInfo.vendor ?? "N/A";
        public string CodeName => _cpuInfo.codeName.ToString();
        public uint CpuId => _cpuInfo.cpuid;
        public string CpuIdString => CpuId.ToString("X8").TrimStart('0');
        public uint BaseModel => _cpuInfo.baseModel;
        public uint ExtendedModel => _cpuInfo.extModel;
        public uint Model => _cpuInfo.model;
        public uint Stepping => _cpuInfo.stepping;
        public uint PatchLevel => _cpuInfo.patchLevel;
        // This is not working correctly, it needs mappings for each generation
        // public string PackageType => $"{_cpuInfo.packageType} ({(int)_cpuInfo.packageType})";

        // Topology
        public int FusedCoreCount => (int)_cpuInfo.topology.cores;
        public int PhysicalCoreCount => (int)_cpuInfo.topology.physicalCores;
        public int NodesPerProcessor => (int)_cpuInfo.topology.cpuNodes;
        public int Threads => (int)_cpuInfo.topology.logicalCores;
        public bool SMT => _cpuInfo.topology.threadsPerCore > 1;
        public int CCDCount => (int)_cpuInfo.topology.ccds;
        public int CCXCount => (int)_cpuInfo.topology.ccxs;
        public int CoresPerCCX => (int)_cpuInfo.topology.coresPerCcx;

        // Board / BIOS
        public string MbVendor { get; private set; }
        public string MbName { get; private set; }
        public string BiosVersion { get; private set; }
        public string AgesaVersion { get; set; }

        // SMU
        public uint SmuVersion { get; private set; }
        public uint SmuTableVersion { get; private set; }
        public string SmuType { get; private set; }
        public string SmuVersionString => SmuVersionToString(SmuVersion);

        // Static access to SMBios
        public static SMBios SMBios => SMBiosSingleton.Instance;

        [Obsolete("Use SmuVersionString property instead.")]
        public string GetSmuVersionString() => SmuVersionString;

        [Obsolete("Use CpuIdString property instead.")]
        public string GetCpuIdString() => CpuIdString;

        private static string SmuVersionToString(uint ver)
        {
            if (ver == 0)
                return "Unknown";

            if ((ver & 0xFF000000) > 0)
                return $"{(ver >> 24) & 0xff}.{(ver >> 16) & 0xff}.{(ver >> 8) & 0xff}.{ver & 0xff}";

            return $"{(ver >> 16) & 0xff}.{(ver >> 8) & 0xff}.{ver & 0xff}";
        }
    }
}
