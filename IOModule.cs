using System;
using System.ComponentModel;
using System.Runtime.InteropServices;

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

            return null;
        }

        public IOModule()
        {
            string fileName = Utils.Is64Bit ? "inpoutx64.dll" : "WinIo32.dll";

            ioModule = LoadLibrary(fileName);

            if (ioModule == IntPtr.Zero)
            {
                int lasterror = Marshal.GetLastWin32Error();
                Win32Exception innerEx = new Win32Exception(lasterror);
                innerEx.Data.Add("LastWin32Error", lasterror);

                throw new Exception("Can't load DLL " + fileName, innerEx);
            }

            // Common
            GetPhysLong = (_GetPhysLong)GetDelegate(ioModule, "GetPhysLong", typeof(_GetPhysLong));
            MapPhysToLin = (_MapPhysToLin)GetDelegate(ioModule, "MapPhysToLin", typeof(_MapPhysToLin));
            UnmapPhysicalMemory = (_UnmapPhysicalMemory)GetDelegate(ioModule, "UnmapPhysicalMemory", typeof(_UnmapPhysicalMemory));

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

        public delegate bool _GetPhysLong(UIntPtr memAddress, out uint data);
        public readonly _GetPhysLong GetPhysLong;

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


        public void Dispose()
        {
            if (ioModule == IntPtr.Zero) return;
            if (!Utils.Is64Bit)
            {
                ShutdownWinIo32();
            }

            FreeLibrary(ioModule);
            ioModule = IntPtr.Zero;
        }

        private static Delegate GetDelegate(IntPtr moduleName, string procName, Type delegateType)
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
