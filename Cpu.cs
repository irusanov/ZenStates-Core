using OpenHardwareMonitor.Hardware;
using System;
using System.IO;

namespace ZenStates.Core
{
    public class Cpu : IDisposable
    {
        private bool disposedValue;
        private const ushort SMU_TIMEOUT = 8192;
        private const string InitializationExceptionText = "CPU module initialization failed.";

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

        public struct CPUInfo
        {
            public uint cpuid;
            public Family family;
            public CodeName codeName;
            public string cpuName;
            public PackageType packageType;
            public uint baseModel;
            public uint extModel;
            public uint model;
            public uint ccds;
            public uint ccxs;
            public uint coresPerCcx;
            public uint cores;
            public uint logicalCores;
            public uint physicalCores;
            public uint threadsPerCore;
            public uint cpuNodes;
            public uint patchLevel;
            public uint coreDisableMap;
            public SVI2 svi2;
        }

        public readonly Utils utils = new Utils();
        public readonly CPUInfo info;
        public readonly SystemInfo systemInfo;
        public readonly SMU smu;
        public readonly PowerTable powerTable;

        public Utils.LibStatus Status { get; }
        public Exception LastError { get; }

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

            uint ccdsPresent = 0, ccdsDown = 0, coreFuse = 0;
            uint fuse1 = 0x5D218;
            uint fuse2 = 0x5D21C;
            uint offset = 0x238;
            uint ccxPerCcd = 2;

            if (Opcode.Cpuid(0x00000001, 0, out uint eax, out uint ebx, out uint ecx, out uint edx))
            {
                info.cpuid = eax;
                info.family = (Family)(((eax & 0xf00) >> 8) + ((eax & 0xff00000) >> 20));
                info.baseModel = (eax & 0xf0) >> 4;
                info.extModel = (eax & 0xf0000) >> 12;
                info.model = info.baseModel + info.extModel;
                info.logicalCores = utils.GetBits(ebx, 16, 8);
            }
            else
            {
                throw new ApplicationException(InitializationExceptionText);
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

            if (Opcode.Cpuid(0x8000001E, 0, out eax, out ebx, out ecx, out edx))
            {
                info.threadsPerCore = utils.GetBits(ebx, 8, 4) + 1;
                info.cpuNodes = ecx >> 8 & 0x7 + 1;

                if (info.threadsPerCore == 0)
                    info.cores = info.logicalCores;
                else
                    info.cores = info.logicalCores / info.threadsPerCore;
            }
            else
            {
                throw new ApplicationException(InitializationExceptionText);
            }

            // Non-critical block
            try
            {
                // Get CCD and CCX configuration
                // https://gitlab.com/leogx9r/ryzen_smu/-/blob/master/userspace/monitor_cpu.c
                if (info.family == Family.FAMILY_19H)
                {
                    offset = 0x598;
                    ccxPerCcd = 1;
                }
                else if (info.family == Family.FAMILY_17H && info.model != 0x71 && info.model != 0x31)
                {
                    fuse1 += 0x40;
                    fuse2 += 0x40;
                }

                if (!ReadDwordEx(fuse1, ref ccdsPresent) || !ReadDwordEx(fuse2, ref ccdsDown))
                    throw new ApplicationException("Could not read CCD fuse!");

                uint ccdEnableMap = utils.GetBits(ccdsPresent, 22, 8);
                uint ccdDisableMap = utils.GetBits(ccdsPresent, 30, 2) | (utils.GetBits(ccdsDown, 0, 6) << 2);
                uint coreDisableMapAddress = 0x30081800 + offset;

                info.ccds = utils.CountSetBits(ccdEnableMap);
                info.ccxs = info.ccds * ccxPerCcd;
                info.physicalCores = info.ccxs * 8 / ccxPerCcd;

                if (ReadDwordEx(coreDisableMapAddress, ref coreFuse))
                    info.coresPerCcx = (8 - utils.CountSetBits(coreFuse & 0xff)) / ccxPerCcd;
                else
                    throw new ApplicationException("Could not read core fuse!");

                uint ccdOffset = 0;

                for (int i = 0; i < info.ccds; i++)
                {
                    if (utils.GetBits(ccdEnableMap, i, 1) == 1)
                    {
                        if (ReadDwordEx(coreDisableMapAddress | ccdOffset, ref coreFuse))
                            info.coreDisableMap |= (coreFuse & 0xff) << i * 8;
                        else
                            throw new ApplicationException($"Could not read core fuse for CCD{i}!");
                    }

                    ccdOffset += 0x2000000;
                }
            }
            catch (Exception ex)
            {
                LastError = ex;
                Status = Utils.LibStatus.PARTIALLY_OK;
            }

            try
            {
                info.patchLevel = GetPatchLevel();
                info.svi2 = GetSVI2Info(info.codeName);
                systemInfo = new SystemInfo(info, smu);
                powerTable = new PowerTable(smu.TableVersion, smu.SMU_TYPE, (uint)(GetDramBaseAddress() & 0xFFFFFFFF));

                if (!SendTestMessage())
                    LastError = new ApplicationException("SMU is not responding to test message!");

                Status = Utils.LibStatus.OK;
            }
            catch (Exception ex)
            {
                LastError = ex;
                Status = Utils.LibStatus.PARTIALLY_OK;
            }
        }

