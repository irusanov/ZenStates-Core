using System.Collections.Generic;
using System.Text;
using ZenStates.Core.Hardware.DRAM.DDR5.Hub;
using ZenStates.Core.Hardware.DRAM.DDR5.Profiles;
using ZenStates.Core.Hardware.DRAM.DDR5.Thermal;

namespace ZenStates.Core.Hardware.DRAM.DDR5.Spd
{
    /// <summary>
    /// Decoded SPD of a DDR5 or LPDDR5/5X module (JESD400-5, JESD406-5), plus the live data of its SPD5118 hub.
    /// Fields that only one of the two standards defines are marked; they stay 0 / null for the other.
    /// </summary>
    public class Ddr5SpdInfo
    {
        // ---- General (bytes 0~3) ------------------------------------------------

        /// <summary>The SPD image (1024 bytes). Bytes that were not read are 0, see <see cref="IsPartial"/>.</summary>
        public byte[] RawSpd;

        /// <summary>The key byte is DDR5, DDR5 NVDIMM-P, LPDDR5 or LPDDR5X.</summary>
        public bool IsValid;

        /// <summary>
        /// Only the base configuration (bytes 0~127), the common module bytes (192~239) and the manufacturing
        /// information (512~554) were read: no CRC check, module specific bytes or XMP / EXPO profiles.
        /// </summary>
        public bool IsPartial;

        public int BytesTotal;
        public int SpdBetaLevel;
        public int SpdRevisionEncoding;
        public int SpdRevisionAdditions;
        public string SpdRevision;

        /// <summary>Key byte (byte 2): 0x12 DDR5, 0x13 LPDDR5, 0x14 DDR5 NVDIMM-P, 0x15 LPDDR5X.</summary>
        public byte DeviceType;
        public string DeviceTypeString;
        /// <summary>"DDR5", "LPDDR5" or "LPDDR5X".</summary>
        public string MemoryFamily;
        /// <summary>LPDDR5 or LPDDR5X: the base configuration follows JESD406-5.</summary>
        public bool IsLpddr5;

        public byte BaseModuleType;
        public string ModuleTypeString;
        public bool IsHybrid;
        public string HybridTypeString;

        // ---- SDRAM (DDR5 bytes 4~11, LPDDR5 bytes 4~6 and 12) ---------------------

        public int FirstDieDensityMbit;
        public int FirstDieCount;
        public string FirstPackageType;
        public int FirstDeviceWidthBits;
        public int FirstRowBits;
        public int FirstColumnBits;
        public int FirstBankGroups;
        public int FirstBanksPerBankGroup;

        /// <summary>DDR5 only: second SDRAM type of an asymmetrical module (bytes 8~11).</summary>
        public int SecondDieDensityMbit;
        public int SecondDieCount;
        public int SecondDeviceWidthBits;
        public int SecondRowBits;
        public int SecondColumnBits;
        public bool IsAsymmetric;

        /// <summary>LPDDR5 only: byte mode device (byte 12 [6]).</summary>
        public bool IsByteMode;
        /// <summary>LPDDR5 only: system sub-channel bus width (byte 13).</summary>
        public int SystemSubChannelWidthBits;
        /// <summary>LPDDR5 only: post package repair (byte 9).</summary>
        public bool PprSupported;
        public bool SoftPprSupported;

        // ---- Module organisation (bytes 234~235) -------------------------------

        /// <summary>Package ranks per sub-channel.</summary>
        public int RanksPerChannel;
        /// <summary>Logical ranks per sub-channel: package ranks times the 3DS stack height.</summary>
        public int LogicalRanksPerChannel;
        public int SubChannelsPerDimm;
        public int PrimaryBusWidthBits;
        public int BusWidthExtensionBits;
        public long TotalCapacityMB;

        // ---- Voltage (DDR5 bytes 16~18; LPDDR5 SPD has none) ---------------------

        public string VddString;
        public string VddqString;
        public string VppString;

        // ---- JEDEC base timing ---------------------------------------------------

