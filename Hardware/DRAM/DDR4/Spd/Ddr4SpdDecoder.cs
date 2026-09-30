using System;
using System.Collections.Generic;
using System.Text;
using ZenStates.Core.Hardware.DRAM.DDR4.Profiles;

namespace ZenStates.Core.Hardware.DRAM.DDR4.Spd
{
    /// <summary>
    /// Decodes a DDR4 SPD image: JEDEC SPD Annex L (bytes 0-383) and Intel XMP 2.0 (bytes 384-486).
    /// Timings are stored in medium timebase units (125 ps) with a signed fine correction (1 ps).
    /// </summary>
    public static class Ddr4SpdDecoder
    {
        public const int SPD_SIZE = 512;

        public const byte DDR4_DEVICE_TYPE = 0x0C;
        public const byte DDR4E_DEVICE_TYPE = 0x0E;

        // Base configuration
        private const int SPD_BYTES = 0;
        private const int SPD_REVISION = 1;
        private const int SPD_DEVICE_TYPE = 2;
        private const int SPD_MODULE_TYPE = 3;
        private const int SPD_DENSITY_BANKS = 4;
        private const int SPD_ADDRESSING = 5;
        private const int SPD_PACKAGE = 6;
        private const int SPD_OPTIONAL_FEATURES = 7;
        private const int SPD_OTHER_FEATURES = 9;
        private const int SPD_SECONDARY_PACKAGE = 10;
        private const int SPD_VDD = 11;
        private const int SPD_ORGANISATION = 12;
        private const int SPD_BUS_WIDTH = 13;
        private const int SPD_THERMAL_SENSOR = 14;
        private const int SPD_EXTENDED_MODULE_TYPE = 15;
        private const int SPD_TIMEBASES = 17;
        private const int SPD_TCK_MIN = 18;
        private const int SPD_TCK_MAX = 19;
        private const int SPD_CAS_FIRST = 20;
        private const int SPD_TAA = 24;
        private const int SPD_TRCD = 25;
        private const int SPD_TRP = 26;
        private const int SPD_TRAS_TRC_MSN = 27;
        private const int SPD_TRAS_LSB = 28;
        private const int SPD_TRC_LSB = 29;
        private const int SPD_TRFC1 = 30;
        private const int SPD_TRFC2 = 32;
        private const int SPD_TRFC4 = 34;
        private const int SPD_TFAW_MSN = 36;
        private const int SPD_TFAW_LSB = 37;
        private const int SPD_TRRD_S = 38;
        private const int SPD_TRRD_L = 39;
        private const int SPD_TCCD_L = 40;
        private const int SPD_TWR_MSN = 41;
        private const int SPD_TWR_LSB = 42;
        private const int SPD_TWTR_MSN = 43;
        private const int SPD_TWTR_S_LSB = 44;
        private const int SPD_TWTR_L_LSB = 45;
        private const int SPD_FINE_TCCD_L = 117;
        private const int SPD_FINE_TRRD_L = 118;
        private const int SPD_FINE_TRRD_S = 119;
        private const int SPD_FINE_TRC = 120;
        private const int SPD_FINE_TRP = 121;
        private const int SPD_FINE_TRCD = 122;
        private const int SPD_FINE_TAA = 123;
        private const int SPD_FINE_TCK_MAX = 124;
        private const int SPD_FINE_TCK_MIN = 125;
        private const int SPD_CRC_BASE = 126;

        // Module specific
        private const int SPD_MODULE_HEIGHT = 128;
        private const int SPD_MODULE_THICKNESS = 129;
        private const int SPD_RAW_CARD = 130;
        private const int SPD_UDIMM_ADDRESS_MAPPING = 131;
        private const int SPD_RDIMM_REGISTER_MFG = 133;
        private const int SPD_RDIMM_REGISTER_REV = 135;
        private const int SPD_RDIMM_ADDRESS_MAPPING = 136;
        private const int SPD_CRC_MODULE = 254;

