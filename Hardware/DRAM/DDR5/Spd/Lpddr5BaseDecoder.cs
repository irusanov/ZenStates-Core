using System;
using System.Diagnostics;
using static ZenStates.Core.Hardware.DRAM.DDR5.Spd.Ddr5SpdBytes;

namespace ZenStates.Core.Hardware.DRAM.DDR5.Spd
{
    /// <summary>
    /// LPDDR5/5X base configuration, bytes 0~127 (JESD406-5). The layout differs from DDR5: timings are stored in medium
    /// timebase units (125 ps) with a signed fine correction (1 ps) at bytes 120~125, there is no CAS latency mask and
    /// the SPD defines no voltages.
    ///
    /// tCK is the period of CK, which runs at an eighth of the data rate: LPDDR5-6400 has tCKAVGmin 1.25 ns (800 MHz).
    /// </summary>
    internal static class Lpddr5BaseDecoder
    {
        private const int SPD_DENSITY_BANKS = 4;      // [7:6] bank groups, [5:4] banks, [3:0] density per die
        private const int SPD_ADDRESSING = 5;         // [5:3] row bits - 12, [2:0] bank / column bits
        private const int SPD_PACKAGE = 6;            // [7] non-monolithic, [6:4] die per package, [3:1] DQs / die width, [0] loading index
        private const int SPD_OPTIONAL_FEATURES = 9;  // [7:6] PPR, [5] soft PPR
        private const int SPD_ORGANISATION = 12;      // [6] byte mode, [5:3] package ranks - 1, [2:0] die width x4 << n
        private const int SPD_SUB_CHANNEL_WIDTH = 13; // [2:0] 001 = 16 bits, 010 = 32 bits
        private const int SPD_TIMEBASES = 17;         // 0x00 = MTB 125 ps, FTB 1 ps

        // Medium timebase bytes and the fine correction of each
        private const int SPD_TCK_MIN = 18;
        private const int SPD_TCK_MAX = 19;
        private const int SPD_TAA = 24;
        private const int SPD_TRCD = 26;
        private const int SPD_TRPAB = 27;
        private const int SPD_TRPPB = 28;
        private const int SPD_TRFCAB = 29;            // 16 bits
        private const int SPD_TRFCPB = 31;            // 16 bits
        private const int SPD_FINE_TRPPB = 120;
        private const int SPD_FINE_TRPAB = 121;
        private const int SPD_FINE_TRCD = 122;
        private const int SPD_FINE_TAA = 123;
        private const int SPD_FINE_TCK_MAX = 124;
        private const int SPD_FINE_TCK_MIN = 125;

        private const int MTB_PS = 125;

        // LPDDR5 (JESD209-5) and LPDDR5X data rates
        private static readonly int[] SpeedBins =
        {
            1600, 2133, 2750, 3200, 3733, 4267, 4800, 5500, 6000, 6400, 7500, 8533, 9600, 10667,
        };

        public static void Decode(byte[] spd, Ddr5SpdInfo info)
        {
            DecodeSdram(spd, info);
            DecodeTimings(spd, info);

            // Sub-channels * bus width / die width * density / 8 * package ranks (JESD406-5)
            info.TotalCapacityMB = info.FirstDieDensityMbit > 0 && info.FirstDeviceWidthBits > 0
                ? (long)info.SubChannelsPerDimm * (info.PrimaryBusWidthBits / info.FirstDeviceWidthBits) *
                  (info.FirstDieDensityMbit / 8) * info.RanksPerChannel
                : 0;

            Ddr5SpdDecoder.SetTimingString(info);
        }

