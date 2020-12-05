using System;
using System.Management;

namespace ZenStates.Core
{
    [Serializable]
    public class SystemInfo
    {
        private void Init(Cpu cpu)
        {
            CpuId = cpu.info.cpuid;
            CpuName = cpu.info.cpuName;
            NodesPerProcessor = cpu.GetCpuNodes();
            PackageType = cpu.info.packageType;
            PatchLevel = cpu.info.patchLevel;
            SmuVersion = cpu.smu.Version;
            FusedCoreCount = (int)cpu.info.cores;
            Threads = (int)cpu.info.logicalCores;
            CCDCount = (int)cpu.info.ccds;
            CCXCount = (int)cpu.info.ccxs;
            NumCoresInCCX = (int)cpu.info.coresPerCcx;
            PhysicalCoreCount = (int)cpu.info.physicalCores;
            CodeName = $"{cpu.info.codeName}";
            SMT = (int)cpu.info.threadsPerCore > 1;
            Model = cpu.info.model;
            ExtendedModel = cpu.info.extModel;

            ManagementObjectSearcher searcher = new ManagementObjectSearcher("SELECT * FROM Win32_BaseBoard");
            foreach (ManagementObject obj in searcher.Get())
            {
                MbVendor = ((string)obj["Manufacturer"]).Trim();
                MbName = ((string)obj["Product"]).Trim();
            }
            if (searcher != null) searcher.Dispose();

            searcher = new ManagementObjectSearcher("SELECT * FROM Win32_BIOS");
            foreach (ManagementObject obj in searcher.Get())
            {
                BiosVersion = ((string)obj["SMBIOSBIOSVersion"]).Trim();
            }
            if (searcher != null) searcher.Dispose();
        }

        public SystemInfo()
        {
            Init(new Cpu());
        }

        public SystemInfo(Cpu cpu)
        {
            if (cpu == null)
                throw new ArgumentNullException("cpu is not initialized");

            Init(cpu);
        }

        public string CpuName { get; set; }
        public string CodeName { get; set; }
        public uint CpuId { get; set; }
        public uint Model { get; set; }
        public uint ExtendedModel { get; set; }
        public uint PackageType { get; set; }
        public int FusedCoreCount { get; set; }
        public int PhysicalCoreCount { get; set; }
        public int NodesPerProcessor { get; set; }
        public int Threads { get; set; }
        public bool SMT { get; set; }

        public int CCDCount { get; set; }
        public int CCXCount { get; set; }
        public int NumCoresInCCX { get; set; }
        public string MbVendor { get; set; }
        public string MbName { get; set; }
        public string BiosVersion { get; set; }
        public uint SmuVersion { get; set; }
        public uint PatchLevel { get; set; }

        private static string SmuVersionToString(uint version)
        {
            string[] versionString = new string[3];
            versionString[0] = ((version & 0x00FF0000) >> 16).ToString("D2");
            versionString[1] = ((version & 0x0000FF00) >> 8).ToString("D2");
            versionString[2] = (version & 0x000000FF).ToString("D2");

            return string.Join(".", versionString);
        }

        public string GetSmuVersionString()
        {
            return SmuVersionToString(SmuVersion);
        }

        public string GetCpuIdString()
        {
            return CpuId.ToString("X8").TrimStart('0');
        }
    }
}
