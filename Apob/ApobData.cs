using System;
using System.Runtime.InteropServices;
using System.Security.Cryptography;

namespace ZenStates.Core
{
    [StructLayout(LayoutKind.Explicit, Pack = 1)]
    public struct ApobData
    {
        [FieldOffset(0)]
        private readonly uint _signature;
        [FieldOffset(0x4)]
        private readonly byte _version;
        [FieldOffset(0x1a7c)]
        private readonly byte _ckOdtA;
        [FieldOffset(0x1a7d)]
        private readonly byte _csOdtA;
        [FieldOffset(0x1a7e)]
        private readonly byte _caOdtA;
        [FieldOffset(0x1a7f)]
        private readonly byte _ckOdtB;
        [FieldOffset(0x1a80)]
        private readonly byte _csOdtB;
        [FieldOffset(0x1a81)]
        private readonly byte _caOdtB;

        [FieldOffset(0x1a82)]
        private readonly byte _procCaDs;
        [FieldOffset(0x1a83)]
        private readonly byte _procCkDs;
        [FieldOffset(0x1a84)]
        private readonly byte _procCsDs;

        [FieldOffset(0x1a8e)]
        private readonly byte _rttNomRdP0;
        [FieldOffset(0x1a8f)]
        private readonly byte _rttNomWrP0;
        [FieldOffset(0x1a90)]
        private readonly byte _rttWrP0;
        [FieldOffset(0x1a91)]
        private readonly byte _rttParkP0;
        [FieldOffset(0x1a92)]
        private readonly byte _rttParkDqsP0;

        [FieldOffset(0x1a93)]
        private readonly byte _dramDqDsPullUpP0;
        [FieldOffset(0x1a94)]
        private readonly byte _dramDqDsPullDownP0;

        [FieldOffset(0x1a95)]
        private readonly byte _procOdtPullUpP0;
        [FieldOffset(0x1a96)]
        private readonly byte _procOdtPullDownP0;
        [FieldOffset(0x1a97)]
        private readonly byte _procDqDsPullUpP0;
        [FieldOffset(0x1a98)]
        private readonly byte _procDqDsPullDownP0;

        /// <summary>
        /// Public fields
        /// </summary>
        public byte Version => _version;
        public string Signature => System.Text.Encoding.ASCII.GetString(BitConverter.GetBytes(_signature));
        
        // Group ODT
        public GroupOdtImpedance CkOdtA => new GroupOdtImpedance(_ckOdtA);
        public GroupOdtImpedance CsOdtA => new GroupOdtImpedance(_csOdtA);
        public GroupOdtImpedance CaOdtA => new GroupOdtImpedance(_caOdtA);
        public GroupOdtImpedance CkOdtB => new GroupOdtImpedance(_ckOdtB);
        public GroupOdtImpedance CsOdtB => new GroupOdtImpedance(_csOdtB);
        public GroupOdtImpedance CaOdtB => new GroupOdtImpedance(_caOdtB);

        // Proc Drive Strengths
        public ProcDataDrvStren ProcCkDs => new ProcDataDrvStren(_procCkDs);
        public ProcDataDrvStren ProcCsDs => new ProcDataDrvStren(_procCsDs);
        public ProcDataDrvStren ProcCaDs => new ProcDataDrvStren(_procCaDs);

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
