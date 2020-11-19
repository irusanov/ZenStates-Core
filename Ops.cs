using OpenLibSys;
using System;
using System.Threading;

namespace ZenStates.Core
{
    public class Ops : IDisposable
    {
        private static Mutex amdSmuMutex;
        private const ushort SMU_TIMEOUT = 8192;
        private readonly Ols Ols;
        private static readonly Utils utils = new Utils();
        public CPUInfo cpuinfo = new CPUInfo();

        public Ops(Ols ols)
        {
            amdSmuMutex = new Mutex();
            Ols = ols ?? throw new ArgumentNullException(nameof(Ols));
            InitCpu();

            //if (!SendTestMessage())
            //    throw new ApplicationException("SMU is not responding");
        }

        public bool SmuWriteReg(uint addr, uint data)
        {
            bool res = false;
            amdSmuMutex.WaitOne(5000);
            if (Ols.WritePciConfigDwordEx(cpuinfo.smu.SMU_PCI_ADDR, cpuinfo.smu.SMU_OFFSET_ADDR, addr) == 1)
                res = (Ols.WritePciConfigDwordEx(cpuinfo.smu.SMU_PCI_ADDR, cpuinfo.smu.SMU_OFFSET_DATA, data) == 1);
            amdSmuMutex.ReleaseMutex();
            return res;
        }

        public bool SmuReadReg(uint addr, ref uint data)
        {
            bool res = false;
            amdSmuMutex.WaitOne(5000);
            if (Ols.WritePciConfigDwordEx(cpuinfo.smu.SMU_PCI_ADDR, cpuinfo.smu.SMU_OFFSET_ADDR, addr) == 1)
                res = (Ols.ReadPciConfigDwordEx(cpuinfo.smu.SMU_PCI_ADDR, cpuinfo.smu.SMU_OFFSET_DATA, ref data) == 1);
            amdSmuMutex.ReleaseMutex();
            return res;
        }

        public bool SmuWaitDone()
        {
            bool res;
            ushort timeout = SMU_TIMEOUT;
            uint data = 0;

            do
                res = SmuReadReg(cpuinfo.smu.SMU_ADDR_RSP, ref data);
            while ((!res || data != 1) && --timeout > 0);

            if (timeout == 0 || data != 1) res = false;

            return res;
        }

        public SMU.Status SendSmuCommand(uint msg, ref uint[] args)
        {
            ushort timeout = SMU_TIMEOUT;
            uint[] cmdArgs = new uint[6];
            int argsLength = args.Length;
            uint status = 0;

            if (argsLength > cmdArgs.Length)
                argsLength = cmdArgs.Length;

            for (int i = 0; i < argsLength; ++i)
                cmdArgs[i] = args[i];

            if (amdSmuMutex.WaitOne(5000))
            {
                // Clear response register
                bool temp;
                do
                    temp = SmuWriteReg(cpuinfo.smu.SMU_ADDR_RSP, 0);
                while ((!temp) && --timeout > 0);

                if (timeout == 0)
                {
                    amdSmuMutex.ReleaseMutex();
                    SmuReadReg(cpuinfo.smu.SMU_ADDR_RSP, ref status);
                    return (SMU.Status)status;
                }

                // Write data
                for (int i = 0; i < cmdArgs.Length; ++i)
                    SmuWriteReg(cpuinfo.smu.SMU_ADDR_ARG + (uint)(i * 4), cmdArgs[i]);

                // Send message
                SmuWriteReg(cpuinfo.smu.SMU_ADDR_MSG, msg);

                // Wait done
                if (!SmuWaitDone())
                {
                    amdSmuMutex.ReleaseMutex();
                    SmuReadReg(cpuinfo.smu.SMU_ADDR_RSP, ref status);
                    return (SMU.Status)status;
                }

                // Read back args
                for (int i = 0; i < args.Length; ++i)
                    SmuReadReg(cpuinfo.smu.SMU_ADDR_ARG + (uint)(i * 4), ref args[i]);
            }

            amdSmuMutex.ReleaseMutex();
            SmuReadReg(cpuinfo.smu.SMU_ADDR_RSP, ref status);

            return (SMU.Status)status;
        }

        // Legacy
        public bool SendSmuCommand(uint msg, uint arg)
        {
            uint[] args = { arg };
            return SendSmuCommand(msg, ref args) == SMU.Status.OK;
        }

        public uint ReadDword(uint value)
        {
            Ols.WritePciConfigDword(cpuinfo.smu.SMU_PCI_ADDR, (byte)cpuinfo.smu.SMU_OFFSET_ADDR, value);
            return Ols.ReadPciConfigDword(cpuinfo.smu.SMU_PCI_ADDR, (byte)cpuinfo.smu.SMU_OFFSET_DATA);
        }

