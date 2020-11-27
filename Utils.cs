using System;

namespace ZenStates.Core
{
    public class Utils
    {
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
    }
}
