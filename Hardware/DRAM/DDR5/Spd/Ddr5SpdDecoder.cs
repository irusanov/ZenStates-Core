using System;
using System.Collections.Generic;
using ZenStates.Core.Hardware.DRAM.DDR5.Profiles;
using static ZenStates.Core.Hardware.DRAM.DDR5.Spd.Ddr5SpdBytes;

namespace ZenStates.Core.Hardware.DRAM.DDR5.Spd
{
    /// <summary>
    /// Decodes the SPD of a DDR5 (JESD400-5) or LPDDR5/5X (JESD406-5) module. Both are 1024 bytes and share everything
    /// except the base configuration (bytes 0~127):
    ///
    ///   bytes   0~127  base configuration    DDR5: <see cref="Ddr5BaseDecoder"/>, LPDDR5/5X: <see cref="Lpddr5BaseDecoder"/>
    ///   bytes 192~239  common module bytes   <see cref="Ddr5CommonDecoder"/> (Annex A.0)
    ///   bytes 240~447  module type specific  (not decoded)
    ///   bytes 510~511  CRC of bytes 0~509
    ///   bytes 512~554  manufacturing         <see cref="Ddr5CommonDecoder"/>
    ///   bytes 640~959  XMP 3.0 / EXPO        <see cref="Ddr5ProfileDecoder"/> (DDR5 only)
    /// </summary>
    public static class Ddr5SpdDecoder
    {
        public const int SPD_SIZE = 1024;

        // Key byte (byte 2)
        public const byte DDR5_DEVICE_TYPE = 0x12;
        public const byte LPDDR5_DEVICE_TYPE = 0x13;
        public const byte DDR5_NVDIMM_P_DEVICE_TYPE = 0x14;
        public const byte LPDDR5X_DEVICE_TYPE = 0x15;

        /// <summary>The ranges a partial read covers: base configuration, common module bytes and manufacturing information.</summary>
        public const int BASE_LENGTH = 128;
        public const int COMMON_FIRST = 192;
        public const int COMMON_LENGTH = 48;
        public const int MANUFACTURING_FIRST = 512;
        public const int MANUFACTURING_LENGTH = 43;

        private const int SPD_BYTES = 0;          // [6:4] SPD size, [7,3:0] beta level
        private const int SPD_REVISION = 1;       // [7:4] encoding level, [3:0] additions level
        private const int SPD_DEVICE_TYPE = 2;    // key byte
        private const int SPD_MODULE_TYPE = 3;    // [7] hybrid, [6:4] hybrid media, [3:0] base module type
        private const int SPD_CRC = 510;          // CRC of bytes 0~509, low byte first

        public static bool IsSupportedDeviceType(byte keyByte)
        {
            return keyByte == DDR5_DEVICE_TYPE || keyByte == DDR5_NVDIMM_P_DEVICE_TYPE || IsLpddr5DeviceType(keyByte);
        }

        public static bool IsLpddr5DeviceType(byte keyByte)
        {
            return keyByte == LPDDR5_DEVICE_TYPE || keyByte == LPDDR5X_DEVICE_TYPE;
        }

        public static Ddr5SpdInfo DecodeFromFile(string path)
        {
            return Decode(System.IO.File.ReadAllBytes(path), false);
        }

        /// <param name="data">SPD image, 1024 bytes (a shorter image is padded with 0).</param>
        /// <param name="partial">Only bytes 0~127, 192~239 and 512~554 were read.</param>
        public static Ddr5SpdInfo Decode(byte[] data, bool partial)
        {
            byte[] spd = new byte[SPD_SIZE];
            if (data != null)
                Array.Copy(data, spd, Math.Min(data.Length, SPD_SIZE));

            Ddr5SpdInfo info = new Ddr5SpdInfo
            {
                RawSpd = spd,
                IsPartial = partial,
                DeviceType = spd[SPD_DEVICE_TYPE],
                SupportedCLs = new List<int>(),
            };
            Ddr5ProfileDecoder.SetEmptyProfiles(info);

            info.IsValid = IsSupportedDeviceType(info.DeviceType);
            if (!info.IsValid)
            {
                info.DeviceTypeString = string.Format("Unknown (0x{0:X2})", info.DeviceType);
                return info;
            }

            DecodeGeneral(spd, info);

            // The organisation (bytes 234~235) is needed by the base decoders for the capacity
            Ddr5CommonDecoder.DecodeModuleCommon(spd, info);

            if (info.IsLpddr5)
                Lpddr5BaseDecoder.Decode(spd, info);
            else
                Ddr5BaseDecoder.Decode(spd, info);

            Ddr5CommonDecoder.DecodeManufacturing(spd, info);

            if (!partial)
            {
                info.BaseCrc = U16(spd, SPD_CRC);
                info.BaseCrcValid = Crc16(spd, 0, SPD_CRC) == info.BaseCrc;

                if (!info.IsLpddr5)
                    Ddr5ProfileDecoder.Decode(spd, info);
            }

            return info;
        }