        private double GetCoreMulti(int index)
        {
            uint eax = default, edx = default;
            if (Ols.RdmsrTx(0xC0010293, ref eax, ref edx, (UIntPtr)(1 << index)) != 1)
            {
                return 0;
            }

            double multi = 25 * (eax & 0xFF) / (12.5 * (eax >> 8 & 0x3F));
            return Math.Round(multi * 4, MidpointRounding.ToEven) / 4;
        }

        public bool WriteMsr(uint msr, uint eax, uint edx)
        {
            bool res = true;

            for (var i = 0; i < cpuinfo.logicalCores; i++)
            {
                if (Ols.WrmsrTx(msr, eax, edx, (UIntPtr)(1 << i)) != 1) res = false;
            }

            return res;
        }

        public SMU.CodeName GetCodeName(uint cpuid, uint packageType)
        {
            SMU.CodeName codeName;

            // CPU Check. Compare family, model, ext family, ext model
            switch (cpuid)
            {
                case 0x00800F11: // CPU \ Zen \ Summit Ridge \ ZP - B0 \ 14nm
                case 0x00800F00: // CPU \ Zen \ Summit Ridge \ ZP - A0 \ 14nm
                    if (packageType == 4 || packageType == 7)
                        codeName = SMU.CodeName.Threadripper;
                    else
                        codeName = SMU.CodeName.SummitRidge;
                    break;
                case 0x00800F12:
                    codeName = SMU.CodeName.Naples;
                    break;
                case 0x00800F82: // CPU \ Zen + \ Pinnacle Ridge \ 12nm
                    if (packageType == 4 || packageType == 7)
                        codeName = SMU.CodeName.Colfax;
                    else
                        codeName = SMU.CodeName.PinnacleRidge;
                    break;
                case 0x00810F81: // APU \ Zen + \ Picasso \ 12nm
                    codeName = SMU.CodeName.Picasso;
                    break;
                case 0x00810F00: // APU \ Zen \ Raven Ridge \ RV - A0 \ 14nm
                case 0x00810F10: // APU \ Zen \ Raven Ridge \ 14nm
                case 0x00820F00: // APU \ Zen \ Raven Ridge 2 \ RV2 - A0 \ 14nm
                case 0x00820F01: // APU \ Zen \ Dali
                    codeName = SMU.CodeName.RavenRidge;
                    break;
                case 0x00870F10: // CPU \ Zen2 \ Matisse \ MTS - B0 \ 7nm + 14nm I/ O Die
                case 0x00870F00: // CPU \ Zen2 \ Matisse \ MTS - A0 \ 7nm + 14nm I/ O Die
                    codeName = SMU.CodeName.Matisse;
                    break;
                case 0x00830F00:
                case 0x00830F10: // CPU \ Epyc 2 \ Rome \ Treadripper 2 \ Castle Peak 7nm
                    if (packageType == 7)
                        codeName = SMU.CodeName.Rome;
                    else
                        codeName = SMU.CodeName.CastlePeak;
                    break;
                case 0x00850F00: // Subor Z+
                    codeName = SMU.CodeName.Fenghuang;
                    break;
                case 0x00860F01: // APU \ Renoir
                    codeName = SMU.CodeName.Renoir;
                    break;
                case 0x00A20F00: // CPU \ Vermeer
                case 0x00A20F10:
                    codeName = SMU.CodeName.Vermeer;
                    break;
                //case 0x00A00F00: // CPU \ Genesis
                //case 0x00A00F10:
                    //codeName = SMU.CodeName.Genesis;
                    //break;
                default:
                    codeName = SMU.CodeName.Unsupported;
                    break;
            }

            return codeName;
        }
        
        // TODO
        public struct CPUInfo
        {
            public uint cpuid;
            public SMU.CpuFamily family;
            public SMU.CodeName codeName;
            public string cpuName;
            public uint packageType; // SMU.PackageType
            public uint model;
            public uint extModel;
            public uint ccds;
            public uint ccxs;
            public uint coresPerCcx;
            public uint cores;
            public uint logicalCores;
            public uint threadsPerCore;
            public uint patchLevel;
            public uint smuVersion;
            public SMU smu;
        }

