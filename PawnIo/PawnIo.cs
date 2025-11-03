using Microsoft.Win32;
using Microsoft.Win32.SafeHandles;
using System;
using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text;

namespace ZenStates.Core
{
    /// <summary>
    /// Adapted from LibreHardwareMonitor's PawnIo.cs
    /// https://github.com/LibreHardwareMonitor/LibreHardwareMonitor/blob/master/LibreHardwareMonitorLib/PawnIo/PawnIo.cs
    /// </summary>
    public class PawnIo
    {
        private const uint DEVICE_TYPE = 41394u << 16;
        private const int FN_NAME_LENGTH = 32;
        private const uint IOCTL_PIO_EXECUTE_FN = 0x841 << 2;
        private const uint IOCTL_PIO_LOAD_BINARY = 0x821 << 2;

        private readonly SafeFileHandle _handle;
        private static readonly Version _version;

        static PawnIo()
        {
            // .NET 2.0 framework defaults to system architecture (x86 or x64)
            RegistryKey subKey = Registry.LocalMachine.OpenSubKey(@"SOFTWARE\\Microsoft\\Windows\\CurrentVersion\\Uninstall\\PawnIO");
            if (subKey != null)
            {
                object val = subKey.GetValue("DisplayVersion");
                if (!TryParseVersion(val, out _version))
                {
                    _version = null;
                }
                subKey.Close();
            }
        }

        private PawnIo(SafeFileHandle handle) => _handle = handle;

        /// <summary>
        /// Gets a value indicating whether PawnIO is installed on the system.
        /// </summary>
        public static bool IsInstalled => Version != null;

        /// <summary>
        /// Retrieves the version information for the installed PawnIO.
        /// </summary>
        public static Version Version { get => _version; }

        /// <summary>
        /// Gets a value indicating whether the underlying handle is currently valid and open.
        /// </summary>
        public bool IsLoaded => _handle != null && _handle.IsInvalid == false && _handle.IsClosed == false;

        public static PawnIo LoadModuleFromResource(Assembly assembly, string resourceName)
        {
            IntPtr handle = CreateFile(@"\\.\PawnIO", FileAccess.GENERIC_READ | FileAccess.GENERIC_WRITE, 0x00000003, IntPtr.Zero, CreationDisposition.OPEN_EXISTING, 0, IntPtr.Zero);
            if (handle == IntPtr.Zero || handle.ToInt64() == -1)
                return new PawnIo(null);

            Stream stream = assembly.GetManifestResourceStream(resourceName);
            MemoryStream memory = new MemoryStream();
            // Use manual copy for .NET 2.0 compatibility
            StreamCopyTo(stream, memory);
            byte[] bin = memory.ToArray();

            if (DeviceIoControl(handle, ControlCode.LoadBinary, bin, (uint)bin.Length, null, 0, out uint read, IntPtr.Zero))
                return new PawnIo(new SafeFileHandle(handle, true));

            return new PawnIo(null);
        }

        /// <summary>
        /// Open a PawnIO instance from a binary file.
        /// </summary>
        /// <param name="filePath">The path to the file</param>
        /// <returns></returns>
        public static PawnIo LoadModuleFromFile(string filePath)
        {
            if (!File.Exists(filePath))
                throw new FileNotFoundException(@"PawnIO module not found.", filePath);

            IntPtr handle = CreateFile(@"\\.\PawnIO", FileAccess.GENERIC_READ | FileAccess.GENERIC_WRITE, 0x00000003, IntPtr.Zero, CreationDisposition.OPEN_EXISTING, 0, IntPtr.Zero);
            if (handle == IntPtr.Zero || handle.ToInt64() == -1)
                return new PawnIo(null);

            byte[] bin = File.ReadAllBytes(filePath);

            if (DeviceIoControl(handle, ControlCode.LoadBinary, bin, (uint)bin.Length, null, 0, out uint read, IntPtr.Zero))
                return new PawnIo(new SafeFileHandle(handle, true));

            return new PawnIo(null);
        }

        public void Close()
        {
            if (IsLoaded)
                _handle.Close();
        }

        public long[] Execute(string name, long[] input, int outLength)
        {
            if (!IsLoaded)
                return new long[outLength];

            byte[] output = new byte[outLength * 8];
            byte[] totalInput = new byte[(input.Length * 8) + FN_NAME_LENGTH];
            byte[] nameBytes = Encoding.ASCII.GetBytes(name);
            int copyLen = Math.Min(FN_NAME_LENGTH - 1, nameBytes.Length);

            Buffer.BlockCopy(nameBytes, 0, totalInput, 0, copyLen);
            Buffer.BlockCopy(input, 0, totalInput, FN_NAME_LENGTH, input.Length * 8);

            bool success = DeviceIoControl(_handle, ControlCode.Execute, totalInput, (uint)totalInput.Length, output, (uint)output.Length, out uint read, IntPtr.Zero);

            if (success && read > 0)
            {
                long[] result = new long[read / 8];
                Buffer.BlockCopy(output, 0, result, 0, (int)read);
                return result;
            }

            return new long[outLength];
        }

