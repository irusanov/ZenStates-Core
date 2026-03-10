using System.Collections.Generic;
using System.Text;

namespace ZenStates.Core
{
    public class Ddr5SpdInfo
    {
        // ── General ──────────────────────────────────────────────────────────

        /// <summary>Raw 1024-byte SPD image.</summary>
        public byte[] RawSpd;

        public int BytesTotal;
        public int SpdRevisionEncoding;
        public int SpdRevisionAdditions;
        public string SpdRevision;
        public byte DeviceType;
        public string DeviceTypeString;
        public bool IsValid;

        // ── Module type ──────────────────────────────────────────────────────

        public byte BaseModuleType;
        public string ModuleTypeString;
        public bool IsHybrid;
        public string HybridTypeString;

        // ── SDRAM density & package ──────────────────────────────────────────

        public int FirstDieDensityMbit;
        public int FirstDieCount;
        public int SecondDieDensityMbit;
        public int SecondDieCount;
        public bool IsAsymmetric;

        // ── SDRAM organisation ───────────────────────────────────────────────

        public int FirstDeviceWidthBits;
        public int FirstColumnBits;
        public int FirstRowBits;
        public int SecondColumnBits;
        public int SecondRowBits;

        // ── Capacity ─────────────────────────────────────────────────────────

        public int ChannelCount;
        public int RanksPerChannel;
        public int SubChannelsPerDimm;
        public int PrimaryBusWidthBits;
        public long TotalCapacityMB;

        // ── Voltage ──────────────────────────────────────────────────────────

        public string VddString;

        // ── Timing (JEDEC base) ──────────────────────────────────────────────

        public int tCKAVGminPs;
        public int tCKAVGmaxPs;

        public int SpeedMTs;
        public string SpeedGrade;
        public double ClockMHz;

        public List<int> SupportedCLs;

        public int tAAminPs;
        public int tRCDminPs;
        public int tRPminPs;
        public int tRASminPs;
        public int tRCminPs;
        public int tWRminPs;

        /// <summary>tRFC values in nanoseconds (as stored in SPD).</summary>
        public int tRFC1minNs;
        public int tRFC2minNs;
        public int tRFCsbMinNs;

        public int CL;
        public int tRCD;
        public int tRP;
        public string TimingString;

        // ── Thermal ──────────────────────────────────────────────────────────

        public bool HasThermalSensor;

        // ── Manufacturing ────────────────────────────────────────────────────

        public byte ModuleMfgIdBank;
        public byte ModuleMfgIdMfr;
        public string ModuleManufacturer;
        public int ModuleMfgYear;
        public int ModuleMfgWeek;
        public string ModuleMfgDate;
        public string ModuleSerialNumber;
        public string ModulePartNumber;
        public int ModuleRevisionCode;

        public byte DramMfgIdBank;
        public byte DramMfgIdMfr;
        public string DramManufacturer;
        public int DramStepping;

        // ── XMP / EXPO ───────────────────────────────────────────────────────

        public bool HasXmp;
        public string XmpRevision;
        public Ddr5XmpProfile[] XmpProfiles;   // up to 5 (3 vendor + 2 user)

        public bool HasExpo;
        public Ddr5ExpoProfile ExpoProfile1;
        public Ddr5ExpoProfile ExpoProfile2;

        // ── Thermal sensor ──────────────────────────────────────────────────

        /// <summary>
        /// Live thermal sensor reading from SPD5118 hub (populated by
        /// ReadAndDecodeAll when SMBus is available, null for file-only decode).
        /// </summary>
        public Ddr5ThermalData ThermalData;

        // ── PMIC ────────────────────────────────────────────────────────────

        /// <summary>
        /// Live PMIC voltage/status data (populated by ReadAndDecodeAll
        /// when SMBus is available, null for file-only decode).
        /// </summary>
        public Ddr5PmicData PmicData;

        // ── Pretty-print ─────────────────────────────────────────────────────