        public void InitCpu()
        {
            uint eax = 0, ebx = 0, ecx = 0, edx = 0;
            uint ccdsPresent = 0, ccdsDown = 0, coreDisableMap = 0;
            uint fuse1 = 0x5D218;
            //uint fuse2 = 0x5D21C;
            uint offset = 0x238;

            if (Ols.Cpuid(0x00000001, ref eax, ref ebx, ref ecx, ref edx) == 1)
            {
                cpuinfo.cpuid = eax;
                cpuinfo.family = (SMU.CpuFamily)(utils.GetBits(eax, 8, 4) + utils.GetBits(eax, 20, 8));
                cpuinfo.model = (eax & 0xf0) >> 4;
                cpuinfo.extModel = cpuinfo.model + ((eax & 0xf0000) >> 12);
                cpuinfo.logicalCores = utils.GetBits(ebx, 16, 8);
            }

            // Package type
            if (Ols.Cpuid(0x80000001, ref eax, ref ebx, ref ecx, ref edx) == 1)
            {
                cpuinfo.packageType = ebx >> 28;
                cpuinfo.codeName = GetCodeName(cpuinfo.cpuid, cpuinfo.packageType);
                cpuinfo.smu = GetMaintainedSettings.GetByType(cpuinfo.codeName);
                cpuinfo.smuVersion = cpuinfo.smu.Version = GetSmuVersion();
            }

            cpuinfo.cpuName = GetCpuName();

            if (Ols.Cpuid(0x8000001E, ref eax, ref ebx, ref ecx, ref edx) == 1)
            {
                cpuinfo.threadsPerCore = utils.GetBits(ebx, 8, 4) + 1;
                if (cpuinfo.threadsPerCore == 0)
                    cpuinfo.cores = cpuinfo.logicalCores;
                else
                    cpuinfo.cores = cpuinfo.logicalCores / cpuinfo.threadsPerCore;
            }

            if (cpuinfo.family == SMU.CpuFamily.FAMILY_19H)
            {
                fuse1 += 0x10;
                //fuse2 += 0x10;
                offset = 0x598;
            }
            else if (cpuinfo.family == SMU.CpuFamily.FAMILY_17H && cpuinfo.extModel != 0x71)
            {
                fuse1 += 0x40;
                //fuse2 += 0x40;
            }

            if (!SmuReadReg(fuse1, ref ccdsPresent)/* || !SmuReadReg(fuse2, ref ccdsDown)*/)
                return;

            uint ccdEnableMap = utils.GetBits(ccdsPresent, 22, 8);
            //uint ccdDisableMap = utils.GetBits(ccdsPresent, 30, 2) | (utils.GetBits(ccdsDown, 0, 6) << 2);

            uint coreDisableMapAddress = (0x30081800 + offset) | ((ccdEnableMap & 1) == 0 ? 0x2000000 : 0u);

            if (!SmuReadReg(coreDisableMapAddress, ref coreDisableMap))
                return;

            cpuinfo.coresPerCcx = (8 - utils.CountSetBits(coreDisableMap & 0xff)) / 2;
            cpuinfo.ccds = utils.CountSetBits(ccdEnableMap);
            cpuinfo.ccxs = (cpuinfo.cores == cpuinfo.coresPerCcx ? 1 : cpuinfo.ccds * 2);

            cpuinfo.patchLevel = GetPatchLevel();
        }

        public string GetCpuName()
        {
            string model = "";
            uint eax = 0, ebx = 0, ecx = 0, edx = 0;

            if (Ols.Cpuid(0x80000002, ref eax, ref ebx, ref ecx, ref edx) == 1)
                model = model + utils.IntToStr(eax) + utils.IntToStr(ebx) + utils.IntToStr(ecx) + utils.IntToStr(edx);

            if (Ols.Cpuid(0x80000003, ref eax, ref ebx, ref ecx, ref edx) == 1)
                model = model + utils.IntToStr(eax) + utils.IntToStr(ebx) + utils.IntToStr(ecx) + utils.IntToStr(edx);

            if (Ols.Cpuid(0x80000004, ref eax, ref ebx, ref ecx, ref edx) == 1)
                model = model + utils.IntToStr(eax) + utils.IntToStr(ebx) + utils.IntToStr(ecx) + utils.IntToStr(edx);

            return model.Trim();
        }

        public int GetCpuNodes()
        {
            uint eax = 0, ebx = 0, ecx = 0, edx = 0;
            if (Ols.Cpuid(0x8000001E, ref eax, ref ebx, ref ecx, ref edx) == 1)
            {
                return Convert.ToInt32(ecx >> 8 & 0x7) + 1;
            }
            return 1;
        }

        public uint GetSmuVersion()
        {
            uint[] args = new uint[6];
            if (SendSmuCommand(cpuinfo.smu.SMU_MSG_GetSmuVersion, ref args) == SMU.Status.OK)
                return args[0];

            return 0;
        }

        public uint GetPatchLevel()
        {
            uint eax = 0, edx = 0;
            if (Ols.Rdmsr(0x8b, ref eax, ref edx) != 1)
                return 0;

            return eax;
        }