        // Manufacturing
        private const int SPD_MOD_MFG_ID = 320;
        private const int SPD_MOD_MFG_LOCATION = 322;
        private const int SPD_MOD_MFG_YEAR = 323;
        private const int SPD_MOD_MFG_WEEK = 324;
        private const int SPD_MOD_SERIAL = 325;
        private const int SPD_MOD_PARTNO = 329;
        private const int SPD_MOD_PARTNO_LENGTH = 20;
        private const int SPD_MOD_REV = 349;
        private const int SPD_DRAM_MFG_ID = 350;
        private const int SPD_DRAM_STEPPING = 352;

        /// <summary>The manufacturing block a partial read covers: bytes 320-353.</summary>
        public const int MANUFACTURING_FIRST = 320;
        public const int MANUFACTURING_LENGTH = 34;

        // Intel XMP 2.0
        private const int XMP_ID0 = 384;
        private const int XMP_ID1 = 385;
        private const int XMP_PROFILES_ENABLED = 386;
        private const int XMP_REVISION = 387;
        private const int XMP_PROFILE_FIRST = 393;
        private const int XMP_PROFILE_SIZE = 47;

        private const int MTB_PS = 125;

        public static bool IsDdr4DeviceType(byte deviceType)
        {
            return deviceType == DDR4_DEVICE_TYPE || deviceType == DDR4E_DEVICE_TYPE;
        }

        public static Ddr4SpdInfo DecodeFromFile(string path)
        {
            return Decode(System.IO.File.ReadAllBytes(path), false);
        }

        /// <param name="data">SPD image, 512 bytes (a shorter image is padded with 0).</param>
        /// <param name="partial">Only bytes 0-127 and 320-353 were read.</param>
        public static Ddr4SpdInfo Decode(byte[] data, bool partial)
        {
            byte[] spd = new byte[SPD_SIZE];
            if (data != null)
                Array.Copy(data, spd, Math.Min(data.Length, SPD_SIZE));

            Ddr4SpdInfo info = new Ddr4SpdInfo
            {
                RawSpd = spd,
                IsPartial = partial,
                SupportedCLs = new List<int>(),
                XmpProfiles = new Ddr4XmpProfile[2],
                DeviceType = spd[SPD_DEVICE_TYPE],
            };

            info.IsValid = IsDdr4DeviceType(info.DeviceType);
            if (!info.IsValid)
                return info;

            DecodeGeneral(spd, info);
            DecodeDensityAndPackage(spd, info);
            DecodeOrganisation(spd, info);
            DecodeTiming(spd, info);

            info.BaseCrcValid = Crc16(spd, 0, 126) == U16(spd, SPD_CRC_BASE);

            if (!partial)
            {
                DecodeModuleSpecific(spd, info);
                info.ModuleCrcValid = Crc16(spd, 128, 126) == U16(spd, SPD_CRC_MODULE);
                DecodeXmp(spd, info);
            }

            DecodeManufacturing(spd, info);
            return info;
        }

        private static void DecodeGeneral(byte[] spd, Ddr4SpdInfo info)
        {
            byte bytes = spd[SPD_BYTES];
            int used = bytes & 0x0F;
            info.BytesUsed = used >= 1 && used <= 4 ? used * 128 : 0;
            int total = (bytes >> 4) & 0x07;
            info.BytesTotal = total == 1 ? 256 : total == 2 ? 512 : 0;

            byte rev = spd[SPD_REVISION];
            info.SpdRevision = string.Format("{0}.{1}", rev >> 4, rev & 0x0F);

            info.DeviceTypeString = info.DeviceType == DDR4E_DEVICE_TYPE ? "DDR4E SDRAM" : "DDR4 SDRAM";
            info.MemoryFamily = "DDR4";

            byte moduleType = spd[SPD_MODULE_TYPE];
            info.BaseModuleType = (byte)(moduleType & 0x0F);
            info.ModuleTypeString = info.BaseModuleType == 0
                ? "Extended (0x" + (spd[SPD_EXTENDED_MODULE_TYPE] & 0x0F).ToString("X") + ")"
                : ModuleTypeName(info.BaseModuleType);
            info.IsHybrid = (moduleType & 0x80) != 0;
            if (info.IsHybrid)
                info.HybridTypeString = ((moduleType >> 4) & 0x07) == 1 ? "NVDIMM" : "Unknown";

            byte vdd = spd[SPD_VDD];
            info.Vdd12Operable = (vdd & 0x01) != 0;
            info.Vdd12Endurant = (vdd & 0x02) != 0;
            info.VddString = info.Vdd12Operable
                ? "1.2 V" + (info.Vdd12Endurant ? " (operable, endurant)" : " (operable)")
                : "Not specified";

            info.HasThermalSensor = (spd[SPD_THERMAL_SENSOR] & 0x80) != 0;

            byte features = spd[SPD_OPTIONAL_FEATURES];
            info.MaximumActivateCount = MaximumActivateCountName(features & 0x0F);
            switch ((features >> 4) & 0x03)
            {
                case 0: info.MaximumActivateWindow = "8192 x tREFI"; break;
                case 1: info.MaximumActivateWindow = "4096 x tREFI"; break;
                case 2: info.MaximumActivateWindow = "2048 x tREFI"; break;
                default: info.MaximumActivateWindow = "Reserved"; break;
            }

            byte other = spd[SPD_OTHER_FEATURES];
            info.PostPackageRepair = ((other >> 6) & 0x03) == 1 ? "Supported, one row per bank group" : "Not supported";
            info.SoftPpr = (other & 0x20) != 0;
        }

