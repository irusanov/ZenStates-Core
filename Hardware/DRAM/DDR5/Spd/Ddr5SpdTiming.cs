using System;

namespace ZenStates.Core.Hardware.DRAM.DDR5.Spd
{
    /// <summary>
    /// A DDR5 timing parameter: the minimum time in picoseconds, an optional lower limit in clocks and the clocks it
    /// takes at the cycle time it was converted with.
    /// </summary>
    public sealed class Ddr5SpdTiming
    {
        /// <summary>Minimum time in picoseconds.</summary>
        public int Ps;

        /// <summary>Lower clock limit (nCK), 0 when not defined.</summary>
        public int MinNck;

        /// <summary>Clocks at the conversion cycle time: the JEDEC rounding algorithm applied to <see cref="Ps"/>, raised to <see cref="MinNck"/>.</summary>
        public int Nck;

        public bool IsDefined
        {
            get { return Ps > 0 || MinNck > 0; }
        }

        public static Ddr5SpdTiming Create(int ps, int minNck, int tckPs)
        {
            return new Ddr5SpdTiming
            {
                Ps = ps,
                MinNck = minNck,
                Nck = Math.Max(Ddr5SpdTimingMath.ToNck(ps, tckPs), minNck),
            };
        }

        public override string ToString()
        {
            if (!IsDefined)
                return "N/A";

            return MinNck > 0
                ? string.Format("{0} ps, min {1} nCK ({2} clk)", Ps, MinNck, Nck)
                : string.Format("{0} ps ({1} clk)", Ps, Nck);
        }
    }
}