        public bool GetOcMode()
        {
            /*
            uint eax = 0;
            uint edx = 0;

            if (ols.Rdmsr(MSR_PStateStat, ref eax, ref edx) == 1)
            {
                // Summit Ridge, Raven Ridge
                return Convert.ToBoolean((eax >> 1) & 1);
            }
            return false;
            */

            return GetPBOScalar() == 0;
        }

        public float GetPBOScalar()
        {
            uint[] args = new uint[6];
            if (SendSmuCommand(cpuinfo.smu.SMU_MSG_GetPBOScalar, ref args) == SMU.Status.OK)
            {
                byte[] bytes = BitConverter.GetBytes(args[0]);
                float scalar = BitConverter.ToSingle(bytes, 0);

                if (scalar > 0)
                    return scalar;
            }
            return 0f;
        }

        public SMU.Status TransferTableToDram()
        {
            uint[] args = { 1, 1, 0, 0, 0, 0 };

            if (cpuinfo.smu.SMU_TYPE == SMU.SmuType.TYPE_APU0)
            {
                args[0] = 3;
                args[1] = 0;
            }

            return SendSmuCommand(cpuinfo.smu.SMU_MSG_TransferTableToDram, ref args);
        }

        public ulong GetDramBaseAddress()
        {
            uint[] args = new uint[6];
            ulong address = 0;

            SMU.Status status = SMU.Status.FAILED;

            switch (cpuinfo.smu.SMU_TYPE)
            {
                // SummitRidge, PinnacleRidge, Colfax
                case SMU.SmuType.TYPE_CPU0:
                case SMU.SmuType.TYPE_CPU1:
                    args[0] = 0;
                    status = SendSmuCommand(cpuinfo.smu.SMU_MSG_GetDramBaseAddress - 1, ref args);
                    if (status != SMU.Status.OK)
                        return 0;

                    status = SendSmuCommand(cpuinfo.smu.SMU_MSG_GetDramBaseAddress, ref args);
                    if (status != SMU.Status.OK)
                        return 0;

                    address = args[0];

                    args[0] = 0;
                    status = SendSmuCommand(cpuinfo.smu.SMU_MSG_GetDramBaseAddress + 2, ref args);
                    if (status != SMU.Status.OK)
                        return 0;
                    break;

                // Matisse, CastlePeak, Rome, Vermeer
                case SMU.SmuType.TYPE_CPU2:
                case SMU.SmuType.TYPE_CPU3:
                    status = SendSmuCommand(cpuinfo.smu.SMU_MSG_GetDramBaseAddress, ref args);
                    if (status != SMU.Status.OK)
                        return 0;
                    address = args[0] | ((ulong)args[1] << 32);
                    break;

                // Renoir
                case SMU.SmuType.TYPE_APU1:
                    status = SendSmuCommand(cpuinfo.smu.SMU_MSG_GetDramBaseAddress, ref args);
                    if (status != SMU.Status.OK)
                        return 0;
                    address = args[0] | ((ulong)args[1] << 32);
                    break;

                // RavenRidge, RavenRidge2, Picasso
                case SMU.SmuType.TYPE_APU0:
                    uint[] parts = new uint[2];

                    args[0] = 3;
                    status = SendSmuCommand(cpuinfo.smu.SMU_MSG_GetDramBaseAddress - 1, ref args);
                    if (status != SMU.Status.OK)
                        return 0;

                    args[0] = 3;
                    status = SendSmuCommand(cpuinfo.smu.SMU_MSG_GetDramBaseAddress, ref args);
                    if (status != SMU.Status.OK)
                        return 0;

                    // First base
                    parts[0] = args[0];

                    args[0] = 5;
                    status = SendSmuCommand(cpuinfo.smu.SMU_MSG_GetDramBaseAddress - 1, ref args);
                    if (status != SMU.Status.OK)
                        return 0;

                    status = SendSmuCommand(cpuinfo.smu.SMU_MSG_GetDramBaseAddress, ref args);
                    if (status != SMU.Status.OK)
                        return 0;

                    // Second base
                    parts[1] = args[0];
                    address = (ulong)parts[1] << 32 | parts[0];
                    break;

                default:
                    break;
            }

            if (status == SMU.Status.OK)
                return address;

            return 0;
        }

        public bool SendTestMessage()
        {
            uint[] args = new uint[6];
            return SendSmuCommand(cpuinfo.smu.SMU_MSG_TestMessage, ref args) == SMU.Status.OK;
        }

        public bool IsProchotEnabled()
        {
            uint data = ReadDword(0x59804);
            return (data & 1) == 1;
        }

        public void Dispose()
        {
            amdSmuMutex.ReleaseMutex();
        }
    }
}
