using System;
using System.Runtime.InteropServices;

namespace ZenStates.Core
{
    public class Utils : IDisposable
    {
        [DllImport("kernel32")]
        public extern static IntPtr LoadLibrary(string lpFileName);

        [DllImport("kernel32", SetLastError = true)]
        private static extern bool FreeLibrary(IntPtr hModule);

        [DllImport("kernel32", CharSet = CharSet.Ansi, SetLastError = true, ExactSpelling = false)]
        private static extern IntPtr GetProcAddress(IntPtr hModule, [MarshalAs(UnmanagedType.LPStr)] string lpProcName);

        private IntPtr ioModule = IntPtr.Zero;

        public enum LibStatus
        {
            OK = 0,
            INITIALIZE_ERROR = 1,
        }

        public LibStatus WinIoStatus { get; private set; } = LibStatus.INITIALIZE_ERROR;

        public Utils()
        {
            string fileName;
            if (IntPtr.Size == 8)
            {
                fileName = "inpoutx64.dll";
            }
            else
            {
                fileName = "WinIo32.dll";
            }

            ioModule = LoadLibrary(fileName);

            if (ioModule == IntPtr.Zero)
                throw new DllNotFoundException($"{fileName} not found!");

            // Common
            GetPhysLong = (_GetPhysLong)GetDelegate(ioModule, "GetPhysLong", typeof(_GetPhysLong));

            // 64bit only
            if (IntPtr.Size == 8)
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
            if (IntPtr.Size == 8)
                return IsInpOutDriverOpen64() != 0;

            return true;
        }

        public bool InitializeWinIo()
        {
            if (IntPtr.Size == 8)
                return true;

            return InitializeWinIo32();
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

            return string.Format("{0}{1}{2}{3}", GetStringPart(part1), GetStringPart(part2), GetStringPart(part3), GetStringPart(part4));
        }

        public double VidToVoltage(uint vid)
        {
            return 1.55 - vid * 0.00625;
        }

        private bool CheckAllZero<T>(ref T[] typedArray)
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

        public void Dispose()
        {
            if (ioModule != IntPtr.Zero)
            {
                if (IntPtr.Size == 4)
                {
                    ShutdownWinIo32();
                }

                FreeLibrary(ioModule);
                ioModule = IntPtr.Zero;
            }
        }

        private Delegate GetDelegate(IntPtr moduleName, string procName, Type delegateType)
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
