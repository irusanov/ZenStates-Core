using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Text;

#if !NET20
using System.Globalization;
#endif
using System.Reflection;
using System.Text.RegularExpressions;
using ZenStates.Core.Drivers;
using ZenStates.Core.PawnIo;
using ZenStates.Core.Common;
using ZenStates.Core.Hardware.DRAM;
using ZenStates.Core.OHWM;
using ZenStates.Core.Hardware.Smu.Commands;
using ZenStates.Core.Hardware;
using ZenStates.Core.Hardware.MutexLock;
using ZenStates.Core.Hardware.Apob;
using ZenStates.Core.Hardware.Aod;
using ZenStates.Core.Hardware.Smu;

namespace ZenStates.Core
{
    public class Cpu : IDisposable
    {
        private readonly AmdFamily17 _pawnAmd;
        private readonly RyzenSmu _pawnRyzenSmu;
        private readonly SmbusDriverBase _smbusPiix4;
        private readonly LpcIO _lpcIO;
        private bool disposedValue;
        private const string InitializationExceptionText = "CPU module initialization failed.";

        // Compiled once.
        // GetCodeName, and RegexOptions.Compiled needs Reflection.Emit, which NativeAOT lacks.
        private static readonly Regex HawkPointModelRegex =
            new Regex(@"\b80\d{2}\b", RegexOptions.CultureInvariant);

        private static readonly Regex HawkPointSeriesRegex =
            new Regex(@"Ryzen [3579](?:\s+PRO)?\s+2\d{2}\s", RegexOptions.CultureInvariant);

        public readonly Version Version = Assembly.GetExecutingAssembly().GetName().Version;

        public RyzenSmu RyzenSmu => _pawnRyzenSmu;

        public enum Family
        {
            UNSUPPORTED = 0x0,
            FAMILY_0FH = 0x0F,
            FAMILY_10H = 0x10,
            FAMILY_12H = 0x12,
            FAMILY_15H = 0x15,
            FAMILY_16H = 0x16,
            FAMILY_17H = 0x17,
            FAMILY_18H = 0x18,
            FAMILY_19H = 0x19,
            FAMILY_1AH = 0x1A,
        };

        public enum CodeName
        {
            Unsupported = 0,
            DEBUG,
            K8,
            K10,
            K12,
            K16,
            Carrizo,
            BristolRidge,
            StoneyRidge,
            Vishera,
            SummitRidge,
            Whitehaven,
            Naples,
            RavenRidge,
            PinnacleRidge,
            Colfax,
            Picasso,
            FireFlight,
            Matisse,
            CastlePeak,
            Rome,
            Dali,
            Renoir,
            VanGogh,
            Vermeer,
            Chagall,
            Milan,
            Cezanne,
            Rembrandt,
            Lucienne,
            Raphael,
            Phoenix,
            Phoenix2,
            Mendocino,
            Genoa,
            StormPeak,
            DragonRange,
            Mero,
            HawkPoint,
            StrixPoint,
            GraniteRidge,
            KrackanPoint,
            KrackanPoint2,
            StrixHalo,
            Turin,
            TurinD,
            Bergamo,
            ShimadaPeak,
            Venice,
            Annapurna,
            MustangPeak,
        };


        // CPUID_Fn80000001_EBX [BrandId Identifier] (BrandId)
        // [31:28] PkgType: package type.
        // Socket FP5/FP6 = 0
        // Socket AM4 = 2
        // Socket SP3 = 4
        // Socket TR4/TRX4 (SP3r2/SP3r3) = 7
        public enum PackageType
        {
            FPX = 0,
            AM4 = 2,
            SP3 = 4,
            TRX = 7,
        }

        public struct SVI2
        {
            public uint coreAddress;
            public uint socAddress;
        }

        public struct CpuTopology
        {
            public uint ccds;
            public uint ccxs;
            public uint coresPerCcx;
            public uint cores;
            public uint logicalCores;
            public uint physicalCores;
            public uint threadsPerCore;
            public uint cpuNodes;
            public uint[] coreDisableMap;
            public uint ccdEnableMap;
            public uint ccdDisableMap;
            public uint fuse1;
            public uint fuse2;
            public uint ccdsPresent;
            public uint ccdsDown;
            public uint[] performanceOfCore;
            /// <summary>Cores of each CCX (L3 domain) in APIC ID order; on Strix Point they differ in size. Null when unknown.</summary>
            public uint[] ccxCoreCounts;
            /// <summary>Core fuse of the first CCD (or die), the bit position of its mask and the raw value per CCD as read.</summary>
            public uint coreFuseAddress;
            public int coreFuseShift;
            public uint[] coreFuseValues;
            /// <summary>Why a fuse reading was not used (it disagreed with CPUID or could not be read); null when all was used.</summary>
            public string fuseNote;
        }

        public struct CPUInfo
        {
            public uint cpuid;
            public Family family;
            public CodeName codeName;
            public string cpuName;
            public string vendor;
            public PackageType packageType;
            public uint baseModel;
            public uint extModel;
            public uint model;
            public uint patchLevel;
            public uint stepping;
            public CpuTopology topology;
            public SVI2 svi2;
            public AOD aod;
            public Apob apob;
            public SMU.SmuType smuType;
        }

        public readonly IODriver io;
        private readonly Mmio mmio;
        public readonly CPUInfo info;
        public readonly SystemInfo systemInfo;
        public readonly SMU smu;
        public readonly PowerTable powerTable;
        public readonly MemoryConfig memoryConfig;
        public CoreOptions Options { get; }

        public IODriver.LibStatus Status { get; }

        /// <summary>
        /// The non-fatal initialization error. When several subsystems failed, an exception whose message lists all
        /// of them (see <see cref="InitErrors"/>) and whose InnerException is the first failure.
        /// </summary>
        public Exception LastError { get; }

        /// <summary>Every non-fatal error recorded during initialization, in the order they occurred.</summary>
        public ReadOnlyCollection<CpuInitError> InitErrors { get; }

        public sealed class CpuInitError
        {
            public CpuInitError(string subsystem, Exception exception)
            {
                Subsystem = subsystem;
                Exception = exception;
            }

            public string Subsystem { get; }
            public Exception Exception { get; }

            public override string ToString()
            {
                return string.Format("{0}: {1}", Subsystem, Exception != null ? Exception.Message : string.Empty);
            }
        }

        /// <summary>
        /// Where a family keeps its CCD and core fuses. Values read from debug reports and checked against the APOB core
        /// map of the same machine are marked as verified; the others are kept from earlier versions.
        /// </summary>
        private sealed class TopologyFuses
        {
            /// <summary>Several CCDs on the package; their presence is read from the CCD fuses.</summary>
            public bool Chiplet;
            /// <summary>Zen / Zen+: one die per node (Threadripper and EPYC have several), each counted as a CCD.</summary>
            public bool DiePerNode;
            /// <summary>CCD fuses: present [23:22] (CCD0/1) and down [31:30], plus CCD2~7 down in [5:0] of the second.</summary>
            public uint CcdFuse1;
            public uint CcdFuse2;
            /// <summary>Packages with up to 8 CCDs (Zen 2/3): the enabled CCDs are the ones not marked down.</summary>
            public bool CcdMapFromDown;
            /// <summary>Core disable fuse of the first CCD (or the die); 0 when not known.</summary>
            public uint CoreFuse;
            /// <summary>Bit position of the core disable mask in <see cref="CoreFuse"/>.</summary>
            public int CoreFuseShift;
            /// <summary>Address step between the core fuses of consecutive CCDs; 0 for a single die.</summary>
            public uint CoreFuseCcdStride;
            public uint CcxPerCcd;
            public uint CoreSlotsPerCcx;
        }

        private const uint CCD_FUSE_ZEN2 = 0x5D218;          // Matisse 3600 / Vermeer 5600X: 0x80400000, 0x3F
        private const uint CCD_FUSE_ZEN4 = 0x5D218 + 0x1A4;  // 0x5D3BC; Raphael 7950X: 0x00E97051, Granite Ridge 9600X: 0x024BEBA3
        private const uint CCD_CORE_FUSE_STRIDE = 1u << 25;