        public int tCKAVGminPs;
        public int tCKAVGmaxPs;

        public int SpeedMTs;
        public string SpeedGrade;
        /// <summary>CK frequency: half the data rate on DDR5, an eighth on LPDDR5.</summary>
        public double ClockMHz;

        /// <summary>DDR5 only: CAS latencies supported (bytes 24~28).</summary>
        public List<int> SupportedCLs;

        public int tAAminPs;
        public int tRCDminPs;
        /// <summary>tRPmin on DDR5, tRPabmin (all banks) on LPDDR5.</summary>
        public int tRPminPs;
        /// <summary>LPDDR5 only: tRPpbmin (per bank).</summary>
        public int tRPpbMinPs;
        /// <summary>DDR5 only.</summary>
        public int tRASminPs;
        public int tRCminPs;
        public int tWRminPs;

        /// <summary>DDR5 only, in nanoseconds as stored.</summary>
        public int tRFC1minNs;
        public int tRFC2minNs;
        public int tRFCsbMinNs;

        /// <summary>LPDDR5 only: tRFCab / tRFCpb in picoseconds.</summary>
        public int tRFCabMinPs;
        public int tRFCpbMinPs;

        /// <summary>Clocks at tCKAVGmin (JEDEC rounding algorithm); RL on LPDDR5.</summary>
        public int CL;
        public int tRCD;
        public int tRP;
        public int tRAS;
        public int tRC;
        public int tWR;
        public string TimingString;

        // ---- Common module bytes (192~239) -----------------------------------

        public string ModuleSpdRevision;
        public string ModuleHeight;
        public string ModuleThickness;
        public string ReferenceRawCard;
        public string OperatingTemperatureRange;
        public bool HeatSpreader;
        public int DramRows;

        /// <summary>Support devices the SPD lists (bytes 194~213).</summary>
        public Ddr5SpdDevice SpdDevice;
        public Ddr5SpdDevice Pmic0;
        public Ddr5SpdDevice Pmic1;
        public Ddr5SpdDevice Pmic2;
        public Ddr5SpdDevice ThermalSensors;
        public bool ThermalSensor0Present;
        public bool ThermalSensor1Present;

        /// <summary>The module has a temperature sensor: the SPD5118 hub's own or a discrete TS0 / TS1.</summary>
        public bool HasThermalSensor;

        // ---- CRC (bytes 510~511, not read on a partial SPD) ---------------------

        public int BaseCrc;
        public bool BaseCrcValid;

        // ---- Manufacturing (bytes 512~554) -------------------------------------

        public byte ModuleMfgIdBank;
        public byte ModuleMfgIdMfr;
        public string ModuleManufacturer;
        public int ModuleMfgLocation;
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

        // ---- XMP 3.0 / EXPO (bytes 640~959, DDR5 only, not read on a partial SPD) ----

        public bool HasXmp;
        public string XmpRevision;
        /// <summary>The three vendor profiles; an entry that is not present has IsValid false.</summary>
        public Ddr5XmpProfile[] XmpProfiles;

        public bool HasExpo;
        public string ExpoRevision;
        public bool ExpoCrcValid;
        public Ddr5ExpoProfile ExpoProfile1;
        public Ddr5ExpoProfile ExpoProfile2;

        // ---- Live data from the SPD5118 hub (null when decoded from a file) ----

        public Spd5118HubInfo HubInfo;
        public Ddr5ThermalData ThermalData;

        private static string Listed(Ddr5SpdDevice device)
        {
            return device != null ? device.ToString() : "Not listed";
        }

