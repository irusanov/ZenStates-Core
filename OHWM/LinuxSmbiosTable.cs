using System;

namespace ZenStates.Core.OHWM
{
    internal static class LinuxSmbiosTable
    {
        public static bool TryRead(out byte[] raw)
        {
            raw = null;
            try
            {
                const string path = "/sys/firmware/dmi/tables/DMI";
                if (!System.IO.File.Exists(path))
                    return false;

                byte[] table = System.IO.File.ReadAllBytes(path);
                if (table.Length == 0)
                    return false;

                // Same layout as the Windows RSMB table: 8 byte header, then the structure table
                raw = new byte[table.Length + 8];
                Array.Copy(table, 0, raw, 8, table.Length);
                return true;
            }
            catch
            {
                raw = null;
                return false;
            }
        }
    }
}
