using System;
using ZenStates.Core.Hardware.DRAM.DDR5.Spd;
using static ZenStates.Core.Hardware.DRAM.DDR5.Spd.Ddr5SpdBytes;

namespace ZenStates.Core.Hardware.DRAM.DDR5.Profiles
{
    /// <summary>
    /// Intel XMP 3.0 and AMD EXPO profiles in the end user section of a DDR5 SPD (bytes 640~959).
    ///
    /// XMP 3.0: a 64-byte header at 640 ("0x0C 0x4A", revision, enabled profiles, profile names) followed by the three
    /// vendor profiles as 64-byte blocks at 704, 768 and 832.
    /// EXPO: "EXPO" at 832, revision, enabled profiles, two 40-byte profiles at 842 and 882 and a CRC of bytes 832~957
    /// at 958. A module with both has EXPO in place of the third XMP profile.
    ///
    /// Profile voltages are encoded as bits [7:5] whole volts plus bits [4:0] x 50 mV.
    /// </summary>
    internal static class Ddr5ProfileDecoder
    {
        private const int XMP_HEADER = 640;
        private const int XMP_REVISION = 642;
        private const int XMP_PROFILES_ENABLED = 643;   // bit n: vendor profile n + 1
        private const int XMP_DYNAMIC_BOOST = 644;      // bit n: vendor profile n + 1
        private const int XMP_PROFILE_NAMES = 654;      // 16 bytes each
        private const int XMP_PROFILE_NAME_LENGTH = 16;
        private const int XMP_PROFILE_FIRST = 704;
        private const int XMP_PROFILE_SIZE = 64;
        private const int XMP_PROFILE_COUNT = 3;

        // XMP profile layout, relative to the profile
        private const int XMP_VPP = 0;
        private const int XMP_VDD = 1;
        private const int XMP_VDDQ = 2;
        private const int XMP_TCK = 5;
        private const int XMP_CAS = 7;      // 5 bytes, as JEDEC bytes 24~28
        private const int XMP_TAA = 13;     // then tRCD, tRP, tRAS, tRC, tWR (ps), tRFC1, tRFC2, tRFCsb (ns)

        private const int EXPO_HEADER = 832;
        private const int EXPO_REVISION = 836;
        private const int EXPO_PROFILES_ENABLED = 837;
        private const int EXPO_PROFILE_FIRST = 842;
        private const int EXPO_PROFILE_SIZE = 40;
        private const int EXPO_CRC = 958;

        // EXPO profile layout, relative to the profile
        private const int EXPO_VDD = 0;
        private const int EXPO_VDDQ = 1;
        private const int EXPO_VPP = 2;
        private const int EXPO_TCK = 4;     // then tAA, tRCD, tRP, tRAS, tRC, tWR (ps), tRFC1, tRFC2, tRFCsb (ns)

        /// <summary>Sets empty (not valid) profiles, so the profile fields are never null.</summary>
        public static void SetEmptyProfiles(Ddr5SpdInfo info)
        {
            info.HasXmp = false;
            info.XmpProfiles = new Ddr5XmpProfile[XMP_PROFILE_COUNT];
            for (int i = 0; i < XMP_PROFILE_COUNT; i++)
                info.XmpProfiles[i] = new Ddr5XmpProfile { ProfileNumber = i + 1 };

            info.HasExpo = false;
            info.ExpoProfile1 = new Ddr5ExpoProfile { ProfileNumber = 1 };
            info.ExpoProfile2 = new Ddr5ExpoProfile { ProfileNumber = 2 };
        }

        public static void Decode(byte[] spd, Ddr5SpdInfo info)
        {
            SetEmptyProfiles(info);
            DecodeExpo(spd, info);
            DecodeXmp(spd, info);
        }

        private static void DecodeExpo(byte[] spd, Ddr5SpdInfo info)
        {
            info.HasExpo = B(spd, EXPO_HEADER) == 'E' && B(spd, EXPO_HEADER + 1) == 'X' &&
                           B(spd, EXPO_HEADER + 2) == 'P' && B(spd, EXPO_HEADER + 3) == 'O';
            if (!info.HasExpo)
                return;

            byte revision = B(spd, EXPO_REVISION);
            info.ExpoRevision = string.Format("{0}.{1}", revision >> 4, revision & 0x0F);
            info.ExpoCrcValid = Crc16(spd, EXPO_HEADER, EXPO_CRC - EXPO_HEADER) == U16(spd, EXPO_CRC);

            // Bit 0 enables profile 1. Which of the higher bits enables profile 2 is not public (a single profile module
            // seen reads 0x03), so profile 2 is taken when any of them is set and its block holds a cycle time and VDD.
            byte enabled = B(spd, EXPO_PROFILES_ENABLED);

            if ((enabled & 0x01) != 0)
                info.ExpoProfile1 = DecodeExpoProfile(spd, 1, EXPO_PROFILE_FIRST);

            if ((enabled & 0x0E) != 0)
            {
                Ddr5ExpoProfile profile2 = DecodeExpoProfile(spd, 2, EXPO_PROFILE_FIRST + EXPO_PROFILE_SIZE);
                if (profile2.VddMv > 0)
                    info.ExpoProfile2 = profile2;
            }
        }

