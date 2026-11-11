using System;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Security.AccessControl;

namespace ZenStates.Core.Drivers
{
    public sealed class IODriver : IDisposable
    {
        // Unicode (LoadLibraryW): the full application path may contain non-ANSI characters.
        [DllImport("kernel32", SetLastError = true, CharSet = CharSet.Unicode)]
        private static extern IntPtr LoadLibrary([MarshalAs(UnmanagedType.LPWStr)] string lpFileName);

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

        public bool IsDeviceAccessRestricted { get; private set; }

        public IODriver()
        {
            try
            {
                string fileName = Utils.Is64BitProcess ? "inpoutx64.dll" : "WinIo32.dll";
                ioModule = LoadDll(fileName);

                if (!Utils.Is64BitProcess)
                {
                    if (NativeMethodsX86.InitializeWinIo())
                    {
                        WinIoStatus = LibStatus.OK;
                    }
                }
                else
                {
                    IsDeviceAccessRestricted = RestrictDeviceAccess();
                }

                _instance = this;
            }
            catch (Exception ex)
            {
                throw new Exception("Error initializing IO module.", ex);
            }
        }

        private static bool RestrictDeviceAccess()
        {
            try
            {
                FileInfo device = new FileInfo(@"\\.\inpoutx64");
                FileSecurity security = device.GetAccessControl();
                security.SetSecurityDescriptorSddlForm("O:BAG:SYD:(A;;FA;;;SY)(A;;FA;;;BA)");
                device.SetAccessControl(security);
                return true;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"IODriver: could not restrict access to the inpoutx64 device ({ex.GetType().Name}): {ex.Message}");
                return false;
            }
        }

        public static IntPtr LoadDll(string filename)
        {
            // Load from the application directory by full path. A bare name goes through the
            // default DLL search order (including the current directory), and this library runs
            // elevated. The later [DllImport]s by bare name bind to this already-loaded module.
            string fullPath = filename;
            if (!Path.IsPathRooted(filename))
            {
                string baseDir = AppDomain.CurrentDomain.BaseDirectory;
                if (!string.IsNullOrEmpty(baseDir))
                    fullPath = Path.Combine(baseDir, filename);
            }

            IntPtr dll = LoadLibrary(fullPath);
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
            if (Utils.Is64BitProcess)
                return NativeMethodsX64.IsInpOutDriverOpen() != 0;
            else
                return WinIoStatus == LibStatus.OK;
        }

        public byte[] ReadMemory(IntPtr baseAddress, int size)
        {
            IntPtr memHandle = IntPtr.Zero;
            IntPtr pdwLinAddr = IntPtr.Zero;

            try
            {
                pdwLinAddr = Utils.Is64BitProcess
                    ? NativeMethodsX64.MapPhysToLin(baseAddress, (uint)size, out memHandle)
                    : NativeMethodsX86.MapPhysToLin(baseAddress, (uint)size, out memHandle);

                if (pdwLinAddr != IntPtr.Zero)
                {
                    byte[] bytes = new byte[size];
                    Marshal.Copy(pdwLinAddr, bytes, 0, bytes.Length);
                    return bytes;
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error reading memory: {ex.Message}");
            }
            finally
            {
                if (pdwLinAddr != IntPtr.Zero)
                {
                    if (Utils.Is64BitProcess)
                        NativeMethodsX64.UnmapPhysicalMemory(memHandle, pdwLinAddr);
                    else
                        NativeMethodsX86.UnmapPhysicalMemory(memHandle, pdwLinAddr);
                }
            }

            return null;
        }

        public byte Inp32(short port) => Utils.Is64BitProcess ? NativeMethodsX64.Inp32(port) : NativeMethodsX86.Inp32(port);
        public void Out32(short port, short value)
        {
            if (Utils.Is64BitProcess) NativeMethodsX64.Out32(port, value);
            else NativeMethodsX86.Out32(port, value);
        }

        public byte DlPortReadPortUchar(ushort port) => Utils.Is64BitProcess ? NativeMethodsX64.DlPortReadPortUchar(port) : NativeMethodsX86.DlPortReadPortUchar(port);
        public void DlPortWritePortUchar(ushort port, byte value)
        {
            if (Utils.Is64BitProcess) NativeMethodsX64.DlPortWritePortUchar(port, value);
            else NativeMethodsX86.DlPortWritePortUchar(port, value);
        }

        public ushort DlPortReadPortUshort(ushort port) => Utils.Is64BitProcess ? NativeMethodsX64.DlPortReadPortUshort(port) : NativeMethodsX86.DlPortReadPortUshort(port);
        public void DlPortWritePortUshort(ushort port, ushort value)
        {
            if (Utils.Is64BitProcess) NativeMethodsX64.DlPortWritePortUshort(port, value);
            else NativeMethodsX86.DlPortWritePortUshort(port, value);
        }

        public uint DlPortReadPortUlong(uint port) => Utils.Is64BitProcess ? NativeMethodsX64.DlPortReadPortUlong(port) : NativeMethodsX86.DlPortReadPortUlong(port);
        public void DlPortWritePortUlong(uint port, uint value)
        {
            if (Utils.Is64BitProcess) NativeMethodsX64.DlPortWritePortUlong(port, value);
            else NativeMethodsX86.DlPortWritePortUlong(port, value);
        }

        public bool GetPhysLong(UIntPtr memAddress, out uint data) => Utils.Is64BitProcess ? NativeMethodsX64.GetPhysLong(memAddress, out data) : NativeMethodsX86.GetPhysLong(memAddress, out data);
        public bool SetPhysLong(UIntPtr memAddress, uint data) => Utils.Is64BitProcess ? NativeMethodsX64.SetPhysLong(memAddress, data) : NativeMethodsX86.SetPhysLong(memAddress, data);

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;

            if (_instance == this)
                _instance = null;

            if (ioModule == IntPtr.Zero) return;

            if (!Utils.Is64BitProcess)
            {
                try { NativeMethodsX86.ShutdownWinIo(); }
                catch
                {
                    // ignored
                }
            }

            FreeLibrary(ioModule);
            ioModule = IntPtr.Zero;
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