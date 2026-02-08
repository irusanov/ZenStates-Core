using System.Text;

namespace ZenStates.Core
{
    public static class AgesaUtils
    {
        private static readonly bool[] Allowed = BuildAllowedTable();
        public const string AGESA_UNKNOWN = @"Unknown";

        public static string ParseVersion(byte[] source)
        {
            // Search for AGESA marker
            byte[] marker = Encoding.ASCII.GetBytes("AGESA!V9");
            int markerOffset = Utils.FindSequence(source, 0, marker);
            if (markerOffset == -1)
            {
                //Debug.WriteLine("AGESA marker not found.");
                return AGESA_UNKNOWN;
            }

            int versionStart = markerOffset + marker.Length;
            versionStart = FindFirstAllowed(source, versionStart);
            int versionEnd = FindFirstInvalid(source, versionStart);

            if (versionEnd > versionStart)
            {
                return Encoding.ASCII.GetString(source, versionStart, versionEnd - versionStart)
                    .Trim('\0', ' ');
            }

            return AGESA_UNKNOWN;
        }

        private static int FindFirstInvalid(byte[] data, int startIndex = 0)
        {
            for (int i = startIndex; i < data.Length; i++)
            {
                if (!Allowed[data[i]])
                    return i;
            }
            return data.Length;
        }

        private static int FindFirstAllowed(byte[] data, int startIndex = 0)
        {
            for (int i = startIndex; i < data.Length; i++)
            {
                if (Allowed[data[i]])
                    return i;
            }
            return -1;
        }

        private static bool[] BuildAllowedTable()
        {
            var table = new bool[256];

            for (int c = '0'; c <= '9'; c++) table[c] = true;
            for (int c = 'A'; c <= 'Z'; c++) table[c] = true;
            for (int c = 'a'; c <= 'z'; c++) table[c] = true;

            table[' '] = true;
            table['.'] = true;
            table['-'] = true;

            return table;
        }
    }
}
