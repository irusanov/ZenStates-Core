using System;
using System.Collections.Generic;
using System.Text;
using ZenStates.Core.DRAM;
using ZenStates.Core.Drivers;

namespace ZenStates.Core
{
    internal static class Ddr5SpdDecoder
    {
        // SPD byte map (JESD400-5C, verified against hardware)
        //
        // Block 0  (bytes   0- 63): Base Configuration & DRAM Parameters
        // Block 1  (bytes  64-127): Additional DRAM Timing Parameters
        // Block 2  (bytes 128-191): Reserved
        // Block 3  (bytes 192-255): Common + Module-Type Specific Parameters
        // Block 4-5(bytes 256-511): Reserved
        // Block 6-7(bytes 512-639): Manufacturing Information
        // Block 8-9(bytes 640-767): End User / XMP 3.0 Profiles
        // Block 10+(bytes 768-1023): Reserved / EXPO Profiles

        // General (Block 0)
        private const int SPD_BYTES_USED = 0;     // [3:0] beta level, [6:4] SPD bytes total
        private const int SPD_REVISION = 1;     // [7:4]=encoding, [3:0]=additions
        private const int SPD_DEVICE_TYPE = 2;     // 0x12 = DDR5
        private const int SPD_MODULE_TYPE = 3;     // [3:0]=base, [4]=hybrid, [6:5]=hybrid_type

        // SDRAM Density, Addressing, I/O Width, Banks
        // Bytes 4-7 = first SDRAM, bytes 8-11 = second (asymmetric only)
        private const int SPD_FIRST_DENSITY = 4;     // [4:0]=density, [7:5]=die_per_pkg
        private const int SPD_FIRST_ADDRESSING = 5;     // [2:0]=col_bits-10, [7:5]=row_bits-16
        private const int SPD_FIRST_IO_WIDTH = 6;     // [7:5]=device_width (0=x4,1=x8,2=x16)
        private const int SPD_FIRST_BANKS = 7;     // [7:5]=bank_groups, [2:0]=banks_per_group
        private const int SPD_SECOND_DENSITY = 8;     // same format as byte 4
        private const int SPD_SECOND_ADDRESSING = 9;     // same format as byte 5
        private const int SPD_SECOND_IO_WIDTH = 10;    // same format as byte 6
        private const int SPD_SECOND_BANKS = 11;    // same format as byte 7

        // Voltage & Thermal
        private const int SPD_NOMINAL_VOLTAGE = 12;
        private const int SPD_THERMAL = 14;

        // Timing (16-bit LE values, picoseconds)
        // Verified offsets from real DDR5 hardware dumps.
        private const int SPD_TCKAVG_MIN_LSB = 20;
        private const int SPD_TCKAVG_MIN_MSB = 21;
        private const int SPD_TCKAVG_MAX_LSB = 22;
        private const int SPD_TCKAVG_MAX_MSB = 23;

        // CAS Latencies Supported: 5 bytes (40 bits), each bit = CL 20 + pos*2
        private const int SPD_CAS_FIRST = 24;    // bytes 24..28

        // Core timing (16-bit LE picoseconds)
        private const int SPD_TAA_MIN_LSB = 30;
        private const int SPD_TAA_MIN_MSB = 31;
        private const int SPD_TRCD_MIN_LSB = 32;
        private const int SPD_TRCD_MIN_MSB = 33;
        private const int SPD_TRP_MIN_LSB = 34;
        private const int SPD_TRP_MIN_MSB = 35;
        private const int SPD_TRAS_MIN_LSB = 36;
        private const int SPD_TRAS_MIN_MSB = 37;
        private const int SPD_TRC_MIN_LSB = 38;
        private const int SPD_TRC_MIN_MSB = 39;
        private const int SPD_TWR_MIN_LSB = 40;
        private const int SPD_TWR_MIN_MSB = 41;

        // Refresh timing (16-bit LE, nanoseconds)
        private const int SPD_TRFC1_LSB = 42;
        private const int SPD_TRFC1_MSB = 43;
        private const int SPD_TRFC2_LSB = 44;
        private const int SPD_TRFC2_MSB = 45;
        private const int SPD_TRFCSB_LSB = 46;
        private const int SPD_TRFCSB_MSB = 47;


        // LPDDR5/5X timing fields
        // Minimum and maximum cycle time are located at bytes 18, 19, 124, and 125.
        // The remaining base timing fields are not described in the uploaded DDR5 documents,
        // so LPDDR5 decoding falls back to the shared DDR5-style positions when present.
        private const int LPDDR5_TCKAVG_MIN = 18;
        private const int LPDDR5_TCKAVG_MAX = 19;
        private const int LPDDR5_TCKAVG_MAX_FINE = 124;
        private const int LPDDR5_TCKAVG_MIN_FINE = 125;

        // Common device information bytes
        private const int SPD_COMMON_DEVICE_REV = 192;
        private const int SPD_COMMON_SPD_MFG_ID_LSB = 194;
        private const int SPD_COMMON_SPD_MFG_ID_MSB = 195;
        private const int SPD_COMMON_SPD_DEVICE_TYPE = 196;
        private const int SPD_COMMON_PMIC0_MFG_ID_LSB = 198;
        private const int SPD_COMMON_PMIC0_MFG_ID_MSB = 199;
        private const int SPD_COMMON_PMIC0_DEVICE_TYPE = 200;
        private const int SPD_COMMON_PMIC1_MFG_ID_LSB = 202;
        private const int SPD_COMMON_PMIC1_MFG_ID_MSB = 203;
        private const int SPD_COMMON_PMIC1_DEVICE_TYPE = 204;
        private const int SPD_COMMON_PMIC2_MFG_ID_LSB = 206;
        private const int SPD_COMMON_PMIC2_MFG_ID_MSB = 207;
        private const int SPD_COMMON_PMIC2_DEVICE_TYPE = 208;
        private const int SPD_COMMON_TS_MFG_ID_LSB = 210;
        private const int SPD_COMMON_TS_MFG_ID_MSB = 211;
        private const int SPD_COMMON_TS_DEVICE_TYPE = 212;

