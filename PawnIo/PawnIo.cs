using System;
using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;
using Microsoft.Win32;

namespace ZenStates.Core
{
    public class PawnIo : IDisposable
    {
        [DllImport("kernel32", SetLastError = true, CharSet = CharSet.Ansi)]
        private static extern IntPtr LoadLibrary([MarshalAs(UnmanagedType.LPStr)] string lpFileName);

        [DllImport("kernel32", SetLastError = true)]
        private static extern bool FreeLibrary(IntPtr hModule);

        [DllImport("kernel32", CharSet = CharSet.Ansi, SetLastError = true, ExactSpelling = false)]
        private static extern IntPtr GetProcAddress(IntPtr hModule, [MarshalAs(UnmanagedType.LPStr)] string lpProcName);

        private IntPtr _handle;
        private bool _disposed;

        public static string InstallPath
        {
            get
            {
                object val = (Registry.GetValue(@"HKEY_LOCAL_MACHINE\SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall\PawnIO", "InstallLocation", null)
                    ?? Registry.GetValue(@"HKEY_LOCAL_MACHINE\SOFTWARE\WOW6432Node\PawnIO", "Install_Dir", null))
                    ?? Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles) + Path.DirectorySeparatorChar + "PawnIO";

                string path = val as string;
                if (!string.IsNullOrEmpty(path) && Directory.Exists(path))
                {
                    return path;
                }

                return null;
            }
        }

        public static bool IsInstalled
        {
            get { return !string.IsNullOrEmpty(InstallPath); }
        }

        [DllImport("PawnIOLib", ExactSpelling = true, PreserveSig = false)]
        private static extern void pawnio_version(out uint version);

        [DllImport("PawnIOLib", ExactSpelling = true, PreserveSig = false)]
        private static extern void pawnio_open(out IntPtr handle);

        [DllImport("PawnIOLib", ExactSpelling = true, PreserveSig = false)]
        private static extern void pawnio_load(
            IntPtr handle,
            [MarshalAs(UnmanagedType.LPArray, SizeParamIndex = 2)] byte[] blob,
            IntPtr size);

        [DllImport("PawnIOLib", ExactSpelling = true, PreserveSig = false)]
        private static extern void pawnio_execute(
            IntPtr handle,
            [MarshalAs(UnmanagedType.LPStr)] string name,
            long[] inArray,
            IntPtr inSize,
            long[] outArray,
            IntPtr outSize,
            out IntPtr returnSize);

        [DllImport("PawnIOLib", ExactSpelling = true, EntryPoint = "pawnio_execute")]
        private static extern int pawnio_execute_hr(
            IntPtr handle,
            [MarshalAs(UnmanagedType.LPStr)] string name,
            long[] inArray,
            IntPtr inSize,
            long[] outArray,
            IntPtr outSize,
            out IntPtr returnSize);

        [DllImport("PawnIOLib", ExactSpelling = true, PreserveSig = false)]
        private static extern void pawnio_close(IntPtr handle);

        private static void TryLoadLibrary()
        {
            try
            {
                uint _;
                pawnio_version(out _);
                return;
            }
            catch
            {
                // ignored
            }

            try
            {
                if (IsInstalled)
                    LoadLibrary(InstallPath + Path.DirectorySeparatorChar + "PawnIOLib");
            }
            catch
            {
                // ignored
            }
        }

        public static Version Version()
        {
            try
            {
                TryLoadLibrary();
                uint version;
                pawnio_version(out version);

                return new Version(
                    (int)((version >> 16) & 0xFF),
                    (int)((version >> 8) & 0xFF),
                    (int)(version & 0xFF),
                    0);
            }
            catch
            {
                return new Version();
            }
        }

        public void Close()
        {
            if (_handle != IntPtr.Zero)
            {
                pawnio_close(_handle);
                _handle = IntPtr.Zero;
            }
        }

        /// <summary>
        /// Opens a PawnIO module from a file on disk.
        /// </summary>
        public static PawnIo OpenFromFile(string filePath)
        {
            PawnIo pawnIO = new PawnIo();

            if (!File.Exists(filePath))
                throw new FileNotFoundException("PawnIO module not found.", filePath);

            TryLoadLibrary();

            try
            {
                byte[] buffer = File.ReadAllBytes(filePath);

                IntPtr handle;
                pawnio_open(out handle);
                pawnio_load(handle, buffer, (IntPtr)buffer.Length);
                pawnIO._handle = handle;
            }
            catch
            {
                // PawnIO not available or failed to load
            }

            return pawnIO;
        }

        private static PawnIo LoadModuleFromResource(Assembly assembly, string resourceName)
        {
            PawnIo pawnIO = new PawnIo();
            Stream s = assembly.GetManifestResourceStream(resourceName);

            if (s != null)
            {
                TryLoadLibrary();

                try
                {
                    byte[] buffer;
                    using (MemoryStream ms = new MemoryStream())
                    {
                        byte[] temp = new byte[4096];
                        int read;
                        while ((read = s.Read(temp, 0, temp.Length)) > 0)
                        {
                            ms.Write(temp, 0, read);
                        }
                        buffer = ms.ToArray();
                    }

                    IntPtr handle;
                    pawnio_open(out handle);
                    pawnio_load(handle, buffer, (IntPtr)buffer.Length);
                    pawnIO._handle = handle;
                }
                catch
                {
                    // PawnIO not available
                }

                s.Dispose();
            }

            return pawnIO;
        }

        public long[] Execute(string name, long[] input, int outLength)
        {
            long[] result = new long[outLength];

            if (_handle == IntPtr.Zero)
                return result;

            IntPtr returnLength;
            pawnio_execute(_handle,
                           name,
                           input,
                           (IntPtr)input.Length,
                           result,
                           (IntPtr)result.Length,
                           out returnLength);

            Array.Resize(ref result, (int)returnLength);
            return result;
        }

        public int ExecuteHr(string name, long[] inBuffer, uint inSize, long[] outBuffer, uint outSize, out uint returnSize)
        {
            if (inBuffer.Length < inSize)
                throw new ArgumentOutOfRangeException("inSize");

            if (outBuffer.Length < outSize)
                throw new ArgumentOutOfRangeException("outSize");

            if (_handle == IntPtr.Zero)
            {
                returnSize = 0;
                return 0;
            }

            IntPtr retSize;
            int ret = pawnio_execute_hr(_handle, name, inBuffer, (IntPtr)inSize, outBuffer, (IntPtr)outSize, out retSize);

            returnSize = (uint)retSize.ToInt32();
            return ret;
        }

        // ------------------------------
        // IDisposable implementation
        // ------------------------------
        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        protected virtual void Dispose(bool disposing)
        {
            if (!_disposed)
            {
                Close();
                _disposed = true;
            }
        }

        ~PawnIo()
        {
            Dispose(false);
        }
    }
}