        public uint MakeCoreMask(uint core = 0, uint ccd = 0, uint ccx = 0)
        {
            uint ccxInCcd = info.family == Family.FAMILY_19H ? 1U : 2U;
            uint coresInCcx = info.coresPerCcx;

            return ((ccd << 4 | ccx % ccxInCcd & 0xF) << 4 | core % coresInCcx & 0xF) << 20;
        }

        public uint[] MakeCmdArgs(uint[] args)
        {
            uint[] cmdArgs = new uint[6];
            int length = args.Length > 6 ? 6 : args.Length;

            for (int i = 0; i < length; i++)
                cmdArgs[i] = args[i];

            return cmdArgs;
        }

        public uint[] MakeCmdArgs(uint arg = 0)
        {
            return MakeCmdArgs(new uint[1] { arg });
        }

        private uint MakePsmMarginArg(int margin)
        {
            int offset = margin < 0 ? 0x100000 : 0;
            return Convert.ToUInt32(offset + margin) & 0xfffff;
        }

        private bool SmuWriteReg(uint addr, uint data)
        {
            if (Ring0.WritePciConfig(smu.SMU_PCI_ADDR, smu.SMU_OFFSET_ADDR, addr))
                return Ring0.WritePciConfig(smu.SMU_PCI_ADDR, smu.SMU_OFFSET_DATA, data);
            return false;
        }

        private bool SmuReadReg(uint addr, ref uint data)
        {
            if (Ring0.WritePciConfig(smu.SMU_PCI_ADDR, smu.SMU_OFFSET_ADDR, addr))
                return Ring0.ReadPciConfig(smu.SMU_PCI_ADDR, smu.SMU_OFFSET_DATA, out data);
            return false;
        }

        // Retry until response register is non-zero and reading RSP register is successful
        private bool SmuWaitDone(Mailbox mailbox)
        {
            bool res;
            ushort timeout = SMU_TIMEOUT;
            uint data = 0;

            do
                res = SmuReadReg(mailbox.SMU_ADDR_RSP, ref data);
            while ((!res || data == 0) && --timeout > 0);

            return timeout != 0 && data > 0;
        }

        public SMU.Status SendSmuCommand(Mailbox mailbox, uint msg, ref uint[] args)
        {
            uint status = 0xFF; // SMU.Status.FAILED;

            // Check all the arguments and don't execute if invalid
            // If the mailbox addresses are not set, they would have the default value of 0x0
            if (mailbox == null || mailbox.SMU_ADDR_MSG == 0 || mailbox.SMU_ADDR_ARG == 0 || mailbox.SMU_ADDR_RSP == 0
                || msg == 0)
                return SMU.Status.FAILED;

            if (Ring0.WaitPciBusMutex(10))
            {
                // Wait done
                if (!SmuWaitDone(mailbox))
                {
                    // Initial probe failed, some other command is still being processed or the PCI read failed
                    Ring0.ReleasePciBusMutex();
                    return SMU.Status.FAILED;
                }

                // Clear response register
                SmuWriteReg(mailbox.SMU_ADDR_RSP, 0);

                // Write data
                uint[] cmdArgs = MakeCmdArgs(args);
                for (int i = 0; i < cmdArgs.Length; ++i)
                    SmuWriteReg(mailbox.SMU_ADDR_ARG + (uint)(i * 4), cmdArgs[i]);

                // Send message
                SmuWriteReg(mailbox.SMU_ADDR_MSG, msg);

                // Wait done
                if (!SmuWaitDone(mailbox))
                {
                    // Timeout reached or PCI read failed
                    Ring0.ReleasePciBusMutex();
                    return SMU.Status.FAILED;
                }

                // If we reach this stage, read final status
                SmuReadReg(mailbox.SMU_ADDR_RSP, ref status);

                if ((SMU.Status)status == SMU.Status.OK)
                {
                    // Read back args
                    for (int i = 0; i < args.Length; ++i)
                        SmuReadReg(mailbox.SMU_ADDR_ARG + (uint)(i * 4), ref args[i]);
                }

                Ring0.ReleasePciBusMutex();
            }

            return (SMU.Status)status;
        }