        // Module organisation (Block 3)
        // byte 234: [5:3]=ranks_per_channel-1
        // byte 235: [2:0]=primary bus width per sub-ch, [5]=sub-channels (0=1, 1=2)
        private const int SPD_MOD_ORG = 234;
        private const int SPD_BUS_WIDTH = 235;

        // Manufacturing (Block 6-7, starting at byte 512)
        private const int SPD_MOD_MFG_ID_LSB = 512;
        private const int SPD_MOD_MFG_ID_MSB = 513;
        private const int SPD_MOD_MFG_LOC = 514;
        private const int SPD_MOD_MFG_YEAR = 515;
        private const int SPD_MOD_MFG_WEEK = 516;
        private const int SPD_MOD_SERIAL = 517;   // 4 bytes (517-520)
        private const int SPD_MOD_PARTNO = 521;   // 30 bytes (521-550)
        private const int SPD_MOD_REV = 551;
        private const int SPD_DRAM_MFG_ID_LSB = 552;
        private const int SPD_DRAM_MFG_ID_MSB = 553;
        private const int SPD_DRAM_STEPPING = 554;

        // XMP 3.0 header (Block 8, byte 640)
        // Intel Extreme Memory Profile 3.0 for DDR5.
        // 3 vendor (read-only) + 2 user (writable) profiles.
        // Bytes 640-767: header + 3 vendor profiles.
        // Bytes 768-895: 2 user profiles + profile names.

        private const int SPD_XMP_HEADER = 640;   // 0x0C 0x4A magic
        private const int SPD_XMP_REVISION = 642;   // [7:4]=encoding, [3:0]=additions
        private const int SPD_XMP_PROF_ENABLE = 643;   // [0]=P1 [1]=P2 [2]=P3 [3]=P4 [4]=P5
        private const int SPD_XMP_DMB_CONFIG = 644;   // Dynamic Memory Boost config

        // Vendor Profile 1: bytes 650-685 (36 bytes)
        private const int SPD_XMP_P1_BASE = 650;
        // Vendor Profile 2: bytes 686-721
        private const int SPD_XMP_P2_BASE = 686;
        // Vendor Profile 3: bytes 722-757
        private const int SPD_XMP_P3_BASE = 722;

        // User Profile 4: bytes 768-803
        private const int SPD_XMP_P4_BASE = 768;
        // User Profile 5: bytes 804-839 (not overlapping EXPO)
        private const int SPD_XMP_P5_BASE = 804;

        // Profile name strings (15 chars each) at bytes 758-767 + 840-895
        // Name 1: 758-767 (partial, continued from overflow)

        // XMP 3.0 per-profile offsets (relative to profile base)
        // Each profile has identical layout, 36 bytes:
        private const int XMP_OFF_VDD = 0;    // VDD voltage code  (code*5 + 1100 mV)
        private const int XMP_OFF_VDDQ = 1;    // VDDQ voltage code (code*5 + 1100 mV)
        private const int XMP_OFF_VPP = 2;    // VPP voltage code  (code*5 + 1500 mV)
        private const int XMP_OFF_FLAGS = 3;    // DMB + misc flags
        private const int XMP_OFF_TCK_LSB = 4;    // tCKAVGmin LE ps
        private const int XMP_OFF_TCK_MSB = 5;
        private const int XMP_OFF_CAS_0 = 6;    // CAS latency bytes (5 bytes)
        private const int XMP_OFF_CAS_4 = 10;
        private const int XMP_OFF_TAA_LSB = 11;    // tAAmin LE ps
        private const int XMP_OFF_TAA_MSB = 12;
        private const int XMP_OFF_TRCD_LSB = 13;
        private const int XMP_OFF_TRCD_MSB = 14;
        private const int XMP_OFF_TRP_LSB = 15;
        private const int XMP_OFF_TRP_MSB = 16;
        private const int XMP_OFF_TRAS_LSB = 17;
        private const int XMP_OFF_TRAS_MSB = 18;
        private const int XMP_OFF_TRC_LSB = 19;
        private const int XMP_OFF_TRC_MSB = 20;
        private const int XMP_OFF_TWR_LSB = 21;
        private const int XMP_OFF_TWR_MSB = 22;
        private const int XMP_OFF_TRFC1_LSB = 23;    // tRFC1 LE ns
        private const int XMP_OFF_TRFC1_MSB = 24;
        private const int XMP_OFF_TRFC2_LSB = 25;    // tRFC2 LE ns
        private const int XMP_OFF_TRFC2_MSB = 26;
        private const int XMP_OFF_TRFCSB_LSB = 27;    // tRFCsb LE ns
        private const int XMP_OFF_TRFCSB_MSB = 28;
        // Bytes 29-35: extended timings (tRRD_S, tRRD_L, tCCD_L, etc.)