        private static string ModuleTypeName(int code)
        {
            switch (code)
            {
                case 0x1: return "RDIMM";
                case 0x2: return "UDIMM";
                case 0x3: return "SO-DIMM";
                case 0x4: return "LRDIMM";
                case 0x5: return "Mini-RDIMM";
                case 0x6: return "Mini-UDIMM";
                case 0x8: return "72b-SO-RDIMM";
                case 0x9: return "72b-SO-UDIMM";
                case 0xC: return "16b-SO-DIMM";
                case 0xD: return "32b-SO-DIMM";
                default: return string.Format("Reserved (0x{0:X})", code);
            }
        }

        private static bool IsRegistered(int moduleType)
        {
            return moduleType == 0x1 || moduleType == 0x4 || moduleType == 0x5 || moduleType == 0x8;
        }

        private static string MaximumActivateCountName(int code)
        {
            switch (code)
            {
                case 0: return "Untested";
                case 1: return "700K";
                case 2: return "600K";
                case 3: return "500K";
                case 4: return "400K";
                case 5: return "300K";
                case 6: return "200K";
                case 8: return "Unlimited";
                default: return "Reserved";
            }
        }

        private static int DieDensityMbit(int code)
        {
            switch (code)
            {
                case 0x0: return 256;
                case 0x1: return 512;
                case 0x2: return 1024;
                case 0x3: return 2048;
                case 0x4: return 4096;
                case 0x5: return 8192;
                case 0x6: return 16384;
                case 0x7: return 32768;
                case 0x8: return 12288;
                case 0x9: return 24576;
                default: return 0;
            }
        }

        private static string PackageTypeName(byte package)
        {
            return (package & 0x80) != 0 ? "Non-monolithic" : "Monolithic";
        }

        private static string SignalLoadingName(int code)
        {
            switch (code)
            {
                case 1: return "Multi load stack";
                case 2: return "Single load stack (3DS)";
                default: return null;
            }
        }

        private static void DecodeDensityAndPackage(byte[] spd, Ddr4SpdInfo info)
        {
            byte density = spd[SPD_DENSITY_BANKS];
            info.DieDensityMbit = DieDensityMbit(density & 0x0F);
            info.BanksPerGroup = ((density >> 4) & 0x03) == 0 ? 4 : 8;
            switch ((density >> 6) & 0x03)
            {
                case 0: info.BankGroups = 1; break;
                case 1: info.BankGroups = 2; break;
                default: info.BankGroups = 4; break;
            }

            byte addressing = spd[SPD_ADDRESSING];
            info.ColumnBits = 9 + (addressing & 0x07);
            info.RowBits = 12 + ((addressing >> 3) & 0x07);

            byte package = spd[SPD_PACKAGE];
            info.PackageType = PackageTypeName(package);
            info.DieCount = ((package >> 4) & 0x07) + 1;
            info.SignalLoading = SignalLoadingName(package & 0x03);
            info.Is3DS = (package & 0x03) == 2;

            // Byte 12 bit 6: odd ranks use the SDRAM described in byte 10
            info.IsAsymmetric = (spd[SPD_ORGANISATION] & 0x40) != 0;
            if (info.IsAsymmetric)
            {
                byte second = spd[SPD_SECONDARY_PACKAGE];
                int ratio = (second >> 2) & 0x03;
                info.SecondDieDensityMbit = info.DieDensityMbit >> ratio;
                info.SecondDieCount = ((second >> 4) & 0x07) + 1;
                info.SecondPackageType = PackageTypeName(second);
            }
        }