        public override string ToString()
        {
            StringBuilder sb = new StringBuilder();
            sb.AppendLine("===============================================");
            sb.AppendLine("  DRAM SPD Decoded Information");
            sb.AppendLine("===============================================");

            if (!IsValid)
            {
                sb.AppendLine("  *** INVALID OR UNSUPPORTED SPD DATA ***");
                sb.AppendFormat("  Device type byte: 0x{0:X2}\n", DeviceType);
                return sb.ToString();
            }

            sb.AppendLine();
            sb.AppendLine("-- General ---------------------------------");
            sb.AppendFormat("  SPD Revision       : {0}\n", SpdRevision);
            sb.AppendFormat("  Device Type        : {0}\n", DeviceTypeString);
            sb.AppendFormat("  Memory Family      : {0}\n", MemoryFamily);
            sb.AppendFormat("  Module Type        : {0}\n", ModuleTypeString);
            sb.AppendFormat("  SPD Bytes Total    : {0}\n", BytesTotal);
            if (IsHybrid)
                sb.AppendFormat("  Hybrid Type        : {0}\n", HybridTypeString);
            if (!IsPartial)
                sb.AppendFormat("  CRC                : 0x{0:X4} ({1})\n", BaseCrc, BaseCrcValid ? "OK" : "MISMATCH");

            sb.AppendLine();
            sb.AppendLine("-- Capacity & Organisation -----------------");
            sb.AppendFormat("  Total Capacity     : {0} MB ({1} GB)\n", TotalCapacityMB, TotalCapacityMB / 1024);
            sb.AppendFormat("  Die Density (1st)  : {0} Mbit ({1} die, {2})\n", FirstDieDensityMbit, FirstDieCount, FirstPackageType);
            if (IsAsymmetric)
                sb.AppendFormat("  Die Density (2nd)  : {0} Mbit ({1} die)\n", SecondDieDensityMbit, SecondDieCount);
            sb.AppendFormat("  Device Width       : x{0}\n", FirstDeviceWidthBits);
            sb.AppendFormat("  Row Bits           : {0}\n", FirstRowBits);
            sb.AppendFormat("  Column Bits        : {0}\n", FirstColumnBits);
            sb.AppendFormat("  Bank Groups        : {0} x {1} banks\n", FirstBankGroups, FirstBanksPerBankGroup);
            sb.AppendFormat("  Ranks/Channel      : {0}\n", RanksPerChannel);
            if (LogicalRanksPerChannel != RanksPerChannel)
                sb.AppendFormat("  Logical Ranks      : {0}\n", LogicalRanksPerChannel);
            sb.AppendFormat("  Sub-Channels       : {0}\n", SubChannelsPerDimm);
            sb.AppendFormat("  Bus Width/Sub-Ch   : {0} bits\n", PrimaryBusWidthBits);
            if (BusWidthExtensionBits > 0)
                sb.AppendFormat("  Bus Extension      : {0} bits\n", BusWidthExtensionBits);
            if (IsLpddr5 && IsByteMode)
                sb.AppendLine("  Byte Mode          : Yes");

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
            if (IsLpddr5)
            {
                sb.AppendFormat("  tRPabmin           : {0} ps\n", tRPminPs);
                sb.AppendFormat("  tRPpbmin           : {0} ps\n", tRPpbMinPs);
                sb.AppendFormat("  tRFCab             : {0:F3} ns\n", tRFCabMinPs / 1000.0);
                sb.AppendFormat("  tRFCpb             : {0:F3} ns\n", tRFCpbMinPs / 1000.0);
            }
            else
            {
                sb.AppendFormat("  tRPmin             : {0} ps\n", tRPminPs);
                sb.AppendFormat("  tRASmin            : {0} ps ({1:F0} ns)\n", tRASminPs, tRASminPs / 1000.0);
                sb.AppendFormat("  tRCmin             : {0} ps ({1:F1} ns)\n", tRCminPs, tRCminPs / 1000.0);
                sb.AppendFormat("  tWRmin             : {0} ps ({1:F0} ns)\n", tWRminPs, tWRminPs / 1000.0);
                sb.AppendFormat("  tRFC1              : {0} ns\n", tRFC1minNs);
                sb.AppendFormat("  tRFC2              : {0} ns\n", tRFC2minNs);
                sb.AppendFormat("  tRFCsb             : {0} ns\n", tRFCsbMinNs);
            }

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

            if (!IsLpddr5)
            {
                sb.AppendLine();
                sb.AppendLine("-- Voltage ---------------------------------");
                sb.AppendFormat("  VDD                : {0}\n", VddString);
                sb.AppendFormat("  VDDQ               : {0}\n", VddqString);
                sb.AppendFormat("  VPP                : {0}\n", VppString);
            }

            sb.AppendLine();
            sb.AppendLine("-- Module ----------------------------------");
            sb.AppendFormat("  Module SPD Revision: {0}\n", ModuleSpdRevision);
            sb.AppendFormat("  Height             : {0}\n", ModuleHeight);
            sb.AppendFormat("  Thickness          : {0}\n", ModuleThickness);
            sb.AppendFormat("  Raw Card           : {0}\n", ReferenceRawCard);
            sb.AppendFormat("  Temperature Range  : {0}\n", OperatingTemperatureRange);
            sb.AppendFormat("  Heat Spreader      : {0}\n", HeatSpreader ? "Yes" : "No");
            if (DramRows > 0)
                sb.AppendFormat("  DRAM Rows          : {0}\n", DramRows);

            sb.AppendLine();
            sb.AppendLine("-- Thermal ---------------------------------");
            sb.AppendFormat("  Thermal Sensor     : {0}\n", HasThermalSensor ? "Present" : "Not present");

            sb.AppendLine();
            sb.AppendLine("-- Support Devices -------------------------");
            sb.AppendFormat("  SPD Device         : {0}\n", Listed(SpdDevice));
            sb.AppendFormat("  PMIC0              : {0}\n", Listed(Pmic0));
            sb.AppendFormat("  PMIC1              : {0}\n", Listed(Pmic1));
            sb.AppendFormat("  PMIC2              : {0}\n", Listed(Pmic2));
            sb.AppendFormat("  TS0 / TS1          : {0} / {1}\n",
                ThermalSensor0Present ? "Present" : "Not listed",
                ThermalSensor1Present ? "Present" : "Not listed");

            sb.AppendLine();
            sb.AppendLine("-- Manufacturing ---------------------------");
            sb.AppendFormat("  Module Manufacturer: {0}\n", ModuleManufacturer);
            sb.AppendFormat("  Module Part Number : {0}\n", ModulePartNumber);
            sb.AppendFormat("  Module Serial      : {0}\n", CoreOptions.Current.PrintSerialNumbers ? ModuleSerialNumber : "****");
            sb.AppendFormat("  Module Date        : {0}\n", ModuleMfgDate);
            sb.AppendFormat("  Module Revision    : 0x{0:X2}\n", ModuleRevisionCode);
            sb.AppendFormat("  DRAM Manufacturer  : {0}\n", DramManufacturer);
            sb.AppendFormat("  DRAM Stepping      : 0x{0:X2}\n", DramStepping);

            if (HasExpo)
            {
                sb.AppendLine();
                sb.AppendFormat("-- AMD EXPO {0} Profile 1 ------------------\n", ExpoRevision);
                if (ExpoProfile1 != null)
                    sb.Append(ExpoProfile1.ToString());
                if (!ExpoCrcValid)
                    sb.AppendLine("  EXPO CRC           : MISMATCH");

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
                sb.AppendFormat("-- Intel XMP 3.0 (Rev {0}) ------------------\n", XmpRevision ?? "?");
                if (XmpProfiles != null)
                {
                    for (int p = 0; p < XmpProfiles.Length; p++)
                    {
                        if (XmpProfiles[p] == null || !XmpProfiles[p].IsValid)
                            continue;

                        sb.AppendLine();
                        sb.AppendFormat("  [XMP Profile {0}]\n", p + 1);
                        sb.Append(XmpProfiles[p].ToString());
                    }
                }
            }

            if (HubInfo != null)
            {
                sb.AppendLine();
                sb.AppendLine("-- SPD Hub (SPD5118) -----------------------");
                sb.Append(HubInfo.ToString());
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

            sb.AppendLine("===============================================");
            return sb.ToString();
        }
    }
}
