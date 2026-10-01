using System;
using System.Collections.Generic;
using static ZenStates.Core.Hardware.DRAM.DDR5.Spd.Ddr5SpdBytes;

namespace ZenStates.Core.Hardware.DRAM.DDR5.Spd
{
    /// <summary>
    /// DDR5 base configuration, bytes 0~127 (JESD400-5). Timings are stored in picoseconds (tRFC in nanoseconds), 16 bits
    /// low byte first. Bytes 4~7 describe the first SDRAM type, bytes 8~11 the second one of an asymmetrical module.
    /// </summary>
    internal static class Ddr5BaseDecoder
    {
        private const int SPD_FIRST_DENSITY = 4;      // [7:5] die per package, [4:0] density per die
        private const int SPD_FIRST_ADDRESSING = 5;   // [7:5] column bits - 10, [4:0] row bits - 16
        private const int SPD_FIRST_IO_WIDTH = 6;     // [7:5] x4 << n
        private const int SPD_FIRST_BANKS = 7;        // [7:5] bank groups 1 << n, [2:0] banks per group 1 << n
        private const int SPD_SECOND_OFFSET = 4;      // bytes 8~11 repeat 4~7 for the second SDRAM

        private const int SPD_VDD = 16;               // [7:4] nominal, [3:2] operable, [1:0] endurant
        private const int SPD_VDDQ = 17;
        private const int SPD_VPP = 18;

        private const int SPD_TCK_MIN = 20;
        private const int SPD_TCK_MAX = 22;
        private const int SPD_CAS_FIRST = 24;         // bytes 24~28: bit n = CL 20 + 2n
        private const int SPD_TAA = 30;
        private const int SPD_TRCD = 32;
        private const int SPD_TRP = 34;
        private const int SPD_TRAS = 36;
        private const int SPD_TRC = 38;
        private const int SPD_TWR = 40;
        private const int SPD_TRFC1 = 42;
        private const int SPD_TRFC2 = 44;
        private const int SPD_TRFCSB = 46;

        private const int SPD_MODULE_ORGANISATION = 234;   // [6] asymmetrical

        private static readonly int[] SpeedBins =
        {
            3200, 3600, 4000, 4400, 4800, 5200, 5600, 6000, 6400, 6800, 7200, 7600, 8000, 8400, 8800, 9200, 9600,
        };

        public static void Decode(byte[] spd, Ddr5SpdInfo info)
        {
            DecodeSdram(spd, info);
            DecodeVoltages(spd, info);
            DecodeTimings(spd, info);
            CalculateCapacity(info);
            Ddr5SpdDecoder.SetTimingString(info);
        }

        private static void DecodeSdram(byte[] spd, Ddr5SpdInfo info)
        {
            byte density1 = B(spd, SPD_FIRST_DENSITY);
            info.FirstDieDensityMbit = DieDensityMbit(density1 & 0x1F);
            info.FirstDieCount = DieCount(density1 >> 5);
            info.FirstPackageType = PackageType(density1 >> 5);
            info.FirstDeviceWidthBits = IoWidth(B(spd, SPD_FIRST_IO_WIDTH));
            DecodeAddressing(B(spd, SPD_FIRST_ADDRESSING), out info.FirstRowBits, out info.FirstColumnBits);

            byte banks = B(spd, SPD_FIRST_BANKS);
            info.FirstBankGroups = 1 << ((banks >> 5) & 0x03);
            info.FirstBanksPerBankGroup = 1 << (banks & 0x03);

            // Bytes 8~11 are 0 on a symmetrical module; byte 234 [6] is the flag
            info.IsAsymmetric = (B(spd, SPD_MODULE_ORGANISATION) & 0x40) != 0;
            if (info.IsAsymmetric)
            {
                byte density2 = B(spd, SPD_FIRST_DENSITY + SPD_SECOND_OFFSET);
                info.SecondDieDensityMbit = DieDensityMbit(density2 & 0x1F);
                info.SecondDieCount = DieCount(density2 >> 5);
                info.SecondDeviceWidthBits = IoWidth(B(spd, SPD_FIRST_IO_WIDTH + SPD_SECOND_OFFSET));
                DecodeAddressing(B(spd, SPD_FIRST_ADDRESSING + SPD_SECOND_OFFSET), out info.SecondRowBits, out info.SecondColumnBits);
            }

            // Logical ranks: the package ranks times the 3DS stack height; even ranks use the first SDRAM, odd the second
            int firstStack = StackHeight(density1 >> 5);
            int secondStack = info.IsAsymmetric ? StackHeight(B(spd, SPD_FIRST_DENSITY + SPD_SECOND_OFFSET) >> 5) : firstStack;
            int evenRanks = (info.RanksPerChannel + 1) / 2;
            int oddRanks = info.RanksPerChannel / 2;
            info.LogicalRanksPerChannel = evenRanks * firstStack + oddRanks * secondStack;
        }

        private static void DecodeAddressing(byte value, out int rowBits, out int columnBits)
        {
            rowBits = 16 + (value & 0x1F);
            columnBits = 10 + ((value >> 5) & 0x07);
        }