        private static TopologyFuses GetTopologyFuses(Family family, CodeName codeName)
        {
            switch (codeName)
            {
                // Zen / Zen+ desktop and HEDT: one die per node, 2 CCX of 4 cores. 0x5D25C [7:0], CCX0 in [3:0]
                // (verified: 1800X 0x100, 1600 AF 0x188 = cores 3 and 7 down)
                case CodeName.SummitRidge:
                case CodeName.PinnacleRidge:
                case CodeName.Whitehaven:
                case CodeName.Colfax:
                case CodeName.Naples:
                    return new TopologyFuses { DiePerNode = true, CoreFuse = 0x5D25C, CcxPerCcd = 2, CoreSlotsPerCcx = 4 };

                // Zen 2 chiplets: 2 CCX of 4 per CCD, core fuse per CCD
                case CodeName.Matisse:
                case CodeName.CastlePeak:
                case CodeName.Rome:
                    return new TopologyFuses
                    {
                        Chiplet = true,
                        CcdFuse1 = CCD_FUSE_ZEN2,
                        CcdFuse2 = CCD_FUSE_ZEN2 + 4,
                        CcdMapFromDown = true,
                        CoreFuse = 0x30081A38,
                        CoreFuseCcdStride = CCD_CORE_FUSE_STRIDE,
                        CcxPerCcd = 2,
                        CoreSlotsPerCcx = 4,
                    };

                // Zen 3 chiplets: 1 CCX of 8 per CCD
                case CodeName.Vermeer:
                case CodeName.Chagall:
                case CodeName.Milan:
                    return new TopologyFuses
                    {
                        Chiplet = true,
                        CcdFuse1 = CCD_FUSE_ZEN2,
                        CcdFuse2 = CCD_FUSE_ZEN2 + 4,
                        CcdMapFromDown = true,
                        CoreFuse = 0x30081D98,
                        CoreFuseCcdStride = CCD_CORE_FUSE_STRIDE,
                        CcxPerCcd = 1,
                        CoreSlotsPerCcx = 8,
                    };

                // Zen 4 / Zen 5 desktop: up to 2 CCDs
                case CodeName.Raphael:
                case CodeName.DragonRange:
                    return new TopologyFuses
                    {
                        Chiplet = true,
                        CcdFuse1 = CCD_FUSE_ZEN4,
                        CcdFuse2 = CCD_FUSE_ZEN4 + 4,
                        CoreFuse = 0x30081CD0,
                        CoreFuseCcdStride = CCD_CORE_FUSE_STRIDE,
                        CcxPerCcd = 1,
                        CoreSlotsPerCcx = 8,
                    };
                case CodeName.GraniteRidge:
                    return new TopologyFuses
                    {
                        Chiplet = true,
                        CcdFuse1 = CCD_FUSE_ZEN4,
                        CcdFuse2 = CCD_FUSE_ZEN4 + 4,
                        CoreFuse = 0x304A03DC,
                        CoreFuseCcdStride = CCD_CORE_FUSE_STRIDE,
                        CcxPerCcd = 1,
                        CoreSlotsPerCcx = 8,
                    };

                // Monolithic APUs: one die, no CCD fuses. The core mask sits at a bit offset in an SMUFUSE register.
                // Cezanne: 0x5D448 [18:11] (verified: 5300G 0x002D9470 = 0xB2, cores 1, 4, 5, 7 down as in its APOB)
                case CodeName.Cezanne:
                    return new TopologyFuses { CoreFuse = 0x5D448, CoreFuseShift = 11, CcxPerCcd = 1, CoreSlotsPerCcx = 8 };
                // Phoenix: 0x5D528 [14:7] (verified: 8400F 0x00002860 = 0x50, cores 4 and 6 down as in its APOB)
                case CodeName.Phoenix:
                case CodeName.HawkPoint:
                    return new TopologyFuses { CoreFuse = 0x5D528, CoreFuseShift = 7, CcxPerCcd = 1, CoreSlotsPerCcx = 8 };

                // Monolithic APUs whose core fuse layout is not known yet (Raven / Picasso 0x5D254, Renoir 0x5D3E8,
                // Rembrandt 0x5D4DC, Strix / Krackan 0x3820AB0 are candidates): counts come from CPUID only
                case CodeName.RavenRidge:
                case CodeName.Picasso:
                case CodeName.Dali:
                case CodeName.FireFlight:
                case CodeName.Mendocino:
                case CodeName.VanGogh:
                    return new TopologyFuses { CcxPerCcd = 1, CoreSlotsPerCcx = 4 };
                case CodeName.Renoir:
                case CodeName.Lucienne:
                    return new TopologyFuses { CcxPerCcd = 2, CoreSlotsPerCcx = 4 };
                case CodeName.Rembrandt:
                case CodeName.Phoenix2:
                case CodeName.Mero:
                    return new TopologyFuses { CcxPerCcd = 1, CoreSlotsPerCcx = 8 };
                // Zen 5 + Zen 5c on one die, no CCDs. The CCXs (L3 domains) come from CPUID; these values are only the
                // fallback when it can't be read. Strix Point has two L3s (Zen 5 and Zen 5c); Krackan is not confirmed.
                case CodeName.StrixPoint:
                    return new TopologyFuses { CcxPerCcd = 2 };
                case CodeName.KrackanPoint:
                case CodeName.KrackanPoint2:
                    return new TopologyFuses { CcxPerCcd = 1 };

                default:
                    // Server parts and anything newer: CCDs from CPUID, assuming one CCX per CCD
                    return new TopologyFuses { Chiplet = family >= Family.FAMILY_19H, CcxPerCcd = family >= Family.FAMILY_19H ? 1u : 2u };
            }
        }

        /// <summary>
        /// The cores of each CCX, in APIC ID order: the extended APIC ID of every core (CPUID 0x8000001E on its first
        /// thread) grouped by the L3 it shares (CPUID 0x8000001D). Null when CPUID can't tell.
        /// </summary>
        private static List<uint> GetCcxCoreCounts(CpuTopology topology)
        {
            int shift = -1;
            for (uint leaf = 0; leaf < 8; leaf++)
            {
                if (!Opcode.Cpuid(0x8000001D, leaf, out uint eax, out _, out _, out _) || (eax & 0x1F) == 0)
                    break;

                if (Utils.GetBits(eax, 5, 3) == 3)
                {
                    uint sharing = Utils.GetBits(eax, 14, 12) + 1;
                    shift = 0;
                    while ((1u << shift) < sharing)
                        shift++;
                    break;
                }
            }

            if (shift < 0 || topology.threadsPerCore == 0)
                return null;

            List<uint> ccxIds = new List<uint>();
            List<uint> counts = new List<uint>();
            for (int i = 0; i < topology.logicalCores; i += (int)topology.threadsPerCore)
            {
                if (!Opcode.CpuidTx(0x8000001E, 0, out uint apicId, out _, out _, out _, GroupAffinity.ForLogicalProcessor(i)))
                    return null;

                uint ccx = apicId >> shift;
                int index = ccxIds.IndexOf(ccx);
                if (index < 0)
                {
                    ccxIds.Add(ccx);
                    counts.Add(1);
                }
                else
                {
                    counts[index]++;
                }
            }

            return counts;
        }

        private CpuTopology GetCpuTopology(Family family, CodeName codeName, uint model)
        {
            CpuTopology topology = new CpuTopology();

            if (Opcode.Cpuid(0x00000001, 0, out uint eax, out uint ebx, out uint ecx, out uint edx))
                topology.logicalCores = Utils.GetBits(ebx, 16, 8);
            else
                throw new ApplicationException(InitializationExceptionText);

            if (Opcode.Cpuid(0x8000001E, 0, out eax, out ebx, out ecx, out edx))
            {
                topology.threadsPerCore = Utils.GetBits(ebx, 8, 4) + 1;
                topology.cpuNodes = ((ecx >> 8) & 0x7) + 1;

                if (topology.threadsPerCore == 0)
                    topology.cores = topology.logicalCores;
                else
                    topology.cores = topology.logicalCores / topology.threadsPerCore;

            }
            else
            {
                throw new ApplicationException(InitializationExceptionText);
            }

            try
            {
                topology.performanceOfCore = new uint[topology.cores];

                for (int i = 0; i < topology.logicalCores; i += (int)topology.threadsPerCore)
                {
                    uint _eax = default; uint _edx = default;
                    if (ReadMsrTx(0xC00102B3, ref _eax, ref _edx, GroupAffinity.ForLogicalProcessor(i)))
                        topology.performanceOfCore[i / topology.threadsPerCore] = _eax & 0xff;
                    else
                        topology.performanceOfCore[i / topology.threadsPerCore] = 0;
                }
            }
            catch { }

            TopologyFuses fuses = GetTopologyFuses(family, codeName);

            // CCX and core counts from CPUID: what the OS sees, whatever the fuse layout
            List<uint> ccxCores = null;
            try
            {
                ccxCores = GetCcxCoreCounts(topology);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"CPU topology: CCX enumeration failed. {ex.Message}");
            }

            if (ccxCores != null && ccxCores.Count > 0)
            {
                topology.ccxCoreCounts = ccxCores.ToArray();
                topology.ccxs = (uint)ccxCores.Count;
                foreach (uint count in ccxCores)
                    topology.coresPerCcx = Math.Max(topology.coresPerCcx, count);
            }

            uint ccxPerCcd = Math.Max(1, fuses.CcxPerCcd);
            uint ccdsFromCcx = topology.ccxs > 0 ? Math.Max(1, topology.ccxs / ccxPerCcd) : 1;

            if (!fuses.Chiplet)
            {
                // A single die, or one per node on Zen / Zen+ multi-die parts
                topology.ccds = fuses.DiePerNode ? Math.Max(1, topology.cpuNodes) : 1;
                topology.ccdEnableMap = (1u << (int)topology.ccds) - 1;
            }

            if (Mutexes.WaitPciBus(5000))
            {
                try
                {
                    if (fuses.Chiplet && fuses.CcdFuse1 != 0)
                        ReadCcdFusesNoLock(fuses, ccdsFromCcx, ref topology);

                    // The core fuses are read per enabled CCD, so the CCDs have to be known first
                    if (topology.ccds == 0)
                    {
                        topology.ccds = ccdsFromCcx;
                        topology.ccdEnableMap = (1u << (int)Math.Min(topology.ccds, 31)) - 1;
                    }

                    if (fuses.CoreFuse != 0)
                        ReadCoreFusesNoLock(fuses, ref topology);
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"Error retrieving CPU topology. {ex}");
                }
                finally
                {
                    Mutexes.ReleasePciBus();
                }
            }

            if (topology.ccds == 0)
            {
                topology.ccds = ccdsFromCcx;
                topology.ccdEnableMap = (1u << (int)Math.Min(topology.ccds, 31)) - 1;
            }

            if (topology.ccxs == 0)
                topology.ccxs = topology.ccds * ccxPerCcd;

            if (topology.coresPerCcx == 0)
                topology.coresPerCcx = Math.Max(1, topology.cores / Math.Max(1, topology.ccxs));

            topology.physicalCores = fuses.CoreSlotsPerCcx > 0
                ? topology.ccds * ccxPerCcd * fuses.CoreSlotsPerCcx
                : topology.cores;

