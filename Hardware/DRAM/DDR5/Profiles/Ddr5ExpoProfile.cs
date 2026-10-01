using System.Text;
using ZenStates.Core.Hardware.DRAM.DDR5.Spd;

namespace ZenStates.Core.Hardware.DRAM.DDR5.Profiles
{
    public class Ddr5ExpoProfile
    {
        public bool IsValid;
        public int ProfileNumber;

        public int tCKAVGminPs;
        public int SpeedMTs;
        public double ClockMHz;
        public string SpeedGrade;

        public int tAAminPs;
        public int tRCDminPs;
        public int tRPminPs;
        public int tRASminPs;
        public int tRCminPs;
        public int tWRminPs;

        public int tRFC1minNs;
        public int tRFC2minNs;
        public int tRFCsbMinNs;

        public int CL;
        public int tRCD;
        public int tRP;
        public int tRAS;
        public int tRC;
        public int tWR;
        public string TimingString;

        /// <summary>Voltage bytes as stored: bits [7:5] volts, bits [4:0] 50 mV steps.</summary>
        public int VddCode;
        public int VddqCode;
        public int VppCode;
        public int VddMv;
        public int VddqMv;
        public int VppMv;

        /// <summary>The profile fills the 16 bytes after tRFCsb with secondary timings (not all modules do).</summary>
        public bool HasSecondaryTimings;

        // Secondary timings in the order of the JEDEC bytes 70~93, converted at the profile's tCK. EXPO stores the
        // time only, without the lower clock limits of the JEDEC bytes.
        public Ddr5SpdTiming tRRD_L;
        public Ddr5SpdTiming tCCD_L;
        public Ddr5SpdTiming tCCD_L_WR;
        public Ddr5SpdTiming tCCD_L_WR2;
        public Ddr5SpdTiming tFAW;
        public Ddr5SpdTiming tCCD_L_WTR;
        public Ddr5SpdTiming tCCD_S_WTR;
        public Ddr5SpdTiming tRTP;

        public override string ToString()
        {
            if (!IsValid) return "  (not present)";

            StringBuilder sb = new StringBuilder();
            sb.AppendFormat("  Speed Grade        : {0}\n", SpeedGrade);
            sb.AppendFormat("  Clock Frequency    : {0:F1} MHz\n", ClockMHz);
            sb.AppendFormat("  Data Rate          : {0} MT/s\n", SpeedMTs);
            sb.AppendFormat("  Timing             : {0}\n", TimingString);
            sb.AppendFormat("  tCKAVGmin          : {0} ps\n", tCKAVGminPs);
            sb.AppendFormat("  tAAmin             : {0} ps (CL {1})\n", tAAminPs, CL);
            sb.AppendFormat("  tRCDmin            : {0} ps ({1} clk)\n", tRCDminPs, tRCD);
            sb.AppendFormat("  tRPmin             : {0} ps ({1} clk)\n", tRPminPs, tRP);
            sb.AppendFormat("  tRASmin            : {0} ps ({1:F1} ns)\n", tRASminPs, tRASminPs / 1000.0);
            sb.AppendFormat("  tRCmin             : {0} ps ({1:F1} ns)\n", tRCminPs, tRCminPs / 1000.0);
            sb.AppendFormat("  tWRmin             : {0} ps ({1:F1} ns)\n", tWRminPs, tWRminPs / 1000.0);
            sb.AppendFormat("  tRFC1              : {0} ns\n", tRFC1minNs);
            sb.AppendFormat("  tRFC2              : {0} ns\n", tRFC2minNs);
            sb.AppendFormat("  tRFCsb             : {0} ns\n", tRFCsbMinNs);
            AppendTiming(sb, "tRRD_L", tRRD_L);
            AppendTiming(sb, "tCCD_L", tCCD_L);
            AppendTiming(sb, "tCCD_L_WR", tCCD_L_WR);
            AppendTiming(sb, "tCCD_L_WR2", tCCD_L_WR2);
            AppendTiming(sb, "tFAW", tFAW);
            AppendTiming(sb, "tCCD_L_WTR", tCCD_L_WTR);
            AppendTiming(sb, "tCCD_S_WTR", tCCD_S_WTR);
            AppendTiming(sb, "tRTP", tRTP);
            sb.AppendFormat("  VDD                : {0} mV ({1:F3} V)\n", VddMv, VddMv / 1000.0);
            sb.AppendFormat("  VDDQ               : {0} mV ({1:F3} V)\n", VddqMv, VddqMv / 1000.0);
            sb.AppendFormat("  VPP                : {0} mV ({1:F3} V)\n", VppMv, VppMv / 1000.0);
            return sb.ToString();
        }

        private static void AppendTiming(StringBuilder sb, string name, Ddr5SpdTiming timing)
        {
            if (timing != null && timing.IsDefined)
                sb.AppendFormat("  {0,-19}: {1}\n", name, timing);
        }
    }
}
