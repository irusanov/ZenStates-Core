using System.Text;

namespace ZenStates.Core
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
        public string TimingString;

        /// <summary>Raw VDD voltage code from SPD (multiply by 5mV + 1100mV base).</summary>
        public int VddCode;
        /// <summary>Raw VDDQ voltage code from SPD.</summary>
        public int VddqCode;
        /// <summary>Calculated VDD in millivolts.</summary>
        public int VddMv;
        /// <summary>Calculated VDDQ in millivolts.</summary>
        public int VddqMv;

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
            sb.AppendFormat("  VDD                : {0} mV ({1:F3} V)\n", VddMv, VddMv / 1000.0);
            sb.AppendFormat("  VDDQ               : {0} mV ({1:F3} V)\n", VddqMv, VddqMv / 1000.0);
            return sb.ToString();
        }
    }
}
