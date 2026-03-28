using System.Runtime.InteropServices;

namespace ZenStates.Core
{
    // Block data is 26 bytes (0x1A)
    [StructLayout(LayoutKind.Explicit, Pack = 1, Size = 0x1A)]
    internal readonly struct ApobData60
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
        // Extended data on 7000 only available in second type of block, which contains the module information
        [FieldOffset(0x12)] public readonly byte ProcCkDs;
        [FieldOffset(0x13)] public readonly byte ProcCsDs;

        /// <summary>
        /// Public fields
        /// </summary>

        //// RTT
        //public Rtt RttNomRd => new Rtt(_rttNomRd);
        //public Rtt RttNomWr => new Rtt(_rttNomWr);
        //public Rtt RttWr => new Rtt(_rttWr);
        //public Rtt RttPark => new Rtt(_rttPark);
        //public Rtt RttParkDqs => new Rtt(_rttParkDqs);

        //// Group ODT
        //public GroupOdtImpedance CkOdtA => new GroupOdtImpedance(_ckOdtA);
        //public GroupOdtImpedance CsOdtA => new GroupOdtImpedance(_csOdtA);
        //public GroupOdtImpedance CaOdtA => new GroupOdtImpedance(_caOdtA);
        //public GroupOdtImpedance CkOdtB => new GroupOdtImpedance(_ckOdtB);
        //public GroupOdtImpedance CsOdtB => new GroupOdtImpedance(_csOdtB);
        //public GroupOdtImpedance CaOdtB => new GroupOdtImpedance(_caOdtB);

        //public ProcOdt ProcOdt => new ProcOdt(_procOdt);

        //public DramDataDrvStren DramDataDs => new DramDataDrvStren(_dramDataDs);
        //public ProcDataDrvStren ProcDqDs => new ProcDataDrvStren(_procDqDs);

        //// Proc Drive Strengths
        ////public ProcOdtImpedance ProcCkDs => new ProcOdtImpedance(_procCkDs);
        ////public ProcOdtImpedance ProcCsDs => new ProcOdtImpedance(_procCsDs);
        //public ProcOdtImpedance ProcCaDs => new ProcOdtImpedance(_procCaDs);
    }
}