        public int ExecuteHr(string name, long[] inBuffer, uint inSize, long[] outBuffer, uint outSize, out uint returnSize)
        {
            if (inBuffer.Length < inSize)
                throw new ArgumentOutOfRangeException(nameof(inSize));

            if (outBuffer.Length < outSize)
                throw new ArgumentOutOfRangeException(nameof(outSize));

            if (!IsLoaded)
            {
                returnSize = 0;
                return 0;
            }

            byte[] output = new byte[outSize * 8]; // 8 bytes per long
            byte[] totalInput = new byte[(inSize * 8) + FN_NAME_LENGTH];
            byte[] nameBytes = Encoding.ASCII.GetBytes(name);
            int nameLen = Math.Min(FN_NAME_LENGTH - 1, nameBytes.Length);

            Buffer.BlockCopy(nameBytes, 0, totalInput, 0, nameLen);
            Buffer.BlockCopy(inBuffer, 0, totalInput, FN_NAME_LENGTH, (int)inSize * 8);


            bool success = DeviceIoControl(
                _handle,
                ControlCode.Execute,
                totalInput,
                (uint)totalInput.Length,
                output,
                (uint)output.Length,
                out uint read,
                IntPtr.Zero
            );

            if (success)
            {
                int copySize = Math.Min((int)read, outBuffer.Length * 8);
                Buffer.BlockCopy(output, 0, outBuffer, 0, copySize);
                returnSize = read / 8;
                return 0; // S_OK
            }

            // Failure: return HRESULT_FROM_WIN32
            returnSize = 0;
            return Marshal.GetHRForLastWin32Error();
        }

        /// <summary>
        /// .NET2.0 compatible Version parser
        /// </summary>
        /// <param name="val"></param>
        /// <param name="version"></param>
        /// <returns></returns>
        private static bool TryParseVersion(object val, out Version version)
        {
            version = null;
            if (val != null)
            {
                try
                {
                    version = new Version(val as string);
                    return true;
                }
                catch (ArgumentException) { }
                catch (FormatException) { }
                catch (OverflowException) { }
            }
            return false;
        }

        private enum ControlCode : uint
        {
            LoadBinary = DEVICE_TYPE | IOCTL_PIO_LOAD_BINARY,
            Execute = DEVICE_TYPE | IOCTL_PIO_EXECUTE_FN
        }

        private enum FileAccess : uint
        {
            GENERIC_READ = 0x80000000,
            GENERIC_WRITE = 0x40000000
        }

        private enum CreationDisposition : uint
        {
            CREATE_NEW = 1,
            CREATE_ALWAYS = 2,
            OPEN_EXISTING = 3,
            OPEN_ALWAYS = 4,
            TRUNCATE_EXISTING = 5
        }

        private enum FileAttributes : uint
        {
            FILE_ATTRIBUTE_NORMAL = 0x80
        }

        [DllImport("kernel32.dll", CallingConvention = CallingConvention.Winapi)]
        private static extern bool DeviceIoControl(
            SafeFileHandle device,
            ControlCode ioControlCode,
            [In] byte[] inBuffer,
            uint inBufferSize,
            [Out] byte[] outBuffer,
            uint nOutBufferSize,
            out uint bytesReturned,
            IntPtr overlapped);

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern bool DeviceIoControl(
            IntPtr hDevice,
            ControlCode dwIoControlCode,
            byte[] lpInBuffer,
            uint nInBufferSize,
            byte[] lpOutBuffer,
            uint nOutBufferSize,
            out uint lpBytesReturned,
            IntPtr lpOverlapped);

        [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Auto)]
        private static extern IntPtr CreateFile(
            string lpFileName,
            FileAccess dwDesiredAccess,
            uint dwShareMode,
            IntPtr lpSecurityAttributes,
            CreationDisposition dwCreationDisposition,
            FileAttributes dwFlagsAndAttributes,
            IntPtr hTemplateFile);

        // Helper for .NET 2.0: Stream.CopyTo replacement
        private static void StreamCopyTo(Stream srcStream, Stream dstStream, int bufferSize = 4096)
        {
            var buffer = new byte[bufferSize];
            int bytesRead;
            while ((bytesRead = srcStream.Read(buffer, 0, buffer.Length)) != 0)
            {
                dstStream.Write(buffer, 0, bytesRead);
            }
        }
    }
}
