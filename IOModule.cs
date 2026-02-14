using System;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Management;
using System.Runtime.InteropServices;
using System.Security.AccessControl;
using System.ServiceProcess;

namespace ZenStates.Core
{
    public sealed class IOModule : IDisposable
    {
        [DllImport("kernel32", SetLastError = true, CharSet = CharSet.Ansi)]
        private static extern IntPtr LoadLibrary([MarshalAs(UnmanagedType.LPStr)] string lpFileName);

        [DllImport("kernel32", SetLastError = true)]
        private static extern bool FreeLibrary(IntPtr hModule);

        [DllImport("kernel32", CharSet = CharSet.Ansi, SetLastError = true, ExactSpelling = false)]
        private static extern IntPtr GetProcAddress(IntPtr hModule, [MarshalAs(UnmanagedType.LPStr)] string lpProcName);

        internal IntPtr ioModule;

        public enum LibStatus
        {
            INITIALIZE_ERROR = 0,
            OK = 1,
            PARTIALLY_OK = 2
        }

        private LibStatus WinIoStatus { get; } = LibStatus.INITIALIZE_ERROR;

        public bool IsInpOutDriverOpen()
        {
            if (Utils.Is64Bit)
                return IsInpOutDriverOpen64() != 0;
            else
                return WinIoStatus == LibStatus.OK;
        }

