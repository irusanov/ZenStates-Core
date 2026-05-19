using System.Text;

namespace ZenStates.Core
{
    public static class AgesaUtils
    {
        private static readonly bool[] Allowed = BuildAllowedTable();

        private static readonly byte[][] Markers =
        {
            Encoding.ASCII.GetBytes("AGESA!V9"),
            Encoding.ASCII.GetBytes("AGESA!BB"),
        };

        public static string ParseVersion(byte[] source)
        {
            if (source == null || source.Length == 0)
                return string.Empty;

            foreach (byte[] marker in Markers)
            {
                int markerOffset = Utils.FindSequence(source, 0, marker);
                if (markerOffset == -1)
                    continue;

                string version = ExtractVersionAt(source, markerOffset + marker.Length);
                if (version.Length > 0)
                    return version;
            }

            return string.Empty;
        }

        private static string ExtractVersionAt(byte[] source, int offset)
        {
            int versionStart = FindFirstAllowed(source, offset);
            if (versionStart == -1)
                return string.Empty;

            int versionEnd = FindFirstInvalid(source, versionStart);
            if (versionEnd <= versionStart)
                return string.Empty;

            return Encoding.ASCII.GetString(source, versionStart, versionEnd - versionStart)
                .Trim('\0', ' ');
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