            return topology;
        }

        private static void AddFuseNote(ref CpuTopology topology, string note)
        {
            topology.fuseNote = topology.fuseNote == null ? note : topology.fuseNote + "; " + note;
        }

        private static uint CcdDisableMap(uint ccdsPresent, uint ccdsDown)
        {
            return Utils.BitSlice(ccdsPresent, 31, 30) | (Utils.BitSlice(ccdsDown, 5, 0) << 2);
        }

        private static uint CcdEnableMap(TopologyFuses fuses, uint ccdsPresent, uint ccdsDown)
        {
            uint down = CcdDisableMap(ccdsPresent, ccdsDown);
            return fuses.CcdMapFromDown ? ~down & 0xFF : Utils.BitSlice(ccdsPresent, 23, 22) & ~down;
        }

        /// <summary>Disabled core slots of one CCD (or die) from its core fuse value.</summary>
        private static uint CoreDisableMask(TopologyFuses fuses, uint value)
        {
            uint slots = fuses.CcxPerCcd * fuses.CoreSlotsPerCcx;
            uint slotMask = slots >= 32 ? 0xFFFFFFFF : (1u << (int)slots) - 1;
            return (value >> fuses.CoreFuseShift) & slotMask;
        }

        /// <summary>
        /// The enabled CCDs from the CCD fuses. Kept only when their number matches the CCXs CPUID reports.
        /// </summary>
        private void ReadCcdFusesNoLock(TopologyFuses fuses, uint ccdsFromCcx, ref CpuTopology topology)
        {
            uint ccdsPresent = 0, ccdsDown = 0;
            if (!ReadDwordExNoLock(fuses.CcdFuse1, ref ccdsPresent) || !ReadDwordExNoLock(fuses.CcdFuse2, ref ccdsDown) ||
                ccdsPresent == 0xFFFFFFFF)
            {
                Debug.WriteLine("Could not read CCD fuse!");
                AddFuseNote(ref topology, string.Format("CCD fuse 0x{0:X5} could not be read; CCDs from CPUID", fuses.CcdFuse1));
                return;
            }

            uint ccdDisableMap = CcdDisableMap(ccdsPresent, ccdsDown);
            uint ccdEnableMap = CcdEnableMap(fuses, ccdsPresent, ccdsDown);

            topology.fuse1 = fuses.CcdFuse1;
            topology.fuse2 = fuses.CcdFuse2;
            topology.ccdsPresent = ccdsPresent;
            topology.ccdsDown = ccdsDown;

            uint enabled = Utils.CountSetBits(ccdEnableMap);
            if (enabled == 0 || (topology.ccxs > 0 && enabled != ccdsFromCcx))
            {
                string note = $"CCD fuses (0x{ccdsPresent:X8}, 0x{ccdsDown:X8}) give {enabled} CCD(s), CPUID {ccdsFromCcx}; CPUID is used";
                Debug.WriteLine(note);
                AddFuseNote(ref topology, note);
                return;
            }

            topology.ccds = enabled;
            topology.ccdEnableMap = ccdEnableMap;
            topology.ccdDisableMap = ccdDisableMap;
        }

        /// <summary>
        /// The disabled cores of each CCD (or die), one bit per core slot, in logical CCD order. The map is dropped when
        /// the cores it leaves enabled don't add up to the cores CPUID reports.
        /// </summary>
        private void ReadCoreFusesNoLock(TopologyFuses fuses, ref CpuTopology topology)
        {
            uint ccds = topology.ccds > 0 ? topology.ccds : 1;
            uint slots = fuses.CcxPerCcd * fuses.CoreSlotsPerCcx;
            uint[] map = new uint[ccds];
            uint enabledCores = 0;
            uint enableMap = topology.ccdEnableMap != 0 ? topology.ccdEnableMap : 1;
            uint[] values = new uint[ccds];

            topology.coreFuseAddress = fuses.CoreFuse;
            topology.coreFuseShift = fuses.CoreFuseShift;
            topology.coreFuseValues = values;

            int logical = 0;
            for (int physical = 0; physical < 32 && logical < ccds; physical++)
            {
                if ((enableMap & (1u << physical)) == 0)
                    continue;

                // Zen / Zen+ multi-die parts: only the fuse of the first die is reachable here; the dies match
                uint address = fuses.CoreFuse + fuses.CoreFuseCcdStride * (uint)physical;
                uint value = 0;
                bool read = ReadDwordExNoLock(address, ref value);
                values[logical] = read ? value : 0xFFFFFFFF;
                if (!read || value == 0xFFFFFFFF)
                {
                    Debug.WriteLine($"Could not read core fuse for CCD{physical}!");
                    AddFuseNote(ref topology, string.Format("Core fuse 0x{0:X8} of CCD{1} could not be read; no core map", address, physical));
                    return;
                }

                map[logical] = CoreDisableMask(fuses, value);
                enabledCores += slots - Utils.CountSetBits(map[logical]);
                logical++;
            }

            if (enabledCores != topology.cores)
            {
                string note = $"Core fuses give {enabledCores} core(s), CPUID {topology.cores}; the core map is not used";
                Debug.WriteLine(note);
                AddFuseNote(ref topology, note);
                return;
            }

            topology.coreDisableMap = map;

            if (topology.ccxs == 0)
            {
                // No CPUID enumeration: cores per CCX from the first CCD's fuse
                topology.coresPerCcx = (slots - Utils.CountSetBits(map[0])) / fuses.CcxPerCcd;
            }
        }

        public Cpu(CoreOptions options = null)
        {
            Options = options ?? new CoreOptions();
            CoreOptions.Current = Options;

#if !NET20
            CultureInfo.DefaultThreadCurrentCulture = CultureInfo.InvariantCulture;
#endif
            if (!PawnIo.PawnIo.IsInstalled)
            {
                ApplicationException notInstalled = new ApplicationException("PawnIO is not installed.");
                WriteCrashLog(notInstalled, null);
                throw notInstalled;
            }

            // Non-fatal errors are collected here and reported through InitErrors/LastError/Status.
            List<CpuInitError> errors = new List<CpuInitError>();

            try
            {
                Opcode.Open();

                info.vendor = GetVendor();
                if (info.vendor != Constants.VENDOR_AMD && info.vendor != Constants.VENDOR_HYGON)
                    throw new Exception("Not an AMD CPU");

                ReportProgress("CPUID");
                if (Opcode.Cpuid(0x00000001, 0, out uint eax, out uint ebx, out uint ecx, out uint edx))
                {
                    info.cpuid = eax;
                    info.family = (Family)(((eax & 0xf00) >> 8) + ((eax & 0xff00000) >> 20));
                    info.baseModel = (eax & 0xf0) >> 4;
                    info.extModel = (eax & 0xf0000) >> 16;
                    info.model = info.baseModel + info.extModel * 0x10;
                    info.stepping = eax & 0xf;
                    // info.logicalCores = Utils.GetBits(ebx, 16, 8);
                }
                else
                {
                    throw new ApplicationException(InitializationExceptionText);
                }

                Mutexes.Open();

                ReportProgress("PawnIO modules");
                try
                {
                    _pawnAmd = new AmdFamily17();
                    _pawnRyzenSmu = new RyzenSmu();
                    _smbusPiix4 = SmbusProvider.Instance;
                    _lpcIO = new LpcIO();
                }
                catch (Exception ex)
                {
                    throw new ApplicationException("Error initializing PawnIO AMD module.", ex);
                }

                if (info.family >= Family.FAMILY_17H && !_pawnAmd.IsLoaded)
                {
                    throw new ApplicationException(
                        "PawnIO Ryzen SMN module could not be loaded. Make sure the PawnIO driver is installed and running, and that the application runs as administrator.");
                }

                if (!_pawnRyzenSmu.IsLoaded)
                {
                    throw new ApplicationException(
                        "PawnIO Ryzen SMU module could not be loaded. Make sure the PawnIO driver is installed and running, and that the application runs as administrator.");
                }

                try
                {
                    io = new IODriver();
                }
                catch (Exception ex)
                {
                    io = null;
                    RecordError(errors, ex, "IODriver");
                }

                info.cpuName = GetCpuName();

                // Package type
                if (Opcode.Cpuid(0x80000001, 0, out eax, out ebx, out ecx, out edx))
                {
                    info.packageType = (PackageType)(ebx >> 28);
                    info.codeName = GetCodeName(info);
                    ReportProgress("SMU");
                    SMU.SetRyzenSmu(_pawnRyzenSmu);
                    smu = GetMaintainedSettings.GetByType(info.codeName);
                    smu.Hsmp.Init(this);
                    smu.Version = GetSmuVersion();
                    var tableVersionResult = GetTableVersion();
                    smu.TableVersion = tableVersionResult.TableVersion;
                    // Temporary workaround for pmt not refreshing with PawnIO module
                    // TODO: Fix in PawnIO RyzenSmu module and remove this
                    if (smu.SMU_TYPE <= SMU.SmuType.TYPE_CPU1)
                    {
                        var result = new CmdResult(6);
                        smu.SendRsmuCommand(0xE, ref result.args);
                    }
                    info.smuType = smu.SMU_TYPE;
                }
                else
                {
                    throw new ApplicationException(InitializationExceptionText);
                }

                mmio = new Mmio(info.family);
            }
            catch (Exception ex)
            {
                WriteCrashLog(ex, errors);
                Dispose(true);
                throw;
            }

            // Non-critical block
            ReportProgress("CPU topology");
            try
            {
                info.topology = GetCpuTopology(info.family, info.codeName, info.model);
            }
            catch (Exception ex)
            {
                RecordError(errors, ex, "CPU topology");
            }

            ReportProgress("Memory configuration");
            try
            {
                memoryConfig = new MemoryConfig(this);
            }
            catch (Exception ex)
            {
                RecordError(errors, ex, "MemoryConfig");
            }

            try
            {
                info.patchLevel = GetPatchLevel();
                info.svi2 = GetSVI2Info(info.codeName);
            }
            catch (Exception ex)
            {
                RecordError(errors, ex, "Patch level/SVI2");
            }

            ReportProgress("AOD");
            try
            {
                info.aod = new AOD(io, this);
            }
            catch (Exception ex)
            {
                RecordError(errors, ex, "AOD");
            }

            ReportProgress("APOB");
            try
            {
                info.apob = new Apob(info, memoryConfig);

                // Soldered LPDDR5 has no SPD device; the APOB keeps the copy the BIOS used
                memoryConfig?.UseApobSpd(info.apob.DimmSpd);
            }
            catch (Exception ex)
            {
                RecordError(errors, ex, "APOB");
            }

            ReportProgress("System info");
            try
            {
                systemInfo = new SystemInfo(info, smu, GetAgesaVersion());
            }
            catch (Exception ex)
            {
                RecordError(errors, ex, "SystemInfo");
            }

            ReportProgress("Power table");
            try
            {
                powerTable = new PowerTable(_pawnRyzenSmu, info.codeName);
                systemInfo?.AddHardware(new Svi3Hardware(powerTable));
            }
            catch (Exception ex)
            {
                RecordError(errors, ex, "PowerTable");
            }

            ReportProgress("SMU test");
            try
            {
                if (!SendTestMessage())
                    RecordError(errors, new ApplicationException("SMU is not responding to test message!"), "SMU");
            }
            catch (Exception ex)
            {
                RecordError(errors, ex, "SMU test message");
            }

            try
            {
                powerTable?.Refresh();
            }
            catch (Exception ex)
            {
                RecordError(errors, ex, "PowerTable refresh");
            }

            InitErrors = errors.AsReadOnly();
            LastError = BuildInitException(errors);
            Status = errors.Count > 0 ? IODriver.LibStatus.PARTIALLY_OK : IODriver.LibStatus.OK;
        }

        private void ReportProgress(string stage)
        {
            Action<string> callback = Options?.InitProgress;
            if (callback == null)
                return;

            try
            {
                callback(stage);
            }
            catch (Exception ex)
            {
                // Progress is informational only; never let it break initialization.
                Debug.WriteLine($"InitProgress callback failed: {ex.Message}");
            }
        }

        private const string CrashLogFileName = "zenstates-core.crash.log";

        // Appends a fatal initialization error (and any non-fatal errors recorded before it) to the crash log.
        private static void WriteCrashLog(Exception ex, List<CpuInitError> errors)
        {
            try
            {
                StringBuilder sb = new StringBuilder();
                sb.AppendFormat("[{0:yyyy-MM-dd HH:mm:ss}] ZenStates-Core fatal initialization error", DateTime.Now).AppendLine();
                sb.AppendFormat("Version: {0}", Assembly.GetExecutingAssembly().GetName().Version).AppendLine();
                sb.AppendFormat("OS: {0}", Environment.OSVersion).AppendLine();
                sb.AppendLine(ex != null ? ex.ToString() : "Unknown error");

                if (errors != null && errors.Count > 0)
                {
                    sb.AppendLine("Errors recorded before the failure:");
                    foreach (CpuInitError error in errors)
                        sb.Append("- ").AppendLine(error.Exception != null ? error.Subsystem + ": " + error.Exception : error.ToString());
                }

                sb.AppendLine();

                string path = System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, CrashLogFileName);
                System.IO.File.AppendAllText(path, sb.ToString());
            }
            catch (Exception logEx)
            {
                // Logging must never mask the original error.
                Debug.WriteLine($"Could not write crash log: {logEx.Message}");
            }
        }

        private static void RecordError(List<CpuInitError> errors, Exception ex, string subsystem)
        {
            Debug.WriteLine($"{subsystem} initialization failed: {ex?.Message}");
            errors.Add(new CpuInitError(subsystem, ex));
        }

        private static Exception BuildInitException(List<CpuInitError> errors)
        {
            if (errors.Count == 0)
                return null;

            if (errors.Count == 1)
                return errors[0].Exception;

            StringBuilder sb = new StringBuilder();
            sb.AppendFormat("{0} subsystems failed to initialize:", errors.Count);
            foreach (CpuInitError error in errors)
                sb.Append(Environment.NewLine).Append("- ").Append(error.ToString());

            return new ApplicationException(sb.ToString(), errors[0].Exception);
        }

        // [31-28] ccd index
        // [27-24] ccx index (always 0 for Zen3 where each ccd has just one ccx)
        // [23-20] core index
        public uint MakeCoreMask(uint core = 0, uint ccd = 0, uint ccx = 0)
        {
            if (info.family > Family.FAMILY_17H)
            {
                return (ccd << 28) | ((core % 8) << 20);
            }

            return (ccd << 28) | ((ccx % 2) << 24) | ((core % 4) << 20);
        }

        /// <summary>
        /// The core mask (see <see cref="MakeCoreMask"/>) of a core given by its logical index: the logical processor
        /// number divided by the threads per core, in the order the OS enumerates them. The physical CCD, CCX and
        /// core come from the APOB core map, so disabled CCDs and cores are skipped the way the firmware did.
        /// Returns null when the APOB has no core map or the index is out of range.
        /// </summary>
        public uint? MakeCoreMaskForLogicalCore(int logicalCore)
        {
            ApobCoreMapCore core = info.apob?.CoreMap?.GetLogicalCore(logicalCore);
            if (core == null)
                return null;

            return MakeCoreMask((uint)core.PhysicalCore, (uint)core.PhysicalCcd, (uint)core.PhysicalCcx);
        }

        public bool ReadDwordExNoLock(uint addr, ref uint data, int maxRetries = 10)
        {
            if (maxRetries < 1)
                maxRetries = 1;

            for (int retry = 0; retry < maxRetries; retry++)
            {
                try
                {
                    return _pawnAmd.ReadSmnNoLock(addr, out data);
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"ReadSmnNoLock(0x{addr:X8}) attempt {retry + 1} threw: {ex.Message}");
                }
            }

            data = 0;
            return false;
        }

        public bool ReadDwordEx(uint addr, ref uint data, int maxRetries = 10)
        {
            using (new PciBusLock())
            {
                return ReadDwordExNoLock(addr, ref data, maxRetries);
            }
        }

        public bool IoReadDwordEx(uint addr, ref uint data, int maxRetries = 10)
        {
            if (io == null)
                return false;

            if (!Mutexes.WaitPciBus(5000))
                return false;

            try
            {
                io.DlPortWritePortUlong(0x0CF8, addr);
                data = unchecked((uint)(io.DlPortReadPortUlong(0x0CFC)));
                return true;
            }
            catch
            {
                return false;
            }
            finally
            {
                Mutexes.ReleasePciBus();
            }
        }

        public uint ReadDwordNoLock(uint addr, int maxRetries = 10)
        {
            uint data = 0;
            ReadDwordExNoLock(addr, ref data, maxRetries);
            return data;
        }

        public bool TryReadDwordNoLock(uint addr, out uint data, int maxRetries = 10)
        {
            data = 0;
            return ReadDwordExNoLock(addr, ref data, maxRetries);
        }

        public uint ReadDword(uint addr, int maxRetries = 10)
        {
            using (new PciBusLock())
            {
                return ReadDwordNoLock(addr, maxRetries);
            }
        }

        public bool WriteDwordEx(uint addr, uint data, int maxRetries = 10)
        {
            using (new PciBusLock())
            {
                return _pawnRyzenSmu.SmuWriteRegNoLock(addr, data);
            }
        }

        // Get the core multiplier for a given logical core index.
        public double GetCoreMulti(int index = 0)
        {
            HwPstateStatus status = GetHwPstateStatus(index);

            if (info.family < Family.FAMILY_1AH)
            {
                if (status.CurCpuDfsId == 0)
                    return 0;

                return 2.0 * status.CurCpuFid / status.CurCpuDfsId;
            }

            return Utils.BitSlice(status.Value, 11, 0) * 5 / 100.0;
        }

        // Get core clock in MHz for a given logical core index and BCLK in MHz (default 100.0).
        public double GetCoreClockMhz(int index = 0, double bclkMhz = 100.0)
        {
            return GetCoreMulti(index) * bclkMhz;
        }

        public struct HwPstateStatus
        {
            private uint _value;

            public uint Value
            {
                get { return _value; }
                set { _value = value; }
            }

            public byte CurCpuFid
            {
                get { return (byte)Utils.BitSlice(_value, 7, 0); }
                set { _value = Utils.SetBits(_value, 0, 8, value); }
            }

            public byte CurCpuDfsId
            {
                get { return (byte)Utils.BitSlice(_value, 13, 8); }
                set { _value = Utils.SetBits(_value, 8, 6, value); }
            }

            public byte CurCpuVid
            {
                get { return (byte)Utils.BitSlice(_value, 21, 14); }
                set { _value = Utils.SetBits(_value, 14, 8, value); }
            }

            public byte CurHwPstate
            {
                get { return (byte)Utils.BitSlice(_value, 24, 22); }
                set { _value = Utils.SetBits(_value, 22, 3, value); }
            }
        }

        public HwPstateStatus GetHwPstateStatus(int index = 0)
        {
            if (index < 0)
                return new HwPstateStatus();

            uint threadsPerCore = info.topology.threadsPerCore == 0 ? 1 : info.topology.threadsPerCore;
            long logicalIndex = (long)index * threadsPerCore;

            if (logicalIndex > int.MaxValue)
                return new HwPstateStatus();

            // Processor groups hold up to 64 logical processors; pick group and bit accordingly.
            GroupAffinity affinity = GroupAffinity.ForLogicalProcessor((int)logicalIndex);

            if (_pawnAmd.ReadMsrTx(Constants.MSR_HW_PSTATE_STATUS, out uint _eax, out _, affinity))
            {
                return new HwPstateStatus { Value = _eax };
            }
            return new HwPstateStatus();
        }

        public bool Cpuid(uint index, ref uint eax, ref uint ebx, ref uint ecx, ref uint edx)
        {
            return Opcode.Cpuid(index, 0, out eax, out ebx, out ecx, out edx);
        }

        public bool ReadMsr(uint index, ref uint eax, ref uint edx)
        {
            return _pawnAmd.ReadMsr(index, out eax, out edx);
        }

        public bool ReadMsrTx(uint index, ref uint eax, ref uint edx, GroupAffinity affinity)
        {
            return _pawnAmd.ReadMsrTx(index, out eax, out edx, affinity);
        }

        public bool WriteMsr(uint msr, uint eax, uint edx)
        {
            bool res = true;

            for (var i = 0; i < info.topology.logicalCores; i++)
            {
                res &= _pawnAmd.WriteMsrTx(msr, eax, edx, GroupAffinity.ForLogicalProcessor(i));
            }

            return res;
        }

        //public void WriteIoPort(uint port, byte value) => Ring0.WriteIoPort(port, value);
        //public byte ReadIoPort(uint port) => Ring0.ReadIoPort(port);
        //public bool ReadPciConfig(uint pciAddress, uint regAddress, ref uint value) => Ring0.ReadPciConfig(pciAddress, regAddress, out value);
        //public bool WritePciConfig(uint pciAddress, uint regAddress, uint value) => Ring0.WritePciConfig(pciAddress, regAddress, value);
        //public uint GetPciAddress(byte bus, byte device, byte function) => Ring0.GetPciAddress(bus, device, function);

        // https://en.wikichip.org/wiki/amd/cpuid
        public CodeName GetCodeName(CPUInfo cpuInfo)
        {
            CodeName codeName = CodeName.Unsupported;

            if (cpuInfo.family == Family.FAMILY_0FH)
            {
                codeName = CodeName.K8;
            }
            else if (cpuInfo.family == Family.FAMILY_10H)
            {
                codeName = CodeName.K10;
            }
            else if (cpuInfo.family == Family.FAMILY_12H)
            {
                codeName = CodeName.K12;
            }
            else if (cpuInfo.family == Family.FAMILY_15H)
            {
                switch (cpuInfo.model)
                {
                    case 0x60:
                        codeName = CodeName.Carrizo;
                        break;
                    case 0x65:
                        codeName = CodeName.BristolRidge;
                        break;
                    case 0x70:
                        codeName = CodeName.StoneyRidge;
                        break;
                    case 0x2:
                        codeName = CodeName.Vishera;
                        break;
                }
            }
            else if (cpuInfo.family == Family.FAMILY_16H)
            {
                codeName = CodeName.K16;
            }
            else if (cpuInfo.family == Family.FAMILY_17H)
            {
                switch (cpuInfo.model)
                {
                    // Zen
                    case 0x1:
                        if (cpuInfo.packageType == PackageType.SP3)
                            codeName = CodeName.Naples;
                        else if (cpuInfo.packageType == PackageType.TRX)
                            codeName = CodeName.Whitehaven;
                        else
                            codeName = CodeName.SummitRidge;
                        break;
                    case 0x11:
                        codeName = CodeName.RavenRidge;
                        break;
                    case 0x20:
                        // Dali seems to be a newer stepping (B1) of RavenRidge (B0), otherwise identical
                        codeName = CodeName.Dali;
                        break;
                    // Zen+
                    case 0x8:
                        if (cpuInfo.packageType == PackageType.SP3 || cpuInfo.packageType == PackageType.TRX)
                            codeName = CodeName.Colfax;
                        else
                            codeName = CodeName.PinnacleRidge;
                        break;
                    case 0x18:
                        // Some APUs that have the CPUID of Picasso are in fact Dali
                        if (Utils.PartialStringMatch(info.cpuName, Constants.MISIDENTIFIED_DALI_APU))
                            codeName = CodeName.Dali;
                        else
                            codeName = CodeName.Picasso;
                        break;
                    case 0x50: // Subor Z+, CPUID 0x00850F00
                        codeName = CodeName.FireFlight;
                        break;
                    // Zen2
                    case 0x31:
                        if (cpuInfo.packageType == PackageType.TRX)
                            codeName = CodeName.CastlePeak;
                        else
                            codeName = CodeName.Rome;
                        break;
                    case 0x60:
                        codeName = CodeName.Renoir;
                        break;
                    case 0x68:
                        codeName = CodeName.Lucienne;
                        break;
                    case 0x71:
                        codeName = CodeName.Matisse;
                        break;
                    case 0x90:
                    case 0x91: // 0x00890F10 https://github.com/InstLatx64/InstLatx64/commit/2fe88fb370d1d71a96a8e78a523891e83f86fc17
                        codeName = CodeName.VanGogh;
                        break;
                    case 0x98:
                        codeName = CodeName.Mero;
                        break;
                    case 0xa0:
                        codeName = CodeName.Mendocino;
                        break;

                    default:
                        codeName = CodeName.Unsupported;
                        break;
                }
            }
            else if (cpuInfo.family == Family.FAMILY_19H)
            {
                switch (cpuInfo.model)
                {
                    case 0x1:
                        codeName = CodeName.Milan;
                        break;
                    case 0x8:
                        codeName = CodeName.Chagall;
                        break;
                    case 0x11:
                        codeName = CodeName.Genoa;
                        break;
                    case 0x18:
                        codeName = CodeName.StormPeak;
                        break;
                    case 0x21:
                        codeName = CodeName.Vermeer;
                        break;
                    case 0x44:
                        codeName = CodeName.Rembrandt;
                        break;
                    case 0x50:
                        codeName = CodeName.Cezanne;
                        break;
                    case 0x61:
                        if ((int)cpuInfo.packageType == 1)
                            codeName = CodeName.DragonRange;
                        else
                            codeName = CodeName.Raphael;
                        break;
                    case 0x74:
                    case 0x75:
                        string hawkPointName = cpuInfo.cpuName ?? string.Empty;
                        bool isHawkPoint = HawkPointModelRegex.IsMatch(hawkPointName)
                            || HawkPointSeriesRegex.IsMatch(hawkPointName);
                        codeName = isHawkPoint ? CodeName.HawkPoint : CodeName.Phoenix;
                        break;
                    case 0x78:
                        codeName = CodeName.Phoenix2;
                        break;
                    // https://github.com/InstLatx64/InstLatx64/commit/d3fd3cddc85b9a32966c54b59477b1c8eb3a60a3
                    case 0x7C:
                        codeName = CodeName.HawkPoint;
                        break;
                    case 0xA0:
                        codeName = CodeName.Bergamo;
                        break;

                    default:
                        codeName = CodeName.Unsupported;
                        break;
                }
            }
            else if (cpuInfo.family == Family.FAMILY_1AH)
            {
                switch (cpuInfo.model)
                {
                    case 0x2:
                        // Also known as Sorano?
                        codeName = CodeName.Turin;
                        break;
                    // https://github.com/InstLatx64/InstLatx64/commit/9e87330a805eb78a8c74f0b63fa767c0571c9e8b
                    case 0x8:
                        codeName = CodeName.ShimadaPeak;
                        break;
                    case 0x11:
                        codeName = CodeName.TurinD;
                        break;
                    case 0x20:
                    case 0x24:
                        codeName = CodeName.StrixPoint;
                        break;
                    case 0x44:
                        // Fire Range is the mobile variant
                        codeName = CodeName.GraniteRidge;
                        break;
                    case 0x60:
                        codeName = CodeName.KrackanPoint;
                        break;
                    // https://github.com/InstLatx64/InstLatx64/commit/66e13a582b9a7ca1b284ea03dd1e3299b8260f24
                    case 0x68:
                        codeName = CodeName.KrackanPoint2;
                        break;
                    case 0x70:
                        codeName = CodeName.StrixHalo;
                        break;

                    // Zen5 based EPYC Embedded for Network Control Planes
                    // https://github.com/InstLatx64/InstLatx64/commit/65da9371d13867485c11db0bb7835a79d1d5106f
                    case 0xD0:
                        codeName = CodeName.Annapurna;
                        break;

                    // Zen6
                    // Do we need to distinguish Venice Dense from Venice Classic?
                    // Dense
                    case 0x50:
                    case 0x90:
                    // Classic
                    case 0xA0:
                    case 0xC0:
                        codeName = CodeName.Venice;
                        break;

                    // Zen6 Threadripper
                    // https://github.com/InstLatx64/InstLatx64/commit/f2711d446543d66ccabe4f9e19f401598b73563d
                    case 0xA8:
                        codeName = CodeName.MustangPeak;
                        break;

                    default:
                        codeName = CodeName.Unsupported;
                        break;
                }
            }

            return codeName;
        }

        // SVI2 interface
        public SVI2 GetSVI2Info(CodeName codeName)
        {
            var svi = new SVI2();

            switch (codeName)
            {
                case CodeName.Carrizo:
                case CodeName.BristolRidge:
                case CodeName.StoneyRidge:
                    break;

                //Zen, Zen+
                case CodeName.SummitRidge:
                case CodeName.PinnacleRidge:
                case CodeName.RavenRidge:
                case CodeName.FireFlight:
                case CodeName.Dali:
                    svi.coreAddress = Constants.F17H_M01H_SVI_TEL_PLANE0;
                    svi.socAddress = Constants.F17H_M01H_SVI_TEL_PLANE1;
                    break;

                // Zen Threadripper/EPYC
                case CodeName.Whitehaven:
                case CodeName.Naples:
                case CodeName.Colfax:
                    svi.coreAddress = Constants.F17H_M01H_SVI_TEL_PLANE1;
                    svi.socAddress = Constants.F17H_M01H_SVI_TEL_PLANE0;
                    break;

                // Zen2 Threadripper/EPYC
                case CodeName.CastlePeak:
                case CodeName.Rome:
                    svi.coreAddress = Constants.F17H_M30H_SVI_TEL_PLANE0;
                    svi.socAddress = Constants.F17H_M30H_SVI_TEL_PLANE1;
                    break;

                // Picasso
                case CodeName.Picasso:
                    if ((smu.Version & 0xFF000000) > 0)
                    {
                        svi.coreAddress = Constants.F17H_M01H_SVI_TEL_PLANE0;
                        svi.socAddress = Constants.F17H_M01H_SVI_TEL_PLANE1;
                    }
                    else
                    {
                        svi.coreAddress = Constants.F17H_M01H_SVI_TEL_PLANE1;
                        svi.socAddress = Constants.F17H_M01H_SVI_TEL_PLANE0;
                    }
                    break;

                // Zen2
                case CodeName.Matisse:
                    svi.coreAddress = Constants.F17H_M70H_SVI_TEL_PLANE0;
                    svi.socAddress = Constants.F17H_M70H_SVI_TEL_PLANE1;
                    break;

                // Zen2 APU, Zen3 APU ?
                case CodeName.Renoir:
                case CodeName.Lucienne:
                case CodeName.Mendocino:
                case CodeName.Cezanne:
                case CodeName.VanGogh:
                case CodeName.Mero:
                case CodeName.Rembrandt:
                case CodeName.Phoenix:
                case CodeName.Phoenix2:
                case CodeName.HawkPoint:
                case CodeName.StrixPoint:
                case CodeName.KrackanPoint:
                case CodeName.KrackanPoint2:
                case CodeName.StrixHalo:
                    svi.coreAddress = Constants.F17H_M60H_SVI_TEL_PLANE0;
                    svi.socAddress = Constants.F17H_M60H_SVI_TEL_PLANE1;
                    break;

                // Zen3, Zen3 Threadripper/EPYC ?
                case CodeName.Vermeer:
                case CodeName.Raphael: // Unknown
                    svi.coreAddress = Constants.F19H_M21H_SVI_TEL_PLANE0;
                    svi.socAddress = Constants.F19H_M21H_SVI_TEL_PLANE1;
                    break;
                case CodeName.Chagall:
                case CodeName.Milan:
                    svi.coreAddress = Constants.F19H_M01H_SVI_TEL_PLANE1;
                    svi.socAddress = Constants.F19H_M01H_SVI_TEL_PLANE0;
                    break;

                default:
                    svi.coreAddress = Constants.F17H_M01H_SVI_TEL_PLANE0;
                    svi.socAddress = Constants.F17H_M01H_SVI_TEL_PLANE1;
                    break;
            }

            return svi;
        }

        public string GetVendor()
        {
            if (Opcode.Cpuid(0, 0, out uint _eax, out uint ebx, out uint ecx, out uint edx))
                return Utils.IntToStr(ebx) + Utils.IntToStr(edx) + Utils.IntToStr(ecx);
            return "";
        }

        public string GetCpuName()
        {
            string model = "";

            if (Opcode.Cpuid(0x80000002, 0, out uint eax, out uint ebx, out uint ecx, out uint edx))
                model = model + Utils.IntToStr(eax) + Utils.IntToStr(ebx) + Utils.IntToStr(ecx) + Utils.IntToStr(edx);

            if (Opcode.Cpuid(0x80000003, 0, out eax, out ebx, out ecx, out edx))
                model = model + Utils.IntToStr(eax) + Utils.IntToStr(ebx) + Utils.IntToStr(ecx) + Utils.IntToStr(edx);

            if (Opcode.Cpuid(0x80000004, 0, out eax, out ebx, out ecx, out edx))
                model = model + Utils.IntToStr(eax) + Utils.IntToStr(ebx) + Utils.IntToStr(ecx) + Utils.IntToStr(edx);

            return model.Trim();
        }

        public uint GetPatchLevel()
        {
            if (_pawnAmd.ReadMsr(0x8b, out uint eax, out _))
                return eax;

            return 0;
        }

        public bool GetOcMode()
        {
            if (info.codeName == CodeName.SummitRidge)
            {
                if (_pawnAmd.ReadMsr(0xC0010063, out uint eax, out _))
                {
                    // Summit Ridge, Raven Ridge
                    return Convert.ToBoolean((eax >> 1) & 1);
                }
                return false;
            }

            if (info.family <= Family.FAMILY_15H)
            {
                return false;
            }

            return Equals(GetPBOScalar(), 0.0f);
        }

        /// <summary>
        /// The PBO scalar, or <see cref="Constants.PBO_SCALAR_MIN"/> when it can't be read. Use <see cref="TryGetPBOScalar"/> to
        /// tell a failed read from a real value.
        /// </summary>
        public float GetPBOScalar()
        {
            return TryGetPBOScalar(out float scalar) ? scalar : Constants.PBO_SCALAR_MIN;
        }

        /// <summary>Reads the PBO scalar. False when the SMU has no such message, refuses it or returns a value out of range.</summary>
        public bool TryGetPBOScalar(out float scalar)
        {
            var cmd = new GetPBOScalar(smu);
            bool ok = cmd.Execute().Success && cmd.IsValid;
            scalar = ok ? cmd.Scalar : 0;
            return ok;
        }

        public bool SendTestMessage(uint arg = 1, Mailbox mbox = null)
        {
            var cmd = new SendTestMessage(smu, mbox);
            CmdResult result = cmd.Execute(arg);
            return result.Success && cmd.IsSumCorrect;
        }

        public uint GetSmuVersion() => _pawnRyzenSmu.GetSmuVersion();

        public double? GetBclk() => mmio?.GetBclk() ?? null;

        public Mmio.ClkGen GetStrapStatus() => mmio?.GetStrapStatus() ?? Mmio.ClkGen.ERROR;

        public bool SetBclk(double blck) => mmio?.SetBclk(blck) ?? false;

        public SMU.Status TransferTableToDram() => new TransferTableToDram(smu).Execute().status;

        public struct TableVersionResult
        {
            public uint TableVersion;
            public uint TableSize;
        }

        public TableVersionResult GetTableVersion()
        {
            return new TableVersionResult { TableVersion = _pawnRyzenSmu.PmTableVersion, TableSize = _pawnRyzenSmu.PmTableSize };
        }

        public uint GetDramBaseAddress() => new GetDramAddress(smu).Execute().args[0];

        public long GetDramBaseAddress64()
        {
            CmdResult result = new GetDramAddress(smu).Execute();
            return (long)result.args[1] << 32 | result.args[0];
        }
        public bool GetLN2Mode() => new GetLN2Mode(smu).Execute().args[0] == 1;

        public bool? IsExpoProfileActive()
        {
            var cmd = new GetEXPOProfileActive(smu);
            cmd.Execute();
            return cmd.IsEXPOProfileActive;
        }

        public SMU.Status SetStapmLimit(uint arg = 0U) => new SetSmuLimit(smu)
            .Execute(smu.Rsmu.SMU_MSG_SetStapmLimit,
                smu.Mp1Smu.SMU_MSG_SetStapmLimit, arg).status;

        public SMU.Status SetFastLimit(uint arg = 0U) => SetPPTLimit(arg);

        public SMU.Status SetSlowLimit(uint arg = 0U)
        {
            SMU.Status status;
            if (smu.SMU_TYPE == SMU.SmuType.TYPE_CPU9)
            {
                status = new SetBristolSmuLimit(smu)
                    .Execute(smu.Mp1Smu.SMU_MSG_SetSlowLimit, arg, arg).status;
            }
            else
            {
                status = new SetSmuLimit(smu)
                    .Execute(smu.Rsmu.SMU_MSG_SetSlowLimit,
                        smu.Mp1Smu.SMU_MSG_SetSlowLimit, arg).status;
            }

            return status;
        }

        public SMU.Status SetSlowTime(uint arg = 0U) => new SetSmuLimit(smu)
            .Execute(smu.Rsmu.SMU_MSG_SetSlowTime,
                smu.Mp1Smu.SMU_MSG_SetSlowTime, arg).status;

        public SMU.Status SetStapmTime(uint arg = 0U) => new SetSmuLimit(smu)
            .Execute(smu.Rsmu.SMU_MSG_SetStapmTime,
                smu.Mp1Smu.SMU_MSG_SetStapmTime, arg).status;

        public SMU.Status SetSttLimit(uint arg = 0U) => new SetSmuLimit(smu)
            .Execute(smu.Rsmu.SMU_MSG_SetSkinTempPowerLimit,
                smu.Mp1Smu.SMU_MSG_SetSkinTempPowerLimit, arg).status;

        public SMU.Status SetApuSkinTempLimit(uint arg = 0U) => new SetGpuSttLimit(smu)
            .Execute(smu.Rsmu.SMU_MSG_SetApuSkinTempLimit,
                smu.Mp1Smu.SMU_MSG_SetApuSkinTempLimit, arg).status;

        public SMU.Status SetGpuSkinTempLimit(uint arg = 0U) => new SetGpuSttLimit(smu)
            .Execute(smu.Rsmu.SMU_MSG_SetDgpuSkinTempLimit,
                smu.Mp1Smu.SMU_MSG_SetDgpuSkinTempLimit, arg).status;

        public SMU.Status SetApuSlowLimit(uint arg = 0U) => new SetSmuLimit(smu)
            .Execute(smu.Rsmu.SMU_MSG_SetApuSlowLimit,
                smu.Mp1Smu.SMU_MSG_SetApuSlowLimit, arg).status;

        public SMU.Status SetTctlMax(uint arg = 0U) => new SetTctlMax(smu)
            .Execute(arg).status;

        public SMU.Status SetPPTLimit(uint arg = 0U) => new SetSmuLimit(smu)
            .Execute(smu.Rsmu.SMU_MSG_SetFastLimit,
                smu.Mp1Smu.SMU_MSG_SetFastLimit, arg).status;

        public SMU.Status SetEDCVDDLimit(uint arg = 0U) => new SetSmuLimit(smu)
            .Execute(smu.Rsmu.SMU_MSG_SetEDCVDDLimit,
                smu.Mp1Smu.SMU_MSG_SetEDCVDDLimit, arg).status;

        public SMU.Status SetEDCSOCLimit(uint arg = 0U) => new SetSmuLimit(smu)
            .Execute(smu.Rsmu.SMU_MSG_SetEDCSocLimit,
                smu.Mp1Smu.SMU_MSG_SetEDCSocLimit, arg).status;

        public SMU.Status SetTDCVDDLimit(uint arg = 0U) => new SetSmuLimit(smu)
            .Execute(smu.Rsmu.SMU_MSG_SetTDCVDDLimit,
                smu.Mp1Smu.SMU_MSG_SetTDCVDDLimit, arg).status;

        public SMU.Status SetTDCSOCLimit(uint arg = 0U) => new SetSmuLimit(smu)
            .Execute(smu.Rsmu.SMU_MSG_SetTDCSocLimit,
                smu.Mp1Smu.SMU_MSG_SetTDCSocLimit, arg).status;

        public SMU.Status SetPsi0Current(uint arg = 0U) => new SetSmuLimit(smu)
            .Execute(smu.Rsmu.SMU_MSG_SetPsi0Current,
                smu.Mp1Smu.SMU_MSG_SetPsi0Current, arg).status;

        public SMU.Status SetPsi0SocCurrent(uint arg = 0U) => new SetSmuLimit(smu)
            .Execute(smu.Rsmu.SMU_MSG_SetPsi0SocCurrent,
                smu.Mp1Smu.SMU_MSG_SetPsi0SocCurrent, arg).status;

        public SMU.Status Psi3CpuCurrent(uint arg = 0U) => new SetSmuLimit(smu)
            .Execute(smu.Rsmu.SMU_MSG_SetPsi3CpuCurrent,
                smu.Mp1Smu.SMU_MSG_SetPsi3CpuCurrent, arg).status;

        public SMU.Status Psi3GfxCurrent(uint arg = 0U) => new SetSmuLimit(smu)
            .Execute(smu.Rsmu.SMU_MSG_SetPsi3GfxCurrent,
                smu.Mp1Smu.SMU_MSG_SetPsi3GfxCurrent, arg).status;

        public SMU.Status SetProchotDeassertionRamp(uint arg = 0U) => new SetProchotDeassertionRamp(smu)
            .Execute(smu.Rsmu.SMU_MSG_SetProchotDeassertionRamp,
                smu.Mp1Smu.SMU_MSG_SetProchotDeassertionRamp, arg).status;

        public SMU.Status SetBristolStapmLimit(uint stapmLimit, uint stapmTime) => new SetBristolSustainPowerLimit(smu)
            .Execute(smu.Mp1Smu.SMU_MSG_SetStapmLimit, stapmLimit, stapmTime).status;

        public SMU.Status SetBristolTdcLimit(uint vddCurrent, uint socCurrent) => new SetBristolSmuLimit(smu)
            .Execute(smu.Mp1Smu.SMU_MSG_SetTDCVDDLimit, vddCurrent, socCurrent).status;

        public SMU.Status SetBristolEdcLimit(uint vddCurrent, uint socCurrent) => new SetBristolSmuLimit(smu)
            .Execute(smu.Mp1Smu.SMU_MSG_SetEDCVDDLimit, vddCurrent, socCurrent).status;

        public SMU.Status SetBristolPsi0Limit(uint vddCurrent, uint socCurrent) => new SetBristolSmuLimit(smu)
            .Execute(smu.Mp1Smu.SMU_MSG_SetPsi0Current, vddCurrent, socCurrent).status;

        public SMU.Status SetFixedGfxClkFreq(uint arg) => new SetFixedGfxClk(smu).Execute(arg).status;

        public SMU.Status SetGfxClkOverdrive(uint freq, uint vid) => new SetGfxClkOverdrive(smu).Execute(freq, vid).status;

        public SMU.Status SetOverclockCpuVid(uint arg) => new SetOverclockCpuVid(smu).Execute(arg).status;

        public SMU.Status EnableOcMode() => new SetOcMode(smu).Execute(true).status;

        public SMU.Status DisableOcMode() => new SetOcMode(smu).Execute(false).status;

        public SMU.Status StartBtcMode(uint mode = 0) => new SetBtcMode(smu).Execute(true, mode).status;

        public SMU.Status StopBtcMode() => new SetBtcMode(smu).Execute(false).status;

        public SMU.Status ManageSmuFeatureState(bool enabled, int bit = 0) => new SetSmuFeature(smu).Execute(enabled, bit).status;

        public SMU.Status SetPowerSavingMode(bool maxPerformance) => new SetPowerSavingMode(smu).Execute(maxPerformance).status;

        public SMU.Status SetPBOScalar(uint scalar) => new SetPBOScalar(smu).Execute(scalar).status;

        public SMU.Status RefreshPowerTable()
        {
            if (powerTable == null)
                return SMU.Status.FAILED;

            SMU.Status status = powerTable.Refresh();

            if (smu != null && smu.TableVersion == 0 && _pawnRyzenSmu != null)
                smu.TableVersion = _pawnRyzenSmu.PmTableVersion;

            return status;
        }

        public int GetCorePerformanceData(uint index)
        {
            CmdResult result = new GetCorePerformanceData(smu).Execute(index);
            if (result.Success)
            {
                return (int)result.args[0];
            }
            return -1;
        }

        [Flags]
        public enum OcCapabilities : uint
        {
            None = 0,
            OverclockingEnabled = 0x1,
            PowerLimitsAvailable = 0x2,
            PboAvailable = 0x4,
            GfxOverclockingAvailable = 0x10,
            ExpoProfilesAvailable = 0x20,
            GfxOverclockingEnabled = 0x20000
        }

        public struct OcCaps
        {
            private readonly OcCapabilities _caps;

            public OcCaps(uint raw)
            {
                _caps = (OcCapabilities)raw;
            }

            public bool this[OcCapabilities flag] => (_caps & flag) != 0;
        }

        public OcCaps GetOverclockingCaps()
        {
            GetIsOverclockable cmd = new GetIsOverclockable(smu);
            CmdResult result = cmd.Execute();

            if (result.Success)
                return cmd.Capabilities;

            return new OcCaps(0);
        }

        public struct PboFusedLimits
        {
            public int PowerLimit;
            public int SlowLimit;
            public int FastLimit;
            public int ApuSlowLimit;
            public int VrmVddTdcCurrent;
            public int VrmSocTdcCurrent;
        }

        public PboFusedLimits? GetPboFusedLimits()
        {
            GetPboFusedLimits cmd = new GetPboFusedLimits(smu);
            CmdResult result = cmd.Execute();

            if (result.Success)
                return cmd.Limits;

            return null;
        }

        public struct SystemPowerLimit
        {
            public int PowerLimit;
            public int TemperatureLimit;
        }

        public SystemPowerLimit? GetSystemPowerLimit()
        {
            GetSystemConfiguredPowerLimit cmd = new GetSystemConfiguredPowerLimit(smu);
            CmdResult result = cmd.Execute();

            if (result.Success && cmd.Limits.PowerLimit > 0)
                return cmd.Limits;

            return null;
        }

        public enum CpuSubsystem
        {
            Cpu,
            Gpu,
            Soc,
            Fclk,
            Vcn,
            Lclk
        }

        public SMU.Status SetCpuSubsystemFrequencyLimit(CpuSubsystem subsystem, uint freq, bool maximum = true) => new SetCpuSubsystemFrequency(smu).Execute(subsystem, freq, maximum).status;

        public uint? GetGpuPsmMargin()
        {
            CmdResult result = new GetGpuPsmMargin(smu).Execute();
            return result.Success ? result.args[0] : (uint?)null;
        }

        public bool SetGpuPsmMargin(int margin) => new SetGpuPsmMargin(smu)
            .Execute(margin).Success;

        public uint? GetPsmMarginSingleCore(uint coreMask)
        {
            CmdResult result = new GetPsmMarginSingleCore(smu).Execute(coreMask);
            return result.Success ? result.args[0] : (uint?)null;
        }

        public uint? GetPsmMarginSingleCore(uint core, uint ccd, uint ccx)
        {
            if (smu.SMU_TYPE >= SMU.SmuType.TYPE_APU0 && smu.SMU_TYPE <= SMU.SmuType.TYPE_APU2)
            {
                return GetPsmMarginSingleCore(core);
            }
            return GetPsmMarginSingleCore(MakeCoreMask(core, ccd, ccx));
        }

        public bool SetPsmMarginAllCores(int margin) => new SetPsmMarginAllCores(smu)
            .Execute(margin).Success;

        public bool SetPsmMarginSingleCore(uint coreMask, int margin) => new SetPsmMarginSingleCore(smu)
            .Execute(coreMask, margin).Success;

        public bool SetPsmMarginSingleCore(uint core, uint ccd, uint ccx, int margin) =>
            SetPsmMarginSingleCore(MakeCoreMask(core, ccd, ccx), margin);

        public SMU.Status SetCurveShaperMargin(int marginHigh = 0, int marginMedium = 0, int marginLow = 0, int frequencyTier = 0) =>
            new SetCurveShaperMargin(smu).Execute(marginHigh, marginMedium, marginLow, frequencyTier).status;

        public uint[] GetAllCurveShaperMargins() => new GetAllCurveShaperMargins(smu).Execute().args;

        public bool SetFrequencyAllCore(uint frequency) => new SetFrequencyAllCore(smu)
            .Execute(frequency).Success;

        public bool SetFrequencySingleCore(uint coreMask, uint frequency) => new SetFrequencySingleCore(smu)
            .Execute(coreMask, frequency).Success;

        public bool SetFrequencySingleCore(uint core, uint ccd, uint ccx, uint frequency) =>
            SetFrequencySingleCore(MakeCoreMask(core, ccd, ccx), frequency);

        private bool SetFrequencyMultipleCores(uint mask, uint frequency, int count)
        {
            // ((i.CCD << 4 | i.CCX % 2 & 0xF) << 4 | i.CORE % 4 & 0xF) << 20;
            for (uint i = 0; i < count; i++)
            {
                mask = Utils.SetBits(mask, 20, 3, i);
                if (!SetFrequencySingleCore(mask, frequency))
                    return false;
            }
            return true;
        }

        public bool SetFrequencyCCX(uint mask, uint frequency) =>
            SetFrequencyMultipleCores(mask, frequency, 8/*SI.NumCoresInCCX*/);

        public bool SetFrequencyCCD(uint mask, uint frequency)
        {
            if (info.topology.ccds == 0)
                return false;

            bool ret = true;
            for (uint i = 0; i < info.topology.ccxs / info.topology.ccds; i++)
            {
                mask = Utils.SetBits(mask, 24, 1, i);
                ret &= SetFrequencyCCX(mask, frequency);
            }

            return ret;
        }

        public uint GetFMax() => new GetBoostLimitFrequency(smu).Execute().args[0];

        public bool SetFMax(uint frequency) => new SetBoostLimitAllCore(smu).Execute(frequency).Success;

        public int GetCurrentHwVid()
        {
            if (!Mutexes.WaitPciBus(5000))
            {
                Debug.WriteLine("GetCurrentHwVid: Timeout waiting for PCI bus mutex");
                return -1;
            }

            try
            {
                uint data = 0;
                uint address = 0;

                if (smu.SMU_TYPE == SMU.SmuType.TYPE_APU0 || smu.SMU_TYPE < SMU.SmuType.TYPE_CPU2)
                {
                    address = 0x5A04C;
                }
                else if (smu.SMU_TYPE == SMU.SmuType.TYPE_APU1 || smu.SMU_TYPE == SMU.SmuType.TYPE_APU2)
                {
                    address = 0x6F05C;
                    if (smu.SMU_TYPE == SMU.SmuType.TYPE_APU2)
                    {
                        if (ReadDwordExNoLock(address, ref data))
                            return (int)((data >> 6) & 0x1FF);
                    }
                }
                else if (smu.SMU_TYPE == SMU.SmuType.TYPE_CPU2 || smu.SMU_TYPE == SMU.SmuType.TYPE_CPU3)
                {
                    address = (uint)(info.packageType == PackageType.AM4 ? 0x5A050 : 0x5A054);
                }
                else if (info.family > Family.FAMILY_17H)
                {
                    address = 0x73014;
                    if (ReadDwordExNoLock(address, ref data))
                        return (int)((data >> 6) & 0x1FF);
                }

                if (address != 0 && ReadDwordExNoLock(address, ref data))
                    return (int)(data >> 24);
            }
            finally
            {
                Mutexes.ReleasePciBus();
            }
            return -1;
        }

        public bool? IsProchotEnabled()
        {
            if (!Mutexes.WaitPciBus(5000))
            {
                Debug.WriteLine("IsProchotEnabled: Timeout waiting for PCI bus mutex");
                return null;
            }

            try
            {
                uint data = ReadDwordNoLock(0x59804);
                return (data & 1) == 1;
            }
            finally
            {
                Mutexes.ReleasePciBus();
            }
        }

        public float? GetCpuTemperature()
        {
            if (!Mutexes.WaitPciBus(5000))
            {
                Debug.WriteLine("GetCpuTemperature: Timeout waiting for PCI bus mutex");
                return null;
            }

            try
            {
                uint thmData = 0;

                if (ReadDwordExNoLock(Constants.THM_CUR_TEMP, ref thmData))
                {
                    float offset = 0.0f;

                    // Get tctl temperature offset
                    // Offset table: https://github.com/torvalds/linux/blob/master/drivers/hwmon/k10temp.c#L78
                    if (info.cpuName.Contains("2700X"))
                        offset = -10.0f;
                    else if (info.cpuName.Contains("1600X") || info.cpuName.Contains("1700X") || info.cpuName.Contains("1800X"))
                        offset = -20.0f;
                    else if (info.cpuName.Contains("Threadripper 19") || info.cpuName.Contains("Threadripper 29"))
                        offset = -27.0f;

                    // THMx000[31:21] = CUR_TEMP, THMx000[19] = CUR_TEMP_RANGE_SEL
                    // Range sel = 0 to 255C (Temp = Tctl - offset)
                    float temperature = (thmData >> 21) * 0.125f + offset;

                    // Range sel = -49 to 206C (Temp = Tctl - offset - 49), also selected by TJ_SEL [17:16] = 3
                    // (Linux k10temp)
                    if ((thmData & Constants.THM_CUR_TEMP_RANGE_SEL_MASK) != 0 ||
                        (thmData & Constants.THM_CUR_TEMP_TJ_SEL_MASK) == Constants.THM_CUR_TEMP_TJ_SEL_MASK)
                        temperature -= 49.0f;

                    return temperature;
                }

                return null;
            }
            finally
            {
                Mutexes.ReleasePciBus();
            }
        }

        /// <summary>
        /// Temperature of one CCD in degrees C, or null when it can't be read or the CCD reports no valid reading
        /// (bit 11 clear, e.g. an absent CCD).
        /// </summary>
        public float? GetSingleCcdTemperature(uint ccd)
        {
            if (!Mutexes.WaitPciBus(5000))
            {
                Debug.WriteLine("GetSingleCcdTemperature: Timeout waiting for PCI bus mutex");
                return null;
            }

            try
            {
                uint thmData = 0;

                if (ReadDwordExNoLock(GetCcdTemperatureRegister() + (ccd * 0x4), ref thmData) &&
                    thmData != 0xFFFFFFFF && (thmData & Constants.THM_CCD_TEMP_VALID) != 0)
                {
                    return (thmData & Constants.THM_CCD_TEMP_MASK) * 0.125f - 49.0f;
                }

                return null;
            }
            finally
            {
                Mutexes.ReleasePciBus();
            }
        }

        /// <summary>
        /// SMN address of the first CCD temperature register: THM base 0x59800 plus a per family / model offset, as in
        /// Linux k10temp (k10temp_probe). Models it does not list keep the register used so far for their family.
        /// </summary>
        private uint GetCcdTemperatureRegister()
        {
            uint model = info.model;

            switch (info.family)
            {
                case Family.FAMILY_17H:
                    if (model >= 0xA0 && model <= 0xAF)
                        return Constants.THM_CUR_TEMP + 0x300;      // Mendocino
                    return Constants.F17H_CCD_TEMP;                  // 0x154

                case Family.FAMILY_19H:
                    if (model <= 0x0F || (model >= 0x20 && model <= 0x2F) || (model >= 0x50 && model <= 0x5F))
                        return Constants.F17H_CCD_TEMP;              // Milan, Chagall, Vermeer, Cezanne: 0x154
                    if ((model >= 0x10 && model <= 0x1F) || (model >= 0x40 && model <= 0x4F) || (model >= 0xA0 && model <= 0xAF))
                        return Constants.THM_CUR_TEMP + 0x300;      // Genoa, Rembrandt, Storm Peak
                    return Constants.F19H_CCD_TEMP;                  // Raphael, Phoenix (0x60~0x7F): 0x308

                case Family.FAMILY_1AH:
                    if (model <= 0x1F)
                        return Constants.THM_CUR_TEMP + 0x1F0;      // Turin
                    return Constants.F19H_CCD_TEMP;                  // Granite Ridge (0x40~0x4F): 0x308

                default:
                    return info.family > Family.FAMILY_1AH ? Constants.F19H_CCD_TEMP : Constants.F17H_CCD_TEMP;
            }
        }

        private string GetAgesaVersion()
        {
            if (io == null)
            {
                try
                {
                    return SMBiosSingleton.Instance.Bios.AgesaVersion;
                }
                catch
                {
                    return string.Empty;
                }
            }

            try
            {
                var data = io.ReadMemory(new IntPtr(0xE0000), (int)(0xFFFFF - 0xE0000));
                return AgesaUtils.ParseVersion(data);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Could not find AGESA version: {ex.Message}");
            }

            return string.Empty;
        }

        public MemoryConfig GetMemoryConfig() => memoryConfig;

        protected virtual void Dispose(bool disposing)
        {
            if (!disposedValue)
            {
                if (disposing)
                {
                    _pawnAmd?.Close();
                    _pawnRyzenSmu?.Dispose();
                    _smbusPiix4?.Dispose();
                    _lpcIO?.Close();
                    systemInfo?.Dispose();
                    io?.Dispose();
                    Mutexes.Close();
                    Opcode.Close();
                }

                disposedValue = true;
            }
        }
        public void Dispose()
        {
            // Do not change this code. Put cleanup code in 'Dispose(bool disposing)' method
            Dispose(disposing: true);
            GC.SuppressFinalize(this);
        }
    }
}
