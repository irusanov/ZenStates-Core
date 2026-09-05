using static ZenStates.Core.Cpu;

namespace ZenStates.Core.Hardware.Apob
{
    public enum ApobBlockKind
    {
        Main,
        Extended
    }

    public enum ApobValueWidth
    {
        UInt16,
        UInt32
    }

    public sealed class ApobFieldOffsets
    {
        public ApobFieldOffsets(
            int gdm = -1,
            int rttNomRd = -1,
            int rttNomWr = -1,
            int rttWr = -1,
            int rttPark = -1,
            int rttParkDqs = -1,
            int dramDataDs = -1,
            int ckOdtA = -1,
            int csOdtA = -1,
            int caOdtA = -1,
            int ckOdtB = -1,
            int csOdtB = -1,
            int caOdtB = -1,
            int procOdt = -1,
            int procDqDs = -1,
            int procCaDs = -1,
            int procCkDs = -1,
            int procCsDs = -1,
            int rttNomRdP0 = -1,
            int rttNomWrP0 = -1,
            int rttWrP0 = -1,
            int rttParkP0 = -1,
            int rttParkDqsP0 = -1,
            int dramDqDsPullUpP0 = -1,
            int dramDqDsPullDownP0 = -1,
            int procOdtPullUpP0 = -1,
            int procOdtPullDownP0 = -1,
            int procDqDsPullUpP0 = -1,
            int procDqDsPullDownP0 = -1,
            int procCaOdt = -1,
            int procCkOdt = -1,
            int procDqOdt = -1,
            int procDqsOdt = -1,
            int procDataDsApu = -1)
        {
            Gdm = gdm;

            RttNomRd = rttNomRd;
            RttNomWr = rttNomWr;
            RttWr = rttWr;
            RttPark = rttPark;
            RttParkDqs = rttParkDqs;
            DramDataDs = dramDataDs;

            CkOdtA = ckOdtA;
            CsOdtA = csOdtA;
            CaOdtA = caOdtA;
            CkOdtB = ckOdtB;
            CsOdtB = csOdtB;
            CaOdtB = caOdtB;

            ProcOdt = procOdt;
            ProcDqDs = procDqDs;
            ProcCaDs = procCaDs;
            ProcCkDs = procCkDs;
            ProcCsDs = procCsDs;

            RttNomRdP0 = rttNomRdP0;
            RttNomWrP0 = rttNomWrP0;
            RttWrP0 = rttWrP0;
            RttParkP0 = rttParkP0;
            RttParkDqsP0 = rttParkDqsP0;

            DramDqDsPullUpP0 = dramDqDsPullUpP0;
            DramDqDsPullDownP0 = dramDqDsPullDownP0;

            ProcOdtPullUpP0 = procOdtPullUpP0;
            ProcOdtPullDownP0 = procOdtPullDownP0;
            ProcDqDsPullUpP0 = procDqDsPullUpP0;
            ProcDqDsPullDownP0 = procDqDsPullDownP0;

            ProcCaOdt = procCaOdt;
            ProcCkOdt = procCkOdt;
            ProcDqOdt = procDqOdt;
            ProcDqsOdt = procDqsOdt;
            ProcDataDsApu = procDataDsApu;
        }

        public int Gdm { get; private set; }
        public int RttNomRd { get; private set; }
        public int RttNomWr { get; private set; }
        public int RttWr { get; private set; }
        public int RttPark { get; private set; }
        public int RttParkDqs { get; private set; }
        public int DramDataDs { get; private set; }

        public int CkOdtA { get; private set; }
        public int CsOdtA { get; private set; }
        public int CaOdtA { get; private set; }
        public int CkOdtB { get; private set; }
        public int CsOdtB { get; private set; }
        public int CaOdtB { get; private set; }

        public int ProcOdt { get; private set; }
        public int ProcDqDs { get; private set; }
        public int ProcCaDs { get; private set; }
        public int ProcCkDs { get; private set; }
        public int ProcCsDs { get; private set; }

        public int RttNomRdP0 { get; private set; }
        public int RttNomWrP0 { get; private set; }
        public int RttWrP0 { get; private set; }
        public int RttParkP0 { get; private set; }
        public int RttParkDqsP0 { get; private set; }

        public int DramDqDsPullUpP0 { get; private set; }
        public int DramDqDsPullDownP0 { get; private set; }

        public int ProcOdtPullUpP0 { get; private set; }
        public int ProcOdtPullDownP0 { get; private set; }
        public int ProcDqDsPullUpP0 { get; private set; }
        public int ProcDqDsPullDownP0 { get; private set; }

        public int ProcCaOdt { get; private set; }
        public int ProcCkOdt { get; private set; }
        public int ProcDqOdt { get; private set; }
        public int ProcDqsOdt { get; private set; }
        public int ProcDataDsApu { get; private set; }
    }

