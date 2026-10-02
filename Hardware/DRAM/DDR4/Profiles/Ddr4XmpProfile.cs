using System.Collections.Generic;
using System.Text;

namespace ZenStates.Core.Hardware.DRAM.DDR4.Profiles
{
    /// <summary>An Intel XMP 2.0 profile of a DDR4 module (SPD bytes 393-439 and 440-486).</summary>
    public class Ddr4XmpProfile
    {
        public bool IsValid;
        public int ProfileNumber;

        /// <summary>Recommended DIMMs per channel (0 when not specified).</summary>
        public int DimmsPerChannel;

        public int VddMv;

        public int tCKAVGminPs;
        public int SpeedMTs;
        public double ClockMHz;
        public string SpeedGrade;

        public List<int> SupportedCLs;

        public int tAAminPs;
        public int tRCDminPs;
        public int tRPminPs;
        public int tRASminPs;
        public int tRCminPs;
        public int tRFC1minPs;
        public int tRFC2minPs;
        public int tRFC4minPs;
        public int tFAWminPs;
        public int tRRD_SminPs;
        public int tRRD_LminPs;
        public int tCCD_LminPs;

        // In clocks at the profile speed
        public int CL;
        public int tRCD;
        public int tRP;
        public int tRAS;
        public int tRC;
        public int tRFC1;
        public int tRFC2;
        public int tRFC4;
        public int tFAW;
        public int tRRD_S;
        public int tRRD_L;
        public int tCCD_L;
        public string TimingString;

        public override string ToString()
        {
            if (!IsValid) return "  (not present)";

            StringBuilder sb = new StringBuilder();
            sb.AppendFormat("  Speed Grade        : {0}\n", SpeedGrade);
            sb.AppendFormat("  Clock Frequency    : {0:F1} MHz\n", ClockMHz);
            sb.AppendFormat("  Data Rate          : {0} MT/s\n", SpeedMTs);
            sb.AppendFormat("  Timing             : {0}\n", TimingString);
            sb.AppendFormat("  VDD                : {0} mV ({1:F2} V)\n", VddMv, VddMv / 1000.0);
            if (DimmsPerChannel > 0)
                sb.AppendFormat("  DIMMs per Channel  : {0}\n", DimmsPerChannel);
            sb.AppendFormat("  tCKAVGmin          : {0} ps\n", tCKAVGminPs);
            sb.AppendFormat("  tAAmin             : {0} ps (CL {1})\n", tAAminPs, CL);
            sb.AppendFormat("  tRCDmin            : {0} ps ({1} clk)\n", tRCDminPs, tRCD);
            sb.AppendFormat("  tRPmin             : {0} ps ({1} clk)\n", tRPminPs, tRP);
            sb.AppendFormat("  tRASmin            : {0} ps ({1} clk)\n", tRASminPs, tRAS);
            sb.AppendFormat("  tRCmin             : {0} ps ({1} clk)\n", tRCminPs, tRC);
            sb.AppendFormat("  tRFC1min           : {0} ns ({1} clk)\n", tRFC1minPs / 1000, tRFC1);
            sb.AppendFormat("  tRFC2min           : {0} ns ({1} clk)\n", tRFC2minPs / 1000, tRFC2);
            sb.AppendFormat("  tRFC4min           : {0} ns ({1} clk)\n", tRFC4minPs / 1000, tRFC4);
            sb.AppendFormat("  tFAWmin            : {0} ps ({1} clk)\n", tFAWminPs, tFAW);
            sb.AppendFormat("  tRRD_Smin          : {0} ps ({1} clk)\n", tRRD_SminPs, tRRD_S);
            sb.AppendFormat("  tRRD_Lmin          : {0} ps ({1} clk)\n", tRRD_LminPs, tRRD_L);
            if (tCCD_LminPs > 0)
                sb.AppendFormat("  tCCD_Lmin          : {0} ps ({1} clk)\n", tCCD_LminPs, tCCD_L);

            if (SupportedCLs != null && SupportedCLs.Count > 0)
            {
                sb.Append("  Supported CLs      : ");
                for (int i = 0; i < SupportedCLs.Count; i++)
                {
                    if (i > 0) sb.Append(", ");
                    sb.Append(SupportedCLs[i]);
                }
                sb.AppendLine();
            }

            return sb.ToString();
        }
    }
}