        // EXPO header (Block 10, byte 832 = 0x340)
        // Magic: ASCII "EXPO" (0x45 0x58 0x50 0x4F)
        // Verified from real AMD EXPO DDR5 DIMM dumps.
        private const int SPD_EXPO_HEADER = 832;
        private const int SPD_EXPO_REVISION = 836;
        private const int SPD_EXPO_PROFILES = 837;   // [0]=profile1, [1]=profile2
        private const int SPD_EXPO_VDD1 = 842;   // VDD voltage code
        private const int SPD_EXPO_VDDQ1 = 843;   // VDDQ voltage code
        private const int SPD_EXPO_TCKMIN1_LSB = 846;   // Profile 1 tCKAVGmin
        private const int SPD_EXPO_TCKMIN1_MSB = 847;
        private const int SPD_EXPO_TAA1_LSB = 848;   // Profile 1 tAAmin
        private const int SPD_EXPO_TAA1_MSB = 849;
        private const int SPD_EXPO_TRCD1_LSB = 850;
        private const int SPD_EXPO_TRCD1_MSB = 851;
        private const int SPD_EXPO_TRP1_LSB = 852;
        private const int SPD_EXPO_TRP1_MSB = 853;
        private const int SPD_EXPO_TRAS1_LSB = 854;
        private const int SPD_EXPO_TRAS1_MSB = 855;
        private const int SPD_EXPO_TRC1_LSB = 856;
        private const int SPD_EXPO_TRC1_MSB = 857;
        private const int SPD_EXPO_TWR1_LSB = 858;
        private const int SPD_EXPO_TWR1_MSB = 859;
        private const int SPD_EXPO_TRFC1_1_LSB = 860;   // tRFC1 (ns)
        private const int SPD_EXPO_TRFC1_1_MSB = 861;
        private const int SPD_EXPO_TRFC2_1_LSB = 862;
        private const int SPD_EXPO_TRFC2_1_MSB = 863;
        private const int SPD_EXPO_TRFCSB1_LSB = 864;
        private const int SPD_EXPO_TRFCSB1_MSB = 865;

        // Device type constant
        private const byte DDR5_DEVICE_TYPE = 0x12;
        private const byte LPDDR5_DEVICE_TYPE = 0x13;


        public static Ddr5SpdInfo Decode(List<byte> spd)
        {
            byte[] raw;
            if (spd != null)
                raw = spd.ToArray();
            else
                raw = new byte[0];
            return Decode(raw);
        }

        public static Ddr5SpdInfo Decode(byte[] spd)
        {
            Ddr5SpdInfo info = new Ddr5SpdInfo();
            info.SupportedCLs = new List<int>();
            info.RawSpd = spd;

            if (spd == null || spd.Length < 128)
            {
                info.IsValid = false;
                info.DeviceTypeString = "Insufficient data";
                return info;
            }

            info.DeviceType = B(spd, SPD_DEVICE_TYPE);
            info.IsValid = (info.DeviceType == DDR5_DEVICE_TYPE || info.DeviceType == LPDDR5_DEVICE_TYPE);

            if (!info.IsValid)
            {
                info.DeviceTypeString = string.Format("Unknown (0x{0:X2})", info.DeviceType);
                info.MemoryFamily = "Unknown";
                return info;
            }

            info.IsLpddr5 = (info.DeviceType == LPDDR5_DEVICE_TYPE);
            if (info.IsLpddr5)
            {
                info.DeviceTypeString = "LPDDR5 SDRAM";
                info.MemoryFamily = "LPDDR5";
            }
            else
            {
                info.DeviceTypeString = "DDR5 SDRAM";
                info.MemoryFamily = "DDR5";
            }

            info.IsPartial = false;

            DecodeSizeAndRevision(spd, info);
            DecodeModuleType(spd, info);
            DecodeSupportDevices(spd, info);
            DecodeDensityAndPackage(spd, info);
            DecodeAddressing(spd, info);
            DecodeVoltageAndThermal(spd, info);
            DecodeTiming(spd, info);
            DecodeModuleOrganisation(spd, info);
            DecodeDeviceWidth(spd, info);
            CalculateCapacity(info);
            DecodeManufacturing(spd, info);
            DetectAndDecodeProfiles(spd, info);
            CalculateTimingString(info);

            return info;
        }

        private static void DecodeSizeAndRevision(byte[] spd, Ddr5SpdInfo info)
        {
            byte b0 = B(spd, SPD_BYTES_USED);
            info.BytesTotal = DecodeSpdSize((b0 >> 4) & 0x07);

            byte b1 = B(spd, SPD_REVISION);
            info.SpdRevisionEncoding = (b1 >> 4) & 0x0F;
            info.SpdRevisionAdditions = b1 & 0x0F;
            info.SpdRevision = string.Format("{0}.{1}", info.SpdRevisionEncoding, info.SpdRevisionAdditions);
        }

        private static int DecodeSpdSize(int code)
        {
            switch (code)
            {
                case 0b001:
                    return 256;
                case 0b010:
                    return 512;
                case 0b011:
                    return 1024;
                default:
                    return 0;
            }
        }

        private static void DecodeModuleType(byte[] spd, Ddr5SpdInfo info)
        {
            byte b = B(spd, SPD_MODULE_TYPE);
            info.BaseModuleType = (byte)(b & 0x0F);
            info.IsHybrid = ((b >> 4) & 0x01) != 0;
            int hybridType = (b >> 5) & 0x03;

            switch (info.BaseModuleType)
            {
                case 0x01: info.ModuleTypeString = "RDIMM"; break;
                case 0x02: info.ModuleTypeString = "UDIMM"; break;
                case 0x03: info.ModuleTypeString = "SO-DIMM"; break;
                case 0x04: info.ModuleTypeString = "LRDIMM"; break;
                case 0x05: info.ModuleTypeString = "CUDIMM"; break;
                case 0x06: info.ModuleTypeString = "CSODIMM"; break;
                case 0x07: info.ModuleTypeString = "MRDIMM"; break;
                case 0x08: info.ModuleTypeString = "CAMM2"; break;
                case 0x0A: info.ModuleTypeString = "DDIMM"; break;
                case 0x0B: info.ModuleTypeString = "Solder-down"; break;
                default:
                    info.ModuleTypeString = string.Format("Unknown (0x{0:X2})", info.BaseModuleType);
                    break;
            }

            if (info.IsHybrid)
            {
                switch (hybridType)
                {
                    case 0x01: info.HybridTypeString = "NVDIMM-N Hybrid"; break;
                    case 0x02: info.HybridTypeString = "NVDIMM-P Hybrid"; break;
                    default:
                        info.HybridTypeString = string.Format("Unknown Hybrid ({0})", hybridType);
                        break;
                }
            }
            else
            {
                info.HybridTypeString = "Not hybrid";
            }
        }

