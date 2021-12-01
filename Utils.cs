using System;
using System.Runtime.InteropServices;

namespace ZenStates.Core
{
    public class Utils : IDisposable
    {
        [DllImport("kernel32")]
        public static extern IntPtr LoadLibrary(string lpFileName);

        [DllImport("kernel32", SetLastError = true)]
        private static extern bool FreeLibrary(IntPtr hModule);

        [DllImport("kernel32", CharSet = CharSet.Ansi, SetLastError = true, ExactSpelling = false)]
        private static extern IntPtr GetProcAddress(IntPtr hModule, [MarshalAs(UnmanagedType.LPStr)] string lpProcName);

        private IntPtr ioModule;
        private readonly bool is64bit = (IntPtr.Size == 8);

        public enum LibStatus
        {
            INITIALIZE_ERROR = 0,
            OK = 1,
            PARTIALLY_OK = 2
        }

        public LibStatus WinIoStatus { get; } = LibStatus.INITIALIZE_ERROR;

        public Utils()
        {
            string fileName = is64bit ?  "inpoutx64.dll" : "WinIo.dll";

            ioModule = LoadLibrary(fileName);

            if (ioModule == IntPtr.Zero)
                throw new DllNotFoundException($"{fileName} not found!");

            // Common
            GetPhysLong = (_GetPhysLong)GetDelegate(ioModule, "GetPhysLong", typeof(_GetPhysLong));
            MapPhysToLin = (_MapPhysToLin)GetDelegate(ioModule, "MapPhysToLin", typeof(_MapPhysToLin));
            UnmapPhysicalMemory = (_UnmapPhysicalMemory)GetDelegate(ioModule, "UnmapPhysicalMemory", typeof(_UnmapPhysicalMemory));

            // 64bit only
            if (is64bit)
            {
                IsInpOutDriverOpen64 = (_IsInpOutDriverOpen64)GetDelegate(ioModule, "IsInpOutDriverOpen", typeof(_IsInpOutDriverOpen64));
            }
            else
            {
                InitializeWinIo32 = (_InitializeWinIo32)GetDelegate(ioModule, "InitializeWinIo", typeof(_InitializeWinIo32));
                ShutdownWinIo32 = (_ShutdownWinIo32)GetDelegate(ioModule, "ShutdownWinIo", typeof(_ShutdownWinIo32));
            }

            if (InitializeWinIo())
            {
                WinIoStatus = LibStatus.OK;
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

        public bool IsInpOutDriverOpen()
        {
            if (is64bit)
                return IsInpOutDriverOpen64() != 0;

            return true;
        }

        public bool InitializeWinIo()
        {
            if (is64bit)
                return true;

            return InitializeWinIo32();
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

        public uint SetBits(uint val, int offset, int n, uint newVal)
        {
            return val & ~(((1U << n) - 1) << offset) | (newVal << offset);
        }

        public uint GetBits(uint val, int offset, int n)
        {
            return (val >> offset) & ~(~0U << n);
        }

        public uint CountSetBits(uint v)
        {
            uint result = 0;

            while (v > 0)
            {
                if ((v & 1) == 1)
                    result++;

                v >>= 1;
            }

            return result;
        }

        public string GetStringPart(uint val)
        {
            return val != 0 ? Convert.ToChar(val).ToString() : "";
        }

        public string IntToStr(uint val)
        {
            uint part1 = val & 0xff;
            uint part2 = val >> 8 & 0xff;
            uint part3 = val >> 16 & 0xff;
            uint part4 = val >> 24 & 0xff;

            return $"{GetStringPart(part1)}{GetStringPart(part2)}{GetStringPart(part3)}{GetStringPart(part4)}";
        }

        public double VidToVoltage(uint vid)
        {
            return 1.55 - vid * 0.00625;
        }

        private static bool CheckAllZero<T>(ref T[] typedArray)
        {
            T[] arr = typedArray;
            bool allZero = true;

            foreach (var value in arr)
            {
                if (Convert.ToUInt32(value) != 0)
                {
                    allZero = false;
                    break;
                }
            }

            return allZero;
        }

        public bool AllZero(byte[] arr) => CheckAllZero(ref arr);

        public bool AllZero(int[] arr) => CheckAllZero(ref arr);

        public bool AllZero(uint[] arr) => CheckAllZero(ref arr);

        public bool AllZero(float[] arr) => CheckAllZero(ref arr);

        public void Dispose()
        {
            if (ioModule == IntPtr.Zero) return;
            if (IntPtr.Size == 4)
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