        // Legacy
        public bool SendSmuCommand(Mailbox mailbox, uint msg, uint arg)
        {
            uint[] args = MakeCmdArgs(arg);
            return SendSmuCommand(mailbox, msg, ref args) == SMU.Status.OK;
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

            for (var i = 0; i < info.logicalCores; i++)
            {
                res = Ring0.WrmsrTx(msr, eax, edx, GroupAffinity.Single(0, i));
            }

            return res;
        }

        public void WriteIoPort(uint port, byte value) => Ring0.WriteIoPort(port, value);

        public bool ReadPciConfig(uint pciAddress, uint regAddress, ref uint value)
        {
            return Ring0.ReadPciConfig(pciAddress, regAddress, out value);
        }

        public uint GetPciAddress(byte bus, byte device, byte function)
        {
            return Ring0.GetPciAddress(bus, device, function);
        }

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
                    case 0x40:
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

        public string GetCpuName()
        {
            string model = "";

            if (Opcode.Cpuid(0x80000002, 0, out uint eax, out uint ebx, out uint ecx, out uint edx))
                model = model + utils.IntToStr(eax) + utils.IntToStr(ebx) + utils.IntToStr(ecx) + utils.IntToStr(edx);

            if (Opcode.Cpuid(0x80000003, 0, out eax, out ebx, out ecx, out edx))
                model = model + utils.IntToStr(eax) + utils.IntToStr(ebx) + utils.IntToStr(ecx) + utils.IntToStr(edx);

            if (Opcode.Cpuid(0x80000004, 0, out eax, out ebx, out ecx, out edx))
                model = model + utils.IntToStr(eax) + utils.IntToStr(ebx) + utils.IntToStr(ecx) + utils.IntToStr(edx);

            return model.Trim();
        }

        public uint GetSmuVersion()
        {
            uint[] args = MakeCmdArgs();
            if (SendSmuCommand(smu.Rsmu, smu.Rsmu.SMU_MSG_GetSmuVersion, ref args) == SMU.Status.OK)
                return args[0];

            return 0;
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
            uint[] args = MakeCmdArgs();
            if (SendSmuCommand(smu.Rsmu, smu.Rsmu.SMU_MSG_GetPBOScalar, ref args) == SMU.Status.OK)
            {
                byte[] bytes = BitConverter.GetBytes(args[0]);
                float scalar = BitConverter.ToSingle(bytes, 0);

                if (scalar > 0)
                    return scalar;
            }
            return 0.0f;
        }

        public SMU.Status TransferTableToDram()
        {
            uint[] args = MakeCmdArgs(new uint[] { 1, 1 });

            if (smu.SMU_TYPE == SMU.SmuType.TYPE_APU0)
            {
                args[0] = 3;
                args[1] = 0;
            }

            return SendSmuCommand(smu.Rsmu, smu.Rsmu.SMU_MSG_TransferTableToDram, ref args);
        }

        public uint GetTableVersion()
        {
            uint[] args = MakeCmdArgs();

            SMU.Status status = SendSmuCommand(smu.Rsmu, smu.Rsmu.SMU_MSG_GetTableVersion, ref args);

            if (status == SMU.Status.OK)
                return args[0];

            return 0;
        }

