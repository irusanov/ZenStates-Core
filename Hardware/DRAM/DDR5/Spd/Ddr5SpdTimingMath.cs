using System;
using System.Collections.Generic;

namespace ZenStates.Core.Hardware.DRAM.DDR5.Spd
{
    /// <summary>Conversion of SPD minimum times to clocks (JESD400-5 / JESD406-5 rounding algorithm).</summary>
    public static class Ddr5SpdTimingMath
    {
        /// <summary>Correction factor of the rounding algorithm, scaled by 1000 (0.30%).</summary>
        private const int CORRECTION = 3;

        /// <summary>
        /// Clocks for a minimum time: the time reduced by the 0.30% correction factor, divided by the clock period and
        /// rounded up to the next clock, with integer math only.
        /// </summary>
        public static int ToNck(int ps, int tckPs)
        {
            if (ps <= 0 || tckPs <= 0)
                return 0;

            long scaled = (long)ps * (1000 - CORRECTION) / tckPs + 1000;
            return (int)(scaled / 1000);
        }

        /// <summary>
        /// DDR5 CAS latency for tAAmin: the rounding algorithm, rounded up to an even value (DDR5 has even CLs only)
        /// and then to the next CL in <paramref name="supportedCls"/>, when given.
        /// </summary>
        public static int ToCl(int tAaPs, int tckPs, List<int> supportedCls)
        {
            int cl = ToNck(tAaPs, tckPs);
            if (cl <= 0)
                return 0;

            if ((cl & 1) != 0)
                cl++;

            if (supportedCls != null)
            {
                for (int i = 0; i < supportedCls.Count; i++)
                {
                    if (supportedCls[i] >= cl)
                        return supportedCls[i];
                }
            }

            return cl;
        }

        /// <summary>The speed bin a data rate computed from tCK is within 1% of, or the rate itself.</summary>
        public static int ToSpeedBin(int mts, int[] bins)
        {
            for (int i = 0; i < bins.Length; i++)
            {
                if (Math.Abs(mts - bins[i]) * 100 <= bins[i])
                    return bins[i];
            }
            return mts;
        }
    }
}