        private static void DecodeSupportDevices(byte[] spd, Ddr5SpdInfo info)
        {
            byte spdType = B(spd, SPD_COMMON_SPD_DEVICE_TYPE);
            byte pmic0Type = B(spd, SPD_COMMON_PMIC0_DEVICE_TYPE);
            byte pmic1Type = B(spd, SPD_COMMON_PMIC1_DEVICE_TYPE);
            byte pmic2Type = B(spd, SPD_COMMON_PMIC2_DEVICE_TYPE);
            byte tsType = B(spd, SPD_COMMON_TS_DEVICE_TYPE);

            info.SpdDevicePresent = ((spdType & 0x80) != 0);
            info.SpdDeviceTypeString = DecodeSupportDeviceType(spdType, true);

            info.Pmic0Present = ((pmic0Type & 0x80) != 0);
            info.Pmic0TypeString = DecodeSupportDeviceType(pmic0Type, false);

            info.Pmic1Present = ((pmic1Type & 0x80) != 0);
            info.Pmic1TypeString = DecodeSupportDeviceType(pmic1Type, false);

            info.Pmic2Present = ((pmic2Type & 0x80) != 0);
            info.Pmic2TypeString = DecodeSupportDeviceType(pmic2Type, false);

            info.ThermalSensor0Present = ((tsType & 0x80) != 0);
            info.ThermalSensor1Present = ((tsType & 0x40) != 0);
        }

        private static string DecodeSupportDeviceType(byte value, bool isSpdDevice)
        {
            int typeCode = value & 0x0F;
            if (isSpdDevice)
            {
                switch (typeCode)
                {
                    case 0x00: return "SPD5118";
                    case 0x01: return "ESPD5216";
                    default: return string.Format("Unknown SPD device (0x{0:X2})", typeCode);
                }
            }

            switch (typeCode)
            {
                case 0x00: return "PMIC5000";
                case 0x01: return "PMIC5010";
                case 0x02: return "PMIC5100";
                case 0x03: return "PMIC5020";
                case 0x04: return "PMIC5120";
                case 0x05: return "PMIC5200";
                case 0x06: return "PMIC5030";
                default: return string.Format("Unknown PMIC (0x{0:X2})", typeCode);
            }
        }

        private static void DecodeDensityAndPackage(byte[] spd, Ddr5SpdInfo info)
        {
            byte bDen1 = B(spd, SPD_FIRST_DENSITY);   // byte 4
            byte bDen2 = B(spd, SPD_SECOND_DENSITY);   // byte 8

            // [4:0] = density code, [7:5] = die per package code
            info.FirstDieDensityMbit = DecodeDieDensity(bDen1 & 0x1F);
            info.FirstDieCount = DecodeDieCount((bDen1 >> 5) & 0x07);

            info.SecondDieDensityMbit = DecodeDieDensity(bDen2 & 0x1F);
            info.SecondDieCount = DecodeDieCount((bDen2 >> 5) & 0x07);

            info.IsAsymmetric = (bDen2 != 0) && (bDen1 != bDen2);
        }

        internal static int DecodeDieDensity(int code)
        {
            switch (code)
            {
                case 0x01: return 4096;     // 4 Gbit
                case 0x02: return 8192;     // 8 Gbit
                case 0x03: return 12288;    // 12 Gbit
                case 0x04: return 16384;    // 16 Gbit
                case 0x05: return 24576;    // 24 Gbit
                case 0x06: return 32768;    // 32 Gbit
                case 0x07: return 49152;    // 48 Gbit
                case 0x08: return 65536;    // 64 Gbit
                default: return 0;
            }
        }

        private static int DecodeDieCount(int code)
        {
            // JESD400-5B: Die per Package = code + 1  (0=1, 1=2, ..., 7=8)
            return code + 1;
        }

        private static void DecodeAddressing(byte[] spd, Ddr5SpdInfo info)
        {
            byte bAddr1 = B(spd, SPD_FIRST_ADDRESSING);   // byte 5
            byte bAddr2 = B(spd, SPD_SECOND_ADDRESSING);  // byte 9

            info.FirstColumnBits = 10 + (bAddr1 & 0x07);
            info.FirstRowBits = 16 + ((bAddr1 >> 5) & 0x07);

            info.SecondColumnBits = 10 + (bAddr2 & 0x07);
            info.SecondRowBits = 16 + ((bAddr2 >> 5) & 0x07);
        }

        private static void DecodeVoltageAndThermal(byte[] spd, Ddr5SpdInfo info)
        {
            byte bt = B(spd, SPD_THERMAL);

            if (info.IsLpddr5)
            {
                if (info.Pmic0Present || info.Pmic1Present || info.Pmic2Present)
                    info.VddString = "Platform-managed rails (PMIC listed in common support-device bytes)";
                else
                    info.VddString = "Platform-managed rails";

                info.HasThermalSensor = info.ThermalSensor0Present || info.ThermalSensor1Present;
            }
            else
            {
                info.VddString = "1.1 V (nominal DDR5)";
                info.HasThermalSensor = ((bt >> 3) & 0x01) != 0;
            }
        }