        public ulong GetDramBaseAddress()
        {
            uint[] args = MakeCmdArgs();
            ulong address = 0;

            SMU.Status status = SMU.Status.FAILED;

            switch (smu.SMU_TYPE)
            {
                // SummitRidge, PinnacleRidge, Colfax
                case SMU.SmuType.TYPE_CPU0:
                case SMU.SmuType.TYPE_CPU1:
                    args = MakeCmdArgs();
                    status = SendSmuCommand(smu.Rsmu, smu.Rsmu.SMU_MSG_GetDramBaseAddress - 1, ref args);
                    if (status != SMU.Status.OK)
                        return 0;

                    args = MakeCmdArgs();
                    status = SendSmuCommand(smu.Rsmu, smu.Rsmu.SMU_MSG_GetDramBaseAddress, ref args);
                    if (status != SMU.Status.OK)
                        return 0;

                    address = args[0];

                    args = MakeCmdArgs();
                    status = SendSmuCommand(smu.Rsmu, smu.Rsmu.SMU_MSG_GetDramBaseAddress + 2, ref args);
                    if (status != SMU.Status.OK)
                        return 0;
                    break;

                // Matisse, CastlePeak, Rome, Vermeer, Chagall?, Milan?
                case SMU.SmuType.TYPE_CPU2:
                case SMU.SmuType.TYPE_CPU3:
                    status = SendSmuCommand(smu.Rsmu, smu.Rsmu.SMU_MSG_GetDramBaseAddress, ref args);
                    if (status != SMU.Status.OK)
                        return 0;
                    address = args[0] | ((ulong)args[1] << 32);
                    break;

                // Renoir, Cezanne, VanGogh, Rembrandt
                case SMU.SmuType.TYPE_APU1:
                case SMU.SmuType.TYPE_APU2:
                    status = SendSmuCommand(smu.Rsmu, smu.Rsmu.SMU_MSG_GetDramBaseAddress, ref args);
                    if (status != SMU.Status.OK)
                        return 0;
                    address = args[0] | ((ulong)args[1] << 32);
                    break;

                // RavenRidge, RavenRidge2, Picasso
                case SMU.SmuType.TYPE_APU0:
                    uint[] parts = new uint[2];

                    args = MakeCmdArgs(3);
                    status = SendSmuCommand(smu.Rsmu, smu.Rsmu.SMU_MSG_GetDramBaseAddress - 1, ref args);
                    if (status != SMU.Status.OK)
                        return 0;

                    args = MakeCmdArgs(3);
                    status = SendSmuCommand(smu.Rsmu, smu.Rsmu.SMU_MSG_GetDramBaseAddress, ref args);
                    if (status != SMU.Status.OK)
                        return 0;

                    // First base
                    parts[0] = args[0];

                    args = MakeCmdArgs(5);
                    status = SendSmuCommand(smu.Rsmu, smu.Rsmu.SMU_MSG_GetDramBaseAddress - 1, ref args);
                    if (status != SMU.Status.OK)
                        return 0;

                    args = MakeCmdArgs(5);
                    status = SendSmuCommand(smu.Rsmu, smu.Rsmu.SMU_MSG_GetDramBaseAddress, ref args);
                    if (status != SMU.Status.OK)
                        return 0;

                    // Second base
                    parts[1] = args[0];
                    address = (ulong)parts[1] << 32 | parts[0];
                    break;
            }

            if (status == SMU.Status.OK)
                return address;

            return 0;
        }

        private SMU.Status SetOcMode(bool enabled, uint arg = 0U)
        {
            uint cmd = enabled ? smu.Rsmu.SMU_MSG_EnableOcMode : smu.Rsmu.SMU_MSG_DisableOcMode;
            if (cmd != 0)
            {
                uint[] args = MakeCmdArgs(arg);
                return SendSmuCommand(smu.Rsmu, cmd, ref args);
            }
            return SMU.Status.UNKNOWN_CMD;
        }

        private SMU.Status SetLimit(uint cmd, uint arg = 0U)
        {
            uint[] args = MakeCmdArgs(arg * 1000);
            return SendSmuCommand(smu.Rsmu, cmd, ref args);
        }

        public SMU.Status SetPPTLimit(uint arg = 0U) => SetLimit(smu.Rsmu.SMU_MSG_SetPPTLimit, arg);
        public SMU.Status SetEDCVDDLimit(uint arg = 0U) => SetLimit(smu.Rsmu.SMU_MSG_SetEDCVDDLimit, arg);
        public SMU.Status SetEDCSOCLimit(uint arg = 0U) => SetLimit(smu.Rsmu.SMU_MSG_SetEDCSOCLimit, arg);
        public SMU.Status SetTDCVDDLimit(uint arg = 0U) => SetLimit(smu.Rsmu.SMU_MSG_SetTDCVDDLimit, arg);
        public SMU.Status SetTDCSOCLimit(uint arg = 0U) => SetLimit(smu.Rsmu.SMU_MSG_SetTDCSOCLimit, arg);

