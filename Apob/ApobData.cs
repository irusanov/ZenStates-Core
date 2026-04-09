namespace ZenStates.Core
{
    public readonly struct ApobData
    {
        public ApobData(
            byte? rttNomRd = null,
            byte? rttNomWr = null,
            byte? rttWr = null,
            byte? rttPark = null,
            byte? rttParkDqs = null,
            byte? dramDataDs = null,
            byte? ckOdtA = null,
            byte? csOdtA = null,
            byte? caOdtA = null,
            byte? ckOdtB = null,
            byte? csOdtB = null,
            byte? caOdtB = null,
            byte? procOdt = null,
            byte? procDqDs = null,
            byte? procCaDs = null,
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
            byte? procDqDsPullDownP0 = null,
            byte? procCaOdt = null,
            byte? procCkOdt = null,
            byte? procDqOdt = null,
            byte? procDqsOdt = null,
            byte? procDataDsApu = null)
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

            _procCaOdt = procCaOdt;
            _procCkOdt = procCkOdt;
            _procDqOdt = procDqOdt;
            _procDqsOdt = procDqsOdt;
            _procDataDsApu = procDataDsApu;
        }

        private readonly byte? _rttNomRd;
        private readonly byte? _rttNomWr;
        private readonly byte? _rttWr;
        private readonly byte? _rttPark;
        private readonly byte? _rttParkDqs;
        private readonly byte? _dramDataDs;

        private readonly byte? _ckOdtA;
        private readonly byte? _csOdtA;
        private readonly byte? _caOdtA;
        private readonly byte? _ckOdtB;
        private readonly byte? _csOdtB;
        private readonly byte? _caOdtB;

        private readonly byte? _procOdt;
        private readonly byte? _procDqDs;
        private readonly byte? _procCaDs;

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

        private readonly byte? _procCaOdt;
        private readonly byte? _procCkOdt;
        private readonly byte? _procDqOdt;
        private readonly byte? _procDqsOdt;
        private readonly byte? _procDataDsApu;

        public Rtt RttNomRd => _rttNomRd.HasValue ? new Rtt(_rttNomRd.Value) : null;
        public Rtt RttNomWr => _rttNomWr.HasValue ? new Rtt(_rttNomWr.Value) : null;
        public Rtt RttWr => _rttWr.HasValue ? new Rtt(_rttWr.Value) : null;
        public Rtt RttPark => _rttPark.HasValue ? new Rtt(_rttPark.Value) : null;
        public Rtt RttParkDqs => _rttParkDqs.HasValue ? new Rtt(_rttParkDqs.Value) : null;

        public DramDataDrvStren DramDataDs => _dramDataDs.HasValue ? new DramDataDrvStren(_dramDataDs.Value) : null;

        public GroupOdtImpedance CkOdtA => _ckOdtA.HasValue ? new GroupOdtImpedance(_ckOdtA.Value) : null;
        public GroupOdtImpedance CsOdtA => _csOdtA.HasValue ? new GroupOdtImpedance(_csOdtA.Value) : null;
        public GroupOdtImpedance CaOdtA => _caOdtA.HasValue ? new GroupOdtImpedance(_caOdtA.Value) : null;
        public GroupOdtImpedance CkOdtB => _ckOdtB.HasValue ? new GroupOdtImpedance(_ckOdtB.Value) : null;
        public GroupOdtImpedance CsOdtB => _csOdtB.HasValue ? new GroupOdtImpedance(_csOdtB.Value) : null;
        public GroupOdtImpedance CaOdtB => _caOdtB.HasValue ? new GroupOdtImpedance(_caOdtB.Value) : null;

        public ProcOdt ProcOdt => _procOdt.HasValue ? new ProcOdt(_procOdt.Value) : null;
        public ProcDataDrvStren ProcDqDs => _procDqDs.HasValue ? new ProcDataDrvStren(_procDqDs.Value) : null;
        public ProcOdtImpedance ProcCaDs => _procCaDs.HasValue ? new ProcOdtImpedance(_procCaDs.Value) : null;
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

        // AM5 APU
        public ProcOdtImpedance ProcCaOdt => _procCaOdt.HasValue ? new ProcOdtImpedance(_procCaOdt.Value) : null;
        public ProcOdtImpedance ProcCkOdt => _procCkOdt.HasValue ? new ProcOdtImpedance(_procCkOdt.Value) : null;
        public ProcOdtImpedance ProcDqOdt => _procDqOdt.HasValue ? new ProcOdtImpedance(_procDqOdt.Value) : null;
        public ProcOdtImpedance ProcDqsOdt => _procDqsOdt.HasValue ? new ProcOdtImpedance(_procDqsOdt.Value) : null;
        public CadBusDrvStren ProcDataDsApu => _procDataDsApu.HasValue ? new CadBusDrvStren(_procDataDsApu.Value) : null;
    }
}