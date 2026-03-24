namespace ZenStates.Core
{
    public readonly struct ApobData
    {
        public ApobData(
            byte rttNomRd,
            byte rttNomWr,
            byte rttWr,
            byte rttPark,
            byte rttParkDqs,
            byte dramDataDs,
            byte ckOdtA,
            byte csOdtA,
            byte caOdtA,
            byte ckOdtB,
            byte csOdtB,
            byte caOdtB,
            byte procOdt,
            byte procDqDs,
            byte procCaDs,
            byte? procCkDs = null,
            byte? procCsDs = null,
            byte? rttNomRdP0 = null,
            byte? rttNomWrP0 = null,
            byte? rttWrP0 = null,
            byte? rttParkP0 = null,
            byte? rttParkDqsP0 = null,
            byte? dramDqDsPullUpP0 = null,
            byte? dramDqDsPullDownP0 = null,
            byte? procOdtPullUpP0 = null,
            byte? procOdtPullDownP0 = null,
            byte? procDqDsPullUpP0 = null,
            byte? procDqDsPullDownP0 = null)
        {
            _rttNomRd = rttNomRd;
            _rttNomWr = rttNomWr;
            _rttWr = rttWr;
            _rttPark = rttPark;
            _rttParkDqs = rttParkDqs;
            _dramDataDs = dramDataDs;

            _ckOdtA = ckOdtA;
            _csOdtA = csOdtA;
            _caOdtA = caOdtA;
            _ckOdtB = ckOdtB;
            _csOdtB = csOdtB;
            _caOdtB = caOdtB;

            _procOdt = procOdt;
            _procDqDs = procDqDs;
            _procCaDs = procCaDs;

            _procCkDs = procCkDs;
            _procCsDs = procCsDs;

            _rttNomRdP0 = rttNomRdP0;
            _rttNomWrP0 = rttNomWrP0;
            _rttWrP0 = rttWrP0;
            _rttParkP0 = rttParkP0;
            _rttParkDqsP0 = rttParkDqsP0;

            _dramDqDsPullUpP0 = dramDqDsPullUpP0;
            _dramDqDsPullDownP0 = dramDqDsPullDownP0;

            _procOdtPullUpP0 = procOdtPullUpP0;
            _procOdtPullDownP0 = procOdtPullDownP0;
            _procDqDsPullUpP0 = procDqDsPullUpP0;
            _procDqDsPullDownP0 = procDqDsPullDownP0;
        }

        private readonly byte _rttNomRd;
        private readonly byte _rttNomWr;
        private readonly byte _rttWr;
        private readonly byte _rttPark;
        private readonly byte _rttParkDqs;
        private readonly byte _dramDataDs;

        private readonly byte _ckOdtA;
        private readonly byte _csOdtA;
        private readonly byte _caOdtA;
        private readonly byte _ckOdtB;
        private readonly byte _csOdtB;
        private readonly byte _caOdtB;

        private readonly byte _procOdt;
        private readonly byte _procDqDs;
        private readonly byte _procCaDs;

        private readonly byte? _procCkDs;
        private readonly byte? _procCsDs;

        private readonly byte? _rttNomRdP0;
        private readonly byte? _rttNomWrP0;
        private readonly byte? _rttWrP0;
        private readonly byte? _rttParkP0;
        private readonly byte? _rttParkDqsP0;

        private readonly byte? _dramDqDsPullUpP0;
        private readonly byte? _dramDqDsPullDownP0;

        private readonly byte? _procOdtPullUpP0;
        private readonly byte? _procOdtPullDownP0;
        private readonly byte? _procDqDsPullUpP0;
        private readonly byte? _procDqDsPullDownP0;

        public Rtt RttNomRd => new Rtt(_rttNomRd);
        public Rtt RttNomWr => new Rtt(_rttNomWr);
        public Rtt RttWr => new Rtt(_rttWr);
        public Rtt RttPark => new Rtt(_rttPark);
        public Rtt RttParkDqs => new Rtt(_rttParkDqs);

        public DramDataDrvStren DramDataDs => new DramDataDrvStren(_dramDataDs);

        public GroupOdtImpedance CkOdtA => new GroupOdtImpedance(_ckOdtA);
        public GroupOdtImpedance CsOdtA => new GroupOdtImpedance(_csOdtA);
        public GroupOdtImpedance CaOdtA => new GroupOdtImpedance(_caOdtA);
        public GroupOdtImpedance CkOdtB => new GroupOdtImpedance(_ckOdtB);
        public GroupOdtImpedance CsOdtB => new GroupOdtImpedance(_csOdtB);
        public GroupOdtImpedance CaOdtB => new GroupOdtImpedance(_caOdtB);

        public ProcOdt ProcOdt => new ProcOdt(_procOdt);
        public ProcDataDrvStren ProcDqDs => new ProcDataDrvStren(_procDqDs);
        public ProcOdtImpedance ProcCaDs => new ProcOdtImpedance(_procCaDs);

        public ProcOdtImpedance ProcCkDs => _procCkDs.HasValue ? new ProcOdtImpedance(_procCkDs.Value) : null;
        public ProcOdtImpedance ProcCsDs => _procCsDs.HasValue ? new ProcOdtImpedance(_procCsDs.Value) : null;

        public Rtt RttNomRdP0 => _rttNomRdP0.HasValue ? new Rtt(_rttNomRdP0.Value) : null;
        public Rtt RttNomWrP0 => _rttNomWrP0.HasValue ? new Rtt(_rttNomWrP0.Value) : null;
        public Rtt RttWrP0 => _rttWrP0.HasValue ? new Rtt(_rttWrP0.Value) : null;
        public Rtt RttParkP0 => _rttParkP0.HasValue ? new Rtt(_rttParkP0.Value) : null;
        public Rtt RttParkDqsP0 => _rttParkDqsP0.HasValue ? new Rtt(_rttParkDqsP0.Value) : null;

        public DramDataDrvStren DramDqDsPullUpP0 => _dramDqDsPullUpP0.HasValue ? new DramDataDrvStren(_dramDqDsPullUpP0.Value) : null;
        public DramDataDrvStren DramDqDsPullDownP0 => _dramDqDsPullDownP0.HasValue ? new DramDataDrvStren(_dramDqDsPullDownP0.Value) : null;

        public ProcOdt ProcOdtPullUpP0 => _procOdtPullUpP0.HasValue ? new ProcOdt(_procOdtPullUpP0.Value) : null;
        public ProcOdt ProcOdtPullDownP0 => _procOdtPullDownP0.HasValue ? new ProcOdt(_procOdtPullDownP0.Value) : null;
        public ProcDataDrvStren ProcDqDsPullUpP0 => _procDqDsPullUpP0.HasValue ? new ProcDataDrvStren(_procDqDsPullUpP0.Value) : null;
        public ProcDataDrvStren ProcDqDsPullDownP0 => _procDqDsPullDownP0.HasValue ? new ProcDataDrvStren(_procDqDsPullDownP0.Value) : null;
    }
}