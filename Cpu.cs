using OpenHardwareMonitor.Hardware;
using System;
using System.IO;
using System.Reflection;
using ZenStates.Core.DRAM;

namespace ZenStates.Core
{
    public class Cpu : IDisposable
    {
        private bool disposedValue;
        private const string InitializationExceptionText = "CPU module initialization failed.";
        public readonly string Version = ((AssemblyFileVersionAttribute)Attribute.GetCustomAttribute(
                Assembly.GetExecutingAssembly(),
                typeof(AssemblyFileVersionAttribute), false)).Version;

        public enum Family
        {
            UNSUPPORTED = 0x0,
            FAMILY_15H = 0x15,
            FAMILY_17H = 0x17,
            FAMILY_18H = 0x18,
            FAMILY_19H = 0x19,
            FAMILY_1AH = 0x1A,
        };

        public enum CodeName
        {
            Unsupported = 0,
            DEBUG,
            BristolRidge,
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
            public uint coreDisableMap;
            public uint ccdEnableMap;
            public uint ccdDisableMap;
            public uint fuse1;
            public uint fuse2;
            public uint coreFuse;
            public uint[] performanceOfCore;
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
        }

        public readonly IOModule io = new IOModule();
        private readonly ACPI_MMIO mmio;
        public readonly CPUInfo info;
        public readonly SystemInfo systemInfo;
        public readonly SMU smu;
        public readonly PowerTable powerTable;
        public readonly MemoryConfig memoryConfig;

        public IOModule.LibStatus Status { get; }
        public Exception LastError { get; }