    public sealed class ApobBlockLayout
    {
        public ApobBlockLayout(string name, int size, ApobFieldOffsets offsets)
        {
            Name = name;
            BlockSize = size;
            Offsets = offsets;
        }

        public string Name { get; private set; }
        public int BlockSize { get; private set; }
        public ApobFieldOffsets Offsets { get; private set; }
    }

    public sealed class ApobCcdlLayout
    {
        public ApobCcdlLayout(ApobBlockKind sourceBlock, byte[] magic, int ccdlBlockOffset, ApobValueWidth valueWidth)
        {
            SourceBlock = sourceBlock;
            Magic = magic;
            CcdlBlockOffset = ccdlBlockOffset;
            ValueWidth = valueWidth;
        }

        public ApobBlockKind SourceBlock { get; private set; }
        public byte[] Magic { get; private set; }
        // start of CCD_L block relative to the end of the magic sequence
        public int CcdlBlockOffset { get; private set; }
        public ApobValueWidth ValueWidth { get; private set; }
    }

    internal sealed class ApobProfile
    {
        public ApobProfile(string name, ApobBlockLayout mainLayout, ApobBlockLayout extendedLayout, ApobCcdlLayout ccdlLayout)
        {
            Name = name;
            MainLayout = mainLayout;
            ExtendedLayout = extendedLayout;
            CcdlLayout = ccdlLayout;
        }

        public string Name { get; private set; }
        public ApobBlockLayout MainLayout { get; private set; }
        public ApobBlockLayout ExtendedLayout { get; private set; }
        public ApobCcdlLayout CcdlLayout { get; private set; }
    }

    internal static class ApobProfiles
    {
        private static readonly byte[] CCDL_BLOCK_MAGIC_ZEN4 = new byte[] { 0x00, 0xD4, 0x30, 0x00 };
        private static readonly byte[] CCDL_BLOCK_MAGIC_ZEN5 = new byte[] { 0x00, 0x50, 0xC3, 0x00 };

        private static readonly ApobFieldOffsets Zen4MainOffsets = new ApobFieldOffsets(
            gdm: 0x1,
            rttNomRd: 0x2,
            rttNomWr: 0x3,
            rttWr: 0x4,
            rttPark: 0x5,
            rttParkDqs: 0x6,
            dramDataDs: 0x7,
            ckOdtA: 0x8,
            csOdtA: 0x9,
            caOdtA: 0xA,
            ckOdtB: 0xB,
            csOdtB: 0xC,
            caOdtB: 0xD,
            procOdt: 0xE,
            procDqDs: 0xF,
            procCaDs: 0x11);

        private static readonly ApobFieldOffsets Zen4ExtendedOffsets = new ApobFieldOffsets(
            gdm: 0x1,
            rttNomRd: 0x2,
            rttNomWr: 0x3,
            rttWr: 0x4,
            rttPark: 0x5,
            rttParkDqs: 0x6,
            dramDataDs: 0x7,
            ckOdtA: 0x8,
            csOdtA: 0x9,
            caOdtA: 0xA,
            ckOdtB: 0xB,
            csOdtB: 0xC,
            caOdtB: 0xD,
            procOdt: 0xE,
            procDqDs: 0xF,
            procCaDs: 0x11,
            // Zen4 extended properties
            procCkDs: 0x12,
            procCsDs: 0x13);

        private static readonly ApobFieldOffsets Zen4ApuMainOffsets = new ApobFieldOffsets(
            gdm: 0x1,
            rttNomRd: 0x2,
            rttNomWr: 0x3,
            rttWr: 0x4,
            rttPark: 0x5,
            rttParkDqs: 0x6,
            dramDataDs: 0x7,
            ckOdtA: 0x8,
            csOdtA: 0x9,
            caOdtA: 0xA,
            ckOdtB: 0xB,
            csOdtB: 0xC,
            caOdtB: 0xD,
            procOdt: 0xE,
            procDqDs: 0xF,
            // unknown_10
            procCaDs: 0x11,
            procCkDs: 0x12,
            procCsDs: 0x13);

        private static readonly ApobFieldOffsets Zen4ApuExtendedOffsets = new ApobFieldOffsets(
            gdm: 0x1,
            rttNomRd: 0x2,
            rttNomWr: 0x3,
            rttWr: 0x4,
            rttPark: 0x5,
            rttParkDqs: 0x6,
            dramDataDs: 0x7,
            ckOdtA: 0x8,
            csOdtA: 0x9,
            caOdtA: 0xA,
            ckOdtB: 0xB,
            csOdtB: 0xC,
            caOdtB: 0xD,
            procOdt: 0xE,
            procDqDs: 0xF,
            // unknown_10
            procCaDs: 0x11,
            procCkDs: 0x12,
            procCsDs: 0x13,
            procCaOdt: 0x1B,
            procCkOdt: 0x1C,
            procDqOdt: 0x1E,
            procDqsOdt: 0x1E,
            procDataDsApu: 0xF);