        private static void DecodeTiming(byte[] spd, Ddr5SpdInfo info)
        {
            if (info.IsLpddr5)
            {
                info.tCKAVGminPs = DecodeLpddr5TckMin(spd);
                info.tCKAVGmaxPs = DecodeLpddr5TckMax(spd);
            }
            else
            {
                info.tCKAVGminPs = U16LE(spd, SPD_TCKAVG_MIN_LSB);
                info.tCKAVGmaxPs = U16LE(spd, SPD_TCKAVG_MAX_LSB);
            }

            if (info.tCKAVGminPs > 0)
            {
                info.ClockMHz = 1000000.0 / (double)info.tCKAVGminPs;
                info.SpeedMTs = (int)Math.Round(2.0 * info.ClockMHz);
                if (!info.IsLpddr5)
                {
                    info.SpeedMTs = RoundToJedecBin(info.SpeedMTs);
                    info.ClockMHz = info.SpeedMTs / 2.0;
                }
            }

            if (info.IsLpddr5)
                info.SpeedGrade = string.Format("LPDDR5-{0}", info.SpeedMTs);
            else
                info.SpeedGrade = string.Format("DDR5-{0}", info.SpeedMTs);

            if (!info.IsLpddr5)
            {
                for (int byteIdx = 0; byteIdx < 5; byteIdx++)
                {
                    byte cb = B(spd, SPD_CAS_FIRST + byteIdx);
                    for (int bit = 0; bit < 8; bit++)
                    {
                        if ((cb & (1 << bit)) != 0)
                        {
                            int clValue = 20 + (byteIdx * 8 + bit) * 2;
                            info.SupportedCLs.Add(clValue);
                        }
                    }
                }
            }

            info.tAAminPs = U16LE(spd, SPD_TAA_MIN_LSB);
            info.tRCDminPs = U16LE(spd, SPD_TRCD_MIN_LSB);
            info.tRPminPs = U16LE(spd, SPD_TRP_MIN_LSB);
            info.tRASminPs = U16LE(spd, SPD_TRAS_MIN_LSB);
            info.tRCminPs = U16LE(spd, SPD_TRC_MIN_LSB);
            info.tWRminPs = U16LE(spd, SPD_TWR_MIN_LSB);

            info.tRFC1minNs = U16LE(spd, SPD_TRFC1_LSB);
            info.tRFC2minNs = U16LE(spd, SPD_TRFC2_LSB);
            info.tRFCsbMinNs = U16LE(spd, SPD_TRFCSB_LSB);
        }

        private static int DecodeLpddr5TckMin(byte[] spd)
        {
            int coarse = B(spd, LPDDR5_TCKAVG_MIN);
            int fine = (sbyte)B(spd, LPDDR5_TCKAVG_MIN_FINE);
            if (coarse <= 0)
                return 0;
            return coarse * 125 + fine;
        }

        private static int DecodeLpddr5TckMax(byte[] spd)
        {
            int coarse = B(spd, LPDDR5_TCKAVG_MAX);
            int fine = (sbyte)B(spd, LPDDR5_TCKAVG_MAX_FINE);
            if (coarse <= 0)
                return 0;
            return coarse * 125 + fine;
        }

        private static int RoundToJedecBin(int mts)
        {
            int[] bins = new int[] {
                    3200, 3600, 4000, 4400, 4800, 5200, 5600,
                    6000, 6400, 6800, 7200, 7600, 8000, 8400, 8800, 9200
                };
            int best = bins[0];
            int bestDelta = Math.Abs(mts - bins[0]);
            for (int i = 1; i < bins.Length; i++)
            {
                int delta = Math.Abs(mts - bins[i]);
                if (delta < bestDelta)
                {
                    bestDelta = delta;
                    best = bins[i];
                }
            }
            return best;
        }

        // Module organisation
        // Byte 234: [5:3] = ranks per channel - 1
        // Byte 235: [2:0] = primary bus width per sub-channel
        //           [5]   = sub-channels per DIMM (0=1, 1=2)
        private static void DecodeModuleOrganisation(byte[] spd, Ddr5SpdInfo info)
        {
            byte bOrg = B(spd, SPD_MOD_ORG);
            byte bBus = B(spd, SPD_BUS_WIDTH);

            // Ranks
            int rankCode = (bOrg >> 3) & 0x07;
            info.RanksPerChannel = rankCode + 1;

            // Sub-channels: bit 5 of byte 235
            int subChBit = (bBus >> 5) & 0x01;
            info.SubChannelsPerDimm = (subChBit == 1) ? 2 : 1;

            // Bus width per sub-channel: bits [2:0]
            int busCode = bBus & 0x07;
            switch (busCode)
            {
                case 0x00: info.PrimaryBusWidthBits = 8; break;
                case 0x01: info.PrimaryBusWidthBits = 16; break;
                case 0x02: info.PrimaryBusWidthBits = 32; break;
                case 0x03: info.PrimaryBusWidthBits = 64; break;
                default: info.PrimaryBusWidthBits = 32; break;
            }

            info.ChannelCount = info.SubChannelsPerDimm;
        }

        // Device width
        // Byte 6: [7:5] = device width (0=x4, 1=x8, 2=x16, 3=x32)
        private static void DecodeDeviceWidth(byte[] spd, Ddr5SpdInfo info)
        {
            byte b6 = B(spd, SPD_FIRST_IO_WIDTH);
            int widthCode = (b6 >> 5) & 0x07;

            switch (widthCode)
            {
                case 0: info.FirstDeviceWidthBits = 4; break;
                case 1: info.FirstDeviceWidthBits = 8; break;
                case 2: info.FirstDeviceWidthBits = 16; break;
                case 3: info.FirstDeviceWidthBits = 32; break;
                default:
                    // Fallback: standard x8 for UDIMM, x4 for RDIMM/LRDIMM
                    info.FirstDeviceWidthBits = (info.BaseModuleType == 0x01 ||
                        info.BaseModuleType == 0x04) ? 4 : 8;
                    break;
            }
        }

        private static void CalculateCapacity(Ddr5SpdInfo info)
        {
            if (info.FirstDieDensityMbit == 0 || info.FirstDeviceWidthBits == 0 ||
                info.PrimaryBusWidthBits == 0)
            {
                info.TotalCapacityMB = 0;
                return;
            }

            // capacity_MB = (density_Mbit / 8) * die_count *
            //               (bus_width_per_subchannel / device_width) *
            //               ranks * sub_channels
            long densityMB = (long)info.FirstDieDensityMbit / 8;
            int devicesPerSubCh = info.PrimaryBusWidthBits / info.FirstDeviceWidthBits;

            info.TotalCapacityMB = densityMB
                * info.FirstDieCount
                * devicesPerSubCh
                * info.RanksPerChannel
                * info.SubChannelsPerDimm;
        }

