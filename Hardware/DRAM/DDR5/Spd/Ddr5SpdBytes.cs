using System.Text;

namespace ZenStates.Core.Hardware.DRAM.DDR5.Spd
{
    /// <summary>Byte helpers shared by the DDR5 and LPDDR5 SPD decoders. Out-of-range reads return 0.</summary>
    internal static class Ddr5SpdBytes
    {
        public static byte B(byte[] spd, int offset)
        {
            return offset >= 0 && offset < spd.Length ? spd[offset] : (byte)0;
        }

        /// <summary>16-bit value, low byte first.</summary>
        public static int U16(byte[] spd, int offset)
        {
            return B(spd, offset) | (B(spd, offset + 1) << 8);
        }

        /// <summary>Two's complement byte, as the fine timebase corrections are stored.</summary>
        public static int S8(byte[] spd, int offset)
        {
            return unchecked((sbyte)B(spd, offset));
        }

        public static int Bcd(byte value)
        {
            return ((value >> 4) & 0x0F) * 10 + (value & 0x0F);
        }

        /// <summary>Printable ASCII characters of a field, trimmed; stops at the first 0.</summary>
        public static string Ascii(byte[] spd, int offset, int length)
        {
            StringBuilder sb = new StringBuilder();
            for (int i = offset; i < offset + length; i++)
            {
                byte c = B(spd, i);
                if (c == 0)
                    break;
                if (c >= 0x20 && c <= 0x7E)
                    sb.Append((char)c);
            }
            return sb.ToString().Trim();
        }

        /// <summary>JESD400-5 / JESD406-5 SPD CRC: CRC-16, polynomial 0x1021, initial value 0, MSB first.</summary>
        public static int Crc16(byte[] data, int offset, int count)
        {
            int crc = 0;
            for (int i = offset; i < offset + count && i < data.Length; i++)
            {
                crc ^= data[i] << 8;
                for (int bit = 0; bit < 8; bit++)
                    crc = (crc & 0x8000) != 0 ? ((crc << 1) ^ 0x1021) & 0xFFFF : (crc << 1) & 0xFFFF;
            }
            return crc;
        }
    }
}
