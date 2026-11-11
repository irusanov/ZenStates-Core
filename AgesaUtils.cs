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
                // The marker can show up more than once (stray copies, other tables), so keep going
                // until one of them is followed by something that really is a version string
                int searchFrom = 0;
                while (searchFrom < source.Length)
                {
                    int markerOffset = Utils.FindSequence(source, searchFrom, marker);
                    if (markerOffset == -1)
                        break;

                    string version = ExtractVersionAt(source, markerOffset + marker.Length);
                    if (IsPlausibleVersion(version))
                        return version;

                    searchFrom = markerOffset + marker.Length;
                }
            }

            return string.Empty;
        }

        // A real one looks like "ComboAm5PI 1.3.0.1d": a name and a dotted number. This rules out
        // leftovers such as "0" that follow a stray marker
        private static bool IsPlausibleVersion(string version)
        {
            if (string.IsNullOrEmpty(version) || version.Length < 5)
                return false;

            bool hasDigit = false;
            bool hasDot = false;
            foreach (char c in version)
            {
                if (c >= '0' && c <= '9') hasDigit = true;
                else if (c == '.') hasDot = true;
            }

            return hasDigit && hasDot;
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