        private static void CalculateTimingString(Ddr5SpdInfo info)
        {
            if (info.tCKAVGminPs > 0 && info.SpeedMTs > 0 && info.tAAminPs > 0)
            {
                double tCKideal = 1000000.0 / (info.SpeedMTs / 2.0);
                info.CL = (int)Math.Ceiling((double)info.tAAminPs / tCKideal - 0.01);
                info.tRCD = (int)Math.Ceiling((double)info.tRCDminPs / tCKideal - 0.01);
                info.tRP = (int)Math.Ceiling((double)info.tRPminPs / tCKideal - 0.01);
            }

            if (info.CL > 0)
            {
                info.TimingString = string.Format("{0}-{1}-{2} @ {3}",
                    info.CL, info.tRCD, info.tRP, info.SpeedGrade);
            }
            else
            {
                info.TimingString = info.SpeedGrade;
            }
        }
        private static void DecodeManufacturing(byte[] spd, Ddr5SpdInfo info)
        {
            if (spd.Length < 555)
            {
                info.ModuleManufacturer = "N/A (SPD too short)";
                info.DramManufacturer = "N/A";
                info.ModulePartNumber = "N/A";
                info.ModuleSerialNumber = "N/A";
                info.ModuleMfgDate = "N/A";
                return;
            }

            // Module manufacturer: JEDEC continuation bank + manufacturer code
            info.ModuleMfgIdBank = B(spd, SPD_MOD_MFG_ID_LSB);
            info.ModuleMfgIdMfr = B(spd, SPD_MOD_MFG_ID_MSB);
            info.ModuleManufacturer = ManufacturerMapping.Lookup(info.ModuleMfgIdBank, info.ModuleMfgIdMfr);

            // Manufacturing date (BCD year + week)
            info.ModuleMfgYear = DecodeBcd(B(spd, SPD_MOD_MFG_YEAR));
            info.ModuleMfgWeek = DecodeBcd(B(spd, SPD_MOD_MFG_WEEK));
            info.ModuleMfgDate = string.Format("20{0:D2}, Week {1:D2}", info.ModuleMfgYear, info.ModuleMfgWeek);

            // Module serial number (4 bytes hex)
            StringBuilder serial = new StringBuilder();
            for (int i = 0; i < 4; i++)
                serial.AppendFormat("{0:X2}", B(spd, SPD_MOD_SERIAL + i));
            info.ModuleSerialNumber = serial.ToString();

            // Module part number (30 bytes printable ASCII)
            StringBuilder partno = new StringBuilder();
            for (int i = 0; i < 30; i++)
            {
                byte c = B(spd, SPD_MOD_PARTNO + i);
                if (c >= 0x20 && c <= 0x7E)
                    partno.Append((char)c);
            }
            info.ModulePartNumber = partno.ToString().Trim();

            info.ModuleRevisionCode = B(spd, SPD_MOD_REV);

            // DRAM manufacturer
            info.DramMfgIdBank = B(spd, SPD_DRAM_MFG_ID_LSB);
            info.DramMfgIdMfr = B(spd, SPD_DRAM_MFG_ID_MSB);
            info.DramManufacturer = ManufacturerMapping.Lookup(info.DramMfgIdBank, info.DramMfgIdMfr);
            info.DramStepping = B(spd, SPD_DRAM_STEPPING);
        }

        private static void DetectAndDecodeProfiles(byte[] spd, Ddr5SpdInfo info)
        {
            if (info.IsLpddr5)
            {
                info.HasExpo = false;
                info.ExpoProfile1 = new Ddr5ExpoProfile();
                info.ExpoProfile2 = new Ddr5ExpoProfile();
                info.HasXmp = false;
                info.XmpProfiles = new Ddr5XmpProfile[5];
                for (int p = 0; p < 5; p++)
                    info.XmpProfiles[p] = new Ddr5XmpProfile();
                return;
            }

            // ── EXPO: ASCII "EXPO" (0x45 0x58 0x50 0x4F) at byte 832 ──
            if (spd.Length > SPD_EXPO_HEADER + 4)
            {
                info.HasExpo = (B(spd, SPD_EXPO_HEADER) == 0x45      // 'E'
                             && B(spd, SPD_EXPO_HEADER + 1) == 0x58   // 'X'
                             && B(spd, SPD_EXPO_HEADER + 2) == 0x50   // 'P'
                             && B(spd, SPD_EXPO_HEADER + 3) == 0x4F); // 'O'
            }

            if (info.HasExpo)
            {
                byte profileBits = B(spd, SPD_EXPO_PROFILES);
                info.ExpoProfile1 = DecodeExpoProfile(spd, 1, SPD_EXPO_TCKMIN1_LSB, SPD_EXPO_VDD1, SPD_EXPO_VDDQ1);
                info.ExpoProfile1.IsValid = (profileBits & 0x01) != 0 && info.ExpoProfile1.tCKAVGminPs > 0;

                // Profile 2 is at a fixed offset after profile 1 (typically +32 bytes)
                // but many DIMMs only have 1 profile. Check if profile 2 bit is set.
                info.ExpoProfile2 = new Ddr5ExpoProfile();
                info.ExpoProfile2.IsValid = false;
                if ((profileBits & 0x02) != 0 && spd.Length > SPD_EXPO_TCKMIN1_LSB + 32)
                {
                    // Profile 2 starts 32 bytes after profile 1 timing block
                    info.ExpoProfile2 = DecodeExpoProfile(spd, 2,
                        SPD_EXPO_TCKMIN1_LSB + 32,
                        SPD_EXPO_VDD1 + 2,     // VDD2 at +2
                        SPD_EXPO_VDDQ1 + 2);   // VDDQ2 at +2
                    info.ExpoProfile2.IsValid = info.ExpoProfile2.tCKAVGminPs > 0;
                }
            }
            else
            {
                info.ExpoProfile1 = new Ddr5ExpoProfile();
                info.ExpoProfile2 = new Ddr5ExpoProfile();
            }

            // XMP 3.0: magic header at byte 640
            if (spd.Length > SPD_XMP_HEADER + 2)
            {
                byte xm0 = B(spd, SPD_XMP_HEADER);
                byte xm1 = B(spd, SPD_XMP_HEADER + 1);
                info.HasXmp = (xm0 == 0x0C && xm1 == 0x4A);
            }

            if (info.HasXmp)
            {
                byte rev = B(spd, SPD_XMP_REVISION);
                info.XmpRevision = string.Format("{0}.{1}", (rev >> 4) & 0x0F, rev & 0x0F);

                byte enableBits = B(spd, SPD_XMP_PROF_ENABLE);
                byte dmbByte = B(spd, SPD_XMP_DMB_CONFIG);

                info.XmpProfiles = new Ddr5XmpProfile[5];

                // Vendor profiles 1-3
                int[] bases = new int[] {
                        SPD_XMP_P1_BASE, SPD_XMP_P2_BASE, SPD_XMP_P3_BASE,
                        SPD_XMP_P4_BASE, SPD_XMP_P5_BASE
                    };

                for (int p = 0; p < 5; p++)
                {
                    bool enabled = ((enableBits >> p) & 1) != 0;
                    if (enabled && spd.Length > bases[p] + 29)
                    {
                        info.XmpProfiles[p] = DecodeXmpProfile(spd, p + 1, bases[p]);
                        // Dynamic Memory Boost flag from header
                        info.XmpProfiles[p].DynamicMemoryBoost = ((dmbByte >> p) & 1) != 0;
                    }
                    else
                    {
                        info.XmpProfiles[p] = new Ddr5XmpProfile();
                    }
                }
            }
            else
            {
                info.XmpProfiles = new Ddr5XmpProfile[5];
                for (int p = 0; p < 5; p++)
                    info.XmpProfiles[p] = new Ddr5XmpProfile();
            }
        }