        private static void DecodeGeneral(byte[] spd, Ddr5SpdInfo info)
        {
            byte bytes = spd[SPD_BYTES];
            switch ((bytes >> 4) & 0x07)
            {
                case 1: info.BytesTotal = 256; break;
                case 2: info.BytesTotal = 512; break;
                case 3: info.BytesTotal = 1024; break;   // SPD5118
                case 4: info.BytesTotal = 2048; break;   // ESPD5216
                default: info.BytesTotal = 0; break;
            }
            info.SpdBetaLevel = ((bytes >> 3) & 0x10) | (bytes & 0x0F);

            byte revision = spd[SPD_REVISION];
            info.SpdRevisionEncoding = revision >> 4;
            info.SpdRevisionAdditions = revision & 0x0F;
            info.SpdRevision = string.Format("{0}.{1}", info.SpdRevisionEncoding, info.SpdRevisionAdditions);

            info.IsLpddr5 = IsLpddr5DeviceType(info.DeviceType);
            switch (info.DeviceType)
            {
                case LPDDR5_DEVICE_TYPE:
                    info.DeviceTypeString = "LPDDR5 SDRAM";
                    info.MemoryFamily = "LPDDR5";
                    break;
                case LPDDR5X_DEVICE_TYPE:
                    info.DeviceTypeString = "LPDDR5X SDRAM";
                    info.MemoryFamily = "LPDDR5X";
                    break;
                case DDR5_NVDIMM_P_DEVICE_TYPE:
                    info.DeviceTypeString = "DDR5 NVDIMM-P";
                    info.MemoryFamily = "DDR5";
                    break;
                default:
                    info.DeviceTypeString = "DDR5 SDRAM";
                    info.MemoryFamily = "DDR5";
                    break;
            }

            byte moduleType = spd[SPD_MODULE_TYPE];
            info.BaseModuleType = (byte)(moduleType & 0x0F);
            info.ModuleTypeString = ModuleTypeName(info.BaseModuleType);
            info.IsHybrid = (moduleType & 0x80) != 0;
            switch ((moduleType >> 4) & 0x07)
            {
                case 0: info.HybridTypeString = "Not hybrid"; break;
                case 1: info.HybridTypeString = "NVDIMM-N"; break;
                case 2: info.HybridTypeString = "NVDIMM-P"; break;
                default: info.HybridTypeString = "Reserved"; break;
            }
        }

        public static string ModuleTypeName(int baseModuleType)
        {
            switch (baseModuleType)
            {
                case 0x01: return "RDIMM";
                case 0x02: return "UDIMM";
                case 0x03: return "SODIMM";
                case 0x04: return "LRDIMM";
                case 0x05: return "CUDIMM";
                case 0x06: return "CSODIMM";
                case 0x07: return "MRDIMM";
                case 0x08: return "CAMM2";
                case 0x09: return "SOCAMM2";
                case 0x0A: return "DDIMM";
                case 0x0B: return "Solder down";
                default: return string.Format("Reserved (0x{0:X})", baseModuleType);
            }
        }

        /// <summary>Speed grade, timing string and the clocks of the base timings at tCKAVGmin.</summary>
        internal static void SetTimingString(Ddr5SpdInfo info)
        {
            int tck = info.tCKAVGminPs;
            if (tck > 0 && info.tAAminPs > 0)
            {
                info.CL = info.IsLpddr5
                    ? Ddr5SpdTimingMath.ToNck(info.tAAminPs, tck)
                    : Ddr5SpdTimingMath.ToCl(info.tAAminPs, tck, info.SupportedCLs);
                info.tRCD = Ddr5SpdTimingMath.ToNck(info.tRCDminPs, tck);
                info.tRP = Ddr5SpdTimingMath.ToNck(info.tRPminPs, tck);
                info.tRAS = Ddr5SpdTimingMath.ToNck(info.tRASminPs, tck);
                // tRC also has to cover tRAS + tRP (DDR5 only, LPDDR5 SPD has neither tRAS nor tRC)
                info.tRC = info.tRCminPs > 0 ? Math.Max(Ddr5SpdTimingMath.ToNck(info.tRCminPs, tck), info.tRAS + info.tRP) : 0;
                info.tWR = Ddr5SpdTimingMath.ToNck(info.tWRminPs, tck);
            }

            info.TimingString = info.CL > 0
                ? string.Format("{0}-{1}-{2} @ {3}", info.CL, info.tRCD, info.tRP, info.SpeedGrade)
                : info.SpeedGrade;
        }
    }
}
