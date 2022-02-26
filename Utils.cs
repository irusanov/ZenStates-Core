using System;
using System.ComponentModel;
using System.Runtime.InteropServices;

namespace ZenStates.Core
{
    public static class Utils
    {
        public static bool Is64Bit => OpenHardwareMonitor.Hardware.OperatingSystem.Is64BitOperatingSystem;

        public static uint SetBits(uint val, int offset, int n, uint newVal)
        {
            return val & ~(((1U << n) - 1) << offset) | (newVal << offset);
        }

        public static uint GetBits(uint val, int offset, int n)
        {
            return (val >> offset) & ~(~0U << n);
        }

        public static uint CountSetBits(uint v)
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

        public static string GetStringPart(uint val)
        {
            return val != 0 ? Convert.ToChar(val).ToString() : "";
        }

        public static string IntToStr(uint val)
        {
            uint part1 = val & 0xff;
            uint part2 = val >> 8 & 0xff;
            uint part3 = val >> 16 & 0xff;
            uint part4 = val >> 24 & 0xff;

            return $"{GetStringPart(part1)}{GetStringPart(part2)}{GetStringPart(part3)}{GetStringPart(part4)}";
        }

        public static double VidToVoltage(uint vid)
        {
            return 1.55 - vid * 0.00625;
        }

        private static bool CheckAllZero<T>(ref T[] typedArray)
        {
            if (typedArray == null)
                return true;

            foreach (var value in typedArray)
            {
                if (Convert.ToUInt32(value) != 0)
                    return false;
            }

            return true;
        }

        public static bool AllZero(byte[] arr) => CheckAllZero(ref arr);

        public static bool AllZero(int[] arr) => CheckAllZero(ref arr);

        public static bool AllZero(uint[] arr) => CheckAllZero(ref arr);

        public static bool AllZero(float[] arr) => CheckAllZero(ref arr);

        public static uint[] MakeCmdArgs(uint[] args)
        {
            uint[] cmdArgs = new uint[6];
            int length = args.Length > 6 ? 6 : args.Length;

            for (int i = 0; i < length; i++)
                cmdArgs[i] = args[i];

            return cmdArgs;
        }

        public static uint[] MakeCmdArgs(uint arg = 0)
        {
            return MakeCmdArgs(new uint[1] { arg });
        }

        // CO margin range seems to be from -30 to 30
        // Margin arg seems to be 16 bits (lowest 16 bits of the command arg)
        public static uint MakePsmMarginArg(int margin)
        {
            if (margin > 30)
                margin = 30;
            else if (margin < -30)
                margin = -30;

            int offset = margin < 0 ? 0x100000 : 0;
            return Convert.ToUInt32(offset + margin) & 0xffff;
        }
    }
}