        private static readonly ApobFieldOffsets Zen5MainOffsets = new ApobFieldOffsets(
            gdm: 0x1,
            rttNomRd: 0x2,
            rttNomWr: 0x3,
            rttWr: 0x4,
            rttPark: 0x5,
            rttParkDqs: 0x6,
            dramDataDs: 0x7,
            ckOdtA: 0x8,
            csOdtA: 0x9,
            caOdtA: 0xA,
            ckOdtB: 0xB,
            csOdtB: 0xC,
            caOdtB: 0xD,
            procOdt: 0xE,
            procDqDs: 0xF,
            procCaDs: 0x11,
            procCkDs: 0x12,
            procCsDs: 0x13,
            rttNomRdP0: 0x1A,
            rttNomWrP0: 0x1B,
            rttWrP0: 0x1C,
            rttParkP0: 0x1D,
            rttParkDqsP0: 0x1E,
            dramDqDsPullUpP0: 0x1F,
            dramDqDsPullDownP0: 0x20,
            procOdtPullUpP0: 0x21,
            procOdtPullDownP0: 0x22,
            procDqDsPullUpP0: 0x23,
            procDqDsPullDownP0: 0x24);

        private static readonly ApobFieldOffsets Zen5ExtendedOffsets = new ApobFieldOffsets(
            gdm: 0x1,
            rttNomRd: 0x2,
            rttNomWr: 0x3,
            rttWr: 0x4,
            rttPark: 0x5,
            rttParkDqs: 0x6,
            dramDataDs: 0x7,
            ckOdtA: 0x8,
            csOdtA: 0x9,
            caOdtA: 0xA,
            ckOdtB: 0xB,
            csOdtB: 0xC,
            caOdtB: 0xD,
            procOdt: 0xE,
            procDqDs: 0xF,
            procCaDs: 0x11,
            procCkDs: 0x12,
            procCsDs: 0x13,
            rttNomRdP0: 0x1A,
            rttNomWrP0: 0x1B,
            rttWrP0: 0x1C,
            rttParkP0: 0x1D,
            rttParkDqsP0: 0x1E,
            dramDqDsPullUpP0: 0x1F,
            dramDqDsPullDownP0: 0x20,
            procOdtPullUpP0: 0x21,
            procOdtPullDownP0: 0x22,
            procDqDsPullUpP0: 0x23,
            procDqDsPullDownP0: 0x24);


        /// <summary>
        /// Zen5 APU (KrackanPoint, KrackanPoint2, StrixPoint, StrixHalo) main block offsets.
        /// These are impossible to figure out completely based on single dump and platforms without ability to adjust values from BIOS.
        /// </summary>
        private static readonly ApobFieldOffsets Zen5ApuMainOffsets = new ApobFieldOffsets(
            gdm: 0x1, // 0x00
            rttNomRd: 0x2, // 0x00
            rttNomWr: 0x3, // 0x00
            rttWr: 0x4, // 0x000
            rttPark: 0x5, // 0x04
            rttParkDqs: 0x6, // 0x04
            dramDataDs: 0x7, // 0x00
            ckOdtA: 0x8, // 0x01
            csOdtA: 0x9, // 0x01
            caOdtA: 0xA, // 0x01
            ckOdtB: 0xB, // 0x05
            csOdtB: 0xC, // 0x05
            caOdtB: 0xD, // 0x05
            procOdt: 0xE, // 0x3c
            procDqDs: 0xF, // 0x3c

            procCaOdt: 0x10, // 0x3c
            procCkOdt: 0x11, // 0x3c
            procDqOdt: 0x12, // 0x3c
            procDqsOdt: 0x13, // 0x3c
            procDataDsApu: 0x14, // 0x1e
            // unknown_15 ? // 0x0c
            procCaDs: 0x16 // 0x1e
            // unknown_16 ? // 0x1e
            // unknown_17 ? // 0x63
            // unknown_18 ? // 0x3f
            // unknown_19 ? // 0x2c
            // unknown_1A ? // 0x2c
            // unknown_1B ? // 0x01
         );