        public byte[] ReadMemory(IntPtr baseAddress, int size)
        {
            try
            {
                if (MapPhysToLin != null && UnmapPhysicalMemory != null)
                {
                    IntPtr pdwLinAddr = MapPhysToLin(baseAddress, (uint)size, out IntPtr pPhysicalMemoryHandle);
                    if (pdwLinAddr != IntPtr.Zero)
                    {
                        byte[] bytes = new byte[size];
                        Marshal.Copy(pdwLinAddr, bytes, 0, bytes.Length);
                        UnmapPhysicalMemory(pPhysicalMemoryHandle, pdwLinAddr);

                        return bytes;
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error reading memory: {ex.Message}");
            }

            return null;
        }

        public static IntPtr LoadDll(string filename)
        {
            IntPtr dll = LoadLibrary(filename);

            if (dll == IntPtr.Zero)
            {
                int lasterror = Marshal.GetLastWin32Error();
                Win32Exception innerEx = new Win32Exception(lasterror);
                innerEx.Data.Add("LastWin32Error", lasterror);

                throw new Exception("Can't load DLL " + filename, innerEx);
            }

            return dll;
        }

        public IOModule()
        {
            try
            {
                // restrict the driver access to system (SY) and builtin admins (BA)
                // TODO: replace with a call to IoCreateDeviceSecure in the driver
                string filePath = @"\\.\inpoutx64";
                FileInfo fileInfo = new FileInfo(filePath);
                FileSecurity fileSecurity = fileInfo.GetAccessControl();
                fileSecurity.SetSecurityDescriptorSddlForm("O:BAG:SYD:(A;;FA;;;SY)(A;;FA;;;BA)");
                fileInfo.SetAccessControl(fileSecurity);
            }
            catch { }

            try
            {
                string fileName = Utils.Is64Bit ? "inpoutx64.dll" : "WinIo32.dll";
                ioModule = LoadDll(fileName);

                // Common
                GetPhysLong = (_GetPhysLong)GetDelegate(ioModule, "GetPhysLong", typeof(_GetPhysLong));
                SetPhysLong = (_SetPhysLong)GetDelegate(ioModule, "SetPhysLong", typeof(_SetPhysLong));
                MapPhysToLin = (_MapPhysToLin)GetDelegate(ioModule, "MapPhysToLin", typeof(_MapPhysToLin));
                UnmapPhysicalMemory = (_UnmapPhysicalMemory)GetDelegate(ioModule, "UnmapPhysicalMemory", typeof(_UnmapPhysicalMemory));
                DlPortReadPortUlong = (_DlPortReadPortUlong)GetDelegate(ioModule, "DlPortReadPortUlong", typeof(_DlPortReadPortUlong));
                DlPortWritePortUlong = (_DlPortWritePortUlong)GetDelegate(ioModule, "DlPortWritePortUlong", typeof(_DlPortWritePortUlong));

                // 64bit only
                if (Utils.Is64Bit)
                {
                    IsInpOutDriverOpen64 = (_IsInpOutDriverOpen64)GetDelegate(ioModule, "IsInpOutDriverOpen", typeof(_IsInpOutDriverOpen64));
                }
                else
                {
                    InitializeWinIo32 = (_InitializeWinIo32)GetDelegate(ioModule, "InitializeWinIo", typeof(_InitializeWinIo32));
                    ShutdownWinIo32 = (_ShutdownWinIo32)GetDelegate(ioModule, "ShutdownWinIo", typeof(_ShutdownWinIo32));

                    if (InitializeWinIo32())
                    {
                        WinIoStatus = LibStatus.OK;
                    }
                }
            }
            catch (Exception ex)
            {
                throw new Exception($"Error initializing IO module: {ex.Message}");
            }
        }

        public delegate ulong _DlPortReadPortUlong(ushort port);
        public readonly _DlPortReadPortUlong DlPortReadPortUlong;

        public delegate void _DlPortWritePortUlong(ushort port, ulong value);
        public readonly _DlPortWritePortUlong DlPortWritePortUlong;

        public delegate bool _GetPhysLong(UIntPtr memAddress, out uint data);
        public readonly _GetPhysLong GetPhysLong;

        public delegate bool _SetPhysLong(UIntPtr memAddress, uint data);
        public readonly _SetPhysLong SetPhysLong;

        private delegate IntPtr _MapPhysToLin(IntPtr pbPhysAddr, uint dwPhysSize, out IntPtr pPhysicalMemoryHandle);
        private readonly _MapPhysToLin MapPhysToLin;

        private delegate bool _UnmapPhysicalMemory(IntPtr PhysicalMemoryHandle, IntPtr pbLinAddr);
        private readonly _UnmapPhysicalMemory UnmapPhysicalMemory;

        // InpOutx64
        private delegate uint _IsInpOutDriverOpen64();
        private readonly _IsInpOutDriverOpen64 IsInpOutDriverOpen64;

        // WinIo
        private delegate bool _InitializeWinIo32();
        private delegate bool _ShutdownWinIo32();
        private readonly _InitializeWinIo32 InitializeWinIo32;
        private readonly _ShutdownWinIo32 ShutdownWinIo32;

        private void CleanupDriver()
        {
            if (IsInpOutDriverOpen())
                return;

            string serviceName = "inpoutx64";
            string registryKeyPath = @"SYSTEM\CurrentControlSet\Services\" + serviceName;
            string driverFilePath = $@"C:\Windows\System32\drivers\{serviceName}.sys";

            try
            {
                // Stop the service
                using (ServiceController serviceController = new ServiceController(serviceName))
                {
                    if (serviceController.Status == ServiceControllerStatus.Running)
                    {
                        serviceController.Stop();
                        serviceController.WaitForStatus(ServiceControllerStatus.Stopped, new TimeSpan(10000));
                        Console.WriteLine("Service stopped successfully.");
                    }

                    if (serviceController.Status != ServiceControllerStatus.Stopped)
                    {

                        Console.WriteLine("Failed to stop the service.");
                        return;
                    }
                }

                // Delete the service
                using (ManagementObject service = new ManagementObject($"Win32_Service.Name='{serviceName}'"))
                {
                    service.Delete();
                    Console.WriteLine("Service deleted successfully.");
                }


                // Remove the registry key using the regedit command-line tool
                Process regeditProcess = new Process();
                regeditProcess.StartInfo.FileName = "regedit";
                regeditProcess.StartInfo.Arguments = $"/s /f \"{registryKeyPath}\"";
                regeditProcess.StartInfo.UseShellExecute = false;
                regeditProcess.Start();

                regeditProcess.WaitForExit();

                if (regeditProcess.ExitCode == 0)
                {
                    Console.WriteLine("Registry key removed successfully.");
                }
                else
                {
                    Console.WriteLine("Failed to remove the registry key.");
                }

                // Delete the driver file
                if (File.Exists(driverFilePath))
                {
                    File.Delete(driverFilePath);
                    Console.WriteLine("Driver file deleted successfully.");
                }
                else
                {
                    Console.WriteLine("Driver file does not exist.");
                }

                Console.WriteLine("Process completed.");
                Console.ReadLine();
            }
            catch (Exception ex)
            {
                Console.WriteLine("An error occurred:");
                Console.WriteLine(ex.Message);
            }
        }

        public void Dispose()
        {
            if (ioModule == IntPtr.Zero) return;
            if (!Utils.Is64Bit)
                ShutdownWinIo32();

            FreeLibrary(ioModule);
            ioModule = IntPtr.Zero;

            //if (Utils.Is64Bit)
            //    CleanupDriver();
        }

        public static Delegate GetDelegate(IntPtr moduleName, string procName, Type delegateType)
        {
            IntPtr ptr = GetProcAddress(moduleName, procName);
            if (ptr != IntPtr.Zero)
            {
                Delegate d = Marshal.GetDelegateForFunctionPointer(ptr, delegateType);
                return d;
            }

            int result = Marshal.GetHRForLastWin32Error();
            throw Marshal.GetExceptionForHR(result);
        }
    }
}
