using OpenLibSys;
using System;
using System.Threading;

namespace ZenStates.Core
{
    public class Cpu : IDisposable
    {
        private bool disposedValue;
        private static Mutex amdSmuMutex;
        private const ushort SMU_TIMEOUT = 8192;
        private const string InitializationExceptionText = "CPU module initialization failed.";
        public enum LibStatus
        {
            OK = 0,
            INITIALIZE_ERROR = 1,
        }

        public enum Family
        {
            UNSUPPORTED = 0x0,
            FAMILY_17H = 0x17,
            FAMILY_18H = 0x18,
            FAMILY_19H = 0x19,
        };

        public enum CodeName : int
        {
            Unsupported = 0,
            DEBUG,
            SummitRidge,
            Threadripper,
            Naples,
            RavenRidge,
            PinnacleRidge,
            Colfax,
            Picasso,
            Fenghuang,
            Matisse,
            CastlePeak,
            Rome,
            Renoir,
            Vermeer,
            Genesis
        };

        public enum PackageType : int
        {
            FP6 = 0,
            AM4 = 2,
            SP3 = 7
        }

        public readonly Utils utils = new Utils();
        public readonly CPUInfo info = new CPUInfo();
        public readonly Ols Ols;
        public readonly SMU smu;

        public struct CPUInfo
        {
            public uint cpuid;
            public Family family;
            public CodeName codeName;
            public string cpuName;
            public uint packageType; // SMU.PackageType
            public uint baseModel;
            public uint extModel;
            public uint model;
            public uint ccds;
            public uint ccxs;
            public uint coresPerCcx;
            public uint cores;
            public uint logicalCores;
            public uint threadsPerCore;
            public uint patchLevel;
        }

        public LibStatus Status { get; private set; } = LibStatus.INITIALIZE_ERROR;

