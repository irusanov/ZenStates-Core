using Microsoft.Win32;
using System;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Security.AccessControl;
using System.ServiceProcess;

namespace ZenStates.Core.Drivers
{
    public sealed class IODriver : IDisposable
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

        private LibStatus WinIoStatus { get; set; } = LibStatus.INITIALIZE_ERROR;

        private volatile bool _disposed;
        private static IODriver _instance;

        public static IODriver Instance => _instance;

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
                Debug.WriteLine($"Error reading memory: {ex.Message}");
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

        public IODriver()
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

                DlPortReadPortUchar = (_DlPortReadPortUchar)GetDelegate(ioModule, "DlPortReadPortUchar", typeof(_DlPortReadPortUchar));
                DlPortWritePortUchar = (_DlPortWritePortUchar)GetDelegate(ioModule, "DlPortWritePortUchar", typeof(_DlPortWritePortUchar));

                DlPortReadPortUshort = (_DlPortReadPortUshort)GetDelegate(ioModule, "DlPortReadPortUshort", typeof(_DlPortReadPortUshort));
                DlPortWritePortUshort = (_DlPortWritePortUshort)GetDelegate(ioModule, "DlPortWritePortUshort", typeof(_DlPortWritePortUshort));

                DlPortReadPortUlong = (_DlPortReadPortUlong)GetDelegate(ioModule, "DlPortReadPortUlong", typeof(_DlPortReadPortUlong));
                DlPortWritePortUlong = (_DlPortWritePortUlong)GetDelegate(ioModule, "DlPortWritePortUlong", typeof(_DlPortWritePortUlong));

                Inp32 = (_Inp32)GetDelegate(ioModule, "Inp32", typeof(_Inp32));
                Out32 = (_Out32)GetDelegate(ioModule, "Out32", typeof(_Out32));

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

                _instance = this;
            }
            catch (Exception ex)
            {
                throw new Exception("Error initializing IO module.", ex);
            }
        }

        internal delegate byte _Inp32(short port);
        internal readonly _Inp32 Inp32;

        internal delegate void _Out32(short port, short value);
        internal readonly _Out32 Out32;

        // 8-bit
        public delegate byte _DlPortReadPortUchar(ushort port);
        public readonly _DlPortReadPortUchar DlPortReadPortUchar;

        public delegate void _DlPortWritePortUchar(ushort port, byte value);
        public readonly _DlPortWritePortUchar DlPortWritePortUchar;

        // 16-bit
        public delegate ushort _DlPortReadPortUshort(ushort port);
        public readonly _DlPortReadPortUshort DlPortReadPortUshort;

        public delegate void _DlPortWritePortUshort(ushort port, ushort value);
        public readonly _DlPortWritePortUshort DlPortWritePortUshort;

        // 32-bit
        public delegate uint _DlPortReadPortUlong(uint port);
        public readonly _DlPortReadPortUlong DlPortReadPortUlong;

        public delegate void _DlPortWritePortUlong(uint port, uint value);
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
            const string serviceName = "inpoutx64";
            const string registryKeyPath = @"SYSTEM\CurrentControlSet\Services\" + serviceName;
            string driverFilePath = $@"C:\Windows\System32\drivers\{serviceName}.sys";

            // Step 1: Disable auto-start
            try
            {
                using (RegistryKey key = Registry.LocalMachine.OpenSubKey(registryKeyPath, writable: true))
                {
                    if (key != null)
                        key.SetValue("Start", 4, RegistryValueKind.DWord); // SERVICE_DISABLED
                }
                Debug.WriteLine("Service marked as disabled in registry.");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Failed to disable service in registry: {ex.Message}");
            }

            // Step 2: Stop the service. If another process is using the driver the stop
            // will fail or time out — in that case abort further cleanup to avoid leaving
            // the system in an inconsistent state (SCM entry gone but driver still loaded).
            bool serviceStopped = false;
            try
            {
                using (ServiceController serviceController = new ServiceController(serviceName))
                {
                    if (serviceController.Status == ServiceControllerStatus.Stopped)
                    {
                        serviceStopped = true;
                    }
                    else if (serviceController.Status == ServiceControllerStatus.Running)
                    {
                        serviceController.Stop();
                        serviceController.WaitForStatus(ServiceControllerStatus.Stopped, TimeSpan.FromSeconds(10));
                        serviceController.Refresh();
                        serviceStopped = serviceController.Status == ServiceControllerStatus.Stopped;
                    }

                    Debug.WriteLine($"Service status after stop attempt: {serviceController.Status}");
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Failed to stop service: {ex.Message}");
            }

            if (!serviceStopped)
            {
                // Auto-start is already disabled (Step 1). The full cleanup will be
                // attempted the next time Dispose() is called when no other consumer holds the driver.
                Debug.WriteLine("Service could not be stopped (driver in use). Deferring cleanup to next session.");
                return;
            }

            // Step 3: Delete the SCM service entry via sc.exe (more reliable than WMI).
            try
            {
                using (Process process = new Process())
                {
                    process.StartInfo.FileName = "sc.exe";
                    process.StartInfo.Arguments = $"delete \"{serviceName}\"";
                    process.StartInfo.UseShellExecute = false;
                    process.StartInfo.CreateNoWindow = true;
                    process.Start();
                    process.WaitForExit(5000);
                    Debug.WriteLine(process.ExitCode == 0
                        ? "Service deleted successfully."
                        : $"sc.exe delete exited with code {process.ExitCode}.");
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Failed to delete service via sc.exe: {ex.Message}");
            }

            // Step 4: Remove the registry key (belt-and-suspenders alongside sc.exe delete).
            try
            {
                if (Registry.LocalMachine.OpenSubKey(registryKeyPath) != null)
                    Registry.LocalMachine.DeleteSubKeyTree(registryKeyPath);
                Debug.WriteLine("Registry key removed successfully.");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Failed to remove the registry key: {ex.Message}");
            }

            // Step 5: Delete the driver file — the kernel may briefly hold the image locked
            // after the service stops, so retry a few times before giving up.
            if (File.Exists(driverFilePath))
            {
                const int maxAttempts = 5;
                const int retryDelayMs = 500;
                bool deleted = false;

                for (int attempt = 1; attempt <= maxAttempts; attempt++)
                {
                    try
                    {
                        File.Delete(driverFilePath);
                        deleted = true;
                        break;
                    }
                    catch (IOException)
                    {
                        Debug.WriteLine($"Driver file locked, retry {attempt}/{maxAttempts}...");
                        System.Threading.Thread.Sleep(retryDelayMs);
                    }
                    catch (UnauthorizedAccessException)
                    {
                        Debug.WriteLine($"Driver file access denied, retry {attempt}/{maxAttempts}...");
                        System.Threading.Thread.Sleep(retryDelayMs);
                    }
                }

                Debug.WriteLine(deleted ? "Driver file deleted successfully." : "Failed to delete driver file after all retries.");
            }
            else
            {
                Debug.WriteLine("Driver file does not exist.");
            }

            Debug.WriteLine("Driver cleanup completed.");
        }

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;

            if (ioModule == IntPtr.Zero) return;

            //if (Utils.Is64Bit)
            //    System.Threading.ThreadPool.QueueUserWorkItem(_ => CleanupDriver());

            if (!Utils.Is64Bit)
                ShutdownWinIo32?.Invoke();

            FreeLibrary(ioModule);
            ioModule = IntPtr.Zero;

            if (_instance == this)
                _instance = null;
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
