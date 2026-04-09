using System.Runtime.InteropServices;

namespace ZenStates.Core
{
    // Block data is 48 bytes for 9000 (0x30)
    [StructLayout(LayoutKind.Explicit, Pack = 1, Size = 0x30)]
    internal readonly struct ApobData1Ah
    {
        [FieldOffset(0x1)] public readonly byte Gdm;
        [FieldOffset(0x2)] public readonly byte RttNomRd;
        [FieldOffset(0x3)] public readonly byte RttNomWr;
        [FieldOffset(0x4)] public readonly byte RttWr;
        [FieldOffset(0x5)] public readonly byte RttPark;
        [FieldOffset(0x6)] public readonly byte RttParkDqs;
        [FieldOffset(0x7)] public readonly byte DramDataDs;

        [FieldOffset(0x8)] public readonly byte CkOdtA;
        [FieldOffset(0x9)] public readonly byte CsOdtA;
        [FieldOffset(0xa)] public readonly byte CaOdtA;
        [FieldOffset(0xb)] public readonly byte CkOdtB;
        [FieldOffset(0xc)] public readonly byte CsOdtB;
        [FieldOffset(0xd)] public readonly byte CaOdtB;
        [FieldOffset(0xe)] public readonly byte ProcOdt;
        [FieldOffset(0xf)] public readonly byte ProcDqDs;

        [FieldOffset(0x11)] public readonly byte ProcCaDs;
        [FieldOffset(0x12)] public readonly byte ProcCkDs;
        [FieldOffset(0x13)] public readonly byte ProcCsDs;

        [FieldOffset(0x1a)] public readonly byte RttNomRdP0;
        [FieldOffset(0x1b)] public readonly byte RttNomWrP0;
        [FieldOffset(0x1c)] public readonly byte RttWrP0;
        [FieldOffset(0x1d)] public readonly byte RttParkP0;
        [FieldOffset(0x1e)] public readonly byte RttParkDqsP0;

        [FieldOffset(0x1f)] public readonly byte DramDqDsPullUpP0;
        [FieldOffset(0x20)] public readonly byte DramDqDsPullDownP0;

        [FieldOffset(0x21)] public readonly byte ProcOdtPullUpP0;
        [FieldOffset(0x22)] public readonly byte ProcOdtPullDownP0;
        [FieldOffset(0x23)] public readonly byte ProcDqDsPullUpP0;
        [FieldOffset(0x24)] public readonly byte ProcDqDsPullDownP0;
    }
}