        private static void DecodeOrganisation(byte[] spd, Ddr4SpdInfo info)
        {
            byte organisation = spd[SPD_ORGANISATION];
            int widthCode = organisation & 0x07;
            info.DeviceWidthBits = widthCode <= 3 ? 4 << widthCode : 0;
            info.PackageRanks = ((organisation >> 3) & 0x07) + 1;

            byte bus = spd[SPD_BUS_WIDTH];
            int busCode = bus & 0x07;
            info.PrimaryBusWidthBits = busCode <= 3 ? 8 << busCode : 0;
            info.BusWidthExtensionBits = ((bus >> 3) & 0x03) == 1 ? 8 : 0;
            info.HasEcc = info.BusWidthExtensionBits > 0;

            // 3DS packages stack several logical ranks in one package rank
            info.LogicalRanks = info.PackageRanks * (info.Is3DS ? info.DieCount : 1);

            if (info.DieDensityMbit == 0 || info.DeviceWidthBits == 0 || info.PrimaryBusWidthBits == 0)
                return;

            int devices = info.PrimaryBusWidthBits / info.DeviceWidthBits;
            long rankMB = (long)info.DieDensityMbit / 8 * devices * (info.Is3DS ? info.DieCount : 1);

            if (!info.IsAsymmetric)
            {
                info.TotalCapacityMB = rankMB * info.PackageRanks;
                return;
            }

            long oddRankMB = (long)info.SecondDieDensityMbit / 8 * devices;
            int evenRanks = (info.PackageRanks + 1) / 2;
            int oddRanks = info.PackageRanks / 2;
            info.TotalCapacityMB = rankMB * evenRanks + oddRankMB * oddRanks;
        }

        private static void DecodeTiming(byte[] spd, Ddr4SpdInfo info)
        {
            info.tCKAVGminPs = Timing(spd, SPD_TCK_MIN, SPD_FINE_TCK_MIN);
            info.tCKAVGmaxPs = Timing(spd, SPD_TCK_MAX, SPD_FINE_TCK_MAX);

            DecodeCasLatencies(spd, SPD_CAS_FIRST, info.SupportedCLs);

            info.tAAminPs = Timing(spd, SPD_TAA, SPD_FINE_TAA);
            info.tRCDminPs = Timing(spd, SPD_TRCD, SPD_FINE_TRCD);
            info.tRPminPs = Timing(spd, SPD_TRP, SPD_FINE_TRP);
            info.tRASminPs = (((spd[SPD_TRAS_TRC_MSN] & 0x0F) << 8) | spd[SPD_TRAS_LSB]) * MTB_PS;
            info.tRCminPs = ((((spd[SPD_TRAS_TRC_MSN] >> 4) & 0x0F) << 8) | spd[SPD_TRC_LSB]) * MTB_PS + Fine(spd, SPD_FINE_TRC);
            info.tRFC1minPs = U16(spd, SPD_TRFC1) * MTB_PS;
            info.tRFC2minPs = U16(spd, SPD_TRFC2) * MTB_PS;
            info.tRFC4minPs = U16(spd, SPD_TRFC4) * MTB_PS;
            info.tFAWminPs = (((spd[SPD_TFAW_MSN] & 0x0F) << 8) | spd[SPD_TFAW_LSB]) * MTB_PS;
            info.tRRD_SminPs = Timing(spd, SPD_TRRD_S, SPD_FINE_TRRD_S);
            info.tRRD_LminPs = Timing(spd, SPD_TRRD_L, SPD_FINE_TRRD_L);
            info.tCCD_LminPs = Timing(spd, SPD_TCCD_L, SPD_FINE_TCCD_L);
            info.tWRminPs = (((spd[SPD_TWR_MSN] & 0x0F) << 8) | spd[SPD_TWR_LSB]) * MTB_PS;
            info.tWTR_SminPs = (((spd[SPD_TWTR_MSN] & 0x0F) << 8) | spd[SPD_TWTR_S_LSB]) * MTB_PS;
            info.tWTR_LminPs = ((((spd[SPD_TWTR_MSN] >> 4) & 0x0F) << 8) | spd[SPD_TWTR_L_LSB]) * MTB_PS;

            double exactMTs = SpeedFromTck(info.tCKAVGminPs);
            info.SpeedMTs = (int)exactMTs;
            info.ClockMHz = exactMTs / 2.0;
            info.SpeedGrade = info.SpeedMTs > 0 ? "DDR4-" + info.SpeedMTs : "Unknown";

            if (exactMTs > 0)
            {
                double tCK = 2000000.0 / exactMTs;
                info.CL = Clocks(info.tAAminPs, tCK);
                info.tRCD = Clocks(info.tRCDminPs, tCK);
                info.tRP = Clocks(info.tRPminPs, tCK);
                info.tRAS = Clocks(info.tRASminPs, tCK);
                info.tRC = Clocks(info.tRCminPs, tCK);
            }

            info.TimingString = info.CL > 0
                ? string.Format("{0}-{1}-{2}-{3} @ {4}", info.CL, info.tRCD, info.tRP, info.tRAS, info.SpeedGrade)
                : info.SpeedGrade;
        }

