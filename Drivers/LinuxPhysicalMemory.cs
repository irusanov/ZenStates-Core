using System;
using System.IO;

namespace ZenStates.Core.Drivers
{
    internal static class LinuxPhysicalMemory
    {
        public static bool ReadDword(long address, out uint value)
        {
            byte[] buf = new byte[4];
            bool ok = LinuxAmdAccess.ReadPhysical(address, buf);
            value = ok ? BitConverter.ToUInt32(buf, 0) : 0;
            return ok;
        }

        public static bool WriteDword(long address, uint value)
        {
            try
            {
                using (var fs = new FileStream("/dev/mem", FileMode.Open, FileAccess.Write, FileShare.ReadWrite, 1))
                {
                    fs.Seek(address, SeekOrigin.Begin);
                    fs.Write(BitConverter.GetBytes(value), 0, 4);
                    fs.Flush();
                    return true;
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"WriteDword(0x{address:X}) failed: {ex.Message}");
                return false;
            }
        }
    }
}
