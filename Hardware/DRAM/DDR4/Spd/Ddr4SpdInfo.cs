using System.Collections.Generic;
using System.Text;
using ZenStates.Core.Hardware.DRAM.DDR4.Profiles;
using ZenStates.Core.Hardware.DRAM.DDR4.Thermal;

namespace ZenStates.Core.Hardware.DRAM.DDR4.Spd
{
    /// <summary>Decoded SPD of a DDR4 module (JEDEC SPD Annex L, 512-byte EE1004 image).</summary>
    public class Ddr4SpdInfo
    {
        // General

        /// <summary>Raw 512-byte SPD image. Bytes that were not read are 0.</summary>
        public byte[] RawSpd;

        public bool IsValid;

        /// <summary>
        /// Only the bytes needed at startup were read: the base configuration (0-127) and the manufacturing
        /// information (320-353). The module specific block, CRC of block 1 and the XMP profiles are missing.
        /// </summary>
        public bool IsPartial;

        public int BytesUsed;
        public int BytesTotal;
        public string SpdRevision;
        public byte DeviceType;
        public string DeviceTypeString;
        public string MemoryFamily;

        // Module type
        public byte BaseModuleType;
        public string ModuleTypeString;
        public bool IsHybrid;
        public string HybridTypeString;

        // SDRAM density, addressing and package
        public int DieDensityMbit;
        public int BankGroups;
        public int BanksPerGroup;
        public int ColumnBits;
        public int RowBits;
        public string PackageType;
        public int DieCount;
        public string SignalLoading;
        public bool Is3DS;

        /// <summary>Odd ranks use a different SDRAM (bytes 10 and 12 bit 6).</summary>
        public bool IsAsymmetric;
        public int SecondDieDensityMbit;
        public int SecondDieCount;
        public string SecondPackageType;

        // Optional features
        public string MaximumActivateCount;
        public string MaximumActivateWindow;
        public string PostPackageRepair;
        public bool SoftPpr;

        // Voltage
        public bool Vdd12Operable;
        public bool Vdd12Endurant;
        public string VddString;

        // Organisation and capacity
        public int DeviceWidthBits;
        public int PackageRanks;
        public int LogicalRanks;
        public int PrimaryBusWidthBits;
        public int BusWidthExtensionBits;
        public bool HasEcc;
        public long TotalCapacityMB;

        // Thermal
        public bool HasThermalSensor;

        // Timing (JEDEC base)
        public int tCKAVGminPs;
        public int tCKAVGmaxPs;
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

        /// <summary>0 when the SPD does not list it (SPD revision 1.0 modules); the JEDEC value is 15 ns.</summary>
        public int tWRminPs;
        public int tWTR_SminPs;
        public int tWTR_LminPs;

        // In clocks at the JEDEC speed
        public int CL;
        public int tRCD;
        public int tRP;
        public int tRAS;
        public int tRC;
        public string TimingString;

        // Module specific (bytes 128-191)
        public string ModuleHeight;
        public string ModuleThickness;
        public string ReferenceRawCard;
        public bool AddressMirrored;
        public string RegisterManufacturer;
        public int RegisterRevision;

        // Integrity
        public bool BaseCrcValid;

        /// <summary>CRC of bytes 128-253. Always false for a partial read.</summary>
        public bool ModuleCrcValid;

        // Manufacturing (bytes 320-352)
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

        // Intel XMP 2.0
        public bool HasXmp;
        public string XmpRevision;
        public Ddr4XmpProfile[] XmpProfiles;

        /// <summary>The module thermal sensor, when it has one (filled in by <see cref="MemoryConfig"/>).</summary>
        public Ddr4ThermalData ThermalData;

