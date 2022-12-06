using System;
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

        public static double VidToVoltageSVI3(uint vid)
        {
            return 0.245 + vid * 0.005;
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
            uint[] cmdArgs = { 0, 0, 0, 0, 0, 0 };
            int length = args.Length > 6 ? 6 : args.Length;

            for (int i = 0; i < length; i++)
                cmdArgs[i] = args[i];

            return cmdArgs;
        }

        public static uint[] MakeCmdArgs(uint arg = 0)
        {
            return MakeCmdArgs(new uint[] { arg });
        }

        // CO margin range seems to be from -30 to 30
        // Margin arg seems to be 16 bits (lowest 16 bits of the command arg)
        // Update 01 Nov 2022 - the range is different on Raphael, -40 is successfully set
        public static uint MakePsmMarginArg(int margin)
        {
            // if (margin > 30)
            //     margin = 30;
            // else if (margin < -30)
            //     margin = -30;

            int offset = margin < 0 ? 0x100000 : 0;
            return Convert.ToUInt32(offset + margin) & 0xffff;
        }

        public static T ByteArrayToStructure<T>(byte[] byteArray) where T : new()
        {
            T structure;
            GCHandle handle = GCHandle.Alloc(byteArray, GCHandleType.Pinned);
            try
            {
                structure = (T)Marshal.PtrToStructure(handle.AddrOfPinnedObject(), typeof(T));
            }
            finally
            {
                handle.Free();
            }
            return structure;
        }

        public static uint ReverseBytes(uint value)
        {
            return (value & 0x000000FFU) << 24 | (value & 0x0000FF00U) << 8 |
                (value & 0x00FF0000U) >> 8 | (value & 0xFF000000U) >> 24;
        }

        public static string GetStringFromBytes(uint value)
        {
            byte[] bytes = BitConverter.GetBytes(value);
            // Array.Reverse(bytes);
            return System.Text.Encoding.ASCII.GetString(bytes).Replace("\0", " ");
        }

        public static string GetStringFromBytes(ulong value)
        {
            byte[] bytes = BitConverter.GetBytes(value);
            // Array.Reverse(bytes);
            return System.Text.Encoding.ASCII.GetString(bytes).Replace("\0", " ");
        }

        public static string GetStringFromBytes(byte[] value)
        {
            return System.Text.Encoding.ASCII.GetString(value).Replace("\0", " ");
        }

        /// <summary>Looks for the next occurrence of a sequence in a byte array</summary>
        /// <param name="array">Array that will be scanned</param>
        /// <param name="start">Index in the array at which scanning will begin</param>
        /// <param name="sequence">Sequence the array will be scanned for</param>
        /// <returns>
        ///   The index of the next occurrence of the sequence of -1 if not found
        /// </returns>
        public static int FindSequence(byte[] array, int start, byte[] sequence)
        {
            int end = array.Length - sequence.Length; // past here no match is possible
            byte firstByte = sequence[0]; // cached to tell compiler there's no aliasing

            while (start <= end)
            {
                // scan for first byte only. compiler-friendly.
                if (array[start] == firstByte)
                {
                    // scan for rest of sequence
                    for (int offset = 1; ; ++offset)
                    {
                        if (offset == sequence.Length)
                        { // full sequence matched?
                            return start;
                        }
                        else if (array[start + offset] != sequence[offset])
                        {
                            break;
                        }
                    }
                }
                ++start;
            }

            // end of array reached without match
            return -1;
        }
    }
}
