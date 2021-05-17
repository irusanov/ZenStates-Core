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
            PackageType = (uint)cpu.info.packageType;
            PatchLevel = cpu.info.patchLevel;
            SmuVersion = cpu.smu.Version;
            SmuTableVersion = cpu.smu.TableVersion;
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

            try
            {
                var scope = new ManagementScope(@"root\cimv2");
                if (!scope.IsConnected)
                    throw new ManagementException(@"Failed to connect to root\cimv2");

                ManagementObjectSearcher searcher = new ManagementObjectSearcher("SELECT * FROM Win32_BaseBoard");
                if (searcher != null)
                {
                    foreach (ManagementObject obj in searcher.Get())
                    {
                        MbVendor = ((string)obj["Manufacturer"]).Trim();
                        MbName = ((string)obj["Product"]).Trim();
                    }
                    searcher.Dispose();
                }

                searcher = new ManagementObjectSearcher("SELECT * FROM Win32_BIOS");
                if (searcher != null) {
                    foreach (ManagementObject obj in searcher.Get())
                    {
                        BiosVersion = ((string)obj["SMBIOSBIOSVersion"]).Trim();
                    }
                    searcher.Dispose();
                }
            }
            catch (ManagementException ex)
            {
                Console.WriteLine("WMI: {0}", ex.Message.ToString());
            }
        }

        public SystemInfo()
        {
            Init(new Cpu());
        }

        public SystemInfo(Cpu cpu)
        {
            if (cpu == null)
                throw new ArgumentNullException("CPU module is not initialized.");

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
        public uint SmuTableVersion { get; set; }
        public uint PatchLevel { get; set; }

        private static string SmuVersionToString(uint ver)
        {

            if ((ver & 0xFF000000) > 0)
            {
                return string.Format("{0}.{1}.{2}.{3}", 
                    (ver >> 24) & 0xff, (ver >> 16) & 0xff, (ver >> 8) & 0xff, ver & 0xff);
            }
            else
            {
                return string.Format("{0}.{1}.{2}",
                    (ver >> 16) & 0xff, (ver >> 8) & 0xff, ver & 0xff);
            }
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