        private static Ddr5ExpoProfile DecodeExpoProfile(byte[] spd, int number, int offset)
        {
            Ddr5ExpoProfile p = new Ddr5ExpoProfile
            {
                ProfileNumber = number,
                VddCode = B(spd, offset + EXPO_VDD),
                VddqCode = B(spd, offset + EXPO_VDDQ),
                VppCode = B(spd, offset + EXPO_VPP),
                tCKAVGminPs = U16(spd, offset + EXPO_TCK),
            };

            p.VddMv = VoltageMv(p.VddCode);
            p.VddqMv = VoltageMv(p.VddqCode);
            p.VppMv = VoltageMv(p.VppCode);

            if (p.tCKAVGminPs <= 0)
                return p;

            int t = offset + EXPO_TCK + 2;
            p.tAAminPs = U16(spd, t);
            p.tRCDminPs = U16(spd, t + 2);
            p.tRPminPs = U16(spd, t + 4);
            p.tRASminPs = U16(spd, t + 6);
            p.tRCminPs = U16(spd, t + 8);
            p.tWRminPs = U16(spd, t + 10);
            p.tRFC1minNs = U16(spd, t + 12);
            p.tRFC2minNs = U16(spd, t + 14);
            p.tRFCsbMinNs = U16(spd, t + 16);

            SetSpeed(p.tCKAVGminPs, out p.SpeedMTs, out p.ClockMHz, out p.SpeedGrade);
            p.CL = Ddr5SpdTimingMath.ToCl(p.tAAminPs, p.tCKAVGminPs, null);
            p.tRCD = Ddr5SpdTimingMath.ToNck(p.tRCDminPs, p.tCKAVGminPs);
            p.tRP = Ddr5SpdTimingMath.ToNck(p.tRPminPs, p.tCKAVGminPs);
            p.TimingString = string.Format("{0}-{1}-{2} @ {3}", p.CL, p.tRCD, p.tRP, p.SpeedGrade);
            p.IsValid = true;
            return p;
        }

        private static void DecodeXmp(byte[] spd, Ddr5SpdInfo info)
        {
            info.HasXmp = B(spd, XMP_HEADER) == 0x0C && B(spd, XMP_HEADER + 1) == 0x4A;
            if (!info.HasXmp)
                return;

            byte revision = B(spd, XMP_REVISION);
            info.XmpRevision = string.Format("{0}.{1}", revision >> 4, revision & 0x0F);

            byte enabled = B(spd, XMP_PROFILES_ENABLED);
            byte dynamicBoost = B(spd, XMP_DYNAMIC_BOOST);

            for (int i = 0; i < XMP_PROFILE_COUNT; i++)
            {
                int offset = XMP_PROFILE_FIRST + i * XMP_PROFILE_SIZE;
                if ((enabled & (1 << i)) == 0)
                    continue;

                // The third vendor profile shares bytes 832~895 with EXPO
                if (info.HasExpo && offset + XMP_PROFILE_SIZE > EXPO_HEADER)
                    continue;

                Ddr5XmpProfile profile = DecodeXmpProfile(spd, i + 1, offset);
                profile.DynamicMemoryBoost = (dynamicBoost & (1 << i)) != 0;
                profile.ProfileName = Ascii(spd, XMP_PROFILE_NAMES + i * XMP_PROFILE_NAME_LENGTH, XMP_PROFILE_NAME_LENGTH);
                info.XmpProfiles[i] = profile;
            }
        }

        private static Ddr5XmpProfile DecodeXmpProfile(byte[] spd, int number, int offset)
        {
            Ddr5XmpProfile p = new Ddr5XmpProfile
            {
                ProfileNumber = number,
                VppCode = B(spd, offset + XMP_VPP),
                VddCode = B(spd, offset + XMP_VDD),
                VddqCode = B(spd, offset + XMP_VDDQ),
                tCKAVGminPs = U16(spd, offset + XMP_TCK),
            };

            p.VddMv = VoltageMv(p.VddCode);
            p.VddqMv = VoltageMv(p.VddqCode);
            p.VppMv = VoltageMv(p.VppCode);

            if (p.tCKAVGminPs <= 0)
                return p;

            p.SupportedCLs = Ddr5BaseDecoder.CasLatencies(spd, offset + XMP_CAS);

            int t = offset + XMP_TAA;
            p.tAAminPs = U16(spd, t);
            p.tRCDminPs = U16(spd, t + 2);
            p.tRPminPs = U16(spd, t + 4);
            p.tRASminPs = U16(spd, t + 6);
            p.tRCminPs = U16(spd, t + 8);
            p.tWRminPs = U16(spd, t + 10);
            p.tRFC1minNs = U16(spd, t + 12);
            p.tRFC2minNs = U16(spd, t + 14);
            p.tRFCsbMinNs = U16(spd, t + 16);

            SetSpeed(p.tCKAVGminPs, out p.SpeedMTs, out p.ClockMHz, out p.SpeedGrade);
            p.CL = Ddr5SpdTimingMath.ToCl(p.tAAminPs, p.tCKAVGminPs, p.SupportedCLs);
            p.tRCD = Ddr5SpdTimingMath.ToNck(p.tRCDminPs, p.tCKAVGminPs);
            p.tRP = Ddr5SpdTimingMath.ToNck(p.tRPminPs, p.tCKAVGminPs);
            p.TimingString = string.Format("{0}-{1}-{2} @ {3}", p.CL, p.tRCD, p.tRP, p.SpeedGrade);
            p.IsValid = true;
            return p;
        }

        // Profile speeds are not limited to JEDEC bins; the rate is rounded to 100 MT/s (tCK is stored in whole ps).
        private static void SetSpeed(int tckPs, out int speedMts, out double clockMhz, out string speedGrade)
        {
            speedMts = (int)Math.Round(2000000.0 / tckPs / 100.0) * 100;
            clockMhz = speedMts / 2.0;
            speedGrade = string.Format("DDR5-{0}", speedMts);
        }

        private static int VoltageMv(int code)
        {
            return ((code >> 5) & 0x07) * 1000 + (code & 0x1F) * 50;
        }
    }
}