        public override string ToString()
        {
            StringBuilder sb = new StringBuilder();
            sb.AppendLine("===============================================");
            sb.AppendLine("  DDR4 SPD Decoded Information");
            sb.AppendLine("===============================================");

            if (!IsValid)
            {
                sb.AppendLine("  *** INVALID OR UNSUPPORTED SPD DATA ***");
                sb.AppendFormat("  Device type byte: 0x{0:X2}\n", DeviceType);
                return sb.ToString();
            }

            if (IsPartial)
                sb.AppendLine("  (partial: only the fields read at startup)");

            sb.AppendLine();
            sb.AppendLine("-- General ---------------------------------");
            sb.AppendFormat("  SPD Revision       : {0}\n", SpdRevision);
            sb.AppendFormat("  Device Type        : {0}\n", DeviceTypeString);
            sb.AppendFormat("  Module Type        : {0}\n", ModuleTypeString);
            if (IsHybrid)
                sb.AppendFormat("  Hybrid Type        : {0}\n", HybridTypeString);
            sb.AppendFormat("  SPD Bytes Used     : {0} of {1}\n", BytesUsed, BytesTotal);
            sb.AppendFormat("  CRC                : base {0}{1}\n", BaseCrcValid ? "OK" : "BAD",
                IsPartial ? "" : ModuleCrcValid ? ", module OK" : ", module BAD");

            sb.AppendLine();
            sb.AppendLine("-- Capacity & Organisation -----------------");
            sb.AppendFormat("  Total Capacity     : {0} MB ({1} GB)\n", TotalCapacityMB, TotalCapacityMB / 1024);
            sb.AppendFormat("  Die Density        : {0} Mbit\n", DieDensityMbit);
            sb.AppendFormat("  Package            : {0}, {1} die{2}{3}\n", PackageType, DieCount, DieCount > 1 ? "s" : "",
                string.IsNullOrEmpty(SignalLoading) ? "" : ", " + SignalLoading);
            if (IsAsymmetric)
                sb.AppendFormat("  Odd Ranks          : {0} Mbit, {1} die(s), {2}\n", SecondDieDensityMbit, SecondDieCount, SecondPackageType);
            sb.AppendFormat("  Device Width       : x{0}\n", DeviceWidthBits);
            sb.AppendFormat("  Bank Groups        : {0} x {1} banks\n", BankGroups, BanksPerGroup);
            sb.AppendFormat("  Row / Column Bits  : {0} / {1}\n", RowBits, ColumnBits);
            sb.AppendFormat("  Ranks              : {0} package, {1} logical\n", PackageRanks, LogicalRanks);
            sb.AppendFormat("  Bus Width          : {0} bits{1}\n", PrimaryBusWidthBits, HasEcc ? string.Format(" + {0} ECC", BusWidthExtensionBits) : "");

            sb.AppendLine();
            sb.AppendLine("-- JEDEC Base Speed & Timing ----------------");
            sb.AppendFormat("  Speed Grade        : {0}\n", SpeedGrade);
            sb.AppendFormat("  Clock Frequency    : {0:F1} MHz\n", ClockMHz);
            sb.AppendFormat("  Timing             : {0}\n", TimingString);
            sb.AppendFormat("  tCKAVGmin / max    : {0} / {1} ps\n", tCKAVGminPs, tCKAVGmaxPs);
            sb.AppendFormat("  tAAmin             : {0} ps (CL {1})\n", tAAminPs, CL);
            sb.AppendFormat("  tRCDmin            : {0} ps ({1} clk)\n", tRCDminPs, tRCD);
            sb.AppendFormat("  tRPmin             : {0} ps ({1} clk)\n", tRPminPs, tRP);
            sb.AppendFormat("  tRASmin            : {0} ps ({1} clk)\n", tRASminPs, tRAS);
            sb.AppendFormat("  tRCmin             : {0} ps ({1} clk)\n", tRCminPs, tRC);
            sb.AppendFormat("  tRFC1 / 2 / 4      : {0} / {1} / {2} ns\n", tRFC1minPs / 1000, tRFC2minPs / 1000, tRFC4minPs / 1000);
            sb.AppendFormat("  tFAWmin            : {0} ps\n", tFAWminPs);
            sb.AppendFormat("  tRRD_S / tRRD_L    : {0} / {1} ps\n", tRRD_SminPs, tRRD_LminPs);
            sb.AppendFormat("  tCCD_Lmin          : {0} ps\n", tCCD_LminPs);
            sb.AppendFormat("  tWRmin             : {0}\n", tWRminPs > 0 ? tWRminPs + " ps" : "not listed");
            sb.AppendFormat("  tWTR_S / tWTR_L    : {0} / {1} ps\n", tWTR_SminPs, tWTR_LminPs);

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
            sb.AppendLine("-- Features --------------------------------");
            sb.AppendFormat("  VDD                : {0}\n", VddString);
            sb.AppendFormat("  Thermal Sensor     : {0}\n", HasThermalSensor ? "Present" : "Not present");
            sb.AppendFormat("  Max Activate Count : {0}, window {1}\n", MaximumActivateCount, MaximumActivateWindow);
            sb.AppendFormat("  Post Package Repair: {0}{1}\n", PostPackageRepair, SoftPpr ? ", soft PPR" : "");

            if (!IsPartial)
            {
                sb.AppendLine();
                sb.AppendLine("-- Module ----------------------------------");
                sb.AppendFormat("  Height / Thickness : {0} / {1}\n", ModuleHeight, ModuleThickness);
                sb.AppendFormat("  Reference Raw Card : {0}\n", ReferenceRawCard);
                sb.AppendFormat("  Address Mirroring  : {0}\n", AddressMirrored ? "Odd ranks mirrored" : "None");
                if (!string.IsNullOrEmpty(RegisterManufacturer))
                    sb.AppendFormat("  Register           : {0}, rev 0x{1:X2}\n", RegisterManufacturer, RegisterRevision);
            }

            sb.AppendLine();
            sb.AppendLine("-- Manufacturing ---------------------------");
            sb.AppendFormat("  Module Manufacturer: {0}\n", ModuleManufacturer);
            sb.AppendFormat("  Module Part Number : {0}\n", ModulePartNumber);
            sb.AppendFormat("  Module Serial      : {0}\n", CoreOptions.Current.PrintSerialNumbers ? ModuleSerialNumber : "****");
            sb.AppendFormat("  Module Date        : {0}\n", ModuleMfgDate);
            sb.AppendFormat("  Module Location    : 0x{0:X2}\n", ModuleMfgLocation);
            sb.AppendFormat("  Module Revision    : 0x{0:X2}\n", ModuleRevisionCode);
            sb.AppendFormat("  DRAM Manufacturer  : {0}\n", DramManufacturer);
            sb.AppendFormat("  DRAM Stepping      : 0x{0:X2}\n", DramStepping);

            if (HasXmp)
            {
                sb.AppendLine();
                sb.AppendFormat("-- Intel XMP {0} ------------------------------\n", XmpRevision ?? "?");
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

            if (ThermalData != null && ThermalData.IsValid)
            {
                sb.AppendLine();
                sb.AppendLine("-- Thermal Sensor (TSOD) --------------------");
                sb.Append(ThermalData.ToString());
            }

            sb.AppendLine("===============================================");
            return sb.ToString();
        }
    }
}