        /// <summary>
        /// Data rate from the clock period, snapped to the 33.3 MT/s grid DDR4 speeds sit on
        /// (2133.3, 2666.7, 3466.7 ...). The SPD only stores the period to 1 ps.
        /// </summary>
        internal static double SpeedFromTck(int tCKps)
        {
            if (tCKps <= 0)
                return 0;

            double mts = 2000000.0 / tCKps;
            return Math.Round(mts * 3.0 / 100.0) * 100.0 / 3.0;
        }

        /// <summary>JEDEC rounding of a minimum time to clocks, with the 2.5 % guard band of the spec.</summary>
        internal static int Clocks(int ps, double tCKps)
        {
            if (ps <= 0 || tCKps <= 0)
                return 0;

            return (int)Math.Floor(ps / tCKps + 0.974);
        }

        /// <summary>Bytes first..first+3: CL 7-36, or CL 23-52 when bit 7 of the last byte is set.</summary>
        private static void DecodeCasLatencies(byte[] spd, int first, List<int> result)
        {
            int baseCl = (spd[first + 3] & 0x80) != 0 ? 23 : 7;
            for (int byteIndex = 0; byteIndex < 4; byteIndex++)
            {
                byte mask = spd[first + byteIndex];
                int bits = byteIndex == 3 ? 6 : 8;
                for (int bit = 0; bit < bits; bit++)
                {
                    if ((mask & (1 << bit)) != 0)
                        result.Add(baseCl + byteIndex * 8 + bit);
                }
            }
        }

        private static void DecodeModuleSpecific(byte[] spd, Ddr4SpdInfo info)
        {
            byte height = spd[SPD_MODULE_HEIGHT];
            int heightCode = height & 0x1F;
            info.ModuleHeight = heightCode == 0 ? "<= 15 mm" : heightCode == 31 ? "> 45 mm" : string.Format("<= {0} mm", 15 + heightCode);

            byte thickness = spd[SPD_MODULE_THICKNESS];
            info.ModuleThickness = string.Format("front <= {0} mm, back <= {1} mm", (thickness & 0x0F) + 1, ((thickness >> 4) & 0x0F) + 1);

            byte rawCard = spd[SPD_RAW_CARD];
            int card = rawCard & 0x1F;
            int revision = (rawCard >> 5) & 0x03;
            if (revision == 3)
                revision += (height >> 5) & 0x07;
            string cardName = card == 31 ? "ZZ" : card < 26 ? ((char)('A' + card)).ToString() : string.Format("0x{0:X2}", card);
            if ((rawCard & 0x80) != 0)
                cardName = string.Format("extended {0}", card + 32);
            info.ReferenceRawCard = string.Format("{0}, revision {1}", cardName, revision);

            if (IsRegistered(info.BaseModuleType))
            {
                info.AddressMirrored = (spd[SPD_RDIMM_ADDRESS_MAPPING] & 0x01) != 0;
                byte bank = spd[SPD_RDIMM_REGISTER_MFG];
                byte mfr = spd[SPD_RDIMM_REGISTER_MFG + 1];
                if (bank != 0 || mfr != 0)
                    info.RegisterManufacturer = ManufacturerMapping.Lookup(bank, mfr);
                info.RegisterRevision = spd[SPD_RDIMM_REGISTER_REV];
            }
            else
            {
                info.AddressMirrored = (spd[SPD_UDIMM_ADDRESS_MAPPING] & 0x01) != 0;
            }
        }