        private static Ddr5ExpoProfile DecodeExpoProfile(byte[] spd,
            int profileNum, int tckOffset, int vddOffset, int vddqOffset)
        {
            Ddr5ExpoProfile p = new Ddr5ExpoProfile();
            p.ProfileNumber = profileNum;

            p.tCKAVGminPs = U16LE(spd, tckOffset);
            if (p.tCKAVGminPs <= 0)
            {
                p.IsValid = false;
                return p;
            }

            p.ClockMHz = 1000000.0 / (double)p.tCKAVGminPs;
            p.SpeedMTs = (int)Math.Round(2.0 * p.ClockMHz);
            p.SpeedMTs = RoundToJedecBin(p.SpeedMTs);
            p.ClockMHz = p.SpeedMTs / 2.0;
            p.SpeedGrade = string.Format("DDR5-{0}", p.SpeedMTs);

            // Timing parameters (16-bit LE picoseconds, consecutive)
            int t = tckOffset + 2; // tAA follows tCK
            p.tAAminPs = U16LE(spd, t); t += 2;
            p.tRCDminPs = U16LE(spd, t); t += 2;
            p.tRPminPs = U16LE(spd, t); t += 2;
            p.tRASminPs = U16LE(spd, t); t += 2;
            p.tRCminPs = U16LE(spd, t); t += 2;
            p.tWRminPs = U16LE(spd, t); t += 2;

            // tRFC in nanoseconds
            p.tRFC1minNs = U16LE(spd, t); t += 2;
            p.tRFC2minNs = U16LE(spd, t); t += 2;
            p.tRFCsbMinNs = U16LE(spd, t);

            // CL calculation with ideal tCK to avoid integer rounding artifacts
            double tCKideal = 1000000.0 / (p.SpeedMTs / 2.0);
            p.CL = (int)Math.Ceiling((double)p.tAAminPs / tCKideal - 0.01);
            p.tRCD = (int)Math.Ceiling((double)p.tRCDminPs / tCKideal - 0.01);
            p.tRP = (int)Math.Ceiling((double)p.tRPminPs / tCKideal - 0.01);
            p.TimingString = string.Format("{0}-{1}-{2} @ {3}",
                p.CL, p.tRCD, p.tRP, p.SpeedGrade);

            // Voltage: code * 5mV + 1100mV base
            p.VddCode = B(spd, vddOffset);
            p.VddqCode = B(spd, vddqOffset);
            p.VddMv = 1100 + p.VddCode * 5;
            p.VddqMv = 1100 + p.VddqCode * 5;

            p.IsValid = true;
            return p;
        }