        // The extended block starts one byte later than the main block, so everything after
        // ProcOdt is shifted by 12
        private static readonly ApobFieldOffsets Zen5ApuExtendedOffsets = new ApobFieldOffsets(
            gdm: 0x1,
            rttNomRd: 0x2,
            rttNomWr: 0x3,
            rttWr: 0x4,
            rttPark: 0x5,
            rttParkDqs: 0x6,
            dramDataDs: 0x7,
            ckOdtA: 0x8,
            csOdtA: 0x9,
            caOdtA: 0xA,
            ckOdtB: 0xB,
            csOdtB: 0xC,
            caOdtB: 0xD,
            procOdt: 0xE,
            procCaDs: 0xF,

            procDataDsApu: 0x18,

            // 0x11..0x1A unidentified
            procDqDs: 0x1B,
            procCaOdt: 0x1C,
            procCkOdt: 0x1D,
            procDqOdt: 0x1E,
            procDqsOdt: 0x1F);


        private static readonly ApobBlockLayout Zen4MainLayout = new ApobBlockLayout("Zen4 19h main", 0x1A, Zen4MainOffsets);
        private static readonly ApobBlockLayout Zen4ExtendedLayout = new ApobBlockLayout("Zen4 19h extended", 0x1A, Zen4ExtendedOffsets);
        private static readonly ApobBlockLayout Zen4ApuMainLayout = new ApobBlockLayout("Zen4 APU main", 0x40, Zen4ApuMainOffsets);
        private static readonly ApobBlockLayout Zen4ApuExtendedLayout = new ApobBlockLayout("Zen4 APU extended", 0x40, Zen4ApuExtendedOffsets);

        private static readonly ApobBlockLayout Zen5MainLayout = new ApobBlockLayout("Zen5 main", 0x30, Zen5MainOffsets);
        private static readonly ApobBlockLayout Zen5ExtendedLayout = new ApobBlockLayout("Zen5 extended", 0x30, Zen5ExtendedOffsets);
        // TODO: maybe the same as Zen4 APU (8000 series), check 8000 dumps
        private static readonly ApobBlockLayout Zen5ApuMainLayout = new ApobBlockLayout("Zen5 APU main", 0x1D, Zen5ApuMainOffsets);
        // Stride is 0x1F, the anchor sits one byte in front of the block
        private static readonly ApobBlockLayout Zen5ApuExtendedLayout = new ApobBlockLayout("Zen5 APU extended", 0x20, Zen5ApuExtendedOffsets);

        // Desktop Zen4, presumably server as well (untested)
        private static readonly ApobProfile Zen4DesktopProfile = new ApobProfile(
            "Zen4 Desktop",
            Zen4MainLayout,
            Zen4ExtendedLayout,
            new ApobCcdlLayout(ApobBlockKind.Extended, CCDL_BLOCK_MAGIC_ZEN4, 0x28, ApobValueWidth.UInt32));

        // Zen4 APU, 8000 series, mobile variants untested
        private static readonly ApobProfile Zen4ApuProfile = new ApobProfile(
            "Zen4 APU",
            Zen4ApuMainLayout,
            Zen4ApuExtendedLayout,
            new ApobCcdlLayout(ApobBlockKind.Extended, CCDL_BLOCK_MAGIC_ZEN4, 0x0E, ApobValueWidth.UInt16));

        // Desktop Zen5 and mobile counterparts, like FireRange
        private static readonly ApobProfile Zen5DesktopProfile = new ApobProfile(
            "Zen5 Desktop",
            Zen5MainLayout,
            Zen5ExtendedLayout,
            new ApobCcdlLayout(ApobBlockKind.Extended, CCDL_BLOCK_MAGIC_ZEN5, 0x0E, ApobValueWidth.UInt16));

        // Mobile Zen5 (KrackanPoint, KrackanPoint2, StrixPoint)
        private static readonly ApobProfile Zen5ApuProfile = new ApobProfile(
            "Zen5 APU",
            Zen5ApuMainLayout,
            Zen5ApuExtendedLayout,
            // UInt16 at magic + 0x1A, reads 14 / 56 / 28 on a Krackan dump at MCLK 2800
            new ApobCcdlLayout(ApobBlockKind.Extended, CCDL_BLOCK_MAGIC_ZEN4, 0x1A, ApobValueWidth.UInt16));


        public static ApobProfile Resolve(CPUInfo cpuInfo)
        {
            if (cpuInfo.family == Family.FAMILY_19H)
            {
                switch (cpuInfo.codeName)
                {
                    case CodeName.Rembrandt:
                    case CodeName.HawkPoint:
                    case CodeName.Phoenix:
                    case CodeName.Phoenix2:
                        return Zen4ApuProfile;
                    default:
                        return Zen4DesktopProfile;
                }
            }

            if (cpuInfo.family == Family.FAMILY_1AH)
            {
                if (cpuInfo.smuType == SMU.SmuType.TYPE_APU2)
                {
                    return Zen5ApuProfile;
                }
                return Zen5DesktopProfile;
            }

            // treat as default?
            return Zen5DesktopProfile;
        }
    }
}