        private static void DecodeSdram(byte[] spd, Ddr5SpdInfo info)
        {
            byte densityBanks = B(spd, SPD_DENSITY_BANKS);
            info.FirstDieDensityMbit = DieDensityMbit(densityBanks & 0x0F);

            int bankGroupsCode = (densityBanks >> 6) & 0x03;
            int banksCode = (densityBanks >> 4) & 0x03;
            info.FirstBankGroups = bankGroupsCode <= 2 ? 1 << bankGroupsCode : 0;
            if (info.IsMemoryDownLayout)
            {
                // LPDDR4 layout: [5:4] are the bank address bits within a bank group (00 = 4 banks)
                info.FirstBanksPerBankGroup = banksCode <= 1 ? 4 << banksCode : 0;
            }
            else
            {
                int banks = banksCode <= 2 ? 4 << banksCode : 0;
                info.FirstBanksPerBankGroup = info.FirstBankGroups > 0 ? banks / info.FirstBankGroups : 0;
            }

            byte addressing = B(spd, SPD_ADDRESSING);
            int rowCode = (addressing >> 3) & 0x07;
            info.FirstRowBits = rowCode <= 6 ? 12 + rowCode : 0;
            info.FirstColumnBits = (addressing & 0x07) <= 1 ? 6 : 0;

            byte package = B(spd, SPD_PACKAGE);
            info.FirstDieCount = DieCount((package >> 4) & 0x07);
            info.FirstPackageType = (package & 0x80) != 0 ? "Non-monolithic" : "Monolithic";

            byte features = B(spd, SPD_OPTIONAL_FEATURES);
            info.PprSupported = ((features >> 6) & 0x03) == 1;
            info.SoftPprSupported = (features & 0x20) != 0;

            byte organisation = B(spd, SPD_ORGANISATION);
            info.IsByteMode = (organisation & 0x40) != 0;
            int widthCode = organisation & 0x07;
            info.FirstDeviceWidthBits = widthCode <= 3 ? 4 << widthCode : 0;

            switch (B(spd, SPD_SUB_CHANNEL_WIDTH) & 0x07)
            {
                case 1: info.SystemSubChannelWidthBits = 16; break;
                case 2: info.SystemSubChannelWidthBits = 32; break;
                default: info.SystemSubChannelWidthBits = 0; break;
            }

            // No 3DS stacks: every package rank is one logical rank
            info.LogicalRanksPerChannel = info.RanksPerChannel;
        }

        private static int DieDensityMbit(int code)
        {
            switch (code)
            {
                case 0x2: return 1024;
                case 0x3: return 2048;
                case 0x4: return 4096;
                case 0x5: return 8192;
                case 0x6: return 16384;
                case 0x7: return 32768;
                case 0x8: return 12288;
                case 0x9: return 24576;
                case 0xA: return 3072;
                case 0xB: return 6144;
                default: return 0;
            }
        }

        private static int DieCount(int code)
        {
            switch (code)
            {
                case 6: return 16;
                case 7: return 8;
                default: return code + 1;
            }
        }

        // Medium timebase value corrected with its signed fine timebase byte
        private static int Time(byte[] spd, int mtbOffset, int ftbOffset)
        {
            int mtb = B(spd, mtbOffset);
            return mtb > 0 ? mtb * MTB_PS + S8(spd, ftbOffset) : 0;
        }

        private static void DecodeTimings(byte[] spd, Ddr5SpdInfo info)
        {
            if (B(spd, SPD_TIMEBASES) != 0)
                Debug.WriteLine(string.Format("LPDDR5 SPD: reserved timebases 0x{0:X2}, using 125 ps / 1 ps.", B(spd, SPD_TIMEBASES)));

            info.tCKAVGminPs = Time(spd, SPD_TCK_MIN, SPD_FINE_TCK_MIN);
            info.tCKAVGmaxPs = Time(spd, SPD_TCK_MAX, SPD_FINE_TCK_MAX);

            // The memory-down layout gives the WCK period (LPDDR5-6400: 0.3125 ns); CK runs at a quarter of WCK
            if (info.IsMemoryDownLayout)
            {
                info.tCKAVGminPs *= 4;
                info.tCKAVGmaxPs *= 4;
            }

            if (info.tCKAVGminPs > 0)
            {
                info.ClockMHz = 1000000.0 / info.tCKAVGminPs;
                info.SpeedMTs = Ddr5SpdTimingMath.ToSpeedBin((int)Math.Round(8000000.0 / info.tCKAVGminPs), SpeedBins);
            }
            info.SpeedGrade = string.Format("{0}-{1}", info.MemoryFamily, info.SpeedMTs);

            info.tAAminPs = Time(spd, SPD_TAA, SPD_FINE_TAA);
            info.tRCDminPs = Time(spd, SPD_TRCD, SPD_FINE_TRCD);
            info.tRPminPs = Time(spd, SPD_TRPAB, SPD_FINE_TRPAB);
            info.tRPpbMinPs = Time(spd, SPD_TRPPB, SPD_FINE_TRPPB);
            info.tRFCabMinPs = U16(spd, SPD_TRFCAB) * MTB_PS;
            info.tRFCpbMinPs = U16(spd, SPD_TRFCPB) * MTB_PS;
        }
    }
}