        private static void DecodeManufacturing(byte[] spd, Ddr4SpdInfo info)
        {
            info.ModuleMfgIdBank = spd[SPD_MOD_MFG_ID];
            info.ModuleMfgIdMfr = spd[SPD_MOD_MFG_ID + 1];
            info.ModuleManufacturer = ManufacturerMapping.Lookup(info.ModuleMfgIdBank, info.ModuleMfgIdMfr);
            info.ModuleMfgLocation = spd[SPD_MOD_MFG_LOCATION];

            info.ModuleMfgYear = DecodeBcd(spd[SPD_MOD_MFG_YEAR]);
            info.ModuleMfgWeek = DecodeBcd(spd[SPD_MOD_MFG_WEEK]);
            info.ModuleMfgDate = info.ModuleMfgYear == 0 && info.ModuleMfgWeek == 0
                ? "N/A"
                : string.Format("20{0:D2}, Week {1:D2}", info.ModuleMfgYear, info.ModuleMfgWeek);

            StringBuilder serial = new StringBuilder();
            for (int i = 0; i < 4; i++)
                serial.AppendFormat("{0:X2}", spd[SPD_MOD_SERIAL + i]);
            info.ModuleSerialNumber = serial.ToString();

            StringBuilder partNumber = new StringBuilder();
            for (int i = 0; i < SPD_MOD_PARTNO_LENGTH; i++)
            {
                byte c = spd[SPD_MOD_PARTNO + i];
                if (c >= 0x20 && c <= 0x7E)
                    partNumber.Append((char)c);
            }
            info.ModulePartNumber = partNumber.ToString().Trim();
            info.ModuleRevisionCode = spd[SPD_MOD_REV];

            info.DramMfgIdBank = spd[SPD_DRAM_MFG_ID];
            info.DramMfgIdMfr = spd[SPD_DRAM_MFG_ID + 1];
            info.DramManufacturer = ManufacturerMapping.Lookup(info.DramMfgIdBank, info.DramMfgIdMfr);
            info.DramStepping = spd[SPD_DRAM_STEPPING];
        }

        private static void DecodeXmp(byte[] spd, Ddr4SpdInfo info)
        {
            if (spd[XMP_ID0] != 0x0C || spd[XMP_ID1] != 0x4A)
                return;

            byte revision = spd[XMP_REVISION];
            info.XmpRevision = string.Format("{0}.{1}", revision >> 4, revision & 0x0F);

            byte enabled = spd[XMP_PROFILES_ENABLED];
            for (int p = 0; p < info.XmpProfiles.Length; p++)
            {
                if ((enabled & (1 << p)) == 0)
                    continue;

                Ddr4XmpProfile profile = DecodeXmpProfile(spd, XMP_PROFILE_FIRST + p * XMP_PROFILE_SIZE);
                if (profile == null)
                    continue;

                profile.ProfileNumber = p + 1;
                profile.DimmsPerChannel = ((enabled >> (2 + p * 2)) & 0x03) + 1;
                info.XmpProfiles[p] = profile;
                info.HasXmp = true;
            }
        }

