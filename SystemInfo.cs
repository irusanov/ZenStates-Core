using System;
using System.Management;
using System.ServiceProcess;

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
                    var mo = (ManagementObject) obj;
                    MbVendor = ((string)mo["Manufacturer"]).Trim();
                    MbName = ((string)mo["Product"]).Trim();
                }
                searcher.Dispose();

                searcher = new ManagementObjectSearcher("SELECT * FROM Win32_BIOS");
                foreach (var obj in searcher.Get())
                {
                    var mo = (ManagementObject) obj;
                    BiosVersion = ((string)mo["SMBIOSBIOSVersion"]).Trim();
                }
                searcher.Dispose();
            }
            catch (ManagementException ex)
            {
                Console.WriteLine("WMI: {0}", ex.Message);
            }
        }

        public SystemInfo()
        {
            Init(new Cpu());
        }

        public SystemInfo(Cpu cpu)
        {
            if (cpu == null)
                throw new ArgumentNullException(nameof(cpu), "CPU module is not initialized.");

            Init(cpu);
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