        public Cpu()
        {
            amdSmuMutex = new Mutex();
            Ols = new Ols();
            CheckOlsStatus();

            uint eax = 0, ebx = 0, ecx = 0, edx = 0;
            uint ccdsPresent = 0, ccdsDown = 0, coreDisableMap = 0;
            uint fuse1 = 0x5D218;
            //uint fuse2 = 0x5D21C;
            uint offset = 0x238;

            if (Ols.Cpuid(0x00000001, ref eax, ref ebx, ref ecx, ref edx) == 1)
            {
                info.cpuid = eax;
                info.family = (Family)(utils.GetBits(eax, 8, 4) + utils.GetBits(eax, 20, 8));
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
            if (Ols.Cpuid(0x80000001, ref eax, ref ebx, ref ecx, ref edx) == 1)
            {
                info.packageType = ebx >> 28;
                info.codeName = GetCodeName(info);
                smu = GetMaintainedSettings.GetByType(info.codeName);
                smu.Version = GetSmuVersion();
            } 
            else
            {
                throw new ApplicationException(InitializationExceptionText);
            }

            info.cpuName = GetCpuName();

            if (Ols.Cpuid(0x8000001E, ref eax, ref ebx, ref ecx, ref edx) == 1)
            {
                info.threadsPerCore = utils.GetBits(ebx, 8, 4) + 1;
                if (info.threadsPerCore == 0)
                    info.cores = info.logicalCores;
                else
                    info.cores = info.logicalCores / info.threadsPerCore;
            }
            else
            {
                throw new ApplicationException(InitializationExceptionText);
            }

            if (info.family == Family.FAMILY_19H)
            {
                fuse1 += 0x10;
                //fuse2 += 0x10;
                offset = 0x598;
            }
            else if (info.family == Family.FAMILY_17H && info.model != 0x71)
            {
                fuse1 += 0x40;
                //fuse2 += 0x40;
            }

            if (!SmuReadReg(fuse1, ref ccdsPresent)/* || !SmuReadReg(fuse2, ref ccdsDown)*/)
                throw new ApplicationException(InitializationExceptionText);

            uint ccdEnableMap = utils.GetBits(ccdsPresent, 22, 8);
            //uint ccdDisableMap = utils.GetBits(ccdsPresent, 30, 2) | (utils.GetBits(ccdsDown, 0, 6) << 2);

            uint coreDisableMapAddress = (0x30081800 + offset) | ((ccdEnableMap & 1) == 0 ? 0x2000000 : 0u);

            if (!SmuReadReg(coreDisableMapAddress, ref coreDisableMap))
                throw new ApplicationException(InitializationExceptionText);

            info.coresPerCcx = (8 - utils.CountSetBits(coreDisableMap & 0xff)) / 2;
            info.ccds = utils.CountSetBits(ccdEnableMap);
            info.ccxs = (info.cores == info.coresPerCcx ? 1 : info.ccds * 2);

            info.patchLevel = GetPatchLevel();

            //if (!SendTestMessage())
            //    throw new ApplicationException("SMU is not responding");

            Status = LibStatus.OK;
        }

        private void CheckOlsStatus()
        {
            // Check support library status
            switch (Ols.GetStatus())
            {
                case (uint)Ols.Status.NO_ERROR:
                    break;
                case (uint)Ols.Status.DLL_NOT_FOUND:
                    throw new ApplicationException("WinRing DLL_NOT_FOUND");
                case (uint)Ols.Status.DLL_INCORRECT_VERSION:
                    throw new ApplicationException("WinRing DLL_INCORRECT_VERSION");
                case (uint)Ols.Status.DLL_INITIALIZE_ERROR:
                    throw new ApplicationException("WinRing DLL_INITIALIZE_ERROR");
            }

            // Check WinRing0 status
            switch (Ols.GetDllStatus())
            {
                case (uint)Ols.OlsDllStatus.OLS_DLL_NO_ERROR:
                    break;
                case (uint)Ols.OlsDllStatus.OLS_DLL_DRIVER_NOT_LOADED:
                    throw new ApplicationException("WinRing OLS_DRIVER_NOT_LOADED");
                case (uint)Ols.OlsDllStatus.OLS_DLL_UNSUPPORTED_PLATFORM:
                    throw new ApplicationException("WinRing OLS_UNSUPPORTED_PLATFORM");
                case (uint)Ols.OlsDllStatus.OLS_DLL_DRIVER_NOT_FOUND:
                    throw new ApplicationException("WinRing OLS_DLL_DRIVER_NOT_FOUND");
                case (uint)Ols.OlsDllStatus.OLS_DLL_DRIVER_UNLOADED:
                    throw new ApplicationException("WinRing OLS_DLL_DRIVER_UNLOADED");
                case (uint)Ols.OlsDllStatus.OLS_DLL_DRIVER_NOT_LOADED_ON_NETWORK:
                    throw new ApplicationException("WinRing DRIVER_NOT_LOADED_ON_NETWORK");
                case (uint)Ols.OlsDllStatus.OLS_DLL_UNKNOWN_ERROR:
                    throw new ApplicationException("WinRing OLS_DLL_UNKNOWN_ERROR");
            }
        }

        public bool SmuWriteReg(uint addr, uint data)
        {
            bool res = false;
            amdSmuMutex.WaitOne(5000);
            if (Ols.WritePciConfigDwordEx(smu.SMU_PCI_ADDR, smu.SMU_OFFSET_ADDR, addr) == 1)
                res = (Ols.WritePciConfigDwordEx(smu.SMU_PCI_ADDR, smu.SMU_OFFSET_DATA, data) == 1);
            amdSmuMutex.ReleaseMutex();
            return res;
        }

        public bool SmuReadReg(uint addr, ref uint data)
        {
            bool res = false;
            amdSmuMutex.WaitOne(5000);
            if (Ols.WritePciConfigDwordEx(smu.SMU_PCI_ADDR, smu.SMU_OFFSET_ADDR, addr) == 1)
                res = (Ols.ReadPciConfigDwordEx(smu.SMU_PCI_ADDR, smu.SMU_OFFSET_DATA, ref data) == 1);
            amdSmuMutex.ReleaseMutex();
            return res;
        }

        public bool SmuWaitDone()
        {
            bool res;
            ushort timeout = SMU_TIMEOUT;
            uint data = 0;

            do
                res = SmuReadReg(smu.SMU_ADDR_RSP, ref data);
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

            //if (amdSmuMutex.WaitOne(5000))
            {
                // Clear response register
                bool temp;
                do
                    temp = SmuWriteReg(smu.SMU_ADDR_RSP, 0);
                while ((!temp) && --timeout > 0);

                if (timeout == 0)
                {
                    //amdSmuMutex.ReleaseMutex();
                    SmuReadReg(smu.SMU_ADDR_RSP, ref status);
                    return (SMU.Status)status;
                }

                // Write data
                for (int i = 0; i < cmdArgs.Length; ++i)
                    SmuWriteReg(smu.SMU_ADDR_ARG + (uint)(i * 4), cmdArgs[i]);

                // Send message
                SmuWriteReg(smu.SMU_ADDR_MSG, msg);

                // Wait done
                if (!SmuWaitDone())
                {
                    //amdSmuMutex.ReleaseMutex();
                    SmuReadReg(smu.SMU_ADDR_RSP, ref status);
                    return (SMU.Status)status;
                }

                // Read back args
                for (int i = 0; i < args.Length; ++i)
                    SmuReadReg(smu.SMU_ADDR_ARG + (uint)(i * 4), ref args[i]);
            }

            //amdSmuMutex.ReleaseMutex();
            SmuReadReg(smu.SMU_ADDR_RSP, ref status);

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
            amdSmuMutex.WaitOne(5000);
            Ols.WritePciConfigDword(smu.SMU_PCI_ADDR, (byte)smu.SMU_OFFSET_ADDR, value);
            uint res = Ols.ReadPciConfigDword(smu.SMU_PCI_ADDR, (byte)smu.SMU_OFFSET_DATA);
            amdSmuMutex.ReleaseMutex();

            return res;
        }

        public double GetCoreMulti(int index = 0)
        {
            uint eax = default, edx = default;
            if (Ols.RdmsrTx(0xC0010293, ref eax, ref edx, (UIntPtr)(1 << index)) != 1)
                return 0;

            double multi = 25 * (eax & 0xFF) / (12.5 * (eax >> 8 & 0x3F));
            return Math.Round(multi * 4, MidpointRounding.ToEven) / 4;
        }

        public bool WriteMsr(uint msr, uint eax, uint edx)
        {
            bool res = true;

            for (var i = 0; i < info.logicalCores; i++)
            {
                if (Ols.WrmsrTx(msr, eax, edx, (UIntPtr)(1 << i)) != 1) res = false;
            }

            return res;
        }

        public CodeName GetCodeName(CPUInfo cpuInfo)
        {
            CodeName codeName;

            // CPU Check. Compare family, model, ext family, ext model
            switch (cpuInfo.cpuid)
            {
                case 0x00800F11: // CPU \ Zen \ Summit Ridge \ ZP - B0 \ 14nm
                case 0x00800F00: // CPU \ Zen \ Summit Ridge \ ZP - A0 \ 14nm
                    if (cpuInfo.packageType == 4 || cpuInfo.packageType == 7)
                        codeName = CodeName.Threadripper;
                    else
                        codeName = CodeName.SummitRidge;
                    break;
                case 0x00800F12:
                    codeName = CodeName.Naples;
                    break;
                case 0x00800F82: // CPU \ Zen + \ Pinnacle Ridge \ 12nm
                    if (cpuInfo.packageType == 4 || cpuInfo.packageType == 7)
                        codeName = CodeName.Colfax;
                    else
                        codeName = CodeName.PinnacleRidge;
                    break;
                case 0x00810F81: // APU \ Zen + \ Picasso \ 12nm
                    codeName = CodeName.Picasso;
                    break;
                case 0x00810F00: // APU \ Zen \ Raven Ridge \ RV - A0 \ 14nm
                case 0x00810F10: // APU \ Zen \ Raven Ridge \ 14nm
                case 0x00820F00: // APU \ Zen \ Raven Ridge 2 \ RV2 - A0 \ 14nm
                case 0x00820F01: // APU \ Zen \ Dali
                    codeName = CodeName.RavenRidge;
                    break;
                case 0x00870F10: // CPU \ Zen2 \ Matisse \ MTS - B0 \ 7nm + 14nm I/ O Die
                case 0x00870F00: // CPU \ Zen2 \ Matisse \ MTS - A0 \ 7nm + 14nm I/ O Die
                    codeName = CodeName.Matisse;
                    break;
                case 0x00830F00:
                case 0x00830F10: // CPU \ Epyc 2 \ Rome \ Treadripper 2 \ Castle Peak 7nm
                    if (cpuInfo.packageType == 7)
                        codeName = CodeName.Rome;
                    else
                        codeName = CodeName.CastlePeak;
                    break;
                case 0x00850F00: // Subor Z+
                    codeName = CodeName.Fenghuang;
                    break;
                case 0x00860F01: // APU \ Renoir
                    codeName = CodeName.Renoir;
                    break;
                case 0x00A20F00: // CPU \ Vermeer
                case 0x00A20F10:
                    codeName = CodeName.Vermeer;
                    break;
                //case 0x00A00F00: // CPU \ Genesis
                //case 0x00A00F10:
                    //codeName = SMU.CodeName.Genesis;
                    //break;
                default:
                    codeName = CodeName.Unsupported;
                    break;
            }

            return codeName;
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
            if (SendSmuCommand(smu.SMU_MSG_GetSmuVersion, ref args) == SMU.Status.OK)
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
            if (SendSmuCommand(smu.SMU_MSG_GetPBOScalar, ref args) == SMU.Status.OK)
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

            if (smu.SMU_TYPE == SMU.SmuType.TYPE_APU0)
            {
                args[0] = 3;
                args[1] = 0;
            }

            return SendSmuCommand(smu.SMU_MSG_TransferTableToDram, ref args);
        }

        public ulong GetDramBaseAddress()
        {
            uint[] args = new uint[6];
            ulong address = 0;

            SMU.Status status = SMU.Status.FAILED;

            switch (smu.SMU_TYPE)
            {
                // SummitRidge, PinnacleRidge, Colfax
                case SMU.SmuType.TYPE_CPU0:
                case SMU.SmuType.TYPE_CPU1:
                    args[0] = 0;
                    status = SendSmuCommand(smu.SMU_MSG_GetDramBaseAddress - 1, ref args);
                    if (status != SMU.Status.OK)
                        return 0;

                    status = SendSmuCommand(smu.SMU_MSG_GetDramBaseAddress, ref args);
                    if (status != SMU.Status.OK)
                        return 0;

                    address = args[0];

                    args[0] = 0;
                    status = SendSmuCommand(smu.SMU_MSG_GetDramBaseAddress + 2, ref args);
                    if (status != SMU.Status.OK)
                        return 0;
                    break;

                // Matisse, CastlePeak, Rome, Vermeer
                case SMU.SmuType.TYPE_CPU2:
                case SMU.SmuType.TYPE_CPU3:
                    status = SendSmuCommand(smu.SMU_MSG_GetDramBaseAddress, ref args);
                    if (status != SMU.Status.OK)
                        return 0;
                    address = args[0] | ((ulong)args[1] << 32);
                    break;

                // Renoir
                case SMU.SmuType.TYPE_APU1:
                    status = SendSmuCommand(smu.SMU_MSG_GetDramBaseAddress, ref args);
                    if (status != SMU.Status.OK)
                        return 0;
                    address = args[0] | ((ulong)args[1] << 32);
                    break;

                // RavenRidge, RavenRidge2, Picasso
                case SMU.SmuType.TYPE_APU0:
                    uint[] parts = new uint[2];

                    args[0] = 3;
                    status = SendSmuCommand(smu.SMU_MSG_GetDramBaseAddress - 1, ref args);
                    if (status != SMU.Status.OK)
                        return 0;

                    args[0] = 3;
                    status = SendSmuCommand(smu.SMU_MSG_GetDramBaseAddress, ref args);
                    if (status != SMU.Status.OK)
                        return 0;

                    // First base
                    parts[0] = args[0];

                    args[0] = 5;
                    status = SendSmuCommand(smu.SMU_MSG_GetDramBaseAddress - 1, ref args);
                    if (status != SMU.Status.OK)
                        return 0;

                    status = SendSmuCommand(smu.SMU_MSG_GetDramBaseAddress, ref args);
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
            return SendSmuCommand(smu.SMU_MSG_TestMessage, ref args) == SMU.Status.OK;
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
                    amdSmuMutex.ReleaseMutex();
                    Ols.DeinitializeOls();
                    Ols.Dispose();
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
