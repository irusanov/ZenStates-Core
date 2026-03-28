using System.Runtime.InteropServices;

namespace ZenStates.Core
{
    // Block data is 64? bytes (0x40)
    [StructLayout(LayoutKind.Explicit, Pack = 1, Size = 0x40)]
    internal readonly struct ApobDataA4
    {
        [FieldOffset(0x1)] public readonly byte Gdm;
        [FieldOffset(0x2)] public readonly byte RttNomRd;
        [FieldOffset(0x3)] public readonly byte RttNomWr;
        [FieldOffset(0x4)] public readonly byte RttWr;
        [FieldOffset(0x5)] public readonly byte RttPark;
        [FieldOffset(0x6)] public readonly byte RttParkDqs;
        [FieldOffset(0x7)] public readonly byte DramDataDs;
        // Not changing when set in bios
        [FieldOffset(0x8)] public readonly byte CkOdtA;
        [FieldOffset(0x9)] public readonly byte CsOdtA;
        [FieldOffset(0xa)] public readonly byte CaOdtA;
        [FieldOffset(0xb)] public readonly byte CkOdtB;
        [FieldOffset(0xc)] public readonly byte CsOdtB;
        [FieldOffset(0xd)] public readonly byte CaOdtB;

        [FieldOffset(0xe)] public readonly byte ProcOdt; // ??
        [FieldOffset(0xf)] public readonly byte ProcDqDs; // Proc Data Drive Strength

        [FieldOffset(0x11)] public readonly byte ProcCaDs;
        [FieldOffset(0x12)] public readonly byte _unknown1;
        [FieldOffset(0x13)] public readonly byte _unknown2;

        [FieldOffset(0x1b)] public readonly byte ProcCaOdt;
        [FieldOffset(0x1c)] public readonly byte ProcCkOdt;
        [FieldOffset(0x1c)] public readonly byte _unknown3;
        [FieldOffset(0x1e)] public readonly byte ProcDqOdt;
        [FieldOffset(0x1e)] public readonly byte ProcDqsOdt;
    }
}