        private static int DieDensityMbit(int code)
        {
            switch (code)
            {
                case 1: return 4096;
                case 2: return 8192;
                case 3: return 12288;
                case 4: return 16384;
                case 5: return 24576;
                case 6: return 32768;
                case 7: return 49152;
                case 8: return 65536;
                default: return 0;
            }
        }

        // 000 monolithic, 001 DDP, 010~101 2H~16H 3DS
        private static int DieCount(int code)
        {
            switch (code & 0x07)
            {
                case 1: return 2;
                case 2: return 2;
                case 3: return 4;
                case 4: return 8;
                case 5: return 16;
                default: return 1;
            }
        }

        private static int StackHeight(int code)
        {
            switch (code & 0x07)
            {
                case 2: return 2;
                case 3: return 4;
                case 4: return 8;
                case 5: return 16;
                default: return 1;
            }
        }

        private static string PackageType(int code)
        {
            switch (code & 0x07)
            {
                case 0: return "Monolithic";
                case 1: return "DDP";
                case 2: return "2H 3DS";
                case 3: return "4H 3DS";
                case 4: return "8H 3DS";
                case 5: return "16H 3DS";
                default: return "Reserved";
            }
        }

        private static int IoWidth(byte value)
        {
            int code = (value >> 5) & 0x07;
            return code <= 3 ? 4 << code : 0;
        }

        private static void DecodeVoltages(byte[] spd, Ddr5SpdInfo info)
        {
            info.VddString = NominalVoltage(B(spd, SPD_VDD), "1.1 V");
            info.VddqString = NominalVoltage(B(spd, SPD_VDDQ), "1.1 V");
            info.VppString = NominalVoltage(B(spd, SPD_VPP), "1.8 V");
        }

        // 0000 is the only nominal voltage defined so far
        private static string NominalVoltage(byte value, string jedecVoltage)
        {
            return (value >> 4) == 0 ? jedecVoltage : string.Format("Reserved (0x{0:X2})", value);
        }

        private static void DecodeTimings(byte[] spd, Ddr5SpdInfo info)
        {
            info.tCKAVGminPs = U16(spd, SPD_TCK_MIN);
            info.tCKAVGmaxPs = U16(spd, SPD_TCK_MAX);

            if (info.tCKAVGminPs > 0)
            {
                info.SpeedMTs = Ddr5SpdTimingMath.ToSpeedBin((int)Math.Round(2000000.0 / info.tCKAVGminPs), SpeedBins);
                info.ClockMHz = info.SpeedMTs / 2.0;
            }
            info.SpeedGrade = string.Format("DDR5-{0}", info.SpeedMTs);

            info.SupportedCLs = CasLatencies(spd, SPD_CAS_FIRST);

            info.tAAminPs = U16(spd, SPD_TAA);
            info.tRCDminPs = U16(spd, SPD_TRCD);
            info.tRPminPs = U16(spd, SPD_TRP);
            info.tRASminPs = U16(spd, SPD_TRAS);
            info.tRCminPs = U16(spd, SPD_TRC);
            info.tWRminPs = U16(spd, SPD_TWR);
            info.tRFC1minNs = U16(spd, SPD_TRFC1);
            info.tRFC2minNs = U16(spd, SPD_TRFC2);
            info.tRFCsbMinNs = U16(spd, SPD_TRFCSB);
        }

        /// <summary>CAS latency mask (5 bytes): bit n of the 40 is CL 20 + 2n.</summary>
        internal static List<int> CasLatencies(byte[] spd, int offset)
        {
            List<int> cls = new List<int>();
            for (int i = 0; i < 40; i++)
            {
                if ((B(spd, offset + i / 8) & (1 << (i % 8))) != 0)
                    cls.Add(20 + 2 * i);
            }
            return cls;
        }

        // Sub-channels * bus width / I/O width * dies per package * density / 8 * package ranks (JESD400-5).
        // On an asymmetrical module the even ranks use the first SDRAM, the odd ranks the second.
        private static void CalculateCapacity(Ddr5SpdInfo info)
        {
            int evenRanks = info.IsAsymmetric ? (info.RanksPerChannel + 1) / 2 : info.RanksPerChannel;
            int oddRanks = info.IsAsymmetric ? info.RanksPerChannel / 2 : 0;

            info.TotalCapacityMB =
                CapacityMB(info, info.FirstDieDensityMbit, info.FirstDieCount, info.FirstDeviceWidthBits, evenRanks) +
                CapacityMB(info, info.SecondDieDensityMbit, info.SecondDieCount, info.SecondDeviceWidthBits, oddRanks);
        }

        private static long CapacityMB(Ddr5SpdInfo info, int densityMbit, int dieCount, int ioWidth, int ranks)
        {
            if (densityMbit <= 0 || ioWidth <= 0 || ranks <= 0)
                return 0;

            return (long)info.SubChannelsPerDimm * (info.PrimaryBusWidthBits / ioWidth) * dieCount * (densityMbit / 8) * ranks;
        }
    }
}
