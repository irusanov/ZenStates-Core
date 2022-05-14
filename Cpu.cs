using OpenHardwareMonitor.Hardware;
using System;
using System.Diagnostics;
using System.IO;

namespace ZenStates.Core
{
    public class Cpu : IDisposable
    {
        private bool disposedValue;
        private const string InitializationExceptionText = "CPU module initialization failed.";

        private static ushort group0 = 0x0;
        private static ulong bitmask0 = (1 << 0);
        private GroupAffinity cpu0Affinity = new GroupAffinity(group0, bitmask0);
        private double _estimatedTimeStampCounterFrequency;
        private double _estimatedTimeStampCounterFrequencyError;
        public bool HasTimeStampCounter;
        public double TimeStampCounterFrequency;

        public float ccd1Temp { get; private set; }
        public float ccd2Temp { get; private set; }
        public bool ccd1TempSupported { get; private set; }
        public bool ccd2TempSupported { get; private set; }
        public float cpuTemp { get; private set; }
        public float cpuVcore { get; private set; }
        public float cpuVsoc { get; private set; }
        public float cpuBusClock { get; private set; }

        public const string VENDOR_AMD = "AuthenticAMD";
        public const string VENDOR_HYGON = "HygonGenuine";
        public const uint F17H_M01H_SVI = 0x0005A000;
        public const uint F17H_M60H_SVI = 0x0006F000; // Renoir only?
        public const uint F17H_M01H_SVI_TEL_PLANE0 = (F17H_M01H_SVI + 0xC);
        public const uint F17H_M01H_SVI_TEL_PLANE1 = (F17H_M01H_SVI + 0x10);
        public const uint F17H_M30H_SVI_TEL_PLANE0 = (F17H_M01H_SVI + 0x14);
        public const uint F17H_M30H_SVI_TEL_PLANE1 = (F17H_M01H_SVI + 0x10);
        public const uint F17H_M60H_SVI_TEL_PLANE0 = (F17H_M60H_SVI + 0x38);
        public const uint F17H_M60H_SVI_TEL_PLANE1 = (F17H_M60H_SVI + 0x3C);
        public const uint F17H_M70H_SVI_TEL_PLANE0 = (F17H_M01H_SVI + 0x10);
        public const uint F17H_M70H_SVI_TEL_PLANE1 = (F17H_M01H_SVI + 0xC);
        public const uint F19H_M21H_SVI_TEL_PLANE0 = (F17H_M01H_SVI + 0x10);
        public const uint F19H_M21H_SVI_TEL_PLANE1 = (F17H_M01H_SVI + 0xC);
        public const uint F17H_M70H_CCD_TEMP = 0x00059954;
        public const uint THM_CUR_TEMP = 0x00059800;
        public const uint THM_CUR_TEMP_RANGE_SEL_MASK = 0x80000;
        public const uint F17H_PCI_CONTROL_REGISTER = 0x60;
        public const uint MSR_PSTATE_0 = 0xC0010064;
        public const uint MSR_PSTATE_CTL = 0xC0010062;
        public const uint MSR_HWCR = 0xC0010015;
        public const uint MSR_MPERF = 0x000000E7;
        public const uint MSR_APERF = 0x000000E8;


        public enum Family
        {
            UNSUPPORTED = 0x0,
            FAMILY_15H = 0x15,
            FAMILY_17H = 0x17,
            FAMILY_18H = 0x18,
            FAMILY_19H = 0x19,
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
            public uint? coreDisableMap;
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
            public CpuTopology topology;
            public SVI2 svi2;
        }

        public readonly IOModule io = new IOModule();
        public readonly CPUInfo info;
        public readonly SystemInfo systemInfo;
        public readonly SMU smu;
        public readonly PowerTable powerTable;

        public IOModule.LibStatus Status { get; }
        public Exception LastError { get; }

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
                topology.cpuNodes = (ecx >> 8 & 0x7) + 1;

