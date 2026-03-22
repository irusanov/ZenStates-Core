using System;
using System.Runtime.InteropServices;
using System.Security.Cryptography;

namespace ZenStates.Core
{
    [StructLayout(LayoutKind.Explicit, Pack = 1)]
    public struct ApobData
    {
        //[FieldOffset(0)]
        //private readonly uint _signature;
        //[FieldOffset(0x4)]
        //private readonly byte _version;
        [FieldOffset(0x8)]
        private readonly byte _ckOdtA;
        [FieldOffset(0x9)]
        private readonly byte _csOdtA;
        [FieldOffset(0xa)]
        private readonly byte _caOdtA;
        [FieldOffset(0xb)]
        private readonly byte _ckOdtB;
        [FieldOffset(0xc)]
        private readonly byte _csOdtB;
        [FieldOffset(0xd)]
        private readonly byte _caOdtB;

        //[FieldOffset(0x1a82)]
        //private readonly byte _procCaOdt;
        //[FieldOffset(0x1a83)]
        //private readonly byte _procCkOdt;
        //[FieldOffset(0x1a84)]
        //private readonly byte _procCsOdt;

        [FieldOffset(0x11)]
        private readonly byte _procCaDs;
        [FieldOffset(0x12)]
        private readonly byte _procCkDs;
        [FieldOffset(0x13)]
        private readonly byte _procCsDs;

        [FieldOffset(0x1a)]
        private readonly byte _rttNomRdP0;
        [FieldOffset(0x1b)]
        private readonly byte _rttNomWrP0;
        [FieldOffset(0x1c)]
        private readonly byte _rttWrP0;
        [FieldOffset(0x1d)]
        private readonly byte _rttParkP0;
        [FieldOffset(0x1e)]
        private readonly byte _rttParkDqsP0;

        [FieldOffset(0x1f)]
        private readonly byte _dramDqDsPullUpP0;
        [FieldOffset(0x20)]
        private readonly byte _dramDqDsPullDownP0;

        [FieldOffset(0x21)]
        private readonly byte _procOdtPullUpP0;
        [FieldOffset(0x22)]
        private readonly byte _procOdtPullDownP0;
        [FieldOffset(0x23)]
        private readonly byte _procDqDsPullUpP0;
        [FieldOffset(0x24)]
        private readonly byte _procDqDsPullDownP0;

        /// <summary>
        /// Public fields
        /// </summary>
        //public byte Version => _version;
        //public string Signature => System.Text.Encoding.ASCII.GetString(BitConverter.GetBytes(_signature));

        // Group ODT
        public GroupOdtImpedance CkOdtA => new GroupOdtImpedance(_ckOdtA);
        public GroupOdtImpedance CsOdtA => new GroupOdtImpedance(_csOdtA);
        public GroupOdtImpedance CaOdtA => new GroupOdtImpedance(_caOdtA);
        public GroupOdtImpedance CkOdtB => new GroupOdtImpedance(_ckOdtB);
        public GroupOdtImpedance CsOdtB => new GroupOdtImpedance(_csOdtB);
        public GroupOdtImpedance CaOdtB => new GroupOdtImpedance(_caOdtB);

        //public ProcOdtImpedance ProcCkOdt => new ProcOdtImpedance(_procCkOdt);
        //public ProcOdtImpedance ProcCsOdt => new ProcOdtImpedance(_procCsOdt);
        //public ProcOdtImpedance ProcCaOdt => new ProcOdtImpedance(_procCaOdt);

        // Proc Drive Strengths
        public ProcOdtImpedance ProcCkDs => new ProcOdtImpedance(_procCkDs);
        public ProcOdtImpedance ProcCsDs => new ProcOdtImpedance(_procCsDs);
        public ProcOdtImpedance ProcCaDs => new ProcOdtImpedance(_procCaDs);

        // RTT
        public Rtt RttNomRdP0 => new Rtt(_rttNomRdP0);
        public Rtt RttNomWrP0 => new Rtt(_rttNomWrP0);
        public Rtt RttWrP0 => new Rtt(_rttWrP0);
        public Rtt RttParkP0 => new Rtt(_rttParkP0);
        public Rtt RttParkDqsP0 => new Rtt(_rttParkDqsP0);


        // DRAM DQ Drive Strength
        public DramDataDrvStren DramDqDsPullUpP0 => new DramDataDrvStren(_dramDqDsPullUpP0);
        public DramDataDrvStren DramDqDsPullDownP0 => new DramDataDrvStren(_dramDqDsPullDownP0);

        // PROC ODT and Data Drive Strength
        public ProcOdt ProcOdtPullUpP0 => new ProcOdt(_procOdtPullUpP0);
        public ProcOdt ProcOdtPullDownP0 => new ProcOdt(_procOdtPullDownP0);
        public ProcDataDrvStren ProcDqDsPullUpP0 => new ProcDataDrvStren(_procDqDsPullUpP0);
        public ProcDataDrvStren ProcDqDsPullDownP0 => new ProcDataDrvStren(_procDqDsPullDownP0);
    }
}
