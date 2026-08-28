using Microsoft.Win32;
using System;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Management;
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

        public IODriver()
        {
            try
            {
                string fileName = Utils.Is64Bit ? "inpoutx64.dll" : "WinIo32.dll";
                ioModule = LoadDll(fileName);

                if (!Utils.Is64Bit)
                {
                    if (NativeMethodsX86.InitializeWinIo())
                    {
                        WinIoStatus = LibStatus.OK;
                    }
                }
                else
                {
                    try
                    {
                        // restrict the driver access to system (SY) and builtin admins (BA)
                        string filePath = @"\\.\inpoutx64";
                        FileInfo fileInfo = new FileInfo(filePath);
                        FileSecurity fileSecurity = fileInfo.GetAccessControl();
                        fileSecurity.SetSecurityDescriptorSddlForm("O:BAG:SYD:(A;;FA;;;SY)(A;;FA;;;BA)");
                        fileInfo.SetAccessControl(fileSecurity);
                    }
                    catch { }
                }

                _instance = this;
            }
            catch (Exception ex)
            {
                throw new Exception("Error initializing IO module.", ex);
            }
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

        public bool IsInpOutDriverOpen()
        {
            if (Utils.Is64Bit)
                return NativeMethodsX64.IsInpOutDriverOpen() != 0;
            else
                return WinIoStatus == LibStatus.OK;
        }

        public byte[] ReadMemory(IntPtr baseAddress, int size)
        {
            try
            {
                IntPtr pdwLinAddr = Utils.Is64Bit 
                    ? NativeMethodsX64.MapPhysToLin(baseAddress, (uint)size, out IntPtr memHandle64) 
                    : NativeMethodsX86.MapPhysToLin(baseAddress, (uint)size, out memHandle64);

                if (pdwLinAddr != IntPtr.Zero)
                {
                    byte[] bytes = new byte[size];
                    Marshal.Copy(pdwLinAddr, bytes, 0, bytes.Length);

                    if (Utils.Is64Bit)
                        NativeMethodsX64.UnmapPhysicalMemory(memHandle64, pdwLinAddr);
                    else
                        NativeMethodsX86.UnmapPhysicalMemory(memHandle64, pdwLinAddr);

                    return bytes;
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error reading memory: {ex.Message}");
            }

            return null;
        }

        public byte Inp32(short port) => Utils.Is64Bit ? NativeMethodsX64.Inp32(port) : NativeMethodsX86.Inp32(port);
        public void Out32(short port, short value)
        {
            if (Utils.Is64Bit) NativeMethodsX64.Out32(port, value);
            else NativeMethodsX86.Out32(port, value);
        }

        public byte DlPortReadPortUchar(ushort port) => Utils.Is64Bit ? NativeMethodsX64.DlPortReadPortUchar(port) : NativeMethodsX86.DlPortReadPortUchar(port);
        public void DlPortWritePortUchar(ushort port, byte value)
        {
            if (Utils.Is64Bit) NativeMethodsX64.DlPortWritePortUchar(port, value);
            else NativeMethodsX86.DlPortWritePortUchar(port, value);
        }

        public ushort DlPortReadPortUshort(ushort port) => Utils.Is64Bit ? NativeMethodsX64.DlPortReadPortUshort(port) : NativeMethodsX86.DlPortReadPortUshort(port);
        public void DlPortWritePortUshort(ushort port, ushort value)
        {
            if (Utils.Is64Bit) NativeMethodsX64.DlPortWritePortUshort(port, value);
            else NativeMethodsX86.DlPortWritePortUshort(port, value);
        }

        public uint DlPortReadPortUlong(uint port) => Utils.Is64Bit ? NativeMethodsX64.DlPortReadPortUlong(port) : NativeMethodsX86.DlPortReadPortUlong(port);
        public void DlPortWritePortUlong(uint port, uint value)
        {
            if (Utils.Is64Bit) NativeMethodsX64.DlPortWritePortUlong(port, value);
            else NativeMethodsX86.DlPortWritePortUlong(port, value);
        }

        public bool GetPhysLong(UIntPtr memAddress, out uint data) => Utils.Is64Bit ? NativeMethodsX64.GetPhysLong(memAddress, out data) : NativeMethodsX86.GetPhysLong(memAddress, out data);
        public bool SetPhysLong(UIntPtr memAddress, uint data) => Utils.Is64Bit ? NativeMethodsX64.SetPhysLong(memAddress, data) : NativeMethodsX86.SetPhysLong(memAddress, data);

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

            if (!Utils.Is64Bit)
            {
                try { NativeMethodsX86.ShutdownWinIo(); }
                catch
                {
                    // ignored
                }
            }

            FreeLibrary(ioModule);
            ioModule = IntPtr.Zero;

            if (_instance == this)
                _instance = null;
        }

        private static class NativeMethodsX64
        {
            private const string Dll = "inpoutx64.dll";

            [DllImport(Dll, EntryPoint = "GetPhysLong", CallingConvention = CallingConvention.StdCall)]
            public static extern bool GetPhysLong(UIntPtr memAddress, out uint data);

            [DllImport(Dll, EntryPoint = "SetPhysLong", CallingConvention = CallingConvention.StdCall)]
            public static extern bool SetPhysLong(UIntPtr memAddress, uint data);

            [DllImport(Dll, EntryPoint = "MapPhysToLin", CallingConvention = CallingConvention.StdCall)]
            public static extern IntPtr MapPhysToLin(IntPtr pbPhysAddr, uint dwPhysSize, out IntPtr pPhysicalMemoryHandle);

            [DllImport(Dll, EntryPoint = "UnmapPhysicalMemory", CallingConvention = CallingConvention.StdCall)]
            public static extern bool UnmapPhysicalMemory(IntPtr physicalMemoryHandle, IntPtr pbLinAddr);

            [DllImport(Dll, EntryPoint = "Inp32", CallingConvention = CallingConvention.StdCall)]
            public static extern byte Inp32(short port);

            [DllImport(Dll, EntryPoint = "Out32", CallingConvention = CallingConvention.StdCall)]
            public static extern void Out32(short port, short value);

            [DllImport(Dll, EntryPoint = "DlPortReadPortUchar", CallingConvention = CallingConvention.StdCall)]
            public static extern byte DlPortReadPortUchar(ushort port);

            [DllImport(Dll, EntryPoint = "DlPortWritePortUchar", CallingConvention = CallingConvention.StdCall)]
            public static extern void DlPortWritePortUchar(ushort port, byte value);

            [DllImport(Dll, EntryPoint = "DlPortReadPortUshort", CallingConvention = CallingConvention.StdCall)]
            public static extern ushort DlPortReadPortUshort(ushort port);

            [DllImport(Dll, EntryPoint = "DlPortWritePortUshort", CallingConvention = CallingConvention.StdCall)]
            public static extern void DlPortWritePortUshort(ushort port, ushort value);

            [DllImport(Dll, EntryPoint = "DlPortReadPortUlong", CallingConvention = CallingConvention.StdCall)]
            public static extern uint DlPortReadPortUlong(uint port);

            [DllImport(Dll, EntryPoint = "DlPortWritePortUlong", CallingConvention = CallingConvention.StdCall)]
            public static extern void DlPortWritePortUlong(uint port, uint value);

            [DllImport(Dll, EntryPoint = "IsInpOutDriverOpen", CallingConvention = CallingConvention.StdCall)]
            public static extern uint IsInpOutDriverOpen();
        }

        private static class NativeMethodsX86
        {
            private const string Dll = "WinIo32.dll";

            [DllImport(Dll, EntryPoint = "GetPhysLong", CallingConvention = CallingConvention.StdCall)]
            public static extern bool GetPhysLong(UIntPtr memAddress, out uint data);

            [DllImport(Dll, EntryPoint = "SetPhysLong", CallingConvention = CallingConvention.StdCall)]
            public static extern bool SetPhysLong(UIntPtr memAddress, uint data);

            [DllImport(Dll, EntryPoint = "MapPhysToLin", CallingConvention = CallingConvention.StdCall)]
            public static extern IntPtr MapPhysToLin(IntPtr pbPhysAddr, uint dwPhysSize, out IntPtr pPhysicalMemoryHandle);

            [DllImport(Dll, EntryPoint = "UnmapPhysicalMemory", CallingConvention = CallingConvention.StdCall)]
            public static extern bool UnmapPhysicalMemory(IntPtr PhysicalMemoryHandle, IntPtr pbLinAddr);

            [DllImport(Dll, EntryPoint = "Inp32", CallingConvention = CallingConvention.StdCall)]
            public static extern byte Inp32(short port);

            [DllImport(Dll, EntryPoint = "Out32", CallingConvention = CallingConvention.StdCall)]
            public static extern void Out32(short port, short value);

            [DllImport(Dll, EntryPoint = "DlPortReadPortUchar", CallingConvention = CallingConvention.StdCall)]
            public static extern byte DlPortReadPortUchar(ushort port);

            [DllImport(Dll, EntryPoint = "DlPortWritePortUchar", CallingConvention = CallingConvention.StdCall)]
            public static extern void DlPortWritePortUchar(ushort port, byte value);

            [DllImport(Dll, EntryPoint = "DlPortReadPortUshort", CallingConvention = CallingConvention.StdCall)]
            public static extern ushort DlPortReadPortUshort(ushort port);

            [DllImport(Dll, EntryPoint = "DlPortWritePortUshort", CallingConvention = CallingConvention.StdCall)]
            public static extern void DlPortWritePortUshort(ushort port, ushort value);

            [DllImport(Dll, EntryPoint = "DlPortReadPortUlong", CallingConvention = CallingConvention.StdCall)]
            public static extern uint DlPortReadPortUlong(uint port);

            [DllImport(Dll, EntryPoint = "DlPortWritePortUlong", CallingConvention = CallingConvention.StdCall)]
            public static extern void DlPortWritePortUlong(uint port, uint value);

            [DllImport(Dll, EntryPoint = "InitializeWinIo", CallingConvention = CallingConvention.StdCall)]
            public static extern bool InitializeWinIo();

            [DllImport(Dll, EntryPoint = "ShutdownWinIo", CallingConvention = CallingConvention.StdCall)]
            public static extern bool ShutdownWinIo();
        }
    }
}