                if (topology.threadsPerCore == 0)
                    topology.cores = topology.logicalCores;
                else
                    topology.cores = topology.logicalCores / topology.threadsPerCore;
            }
            else
            {
                throw new ApplicationException(InitializationExceptionText);
            }

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
            }
            else if (family == Family.FAMILY_17H && model != 0x71 && model != 0x31)
            {
                fuse1 += 0x40;
                fuse2 += 0x40;
            }

            if (!ReadDwordEx(fuse1, ref ccdsPresent) || !ReadDwordEx(fuse2, ref ccdsDown))
                throw new ApplicationException("Could not read CCD fuse!");

            uint ccdEnableMap = Utils.GetBits(ccdsPresent, 22, 8);
            uint ccdDisableMap = Utils.GetBits(ccdsPresent, 30, 2) | (Utils.GetBits(ccdsDown, 0, 6) << 2);
            uint coreDisableMapAddress = 0x30081800 + offset;

            topology.ccds = Utils.CountSetBits(ccdEnableMap);
            topology.ccxs = topology.ccds * ccxPerCcd;
            topology.physicalCores = topology.ccxs * 8 / ccxPerCcd;

            if (ReadDwordEx(coreDisableMapAddress, ref coreFuse))
                topology.coresPerCcx = (8 - Utils.CountSetBits(coreFuse & 0xff)) / ccxPerCcd;
            else
                throw new ApplicationException("Could not read core fuse!");

            uint ccdOffset = 0;

            for (int i = 0; i < topology.ccds; i++)
            {
                if (Utils.GetBits(ccdEnableMap, i, 1) == 1)
                {
                    if (ReadDwordEx(coreDisableMapAddress | ccdOffset, ref coreFuse))
                        topology.coreDisableMap |= (coreFuse & 0xff) << i * 8;
                    else
                        throw new ApplicationException($"Could not read core fuse for CCD{i}!");
                }

                ccdOffset += 0x2000000;
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

            info.vendor = GetVendor();
            if (info.vendor != VENDOR_AMD && info.vendor != VENDOR_HYGON)
                throw new Exception("Not an AMD CPU");

            if (Opcode.Cpuid(0x00000001, 0, out uint eax, out uint ebx, out uint ecx, out uint edx))
            {
                info.cpuid = eax;
                info.family = (Family)(((eax & 0xf00) >> 8) + ((eax & 0xff00000) >> 20));
                info.baseModel = (eax & 0xf0) >> 4;
                info.extModel = (eax & 0xf0000) >> 12;
                info.model = info.baseModel + info.extModel;
                // info.logicalCores = Utils.GetBits(ebx, 16, 8);
            }
            else
            {
                throw new ApplicationException(InitializationExceptionText);
            }


            if (Opcode.Cpuid(0x00000001, 0, out eax, out ebx, out ecx, out edx))
            {
                HasTimeStampCounter = (edx & 0x10) != 0;
            }

            // Package type
            if (Opcode.Cpuid(0x80000001, 0, out eax, out ebx, out ecx, out edx))
            {
                info.packageType = (PackageType)(ebx >> 28);
                info.codeName = GetCodeName(info);
                smu = GetMaintainedSettings.GetByType(info.codeName);
                smu.Version = GetSmuVersion();
                smu.TableVersion = GetTableVersion();
            }
            else
            {
                throw new ApplicationException(InitializationExceptionText);
            }

            info.cpuName = GetCpuName();

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
                systemInfo = new SystemInfo(info, smu);
                powerTable = new PowerTable(smu, io);

                if (!SendTestMessage())
                    LastError = new ApplicationException("SMU is not responding to test message!");

                Status = IOModule.LibStatus.OK;
            }
            catch (Exception ex)
            {
                LastError = ex;
                Status = IOModule.LibStatus.PARTIALLY_OK;
            }

            try
            {
                if (HasTimeStampCounter)
                {
                    GroupAffinity previousAffinity = ThreadAffinity.Set(cpu0Affinity);
                    EstimateTimeStampCounterFrequency(out _estimatedTimeStampCounterFrequency, out _estimatedTimeStampCounterFrequencyError);
                    ThreadAffinity.Set(previousAffinity);
                }
                else
                {
                    _estimatedTimeStampCounterFrequency = 0;
                }

                TimeStampCounterFrequency = _estimatedTimeStampCounterFrequency;

            }
            catch (Exception ex)
            {
                LastError = ex;
                Status = IOModule.LibStatus.PARTIALLY_OK;
            }

            ccd1Temp = 0;
            ccd2Temp = 0;
            ccd1TempSupported = false;
            ccd2TempSupported = false;
            cpuTemp = 0;
            cpuVcore = 0;
            cpuVsoc = 0;
            cpuBusClock = 0;

            RefreshSensors();

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

        public bool ReadDwordEx(uint addr, ref uint data)
        {
            bool res = false;
            if (Ring0.WaitPciBusMutex(10))
            {
                if (Ring0.WritePciConfig(smu.SMU_PCI_ADDR, smu.SMU_OFFSET_ADDR, addr))
                    res = Ring0.ReadPciConfig(smu.SMU_PCI_ADDR, smu.SMU_OFFSET_DATA, out data);
                Ring0.ReleasePciBusMutex();
            }
            return res;
        }

        public uint ReadDword(uint addr)
        {
            uint data = 0;

            if (Ring0.WaitPciBusMutex(10))
            {
                Ring0.WritePciConfig(smu.SMU_PCI_ADDR, (byte)smu.SMU_OFFSET_ADDR, addr);
                Ring0.ReadPciConfig(smu.SMU_PCI_ADDR, (byte)smu.SMU_OFFSET_DATA, out data);
                Ring0.ReleasePciBusMutex();
            }

            return data;
        }

        public bool WriteDwordEx(uint addr, uint data)
        {
            bool res = false;
            if (Ring0.WaitPciBusMutex(10))
            {
                if (Ring0.WritePciConfig(smu.SMU_PCI_ADDR, (byte)smu.SMU_OFFSET_ADDR, addr))
                    res = Ring0.WritePciConfig(smu.SMU_PCI_ADDR, (byte)smu.SMU_OFFSET_DATA, data);
                Ring0.ReleasePciBusMutex();
            }

            return res;
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

        public bool ReadMsrTx(uint index, ref uint eax, ref uint edx)
        {
            GroupAffinity affinity = GroupAffinity.Single(0, (int)index);

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
                        codeName = CodeName.VanGogh;
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
                    // Does Chagall (Zen3 TR) has different model number than Milan?
                    case 0x1:
                        if (cpuInfo.packageType == PackageType.TRX)
                            codeName = CodeName.Chagall;
                        else
                            codeName = CodeName.Milan;
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
                    svi.coreAddress = F17H_M01H_SVI_TEL_PLANE0;
                    svi.socAddress = F17H_M01H_SVI_TEL_PLANE1;
                    break;

                // Zen Threadripper/EPYC
                case CodeName.Whitehaven:
                case CodeName.Naples:
                case CodeName.Colfax:
                    svi.coreAddress = F17H_M01H_SVI_TEL_PLANE1;
                    svi.socAddress = F17H_M01H_SVI_TEL_PLANE0;
                    break;

                // Zen2 Threadripper/EPYC
                case CodeName.CastlePeak:
                case CodeName.Rome:
                    svi.coreAddress = F17H_M30H_SVI_TEL_PLANE0;
                    svi.socAddress = F17H_M30H_SVI_TEL_PLANE1;
                    break;

                // Picasso
                case CodeName.Picasso:
                    if ((smu.Version & 0xFF000000) > 0)
                    {
                        svi.coreAddress = F17H_M01H_SVI_TEL_PLANE0;
                        svi.socAddress = F17H_M01H_SVI_TEL_PLANE1;
                    }
                    else
                    {
                        svi.coreAddress = F17H_M01H_SVI_TEL_PLANE1;
                        svi.socAddress = F17H_M01H_SVI_TEL_PLANE0;
                    }
                    break;

                // Zen2
                case CodeName.Matisse:
                    svi.coreAddress = F17H_M70H_SVI_TEL_PLANE0;
                    svi.socAddress = F17H_M70H_SVI_TEL_PLANE1;
                    break;

                // Zen2 APU, Zen3 APU ?
                case CodeName.Renoir:
                case CodeName.Lucienne:
                case CodeName.Cezanne:
                case CodeName.VanGogh:
                case CodeName.Rembrandt:
                    svi.coreAddress = F17H_M60H_SVI_TEL_PLANE0;
                    svi.socAddress = F17H_M60H_SVI_TEL_PLANE1;
                    break;

                // Zen3, Zen3 Threadripper/EPYC ?
                case CodeName.Vermeer:
                case CodeName.Chagall:
                case CodeName.Milan:
                    svi.coreAddress = F19H_M21H_SVI_TEL_PLANE0;
                    svi.socAddress = F19H_M21H_SVI_TEL_PLANE1;
                    break;

                default:
                    svi.coreAddress = F17H_M01H_SVI_TEL_PLANE0;
                    svi.socAddress = F17H_M01H_SVI_TEL_PLANE1;
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

        public bool SendTestMessage() => new SMUCommands.SendTestMessage(smu).Execute().Success;
        public uint GetSmuVersion() => new SMUCommands.GetSmuVersion(smu).Execute().args[0];
        public SMU.Status TransferTableToDram() => new SMUCommands.TransferTableToDram(smu).Execute().status;
        public uint GetTableVersion() => new SMUCommands.GetTableVersion(smu).Execute().args[0];
        public uint GetDramBaseAddress() => new SMUCommands.GetDramAddress(smu).Execute().args[0];
        public SMU.Status SetPPTLimit(uint arg = 0U) => new SMUCommands.SetSmuLimit(smu).Execute(smu.Rsmu.SMU_MSG_SetPPTLimit, arg).status;
        public SMU.Status SetEDCVDDLimit(uint arg = 0U) => new SMUCommands.SetSmuLimit(smu).Execute(smu.Rsmu.SMU_MSG_SetEDCVDDLimit, arg).status;
        public SMU.Status SetEDCSOCLimit(uint arg = 0U) => new SMUCommands.SetSmuLimit(smu).Execute(smu.Rsmu.SMU_MSG_SetEDCSOCLimit, arg).status;
        public SMU.Status SetTDCVDDLimit(uint arg = 0U) => new SMUCommands.SetSmuLimit(smu).Execute(smu.Rsmu.SMU_MSG_SetTDCVDDLimit, arg).status;
        public SMU.Status SetTDCSOCLimit(uint arg = 0U) => new SMUCommands.SetSmuLimit(smu).Execute(smu.Rsmu.SMU_MSG_SetTDCSOCLimit, arg).status;
        public SMU.Status EnableOcMode() => new SMUCommands.SetOcMode(smu).Execute(true).status;
        public SMU.Status DisableOcMode() => new SMUCommands.SetOcMode(smu).Execute(true).status;
        public SMU.Status SetPBOScalar(uint scalar) => new SMUCommands.SetPBOScalar(smu).Execute(scalar).status;
        public SMU.Status RefreshPowerTable() => powerTable.Refresh();
        public int? GetPsmMarginSingleCore(uint coreMask)
        {
            SMUCommands.CmdResult result = new SMUCommands.GetPsmMarginSingleCore(smu).Execute(coreMask);
            if (result.Success)
                return (int)result.args[0];
            return null;
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
            for (uint i = 0; i < systemInfo.CCXCount / systemInfo.CCDCount; i++)
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
        public bool RefreshSensors()
        {

            if (info.family != Family.FAMILY_17H && info.family != Family.FAMILY_19H) return false;

            if (Ring0.WaitPciBusMutex(10))
            {
                GroupAffinity previousAffinity = ThreadAffinity.Set(cpu0Affinity);

                Ring0.WritePciConfig(0x00, F17H_PCI_CONTROL_REGISTER, THM_CUR_TEMP);
                Ring0.ReadPciConfig(0x00, F17H_PCI_CONTROL_REGISTER + 4, out uint temperature);

                uint smuSvi0Tfn = 0;
                uint smuSvi0TelPlane0 = 0;
                uint smuSvi0TelPlane1 = 0;

                Ring0.WritePciConfig(0x00, F17H_PCI_CONTROL_REGISTER, F17H_M01H_SVI + 0x8);
                Ring0.ReadPciConfig(0x00, F17H_PCI_CONTROL_REGISTER + 4, out smuSvi0Tfn);

                Ring0.WritePciConfig(0x00, F17H_PCI_CONTROL_REGISTER, info.svi2.coreAddress);
                Ring0.ReadPciConfig(0x00, F17H_PCI_CONTROL_REGISTER + 4, out smuSvi0TelPlane0);

                Ring0.WritePciConfig(0x00, F17H_PCI_CONTROL_REGISTER, info.svi2.socAddress);
                Ring0.ReadPciConfig(0x00, F17H_PCI_CONTROL_REGISTER + 4, out smuSvi0TelPlane1);

                for (int ccd = 0; ccd < 2; ccd++)
                {
                    Ring0.WritePciConfig(0x00, F17H_PCI_CONTROL_REGISTER, F17H_M70H_CCD_TEMP + ((uint)ccd * 0x4));
                    Ring0.ReadPciConfig(0x00, F17H_PCI_CONTROL_REGISTER + 4, out uint ccdRawTemp);

                    ccdRawTemp &= 0xFFF;
                    float ccdTemp = ((ccdRawTemp * 125) - 305000) * 0.001f;
                    if (ccd == 0)
                    {
                        if (ccdRawTemp > 0 && ccdTemp < 125) // Zen 2 reports 95 degrees C max, but it might exceed that.
                        {
                            ccd1Temp = ccdTemp;
                            ccd1TempSupported = true;
                        }
                        else
                        {
                            ccd1Temp = 0;
                            ccd1TempSupported = false;
                        }
                    }
                    if (ccd == 1)
                    {
                        if (ccdRawTemp > 0 && ccdTemp < 125) // Zen 2 reports 95 degrees C max, but it might exceed that.
                        {
                            ccd2Temp = ccdTemp;
                            ccd2TempSupported = true;
                        }
                        else
                        {
                            ccd2Temp = 0;
                            ccd2TempSupported = false;
                        }
                    }
                }

                double timeStampCounterMultiplier = GetTimeStampCounterMultiplier();
                if (timeStampCounterMultiplier > 0)
                {
                    cpuBusClock = (float)(TimeStampCounterFrequency / timeStampCounterMultiplier);
                }
                else
                {
                    cpuBusClock = 0;
                }

                Ring0.ReleasePciBusMutex();

                ThreadAffinity.Set(previousAffinity);

                // current temp Bit [31:21]
                // If bit 19 of the Temperature Control register is set, there is an additional offset of 49 degrees C.
                bool tempOffsetFlag = (temperature & THM_CUR_TEMP_RANGE_SEL_MASK) != 0;
                temperature = (temperature >> 21) * 125;

                float offset = 0.0f;

                // Offset table: https://github.com/torvalds/linux/blob/master/drivers/hwmon/k10temp.c#L78

                if (info.cpuName.Contains("2700X"))
                    offset = -10.0f;
                else if (info.cpuName.Contains("1600X") || info.cpuName.Contains("1700X") || info.cpuName.Contains("1800X"))
                    offset = -20.0f;
                else if (info.cpuName.Contains("Threadripper 19") || info.cpuName.Contains("Threadripper 29"))
                    offset = -27.0f;

                float t = temperature * 0.001f;
                if (tempOffsetFlag)
                    t += -49.0f;

                cpuTemp = offset < 0 ? t + offset : t;

                const double vidStep = 0.00625;
                double vcc;
                uint svi0PlaneXVddCor;

                // Core (0x01)
                if ((smuSvi0Tfn & 0x01) == 0)
                {
                    svi0PlaneXVddCor = (smuSvi0TelPlane0 >> 16) & 0xff;
                    vcc = 1.550 - vidStep * svi0PlaneXVddCor;
                    cpuVcore = (float)vcc;
                }
                else
                {
                    cpuVcore = 0;
                }

                // SoC (0x02)
                if (info.model == 0x11 || info.model == 0x21 || info.model == 0x71 || info.model == 0x31 || (smuSvi0Tfn & 0x02) == 0)
                {
                    svi0PlaneXVddCor = (smuSvi0TelPlane1 >> 16) & 0xff;
                    vcc = 1.550 - vidStep * svi0PlaneXVddCor;
                    cpuVsoc = (float)vcc;
                }
                else
                {
                    cpuVsoc = 0;
                }

            }
            return true;
        }
        private void EstimateTimeStampCounterFrequency(out double frequency, out double error)
        {
            // preload the function
            EstimateTimeStampCounterFrequency(0, out double f, out double e);
            EstimateTimeStampCounterFrequency(0, out f, out e);

            // estimate the frequency
            error = double.MaxValue;
            frequency = 0;
            for (int i = 0; i < 5; i++)
            {
                EstimateTimeStampCounterFrequency(0.025, out f, out e);
                if (e < error)
                {
                    error = e;
                    frequency = f;
                }

                if (error < 1e-4)
                    break;
            }
        }
        private static void EstimateTimeStampCounterFrequency(double timeWindow, out double frequency, out double error)
        {
            long ticks = (long)(timeWindow * Stopwatch.Frequency);

            long timeBegin = Stopwatch.GetTimestamp() + (long)Math.Ceiling(0.001 * ticks);
            long timeEnd = timeBegin + ticks;

            while (Stopwatch.GetTimestamp() < timeBegin)
            { }

            ulong countBegin = Opcode.Rdtsc();
            long afterBegin = Stopwatch.GetTimestamp();

            while (Stopwatch.GetTimestamp() < timeEnd)
            { }

            ulong countEnd = Opcode.Rdtsc();
            long afterEnd = Stopwatch.GetTimestamp();

            double delta = timeEnd - timeBegin;
            frequency = 1e-6 * ((double)(countEnd - countBegin) * Stopwatch.Frequency) / delta;

            double beginError = (afterBegin - timeBegin) / delta;
            double endError = (afterEnd - timeEnd) / delta;
            error = beginError + endError;
        }
        private double GetTimeStampCounterMultiplier()
        {
            if (info.family == Family.FAMILY_17H)
            {
                uint ctlEax, ctlEdx;
                Ring0.Rdmsr(MSR_PSTATE_CTL, out ctlEax, out ctlEdx);
                Ring0.Wrmsr(MSR_PSTATE_CTL, ctlEax | (0 << 0) | (1 << 0), ctlEdx);
            }

            Ring0.Rdmsr(MSR_PSTATE_0, out uint eax, out _);
            uint cpuDfsId = (eax >> 8) & 0x3f;
            uint cpuFid = eax & 0xff;
            return 2.0 * cpuFid / cpuDfsId;
        }

        public void Dispose()
        {
            // Do not change this code. Put cleanup code in 'Dispose(bool disposing)' method
            Dispose(disposing: true);
            GC.SuppressFinalize(this);
        }
    }
}
