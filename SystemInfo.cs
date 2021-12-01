using System;
using System.Management;
using System.ServiceProcess;
using static ZenStates.Core.Cpu;

namespace ZenStates.Core
{
    [Serializable]
    public class SystemInfo
    {
        public SystemInfo(CPUInfo info, SMU smu)
        {
            CpuId = info.cpuid;
            CpuName = info.cpuName != null ? info.cpuName : "N/A";
            NodesPerProcessor = (int)info.cpuNodes;
            PackageType = (uint)info.packageType;
            PatchLevel = info.patchLevel;
            SmuVersion = smu.Version;
            SmuTableVersion = smu.TableVersion;
            FusedCoreCount = (int)info.cores;
            Threads = (int)info.logicalCores;
            CCDCount = (int)info.ccds;
            CCXCount = (int)info.ccxs;
            NumCoresInCCX = (int)info.coresPerCcx;
            PhysicalCoreCount = (int)info.physicalCores;
            CodeName = $"{info.codeName}";
            SMT = (int)info.threadsPerCore > 1;
            Model = info.model;
            ExtendedModel = info.extModel;

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

        public string CpuName { get; private set; }
        public string CodeName { get; private set; }
        public uint CpuId { get; private set; }
        public uint Model { get; private set; }
        public uint ExtendedModel { get; private set; }
        public uint PackageType { get; private set; }
        public int FusedCoreCount { get; private set; }
        public int PhysicalCoreCount { get; private set; }
        public int NodesPerProcessor { get; private set; }
        public int Threads { get; private set; }
        public bool SMT { get; private set; }
        public int CCDCount { get; private set; }
        public int CCXCount { get; private set; }
        public int NumCoresInCCX { get; private set; }
        public string MbVendor { get; private set; }
        public string MbName { get; private set; }
        public string BiosVersion { get; private set; }
        public uint SmuVersion { get; private set; }
        public uint SmuTableVersion { get; private set; }
        public uint PatchLevel { get; private set; }

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