        private static Ddr5XmpProfile DecodeXmpProfile(byte[] spd,
            int profileNum, int baseOff)
        {
            Ddr5XmpProfile p = new Ddr5XmpProfile();
            p.ProfileNumber = profileNum;

            // Voltages
            p.VddCode = B(spd, baseOff + XMP_OFF_VDD);
            p.VddqCode = B(spd, baseOff + XMP_OFF_VDDQ);
            p.VppCode = B(spd, baseOff + XMP_OFF_VPP);
            p.VddMv = 1100 + p.VddCode * 5;
            p.VddqMv = 1100 + p.VddqCode * 5;
            p.VppMv = 1500 + p.VppCode * 5;

            // tCKAVGmin (16-bit LE picoseconds)
            p.tCKAVGminPs = U16LE(spd, baseOff + XMP_OFF_TCK_LSB);
            if (p.tCKAVGminPs <= 0)
            {
                p.IsValid = false;
                return p;
            }

            p.ClockMHz = 1000000.0 / (double)p.tCKAVGminPs;
            p.SpeedMTs = (int)Math.Round(2.0 * p.ClockMHz);
            p.SpeedMTs = RoundToJedecBin(p.SpeedMTs);
            p.ClockMHz = p.SpeedMTs / 2.0;
            p.SpeedGrade = string.Format("DDR5-{0}", p.SpeedMTs);

            // CAS latencies supported (5 bytes at +6..+10)
            p.SupportedCLs = new List<int>();
            for (int byteIdx = 0; byteIdx < 5; byteIdx++)
            {
                byte clByte = B(spd, baseOff + XMP_OFF_CAS_0 + byteIdx);
                for (int bit = 0; bit < 8; bit++)
                {
                    if ((clByte & (1 << bit)) != 0)
                    {
                        // XMP uses same CL encoding as JEDEC base:
                        // byte0 bit0=CL20, bit1=CL22, ... byte4 bit7=CL98
                        int cl = 20 + (byteIdx * 8 + bit) * 2;
                        p.SupportedCLs.Add(cl);
                    }
                }
            }

            // Timing parameters (16-bit LE picoseconds)
            p.tAAminPs = U16LE(spd, baseOff + XMP_OFF_TAA_LSB);
            p.tRCDminPs = U16LE(spd, baseOff + XMP_OFF_TRCD_LSB);
            p.tRPminPs = U16LE(spd, baseOff + XMP_OFF_TRP_LSB);
            p.tRASminPs = U16LE(spd, baseOff + XMP_OFF_TRAS_LSB);
            p.tRCminPs = U16LE(spd, baseOff + XMP_OFF_TRC_LSB);
            p.tWRminPs = U16LE(spd, baseOff + XMP_OFF_TWR_LSB);

            // tRFC in nanoseconds
            p.tRFC1minNs = U16LE(spd, baseOff + XMP_OFF_TRFC1_LSB);
            p.tRFC2minNs = U16LE(spd, baseOff + XMP_OFF_TRFC2_LSB);
            p.tRFCsbMinNs = U16LE(spd, baseOff + XMP_OFF_TRFCSB_LSB);

            // CL calculation with ideal tCK to avoid integer rounding artifacts
            double tCKideal = 1000000.0 / (p.SpeedMTs / 2.0);
            p.CL = (int)Math.Ceiling((double)p.tAAminPs / tCKideal - 0.01);
            p.tRCD = (int)Math.Ceiling((double)p.tRCDminPs / tCKideal - 0.01);
            p.tRP = (int)Math.Ceiling((double)p.tRPminPs / tCKideal - 0.01);
            p.TimingString = string.Format("{0}-{1}-{2} @ {3}",
                p.CL, p.tRCD, p.tRP, p.SpeedGrade);

            p.IsValid = true;
            return p;
        }

        private static byte B(byte[] spd, int offset)
        {
            if (offset < 0 || offset >= spd.Length) return 0;
            return spd[offset];
        }

        private static int U16LE(byte[] spd, int offset)
        {
            return (int)B(spd, offset) | ((int)B(spd, offset + 1) << 8);
        }

        private static int DecodeBcd(byte b)
        {
            return ((b >> 4) & 0x0F) * 10 + (b & 0x0F);
        }

        /// <summary>Quick check: does this SPD identify as DDR5?</summary>
        public static bool IsDdr5(byte[] spd)
        {
            if (spd == null || spd.Length < 3) return false;
            return spd[SPD_DEVICE_TYPE] == DDR5_DEVICE_TYPE || spd[SPD_DEVICE_TYPE] == LPDDR5_DEVICE_TYPE;
        }

        /// <summary>Extract just the JEDEC speed grade without full decode.</summary>
        public static string GetSpeedGrade(byte[] spd)
        {
            if (spd == null || spd.Length < 3)
                return "Unknown";

            byte deviceType = spd[SPD_DEVICE_TYPE];
            int tCK;
            int mts;

            if (deviceType == LPDDR5_DEVICE_TYPE)
            {
                tCK = DecodeLpddr5TckMin(spd);
                if (tCK <= 0) return "Unknown";
                mts = (int)Math.Round(2000000.0 / (double)tCK);
                return string.Format("LPDDR5-{0}", mts);
            }

            tCK = U16LE(spd, SPD_TCKAVG_MIN_LSB);
            if (tCK <= 0) return "Unknown";
            double mhz = 1000000.0 / (double)tCK;
            mts = RoundToJedecBin((int)Math.Round(2.0 * mhz));
            return string.Format("DDR5-{0}", mts);
        }

        /// <summary>Extract just the module part number without full decode.</summary>
        public static string GetPartNumber(byte[] spd)
        {
            if (spd == null || spd.Length < SPD_MOD_PARTNO + 30) return "";
            StringBuilder sb = new StringBuilder();
            for (int i = 0; i < 30; i++)
            {
                byte c = spd[SPD_MOD_PARTNO + i];
                if (c >= 0x20 && c <= 0x7E)
                    sb.Append((char)c);
            }
            return sb.ToString().Trim();
        }

        /// <summary>
        /// Read and decode SPD from all discovered DDR5/LPDDR5 modules.
        /// Also reads live thermal sensor data from SPD5118 hubs.
        /// </summary>
        internal static Dictionary<byte, Ddr5SpdInfo> ReadAndDecodeAll(SmbusDriverBase smbus)
        {
            if (smbus == null)
                throw new ArgumentNullException("smbus");

            Dictionary<byte, Ddr5SpdInfo> result = new Dictionary<byte, Ddr5SpdInfo>();

            if (!Mutexes.WaitSmbus(5000))
                return result;

            try
            {
                result = Ddr5SpdReader.ReadDdr5SpdAllNoLock();

                if (result != null)
                {
                    foreach (KeyValuePair<byte, Ddr5SpdInfo> kvp in result)
                        Ddr5SpdReader.ReadLiveDevicesNoLock(kvp.Key, kvp.Value, smbus);
                }
            }
            finally
            {
                Mutexes.ReleaseSmbus();
            }

            return result;
        }

        /// <summary>Decode from a raw binary SPD file on disk.</summary>
        public static Ddr5SpdInfo DecodeFromFile(string path)
        {
            byte[] data = System.IO.File.ReadAllBytes(path);
            return Decode(data);
        }
    }
}
