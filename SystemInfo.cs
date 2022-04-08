using System;
using System.Management;
using System.ServiceProcess;
using static ZenStates.Core.Cpu;

namespace ZenStates.Core
{
    [Serializable]
    public class SystemInfo
    {
        private static CPUInfo cpuInfo;
        public SystemInfo(CPUInfo info, SMU smu)
        {
            cpuInfo = info;
            SmuVersion = smu.Version;
            SmuTableVersion = smu.TableVersion;

            try
            {
                var sc = new ServiceController("Winmgmt");
                if (sc.Status != ServiceControllerStatus.Running)
                    throw new ManagementException(@"Windows Management Instrumentation service is not running");

                var scope = new ManagementScope(@"root\cimv2");
                scope.Connect();

                if (!scope.IsConnected)
                    throw new ManagementException(@"Failed to connect to root\cimv2");

                ManagementObjectSearcher searcher = new ManagementObjectSearcher("SELECT * FROM Win32_BaseBoard");
                foreach (var obj in searcher.Get())
                {
                    var mo = (ManagementObject)obj;
                    MbVendor = ((string)mo["Manufacturer"]).Trim();
                    MbName = ((string)mo["Product"]).Trim();
                }
                searcher.Dispose();

                searcher = new ManagementObjectSearcher("SELECT * FROM Win32_BIOS");
                foreach (var obj in searcher.Get())
                {
                    var mo = (ManagementObject)obj;
                    BiosVersion = ((string)mo["SMBIOSBIOSVersion"]).Trim();
                }
                searcher.Dispose();
            }
            catch (ManagementException ex)
            {
                Console.WriteLine("WMI: {0}", ex.Message);
            }
        }

        public string CpuName => cpuInfo.cpuName != null ? cpuInfo.cpuName : "N/A";
        public string CodeName => cpuInfo.codeName.ToString();
        public uint CpuId => cpuInfo.cpuid;
        public uint Model => cpuInfo.model;
        public uint ExtendedModel => cpuInfo.extModel;
        public string PackageType => $"{cpuInfo.packageType} ({(int)cpuInfo.packageType})";
        public int FusedCoreCount => (int)cpuInfo.cores;
        public int PhysicalCoreCount => (int)cpuInfo.physicalCores;
        public int NodesPerProcessor => (int)cpuInfo.cpuNodes;
        public int Threads => (int)cpuInfo.logicalCores;
        public bool SMT => (int)cpuInfo.threadsPerCore > 1;
        public int CCDCount => (int)cpuInfo.ccds;
        public int CCXCount => (int)cpuInfo.ccxs;
        public int NumCoresInCCX => (int)cpuInfo.coresPerCcx;
        public string MbVendor { get; private set; }
        public string MbName { get; private set; }
        public string BiosVersion { get; private set; }
        public uint SmuVersion { get; private set; }
        public uint SmuTableVersion { get; private set; }
        public uint PatchLevel => cpuInfo.patchLevel;
        public string GetSmuVersionString() => SmuVersionToString(SmuVersion);

        public string GetCpuIdString() => CpuId.ToString("X8").TrimStart('0');

        private static string SmuVersionToString(uint ver)
        {

            if ((ver & 0xFF000000) > 0)
            {
                return $"{(ver >> 24) & 0xff}.{(ver >> 16) & 0xff}.{(ver >> 8) & 0xff}.{ver & 0xff}";
            }

            return $"{(ver >> 16) & 0xff}.{(ver >> 8) & 0xff}.{ver & 0xff}";
        }
    }
}