        /**
         * Core fuse
         * CastlePeak: 0x30081A38
         * Cezanne: 0x5D449
         * Chagall: 0x30081D98
         * Colfax: 0x5D25C
         * Matisse: 0x30081A38
         * Picasso: 0x5D254
         * Pinnacle: 0x5D25C
         * Raphael: 0x30081CD0
         * Raven2: 0x5D254
         * Raven:  0x5D254
         * Rembrandt: 0x5D4DC
         * Renoir: 0x5D3E8
         * Summit: 0x5D25C
         * Threadripper: 0x5D25C
         * Vermeer: 0x30081D98
         */

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
                    if (Ring0.RdmsrTx(0xC00102B3, out eax, out edx, GroupAffinity.Single(0, i)))
                        topology.performanceOfCore[i / topology.threadsPerCore] = eax & 0xff;
                    else
                        topology.performanceOfCore[i / topology.threadsPerCore] = 0;
                }
            }
            catch { }

            uint ccdsPresent = 0, ccdsDown = 0, coreFuse = 0;
            uint fuse1 = 0x5D218;
            uint fuse2 = 0x5D21C;
            uint offset = 0x238;
            uint ccxPerCcd = 2;

            // Get CCD and CCX configuration
            // https://gitlab.com/leogx9r/ryzen_smu/-/blob/master/userspace/monitor_cpu.c
            if (family == Family.FAMILY_19H)
            {
                offset = 0x598;
                ccxPerCcd = 1;
                if (codeName == CodeName.Raphael || codeName == CodeName.DragonRange)
                {
                    offset = 0x4D0;
                    fuse1 += 0x1A4;
                    fuse2 += 0x1A4;
                }
            }
            else if (family == Family.FAMILY_17H && model != 0x71 && model != 0x31)
            {
                fuse1 += 0x40; // 0x5D258
                fuse2 += 0x40; // 0x5D25C
            }

            if (ReadDwordEx(fuse1, ref ccdsPresent) && ReadDwordEx(fuse2, ref ccdsDown))
            {
                uint ccdEnableMap = Utils.GetBits(ccdsPresent, 22, 8);
                uint ccdDisableMap = Utils.GetBits(ccdsPresent, 30, 2) | (Utils.GetBits(ccdsDown, 0, 6) << 2);
                uint coreDisableMapAddress = 0x30081800 + offset;
                uint enabledCcd = Utils.CountSetBits(ccdEnableMap);

                topology.ccds = enabledCcd > 0 ? enabledCcd : 1;
                topology.ccxs = topology.ccds * ccxPerCcd;
                topology.physicalCores = topology.ccxs * 8 / ccxPerCcd;
                topology.ccdEnableMap = ccdEnableMap;
                topology.ccdDisableMap = ccdDisableMap;
                topology.fuse1 = fuse1;
                topology.fuse2 = fuse2;

                if (ReadDwordEx(coreDisableMapAddress, ref coreFuse))
                {
                    topology.coresPerCcx = (8 - Utils.CountSetBits(coreFuse & 0xff)) / ccxPerCcd;
                    topology.coreFuse = coreFuse;
                }
                else
                {
                    Console.WriteLine("Could not read core fuse!");
                }

                for (int i = 0; i < topology.ccds; i++)
                {
                    if (Utils.GetBits(ccdEnableMap, i, 1) == 1)
                    {
                        if (ReadDwordEx(((uint)i << 25) + coreDisableMapAddress, ref coreFuse))
                            topology.coreDisableMap |= (coreFuse & 0xff) << i * 8;
                        else
                            Console.WriteLine($"Could not read core fuse for CCD{i}!");
                    }
                }
            }
            else
            {
                Console.WriteLine("Could not read CCD fuse!");
            }

            return topology;
        }

        public Cpu()
        {
            Ring0.Open();

            if (!Ring0.IsOpen)
            {
                string errorReport = Ring0.GetReport();
                using (var sw = new StreamWriter("WinRing0.txt", true))
                {
                    sw.Write(errorReport);
                }

                throw new ApplicationException("Error opening WinRing kernel driver");
            }

            Opcode.Open();
            mmio = new ACPI_MMIO(io);

            info.vendor = GetVendor();
            if (info.vendor != Constants.VENDOR_AMD && info.vendor != Constants.VENDOR_HYGON)
                throw new Exception("Not an AMD CPU");

            if (Opcode.Cpuid(0x00000001, 0, out uint eax, out uint ebx, out uint ecx, out uint edx))
            {
                info.cpuid = eax;
                info.family = (Family)(((eax & 0xf00) >> 8) + ((eax & 0xff00000) >> 20));
                info.baseModel = (eax & 0xf0) >> 4;
                info.extModel = (eax & 0xf0000) >> 12;
                info.model = info.baseModel + info.extModel;
                info.stepping = eax & 0xf;
                // info.logicalCores = Utils.GetBits(ebx, 16, 8);
            }
            else
            {
                throw new ApplicationException(InitializationExceptionText);
            }

            info.cpuName = GetCpuName();

            // Package type
            if (Opcode.Cpuid(0x80000001, 0, out eax, out ebx, out ecx, out edx))
            {
                info.packageType = (PackageType)(ebx >> 28);
                info.codeName = GetCodeName(info);
                smu = GetMaintainedSettings.GetByType(info.codeName);
                smu.Hsmp.Init(this);
                smu.Version = GetSmuVersion();
                smu.TableVersion = GetTableVersion();
            }
            else
            {
                throw new ApplicationException(InitializationExceptionText);
            }

            // Non-critical block
            try
            {
                info.topology = GetCpuTopology(info.family, info.codeName, info.model);
            }
            catch (Exception ex)
            {
                LastError = ex;
                Status = IOModule.LibStatus.PARTIALLY_OK;
            }

            try
            {
                info.patchLevel = GetPatchLevel();
                info.svi2 = GetSVI2Info(info.codeName);
                info.aod = new AOD(io, info.codeName);
                systemInfo = new SystemInfo(info, smu);
                powerTable = new PowerTable(smu, io, mmio);

                if (!SendTestMessage())
                    LastError = new ApplicationException("SMU is not responding to test message!");

                Status = IOModule.LibStatus.OK;
            }
            catch (Exception ex)
            {
                LastError = ex;
                Status = IOModule.LibStatus.PARTIALLY_OK;
            }

            memoryConfig = new MemoryConfig(this);
        }

        // [31-28] ccd index
        // [27-24] ccx index (always 0 for Zen3 where each ccd has just one ccx)
        // [23-20] core index
        public uint MakeCoreMask(uint core = 0, uint ccd = 0, uint ccx = 0)
        {
            uint ccxInCcd = info.family == Family.FAMILY_19H ? 1U : 2U;
            uint coresInCcx = 8 / ccxInCcd;

            return ((ccd << 4 | ccx % ccxInCcd & 0xF) << 4 | core % coresInCcx & 0xF) << 20;
        }

        public bool ReadDwordEx(uint addr, ref uint data, int maxRetries = 10)
        {
            for (int retry = 0; retry < maxRetries; retry++)
            {
                if (Ring0.WaitPciBusMutex(10))
                {
                    if (Ring0.WritePciConfig(smu.SMU_PCI_ADDR, smu.SMU_OFFSET_ADDR, addr)
                        && Ring0.ReadPciConfig(smu.SMU_PCI_ADDR, smu.SMU_OFFSET_DATA, out data))
                    {
                        Ring0.ReleasePciBusMutex();
                        return true;
                    }

                    Ring0.ReleasePciBusMutex();
                }
            }

            return false;
        }

        public bool ReadDword(uint addr, ref uint data, int maxRetries = 10)
        {
            for (int retry = 0; retry < maxRetries; retry++)
            {
                if (Ring0.WaitPciBusMutex(10))
                {
                    if (Ring0.WritePciConfig(smu.SMU_PCI_ADDR, (byte)smu.SMU_OFFSET_ADDR, addr) &&
                        Ring0.ReadPciConfig(smu.SMU_PCI_ADDR, (byte)smu.SMU_OFFSET_DATA, out data))
                    {
                        Ring0.ReleasePciBusMutex();
                        return true;
                    }

                    Ring0.ReleasePciBusMutex();
                }
            }

            return false;
        }

        public uint ReadDword(uint addr, int maxRetries = 10)
        {
            uint data = 0;
            ReadDword(addr, ref data, maxRetries);
            return data;
        }

        public bool WriteDwordEx(uint addr, uint data, int maxRetries = 10)
        {
            for (int retry = 0; retry < maxRetries; retry++)
            {
                if (Ring0.WaitPciBusMutex(10))
                {
                    if (Ring0.WritePciConfig(smu.SMU_PCI_ADDR, (byte)smu.SMU_OFFSET_ADDR, addr) &&
                        Ring0.WritePciConfig(smu.SMU_PCI_ADDR, (byte)smu.SMU_OFFSET_DATA, data))
                    {
                        Ring0.ReleasePciBusMutex();
                        return true;
                    }

                    Ring0.ReleasePciBusMutex();
                }
            }

            return false;
        }

        public double GetCoreMulti(int index = 0)
        {
            if (!Ring0.RdmsrTx(0xC0010293, out uint eax, out uint edx, GroupAffinity.Single(0, index)))
                return 0;

            double multi = 25 * (eax & 0xFF) / (12.5 * (eax >> 8 & 0x3F));
            return Math.Round(multi * 4, MidpointRounding.ToEven) / 4;
        }

        public bool Cpuid(uint index, ref uint eax, ref uint ebx, ref uint ecx, ref uint edx)
        {
            return Opcode.Cpuid(index, 0, out eax, out ebx, out ecx, out edx);
        }

        public bool ReadMsr(uint index, ref uint eax, ref uint edx)
        {
            return Ring0.Rdmsr(index, out eax, out edx);
        }

        public bool ReadMsrTx(uint index, ref uint eax, ref uint edx, int i)
        {
            GroupAffinity affinity = GroupAffinity.Single(0, i);

            return Ring0.RdmsrTx(index, out eax, out edx, affinity);
        }

        public bool WriteMsr(uint msr, uint eax, uint edx)
        {
            bool res = true;

            for (var i = 0; i < info.topology.logicalCores; i++)
            {
                res = Ring0.WrmsrTx(msr, eax, edx, GroupAffinity.Single(0, i));
            }

            return res;
        }

        public void WriteIoPort(uint port, byte value) => Ring0.WriteIoPort(port, value);
        public byte ReadIoPort(uint port) => Ring0.ReadIoPort(port);
        public bool ReadPciConfig(uint pciAddress, uint regAddress, ref uint value) => Ring0.ReadPciConfig(pciAddress, regAddress, out value);
        public uint GetPciAddress(byte bus, byte device, byte function) => Ring0.GetPciAddress(bus, device, function);

        // https://en.wikichip.org/wiki/amd/cpuid
        public CodeName GetCodeName(CPUInfo cpuInfo)
        {
            CodeName codeName = CodeName.Unsupported;

            if (cpuInfo.family == Family.FAMILY_15H)
            {
                switch (cpuInfo.model)
                {
                    case 0x65:
                        codeName = CodeName.BristolRidge;
                        break;
                }
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
                        codeName = CodeName.Phoenix;
                        break;
                    // case 0x75:
                    //    codeName = CodeName.HawkPoint;
                    //    break;
                    case 0x78:
                        codeName = CodeName.Phoenix2;
                        break;
                    case 0xa0:
                        codeName = CodeName.Mendocino;
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
                case CodeName.BristolRidge:
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
            if (Ring0.Rdmsr(0x8b, out uint eax, out uint edx))
                return eax;

            return 0;
        }

        public bool GetOcMode()
        {
            if (info.codeName == CodeName.SummitRidge)
            {
                if (Ring0.Rdmsr(0xC0010063, out uint eax, out uint edx))
                {
                    // Summit Ridge, Raven Ridge
                    return Convert.ToBoolean((eax >> 1) & 1);
                }
                return false;
            }

            if (info.family == Family.FAMILY_15H)
            {
                return false;
            }

            return Equals(GetPBOScalar(), 0.0f);
        }

        public float GetPBOScalar()
        {
            var cmd = new SMUCommands.GetPBOScalar(smu);
            cmd.Execute();

            return cmd.Scalar;
        }

        public bool SendTestMessage(uint arg = 1, Mailbox mbox = null)
        {
            var cmd = new SMUCommands.SendTestMessage(smu, mbox);
            SMUCommands.CmdResult result = cmd.Execute(arg);
            return result.Success && cmd.IsSumCorrect;
        }
        public uint GetSmuVersion() => new SMUCommands.GetSmuVersion(smu).Execute().args[0];
        public double? GetBclk() => mmio.GetBclk();
        public bool SetBclk(double blck) => mmio.SetBclk(blck);
        public SMU.Status TransferTableToDram() => new SMUCommands.TransferTableToDram(smu).Execute().status;
        public uint GetTableVersion() => new SMUCommands.GetTableVersion(smu).Execute().args[0];
        public uint GetDramBaseAddress() => new SMUCommands.GetDramAddress(smu).Execute().args[0];
        public long GetDramBaseAddress64()
        {
            SMUCommands.CmdResult result = new SMUCommands.GetDramAddress(smu).Execute();
            return (long)result.args[1] << 32 | result.args[0];
        }
        public bool GetLN2Mode() => new SMUCommands.GetLN2Mode(smu).Execute().args[0] == 1;
        public SMU.Status SetPPTLimit(uint arg = 0U) => new SMUCommands.SetSmuLimit(smu).Execute(smu.Rsmu.SMU_MSG_SetPPTLimit, arg).status;
        public SMU.Status SetEDCVDDLimit(uint arg = 0U) => new SMUCommands.SetSmuLimit(smu).Execute(smu.Rsmu.SMU_MSG_SetEDCVDDLimit, arg).status;
        public SMU.Status SetEDCSOCLimit(uint arg = 0U) => new SMUCommands.SetSmuLimit(smu).Execute(smu.Rsmu.SMU_MSG_SetEDCSOCLimit, arg).status;
        public SMU.Status SetTDCVDDLimit(uint arg = 0U) => new SMUCommands.SetSmuLimit(smu).Execute(smu.Rsmu.SMU_MSG_SetTDCVDDLimit, arg).status;
        public SMU.Status SetTDCSOCLimit(uint arg = 0U) => new SMUCommands.SetSmuLimit(smu).Execute(smu.Rsmu.SMU_MSG_SetTDCSOCLimit, arg).status;
        public SMU.Status SetOverclockCpuVid(byte arg) => new SMUCommands.SetOverclockCpuVid(smu).Execute(arg).status;
        public SMU.Status EnableOcMode() => new SMUCommands.SetOcMode(smu).Execute(true).status;
        public SMU.Status DisableOcMode() => new SMUCommands.SetOcMode(smu).Execute(false).status;
        public SMU.Status SetPBOScalar(uint scalar) => new SMUCommands.SetPBOScalar(smu).Execute(scalar).status;
        public SMU.Status RefreshPowerTable() => powerTable != null ? powerTable.Refresh() : SMU.Status.FAILED;
        public int? GetPsmMarginSingleCore(uint coreMask)
        {
            SMUCommands.CmdResult result = new SMUCommands.GetPsmMarginSingleCore(smu).Execute(coreMask);
            return result.Success ? (int)result.args[0] : (int?)null;
        }
        public int? GetPsmMarginSingleCore(uint core, uint ccd, uint ccx) => GetPsmMarginSingleCore(MakeCoreMask(core, ccd, ccx));
        public bool SetPsmMarginAllCores(int margin) => new SMUCommands.SetPsmMarginAllCores(smu).Execute(margin).Success;
        public bool SetPsmMarginSingleCore(uint coreMask, int margin) => new SMUCommands.SetPsmMarginSingleCore(smu).Execute(coreMask, margin).Success;
        public bool SetPsmMarginSingleCore(uint core, uint ccd, uint ccx, int margin) => SetPsmMarginSingleCore(MakeCoreMask(core, ccd, ccx), margin);
        public bool SetFrequencyAllCore(uint frequency) => new SMUCommands.SetFrequencyAllCore(smu).Execute(frequency).Success;
        public bool SetFrequencySingleCore(uint coreMask, uint frequency) => new SMUCommands.SetFrequencySingleCore(smu).Execute(coreMask, frequency).Success;
        public bool SetFrequencySingleCore(uint core, uint ccd, uint ccx, uint frequency) => SetFrequencySingleCore(MakeCoreMask(core, ccd, ccx), frequency);
        private bool SetFrequencyMultipleCores(uint mask, uint frequency, int count)
        {
            // ((i.CCD << 4 | i.CCX % 2 & 0xF) << 4 | i.CORE % 4 & 0xF) << 20;
            for (uint i = 0; i < count; i++)
            {
                mask = Utils.SetBits(mask, 20, 2, i);
                if (!SetFrequencySingleCore(mask, frequency))
                    return false;
            }
            return true;
        }
        public bool SetFrequencyCCX(uint mask, uint frequency) => SetFrequencyMultipleCores(mask, frequency, 8/*SI.NumCoresInCCX*/);
        public bool SetFrequencyCCD(uint mask, uint frequency)
        {
            bool ret = true;
            for (uint i = 0; i < info.topology.ccxs / info.topology.ccds; i++)
            {
                mask = Utils.SetBits(mask, 24, 1, i);
                ret = SetFrequencyCCX(mask, frequency);
            }

            return ret;
        }
        public bool IsProchotEnabled()
        {
            uint data = ReadDword(0x59804);
            return (data & 1) == 1;
        }

        public float? GetCpuTemperature()
        {
            uint thmData = 0;

            if (ReadDwordEx(Constants.THM_CUR_TEMP, ref thmData))
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

                // Range sel = -49 to 206C (Temp = Tctl - offset - 49)
                if ((thmData & Constants.THM_CUR_TEMP_RANGE_SEL_MASK) != 0)
                    temperature -= 49.0f;

                return temperature;
            }

            return null;
        }

        public float? GetSingleCcdTemperature(uint ccd)
        {
            uint thmData = 0;

            if (ReadDwordEx(Constants.F17H_M70H_CCD_TEMP + (ccd * 0x4), ref thmData))
            {
                float ccdTemp = (thmData & 0xfff) * 0.125f - 305.0f;
                if (ccdTemp > 0 && ccdTemp < 125) // Zen 2 reports 95 degrees C max, but it might exceed that.
                    return ccdTemp;
                return 0;
            }

            return null;
        }

        public MemoryConfig GetMemoryConfig() => memoryConfig;

        protected virtual void Dispose(bool disposing)
        {
            if (!disposedValue)
            {
                if (disposing)
                {
                    io.Dispose();
                    Ring0.Close();
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