        // XMP 2.0 profile: the JEDEC timing bytes in the same order, fine corrections at the end
        private static Ddr4XmpProfile DecodeXmpProfile(byte[] spd, int o)
        {
            int tCK = Timing(spd, o + 3, o + 38);
            if (tCK <= 0)
                return null;

            byte vdd = spd[o];
            Ddr4XmpProfile p = new Ddr4XmpProfile
            {
                VddMv = ((vdd >> 7) & 0x01) * 1000 + (vdd & 0x7F) * 10,
                tCKAVGminPs = tCK,
                SupportedCLs = new List<int>(),
                tAAminPs = Timing(spd, o + 8, o + 37),
                tRCDminPs = Timing(spd, o + 9, o + 36),
                tRPminPs = Timing(spd, o + 10, o + 35),
                tRASminPs = (((spd[o + 11] & 0x0F) << 8) | spd[o + 12]) * MTB_PS,
                tRCminPs = ((((spd[o + 11] >> 4) & 0x0F) << 8) | spd[o + 13]) * MTB_PS + Fine(spd, o + 34),
                tRFC1minPs = U16(spd, o + 14) * MTB_PS,
                tRFC2minPs = U16(spd, o + 16) * MTB_PS,
                tRFC4minPs = U16(spd, o + 18) * MTB_PS,
                tFAWminPs = (((spd[o + 20] & 0x0F) << 8) | spd[o + 21]) * MTB_PS,
                tRRD_SminPs = Timing(spd, o + 22, o + 33),
                tRRD_LminPs = Timing(spd, o + 23, o + 32),
                tCCD_LminPs = Timing(spd, o + 24, o + 31),
            };

            DecodeCasLatencies(spd, o + 4, p.SupportedCLs);

            double exactMTs = SpeedFromTck(tCK);
            double tCKideal = 2000000.0 / exactMTs;
            p.SpeedMTs = (int)exactMTs;
            p.ClockMHz = exactMTs / 2.0;
            p.SpeedGrade = "DDR4-" + p.SpeedMTs;

            p.CL = Clocks(p.tAAminPs, tCKideal);
            p.tRCD = Clocks(p.tRCDminPs, tCKideal);
            p.tRP = Clocks(p.tRPminPs, tCKideal);
            p.tRAS = Clocks(p.tRASminPs, tCKideal);
            p.tRC = Clocks(p.tRCminPs, tCKideal);
            p.tRFC1 = Clocks(p.tRFC1minPs, tCKideal);
            p.tRFC2 = Clocks(p.tRFC2minPs, tCKideal);
            p.tRFC4 = Clocks(p.tRFC4minPs, tCKideal);
            p.tFAW = Clocks(p.tFAWminPs, tCKideal);
            p.tRRD_S = Clocks(p.tRRD_SminPs, tCKideal);
            p.tRRD_L = Clocks(p.tRRD_LminPs, tCKideal);
            p.tCCD_L = Clocks(p.tCCD_LminPs, tCKideal);
            p.TimingString = string.Format("{0}-{1}-{2}-{3} @ {4}", p.CL, p.tRCD, p.tRP, p.tRAS, p.SpeedGrade);

            p.IsValid = true;
            return p;
        }

        /// <summary>A timing in medium timebase units with its signed fine correction, in ps.</summary>
        private static int Timing(byte[] spd, int mtbOffset, int fineOffset)
        {
            int mtb = spd[mtbOffset];
            if (mtb == 0)
                return 0;

            return mtb * MTB_PS + Fine(spd, fineOffset);
        }

        private static int Fine(byte[] spd, int offset)
        {
            return unchecked((sbyte)spd[offset]);
        }

        private static int U16(byte[] spd, int offset)
        {
            return spd[offset] | (spd[offset + 1] << 8);
        }

        private static int DecodeBcd(byte b)
        {
            return ((b >> 4) & 0x0F) * 10 + (b & 0x0F);
        }

        /// <summary>The CRC-16 of the SPD (polynomial 0x1021, initial value 0).</summary>
        internal static int Crc16(byte[] data, int offset, int count)
        {
            int crc = 0;
            for (int i = offset; i < offset + count; i++)
            {
                crc ^= data[i] << 8;
                for (int bit = 0; bit < 8; bit++)
                    crc = ((crc & 0x8000) != 0 ? (crc << 1) ^ 0x1021 : crc << 1) & 0xFFFF;
            }
            return crc;
        }
    }
}