        public override string ToString()
        {
            StringBuilder sb = new StringBuilder();
            sb.AppendLine("===============================================");
            sb.AppendLine("  DDR5 SPD Decoded Information");
            sb.AppendLine("===============================================");

            if (!IsValid)
            {
                sb.AppendLine("  *** INVALID OR NON-DDR5 SPD DATA ***");
                sb.AppendFormat("  Device type byte: 0x{0:X2}\n", DeviceType);
                return sb.ToString();
            }

            sb.AppendLine();
            sb.AppendLine("-- General ---------------------------------");
            sb.AppendFormat("  SPD Revision       : {0}\n", SpdRevision);
            sb.AppendFormat("  Device Type        : {0}\n", DeviceTypeString);
            sb.AppendFormat("  Module Type        : {0}\n", ModuleTypeString);
            sb.AppendFormat("  SPD Bytes Total    : {0}\n", BytesTotal);
            if (IsHybrid)
                sb.AppendFormat("  Hybrid Type        : {0}\n", HybridTypeString);

            sb.AppendLine();
            sb.AppendLine("-- Capacity & Organisation -----------------");
            sb.AppendFormat("  Total Capacity     : {0} MB ({1} GB)\n", TotalCapacityMB, TotalCapacityMB / 1024);
            sb.AppendFormat("  Die Density (1st)  : {0} Mbit ({1} die)\n", FirstDieDensityMbit, FirstDieCount);
            if (IsAsymmetric)
                sb.AppendFormat("  Die Density (2nd)  : {0} Mbit ({1} die)\n", SecondDieDensityMbit, SecondDieCount);
            sb.AppendFormat("  Device Width       : x{0}\n", FirstDeviceWidthBits);
            sb.AppendFormat("  Column Bits        : {0}\n", FirstColumnBits);
            sb.AppendFormat("  Row Bits           : {0}\n", FirstRowBits);
            sb.AppendFormat("  Ranks/Channel      : {0}\n", RanksPerChannel);
            sb.AppendFormat("  Sub-Channels/DIMM  : {0}\n", SubChannelsPerDimm);
            sb.AppendFormat("  Bus Width/Sub-Ch   : {0} bits\n", PrimaryBusWidthBits);

            sb.AppendLine();
            sb.AppendLine("-- JEDEC Base Speed & Timing ----------------");
            sb.AppendFormat("  Speed Grade        : {0}\n", SpeedGrade);
            sb.AppendFormat("  Clock Frequency    : {0:F1} MHz\n", ClockMHz);
            sb.AppendFormat("  Data Rate          : {0} MT/s\n", SpeedMTs);
            sb.AppendFormat("  Timing             : {0}\n", TimingString);
            sb.AppendFormat("  tCKAVGmin          : {0} ps\n", tCKAVGminPs);
            sb.AppendFormat("  tCKAVGmax          : {0} ps\n", tCKAVGmaxPs);
            sb.AppendFormat("  tAAmin             : {0} ps\n", tAAminPs);
            sb.AppendFormat("  tRCDmin            : {0} ps\n", tRCDminPs);
            sb.AppendFormat("  tRPmin             : {0} ps\n", tRPminPs);
            sb.AppendFormat("  tRASmin            : {0} ps ({1:F0} ns)\n", tRASminPs, tRASminPs / 1000.0);
            sb.AppendFormat("  tRCmin             : {0} ps ({1:F1} ns)\n", tRCminPs, tRCminPs / 1000.0);
            sb.AppendFormat("  tWRmin             : {0} ps ({1:F0} ns)\n", tWRminPs, tWRminPs / 1000.0);
            sb.AppendFormat("  tRFC1              : {0} ns\n", tRFC1minNs);
            sb.AppendFormat("  tRFC2              : {0} ns\n", tRFC2minNs);
            sb.AppendFormat("  tRFCsb             : {0} ns\n", tRFCsbMinNs);

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

            sb.AppendLine();
            sb.AppendLine("-- Voltage ---------------------------------");
            sb.AppendFormat("  VDD                : {0}\n", VddString);

            sb.AppendLine();
            sb.AppendLine("-- Thermal ---------------------------------");
            sb.AppendFormat("  Thermal Sensor     : {0}\n", HasThermalSensor ? "Present" : "Not present");

            sb.AppendLine();
            sb.AppendLine("-- Manufacturing ---------------------------");
            sb.AppendFormat("  Module Manufacturer: {0}\n", ModuleManufacturer);
            sb.AppendFormat("  Module Part Number : {0}\n", ModulePartNumber);
            sb.AppendFormat("  Module Serial      : {0}\n", ModuleSerialNumber);
            sb.AppendFormat("  Module Date        : {0}\n", ModuleMfgDate);
            sb.AppendFormat("  Module Revision    : 0x{0:X2}\n", ModuleRevisionCode);
            sb.AppendFormat("  DRAM Manufacturer  : {0}\n", DramManufacturer);
            sb.AppendFormat("  DRAM Stepping      : 0x{0:X2}\n", DramStepping);

            if (HasExpo)
            {
                sb.AppendLine();
                sb.AppendLine("-- AMD EXPO Profile 1 ----------------------");
                if (ExpoProfile1 != null)
                    sb.Append(ExpoProfile1.ToString());

                if (ExpoProfile2 != null && ExpoProfile2.IsValid)
                {
                    sb.AppendLine();
                    sb.AppendLine("-- AMD EXPO Profile 2 ----------------------");
                    sb.Append(ExpoProfile2.ToString());
                }
            }

            if (HasXmp)
            {
                sb.AppendLine();
                sb.AppendFormat("-- Intel XMP 3.0 (Rev {0}) ------------------\n",
                    XmpRevision != null ? XmpRevision : "?");
                if (XmpProfiles != null)
                {
                    for (int p = 0; p < XmpProfiles.Length; p++)
                    {
                        if (XmpProfiles[p] != null && XmpProfiles[p].IsValid)
                        {
                            string label = (p < 3) ?
                                string.Format("XMP Profile {0}", p + 1) :
                                string.Format("XMP User Profile {0}", p + 1);
                            sb.AppendLine();
                            sb.AppendFormat("  [{0}]\n", label);
                            sb.Append(XmpProfiles[p].ToString());
                        }
                    }
                }
            }

            if (ThermalData != null && ThermalData.IsValid)
            {
                sb.AppendLine();
                sb.AppendLine("-- Thermal Sensor (SPD5118) -----------------");
                if (!ThermalData.TempSensorEnabled)
                    sb.AppendLine("  Sensor present but DISABLED");
                else
                    sb.Append(ThermalData.ToString());
            }

            if (PmicData != null && PmicData.IsValid)
            {
                sb.AppendLine();
                sb.AppendLine("-- PMIC (Power Management IC) ---------------");
                sb.Append(PmicData.ToString());
            }

            sb.AppendLine("===============================================");
            return sb.ToString();
        }
    }
}
