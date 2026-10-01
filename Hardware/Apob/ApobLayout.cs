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
            // start of CCD_L block relative to the end of the magic sequence
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
        public ApobProfile(string name, ApobBlockLayout mainLayout, ApobBlockLayout extendedLayout, ApobCcdlLayout ccdlLayout,
            ApobChannelTimingLayout channelTimingLayout = null)
        {
            Name = name;
            MainLayout = mainLayout;
            ExtendedLayout = extendedLayout;
            CcdlLayout = ccdlLayout;
            ChannelTimingLayout = channelTimingLayout;
        }

        public string Name { get; private set; }
        public ApobBlockLayout MainLayout { get; private set; }
        public ApobBlockLayout ExtendedLayout { get; private set; }
        public ApobCcdlLayout CcdlLayout { get; private set; }

        /// <summary>The per channel timing blocks of the GEN configuration info entry, null when not known.</summary>
        public ApobChannelTimingLayout ChannelTimingLayout { get; private set; }
    }

    internal static class ApobProfiles
    {
        private static readonly byte[] CCDL_BLOCK_MAGIC_ZEN4 = new byte[] { 0x00, 0x00, 0x00, 0xD4, 0x30, 0x00 };
        private static readonly byte[] CCDL_BLOCK_MAGIC_ZEN5 = new byte[] { 0x00, 0x00, 0x00, 0x50, 0xC3, 0x00 };

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
            // unknown_10..17
            procCaDs: 0x18,
            procCkDs: 0x19,
            procCsDs: 0x1A,
            procCaOdt: 0x1B,
            procCkOdt: 0x1C,
            procDqOdt: 0x1D,
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
            procCsDs: 0x13);


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


        // The main block sizes are the per channel record sizes of the type 25 entry: 24 bytes on a 7950X, 29 on an
        // 8400F (Phoenix, 4 records with 2 used), 48 on Granite Ridge
        private static readonly ApobBlockLayout Zen4MainLayout = new ApobBlockLayout("Zen4 19h main", 0x18, Zen4MainOffsets);
        private static readonly ApobBlockLayout Zen4ExtendedLayout = new ApobBlockLayout("Zen4 19h extended", 0x1A, Zen4ExtendedOffsets);
        private static readonly ApobBlockLayout Zen4ApuMainLayout = new ApobBlockLayout("Zen4 APU main", 0x1D, Zen4ApuMainOffsets);
        private static readonly ApobBlockLayout Zen4ApuExtendedLayout = new ApobBlockLayout("Zen4 APU extended", 0x20, Zen4ApuExtendedOffsets);

        private static readonly ApobBlockLayout Zen5MainLayout = new ApobBlockLayout("Zen5 main", 0x30, Zen5MainOffsets);
        private static readonly ApobBlockLayout Zen5ExtendedLayout = new ApobBlockLayout("Zen5 extended", 0x30, Zen5ExtendedOffsets);
        // TODO: maybe the same as Zen4 APU (8000 series), check 8000 dumps
        private static readonly ApobBlockLayout Zen5ApuMainLayout = new ApobBlockLayout("Zen5 APU main", 0x1D, Zen5ApuMainOffsets);
        private static readonly ApobBlockLayout Zen5ApuExtendedLayout = new ApobBlockLayout("Zen5 APU extended", 0x20, Zen5ApuExtendedOffsets);

        // Per channel timing block of the GEN configuration info entry (group 7, type 3), offsets from the start
        // of the block. The field order is the same on Zen 4 (u32 fields) and Zen 5 (u16 fields, with the data
        // rate in front and two more fields after FAW).
        private static readonly ApobChannelTimingLayout Zen5ChannelTimingLayout = new ApobChannelTimingLayout(
            "Zen5 channel timings",
            7, 3,
            ApobValueWidth.UInt16,
            dataRateOffset: 0x00,
            memClkOffset: 0x02,
            halfMemClkOffset: 0x04,
            minMemClk: 800,
            maxMemClk: 7000,
            clOffset: 0x08,
            minCl: 10,
            maxCl: 100,
            // Extended copy of the ODT / drive strength record, from the timing block (0xABF and 0x112F)
            extendedRecordOffset: 0x41F,
            fields: new[]
            {
                new ApobTimingField("DfiClk", 0x04),
                new ApobTimingField("Cas", 0x08),
                new ApobTimingField("RcdWr", 0x0A),
                new ApobTimingField("RcdRd", 0x0C),
                new ApobTimingField("Rp", 0x0E),
                new ApobTimingField("Ras", 0x10),
                new ApobTimingField("Rc", 0x12),
                new ApobTimingField("Cwl", 0x14),
                new ApobTimingField("Rrds", 0x16),
                new ApobTimingField("Rrdl", 0x18),
                new ApobTimingField("Faw", 0x1A),
                new ApobTimingField("Wtrl", 0x20),
                new ApobTimingField("Wtrs", 0x22),
                new ApobTimingField("Rtp", 0x24),
                new ApobTimingField("Wr", 0x26),
                new ApobTimingField("Xs", 0x34),
                new ApobTimingField("Xp", 0x38),
                new ApobTimingField("Pd", 0x3A),
                new ApobTimingField("Refi", 0x3C),
                new ApobTimingField("Rfc1", 0x4A),
                new ApobTimingField("Rfc2", 0x50),
                new ApobTimingField("RfcSb", 0x5C),
                new ApobTimingField("Cacsh", 0x68),
                new ApobTimingField("Csh", 0x6A),
                new ApobTimingField("Ccdl", 0x8E),
                new ApobTimingField("CcdlWr", 0x90),
                new ApobTimingField("CcdlWr2", 0x92),
                new ApobTimingField("Dllk", 0x9A),
                new ApobTimingField("EcsC", 0x9C),
            });

        // Zen 4 (Raphael, Phoenix): u32 fields and no data rate, the block starts at MEMCLK
        private static readonly ApobTimingField[] Zen4TimingFields = new[]
            {
                new ApobTimingField("DfiClk", 0x04),
                new ApobTimingField("Cas", 0x0C),
                new ApobTimingField("RcdWr", 0x10),
                new ApobTimingField("RcdRd", 0x14),
                new ApobTimingField("Rp", 0x18),
                new ApobTimingField("Ras", 0x1C),
                new ApobTimingField("Rc", 0x20),
                new ApobTimingField("Cwl", 0x24),
                new ApobTimingField("Rrds", 0x28),
                new ApobTimingField("Rrdl", 0x2C),
                new ApobTimingField("Faw", 0x30),
                new ApobTimingField("Wtrl", 0x34),
                new ApobTimingField("Wtrs", 0x38),
                new ApobTimingField("Rtp", 0x3C),
                new ApobTimingField("Wr", 0x40),
                new ApobTimingField("Xs", 0x5C),
                new ApobTimingField("Xp", 0x64),
                new ApobTimingField("Pd", 0x68),
                new ApobTimingField("Refi", 0x6C),
                new ApobTimingField("Rfc1", 0x88),
                new ApobTimingField("Rfc2", 0x94),
                new ApobTimingField("RfcSb", 0xAC),
                new ApobTimingField("Cacsh", 0xC4),
                new ApobTimingField("Csh", 0xC8),
                new ApobTimingField("Ccdl", 0x100),
                new ApobTimingField("CcdlWr", 0x104),
                new ApobTimingField("CcdlWr2", 0x108),
                new ApobTimingField("Dllk", 0x118),
                new ApobTimingField("EcsC", 0x11C),
            };

        private static readonly ApobChannelTimingLayout Zen4ChannelTimingLayout = new ApobChannelTimingLayout(
            "Zen4 channel timings",
            7, 3,
            ApobValueWidth.UInt32,
            dataRateOffset: -1,
            memClkOffset: 0x00,
            halfMemClkOffset: 0x04,
            minMemClk: 800,
            maxMemClk: 7000,
            clOffset: 0x0C,
            minCl: 10,
            maxCl: 100,
            fields: Zen4TimingFields,
            // Extended copy from the timing block (0xBF3 and 0x1397 on a 7950X)
            extendedRecordOffset: 0x58F);

        // Phoenix: a timing block per memory P-state (0x180 apart, 3 per channel on an 8400F), and an extended
        // record per P-state (0x1F apart) from the first block of the channel (0xC6F and 0x14A7)
        private static readonly ApobChannelTimingLayout Zen4ApuChannelTimingLayout = new ApobChannelTimingLayout(
            "Zen4 APU channel timings",
            7, 3,
            ApobValueWidth.UInt32,
            dataRateOffset: -1,
            memClkOffset: 0x00,
            halfMemClkOffset: 0x04,
            minMemClk: 800,
            maxMemClk: 7000,
            clOffset: 0x0C,
            minCl: 10,
            maxCl: 100,
            fields: Zen4TimingFields,
            extendedRecordOffset: 0x5FF,
            extendedRecordStride: 0x1F,
            pStateBlockStride: 0x180);

        // Zen 5 APU (Krackan): the Zen 5 block, 3 memory P-states per channel (0x114 apart: DDR5-5600, 4800 and
        // 2000 on a Ryzen AI 7 350) and an extended record per P-state (0x1F apart) from the first block of the
        // channel (0xAD7 and 0x1173). The refresh fields differ from Granite Ridge: the programmed RFC1 / RFC2 /
        // RFCsb slots there (0x4A / 0x50 / 0x5C) are 0, the values are at 0x48 / 0x4E / 0x5A. Those also equal
        // the JEDEC values at 0x4C / 0x54 / 0x5E on a laptop running JEDEC timings, so they are tentative.
        private static readonly ApobChannelTimingLayout Zen5ApuChannelTimingLayout = new ApobChannelTimingLayout(
            "Zen5 APU channel timings",
            7, 3,
            ApobValueWidth.UInt16,
            dataRateOffset: 0x00,
            memClkOffset: 0x02,
            halfMemClkOffset: 0x04,
            minMemClk: 800,
            maxMemClk: 7000,
            clOffset: 0x08,
            minCl: 10,
            maxCl: 100,
            fields: new[]
            {
                new ApobTimingField("DfiClk", 0x04),
                new ApobTimingField("Cas", 0x08),
                new ApobTimingField("RcdWr", 0x0A),
                new ApobTimingField("RcdRd", 0x0C),
                new ApobTimingField("Rp", 0x0E),
                new ApobTimingField("Ras", 0x10),
                new ApobTimingField("Rc", 0x12),
                new ApobTimingField("Cwl", 0x14),
                new ApobTimingField("Rrds", 0x16),
                new ApobTimingField("Rrdl", 0x18),
                new ApobTimingField("Faw", 0x1A),
                new ApobTimingField("Wtrl", 0x20),
                new ApobTimingField("Wtrs", 0x22),
                new ApobTimingField("Rtp", 0x24),
                new ApobTimingField("Wr", 0x26),
                new ApobTimingField("Xs", 0x34),
                new ApobTimingField("Xp", 0x38),
                new ApobTimingField("Pd", 0x3A),
                new ApobTimingField("Refi", 0x3C),
                new ApobTimingField("Rfc1", 0x48, true),
                new ApobTimingField("Rfc2", 0x4E, true),
                new ApobTimingField("RfcSb", 0x5A, true),
                new ApobTimingField("Cacsh", 0x68),
                new ApobTimingField("Csh", 0x6A),
                new ApobTimingField("Ccdl", 0x8E),
                new ApobTimingField("CcdlWr", 0x90),
                new ApobTimingField("CcdlWr2", 0x92),
                new ApobTimingField("Dllk", 0x9A),
                new ApobTimingField("EcsC", 0x9C),
            },
            extendedRecordOffset: 0x44F,
            extendedRecordStride: 0x1F,
            pStateBlockStride: 0x114);

        // Desktop Zen4, presumably server as well (untested)
        private static readonly ApobProfile Zen4DesktopProfile = new ApobProfile(
            "Zen4 Desktop",
            Zen4MainLayout,
            Zen4ExtendedLayout,
            new ApobCcdlLayout(ApobBlockKind.Extended, CCDL_BLOCK_MAGIC_ZEN4, 0x28, ApobValueWidth.UInt32),
            Zen4ChannelTimingLayout);

        // Zen4 APU, 8000 series, mobile variants untested
        private static readonly ApobProfile Zen4ApuProfile = new ApobProfile(
            "Zen4 APU",
            Zen4ApuMainLayout,
            Zen4ApuExtendedLayout,
            new ApobCcdlLayout(ApobBlockKind.Extended, CCDL_BLOCK_MAGIC_ZEN4, 0x28, ApobValueWidth.UInt32),
            Zen4ApuChannelTimingLayout);

        // Desktop Zen5 and mobile counterparts, like FireRange
        private static readonly ApobProfile Zen5DesktopProfile = new ApobProfile(
            "Zen5 Desktop",
            Zen5MainLayout,
            Zen5ExtendedLayout,
            new ApobCcdlLayout(ApobBlockKind.Extended, CCDL_BLOCK_MAGIC_ZEN5, 0x0E, ApobValueWidth.UInt16),
            Zen5ChannelTimingLayout);

        // Mobile Zen5 (KrackanPoint, KrackanPoint2, StrixPoint)
        private static readonly ApobProfile Zen5ApuProfile = new ApobProfile(
            "Zen5 APU",
            Zen5ApuMainLayout,
            Zen5ApuExtendedLayout,
            // UInt16 at magic + 0x1A, reads 14 / 56 / 28 on a Krackan dump at MCLK 2800
            new ApobCcdlLayout(ApobBlockKind.Extended, CCDL_BLOCK_MAGIC_ZEN4, 0x1A, ApobValueWidth.UInt16),
            Zen5ApuChannelTimingLayout);


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

            // Unsupported
            return null;
        }
    }
}