        // TODO: Set OC vid based on current PState0 VID
        public SMU.Status EnableOcMode() => SetOcMode(true);

        public SMU.Status DisableOcMode() => SetOcMode(false);

        public SMU.Status SetPBOScalar(uint arg = 1)
        {
            uint[] args = MakeCmdArgs(arg * 100);
            return SendSmuCommand(smu.Rsmu, smu.Rsmu.SMU_MSG_SetPBOScalar, ref args);
        }

        public SMU.Status RefreshPowerTable()
        {
            if (powerTable != null && powerTable.DramBaseAddress > 0)
            {
                try
                {
                    SMU.Status status = TransferTableToDram();

                    if (status != SMU.Status.OK)
                        return status;

                    float[] table = new float[powerTable.TableSize / 4];

                    if (utils.Is64Bit)
                    {
                        byte[] bytes = utils.ReadMemory(new IntPtr(powerTable.DramBaseAddress), powerTable.TableSize);
                        if (bytes != null && bytes.Length > 0)
                            Buffer.BlockCopy(bytes, 0, table, 0, bytes.Length);
                        else
                            return SMU.Status.FAILED;
                    }
                    else
                    {
                        /*uint data = 0;

                        for (int i = 0; i < table.Length; ++i)
                        {
                            Ring0.ReadMemory((ulong)(powerTable.DramBaseAddress), ref data);
                            byte[] bytes = BitConverter.GetBytes(data);
                            table[i] = BitConverter.ToSingle(bytes, 0);
                            //table[i] = data;
                        }*/

                        for (int i = 0; i < table.Length; ++i)
                        {
                            int offset = i * 4;
                            utils.GetPhysLong((UIntPtr)(powerTable.DramBaseAddress + offset), out uint data);
                            byte[] bytes = BitConverter.GetBytes(data);
                            Buffer.BlockCopy(bytes, 0, table, offset, bytes.Length);
                        }
                    }

                    if (utils.AllZero(table))
                        status = SMU.Status.FAILED;
                    else
                        powerTable.Table = table;

                    return status;
                }
                catch { }
            }
            return SMU.Status.FAILED;
        }

        public bool SetPsmMarginAllCores(int margin)
        {
            uint m = MakePsmMarginArg(margin);
            uint[] args = MakeCmdArgs(m);
            return SendSmuCommand(smu.Mp1Smu, smu.Mp1Smu.SMU_MSG_SetAllDldoPsmMargin, ref args) == SMU.Status.OK;
        }

        // Set DLDO Psm margin for a single core
        public bool SetPsmMarginSingleCore(uint coreMask, int margin)
        {
            uint m = MakePsmMarginArg(margin);
            uint[] args = MakeCmdArgs((coreMask & 0xfff00000) | m);

            return SendSmuCommand(smu.Mp1Smu, smu.Mp1Smu.SMU_MSG_SetDldoPsmMargin, ref args) == SMU.Status.OK;
        }

        public bool SetPsmMarginSingleCore(uint core, uint ccd, uint ccx, int margin)
        {
            uint coreMask = MakeCoreMask(core, ccd, ccx);
            return SetPsmMarginSingleCore(coreMask, margin);
        }

        // Get DLDO Psm margin
        public int GetPsmMarginSingleCore(uint coreMask)
        {
            uint[] args = MakeCmdArgs(coreMask & 0xfff00000);

            if (SendSmuCommand(smu.Mp1Smu, smu.Mp1Smu.SMU_MSG_GetDldoPsmMargin, ref args) == SMU.Status.OK)
            {
                // What is the CO range, should we clamp to -30/30?
                uint ret = args[0];
                if ((ret >> 31 & 1) == 1)
                    return -(Convert.ToInt32(~ret & 0x7ffff) + 1);
                else
                    return Convert.ToInt32(ret & 0x7ffff);
            }
            return 0;
        }

        public int GetPsmMarginSingleCore(uint core, uint ccd, uint ccx)
        {
            uint coreMask = MakeCoreMask(core, ccd, ccx);
            return GetPsmMarginSingleCore(coreMask);
        }

        public bool SendTestMessage()
        {
            uint[] args = MakeCmdArgs();
            return SendSmuCommand(smu.Rsmu, smu.Rsmu.SMU_MSG_TestMessage, ref args) == SMU.Status.OK;
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
                    utils.Dispose();
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
