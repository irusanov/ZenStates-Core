using System;
using System.Collections.Generic;

namespace ZenStates.Core
{
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Naming", "CA1707:Identifiers should not contain underscores", Justification = "<Pending>")]
    public abstract class SMU
    {
        public enum SmuType
        {
            TYPE_CPU0 = 0x0,
            TYPE_CPU1 = 0x1,
            TYPE_CPU2 = 0x2,
            TYPE_CPU3 = 0x3,
            TYPE_APU0 = 0x10,
            TYPE_APU1 = 0x11,
            TYPE_UNSUPPORTED = 0xFF,
        };

        public enum Status : byte
        {
            OK                      = 0x01,
            FAILED                  = 0xFF,
            UNKNOWN_CMD             = 0xFE,
            CMD_REJECTED_PREREQ     = 0xFD,
            CMD_REJECTED_BUSY       = 0xFC
        }

        public SMU()
        {
            Version = 0;
            // SMU
            //ManualOverclockSupported = false;

            SMU_TYPE = SmuType.TYPE_UNSUPPORTED;

            SMU_PCI_ADDR = 0x00000000;
            SMU_OFFSET_ADDR = 0x60;
            SMU_OFFSET_DATA = 0x64;

            SMU_ADDR_MSG = 0x0;
            SMU_ADDR_RSP = 0x0;
            SMU_ADDR_ARG = 0x0;

            // SMU Messages
            SMU_MSG_TestMessage = 0x1;
            SMU_MSG_GetSmuVersion = 0x2;
            SMU_MSG_GetTableVersion = 0x0;
            SMU_MSG_TransferTableToDram = 0x0;
            SMU_MSG_GetDramBaseAddress = 0x0;
            SMU_MSG_SetOverclockFrequencyAllCores = 0x0;
            SMU_MSG_SetOverclockFrequencyPerCore = 0x0;
            SMU_MSG_SetOverclockCpuVid = 0x0;
            SMU_MSG_EnableOcMode = 0x0;
            SMU_MSG_DisableOcMode = 0x0;
            SMU_MSG_GetPBOScalar = 0x0;
            SMU_MSG_SetPBOScalar = 0x0;
            SMU_MSG_SetPPTLimit = 0x0;
            SMU_MSG_SetTDCLimit = 0x0;
            SMU_MSG_SetEDCLimit = 0x0;
        }

        public uint Version { get; set; }
        public uint TableVersion { get; set; }
        //public bool ManualOverclockSupported { get; protected set; }

        public SmuType SMU_TYPE { get; protected set; }

        public uint SMU_PCI_ADDR { get; protected set; }
        public uint SMU_OFFSET_ADDR { get; protected set; }
        public uint SMU_OFFSET_DATA { get; protected set; }

        public uint SMU_ADDR_MSG { get; set; }
        public uint SMU_ADDR_RSP { get; set; }
        public uint SMU_ADDR_ARG { get; set; }

        public uint SMU_MSG_TestMessage { get; protected set; }
        public uint SMU_MSG_GetSmuVersion { get; protected set; }
        public uint SMU_MSG_GetTableVersion { get; protected set; }
        public uint SMU_MSG_TransferTableToDram { get; protected set; }
        public uint SMU_MSG_GetDramBaseAddress { get; protected set; }
        public uint SMU_MSG_SetOverclockFrequencyAllCores { get; protected set; }
        public uint SMU_MSG_SetOverclockFrequencyPerCore { get; protected set; }
        public uint SMU_MSG_SetOverclockCpuVid { get; protected set; }
        public uint SMU_MSG_EnableOcMode { get; protected set; }
        public uint SMU_MSG_DisableOcMode { get; protected set; }
        public uint SMU_MSG_GetPBOScalar { get; protected set; }
        public uint SMU_MSG_SetPBOScalar { get; protected set; }
        public uint SMU_MSG_SetPPTLimit { get; protected set; }
        public uint SMU_MSG_SetTDCLimit { get; protected set; }
        public uint SMU_MSG_SetEDCLimit { get; protected set; }
    }

    // Zen (Summit Ridge), ThreadRipper (Whitehaven)
    public class SummitRidgeSettings : SMU
    {
        public SummitRidgeSettings()
        {
            SMU_TYPE = SmuType.TYPE_CPU0;

            SMU_ADDR_MSG = 0x03B1051C;
            SMU_ADDR_RSP = 0x03B10568;
            SMU_ADDR_ARG = 0x03B10590;

            SMU_MSG_TransferTableToDram = 0xA;
            SMU_MSG_GetDramBaseAddress = 0xC;

            // SMU_MSG_EnableOcMode = 0x63; // Disable PROCHOT

            /*
            SMU_ADDR_MSG = 0x03B10528;
            SMU_ADDR_RSP = 0x03B10564;
            SMU_ADDR_ARG = 0x03B10598;

            SMU_MSG_EnableOcMode = 0x23;
            SMU_MSG_DisableOcMode = 0x24;
            SMU_MSG_SetOverclockFrequencyAllCores = 0x26;
            SMU_MSG_SetOverclockFrequencyPerCore = 0x27;
            SMU_MSG_SetOverclockCpuVid = 0x28;
            */
        }
    }

    // Zen+ (Pinnacle Ridge)
    public class ZenPSettings : SMU
    {
        public ZenPSettings()
        {
            SMU_TYPE = SmuType.TYPE_CPU1;

            SMU_ADDR_MSG = 0x03B1051C;
            SMU_ADDR_RSP = 0x03B10568;
            SMU_ADDR_ARG = 0x03B10590;

            SMU_MSG_TransferTableToDram = 0xA;
            SMU_MSG_GetDramBaseAddress = 0xC;
            SMU_MSG_EnableOcMode = 0x6B; //0x63; <-- Disable PROCHOT?
            //SMU_MSG_DisableOcMode = 0x64;
            SMU_MSG_SetOverclockFrequencyAllCores = 0x6C;
            SMU_MSG_SetOverclockFrequencyPerCore = 0x6D;
            SMU_MSG_SetOverclockCpuVid = 0x6E;
            
            SMU_MSG_SetPPTLimit = 0x64; // ?
            SMU_MSG_SetTDCLimit = 0x65; // ?
            SMU_MSG_SetEDCLimit = 0x66;

            SMU_MSG_SetPBOScalar = 0x6A;
            SMU_MSG_GetPBOScalar = 0x6F;
        }
    }

    // TR2 (Colfax) 
    public class ColfaxSettings : SMU
    {
        public ColfaxSettings()
        {
            SMU_TYPE = SmuType.TYPE_CPU1;

            SMU_ADDR_MSG = 0x03B1051C;
            SMU_ADDR_RSP = 0x03B10568;
            SMU_ADDR_ARG = 0x03B10590;

            SMU_MSG_TransferTableToDram = 0xA;
            SMU_MSG_GetDramBaseAddress = 0xC;
            SMU_MSG_EnableOcMode = 0x63;
            SMU_MSG_DisableOcMode = 0x64;
            SMU_MSG_SetOverclockFrequencyAllCores = 0x68;
            SMU_MSG_SetOverclockFrequencyPerCore = 0x69;
            SMU_MSG_SetOverclockCpuVid = 0x6A;

            SMU_MSG_SetTDCLimit = 0x6B; // ?
            SMU_MSG_SetEDCLimit = 0x6C; // ?

            SMU_MSG_SetPBOScalar = 0x6F;
            SMU_MSG_GetPBOScalar = 0x70;
        }
    }

    // Ryzen 3000 (Matisse), TR 3000 (Castle Peak)
    public class Zen2Settings : SMU
    {
        public Zen2Settings()
        {
            SMU_TYPE = SmuType.TYPE_CPU2;

            SMU_ADDR_MSG = 0x03B10524;
            SMU_ADDR_RSP = 0x03B10570;
            SMU_ADDR_ARG = 0x03B10A40;

            SMU_MSG_TransferTableToDram = 0x5;
            SMU_MSG_GetDramBaseAddress = 0x6;
            SMU_MSG_GetTableVersion = 0x8;
            SMU_MSG_EnableOcMode = 0x5A;
            SMU_MSG_DisableOcMode = 0x5B;
            SMU_MSG_SetOverclockFrequencyAllCores = 0x5C;
            SMU_MSG_SetOverclockFrequencyPerCore = 0x5D;
            SMU_MSG_SetOverclockCpuVid = 0x61;
            SMU_MSG_SetPPTLimit = 0x53;
            SMU_MSG_SetTDCLimit = 0x54;
            SMU_MSG_SetEDCLimit = 0x55;
            SMU_MSG_SetPBOScalar = 0x58;
            SMU_MSG_GetPBOScalar = 0x6C;
        }
    }

    // Ryzen 5000 (Vermeer), TR 5000 (Chagall)?
    public class Zen3Settings : Zen2Settings
    {
        public Zen3Settings()
        {
            SMU_TYPE = SmuType.TYPE_CPU3;
        }
    }

    // Epyc 2 (Rome) ES
    public class RomeSettings : SMU
    {
        public RomeSettings()
        {
            SMU_TYPE = SmuType.TYPE_CPU2;

            SMU_ADDR_MSG = 0x03B10524;
            SMU_ADDR_RSP = 0x03B10570;
            SMU_ADDR_ARG = 0x03B10A40;

            SMU_MSG_TransferTableToDram = 0x5;
            SMU_MSG_GetDramBaseAddress = 0x6;
            SMU_MSG_GetTableVersion = 0x8;
            SMU_MSG_SetOverclockFrequencyAllCores = 0x18;
            // SMU_MSG_SetOverclockFrequencyPerCore = 0x19;
            SMU_MSG_SetOverclockCpuVid = 0x12;
        }
    }

    // RavenRidge, RavenRidge 2, FireFlight, Picasso
    public class APUSettings0 : SMU
    {
        public APUSettings0()
        {
            SMU_TYPE = SmuType.TYPE_APU0;

            SMU_ADDR_MSG = 0x03B10A20;
            SMU_ADDR_RSP = 0x03B10A80;
            SMU_ADDR_ARG = 0x03B10A88;

            SMU_MSG_GetDramBaseAddress = 0xB;
            SMU_MSG_GetTableVersion = 0xC;
            SMU_MSG_TransferTableToDram = 0x3D;

            SMU_MSG_GetPBOScalar = 0x62;
            SMU_MSG_EnableOcMode = 0x69;
            SMU_MSG_DisableOcMode = 0x6A;
            SMU_MSG_SetOverclockFrequencyAllCores = 0x7D;
            SMU_MSG_SetOverclockFrequencyPerCore = 0x7E;
            SMU_MSG_SetOverclockCpuVid = 0x7F;
        }
    }

    // Renoir
    public class APUSettings1 : SMU
    {
        public APUSettings1()
        {
            SMU_TYPE = SmuType.TYPE_APU1;

            SMU_ADDR_MSG = 0x03B10A20;
            SMU_ADDR_RSP = 0x03B10A80;
            SMU_ADDR_ARG = 0x03B10A88;

            //SMU_MSG_GetPBOScalar = 0xF;
            SMU_MSG_GetTableVersion = 0x6;
            SMU_MSG_TransferTableToDram = 0x65;
            SMU_MSG_GetDramBaseAddress = 0x66;
            SMU_MSG_EnableOcMode = 0x17;
            SMU_MSG_DisableOcMode = 0x18;
            SMU_MSG_SetOverclockFrequencyAllCores = 0x19;
            SMU_MSG_SetOverclockFrequencyPerCore = 0x1A;
            SMU_MSG_SetOverclockCpuVid = 0x1B;
        }
    }

    public class UnsupportedSettings : SMU
    {
        public UnsupportedSettings()
        {
            SMU_TYPE = SmuType.TYPE_UNSUPPORTED;
        }
    }

    public static class GetMaintainedSettings
    {
        private static readonly Dictionary<Cpu.CodeName, SMU> settings = new Dictionary<Cpu.CodeName, SMU>()
        {
            // Zen
            { Cpu.CodeName.SummitRidge, new SummitRidgeSettings() },
            { Cpu.CodeName.Naples, new SummitRidgeSettings() },
            { Cpu.CodeName.Whitehaven, new SummitRidgeSettings() },

            // Zen+
            { Cpu.CodeName.PinnacleRidge, new ZenPSettings() },
            { Cpu.CodeName.Colfax, new ColfaxSettings() },

            // Zen2
            { Cpu.CodeName.Matisse, new Zen2Settings() },
            { Cpu.CodeName.CastlePeak, new Zen2Settings() },
            { Cpu.CodeName.Rome, new RomeSettings() },

            // Zen3
            { Cpu.CodeName.Vermeer, new Zen3Settings() },
            // Chagall and Milan are unknown for now
            { Cpu.CodeName.Chagall, new UnsupportedSettings() },
            { Cpu.CodeName.Milan, new UnsupportedSettings() },

            // APU
            { Cpu.CodeName.RavenRidge, new APUSettings0() },
            { Cpu.CodeName.Dali, new APUSettings0() },
            { Cpu.CodeName.FireFlight, new APUSettings0() },
            { Cpu.CodeName.Picasso, new APUSettings0() },

            { Cpu.CodeName.Renoir, new APUSettings1() },
            { Cpu.CodeName.VanGogh, new UnsupportedSettings() },
            { Cpu.CodeName.Cezanne, new UnsupportedSettings() },
            { Cpu.CodeName.Rembrandt, new UnsupportedSettings() },

            { Cpu.CodeName.Unsupported, new UnsupportedSettings() },
        };

        public static SMU GetByType(Cpu.CodeName type)
        {
            if (!settings.TryGetValue(type, out SMU output))
            {
                throw new NotImplementedException();
            }
            return output;
        }
    }

    public static class GetSMUStatus
    {
        private static readonly Dictionary<SMU.Status, String> status = new Dictionary<SMU.Status, string>()
        {
            { SMU.Status.OK, "OK" },
            { SMU.Status.FAILED, "Failed" },
            { SMU.Status.UNKNOWN_CMD, "Unknown Command" },
            { SMU.Status.CMD_REJECTED_PREREQ, "CMD Rejected Prereq" },
            { SMU.Status.CMD_REJECTED_BUSY, "CMD Rejected Busy" }
        };

        public static string GetByType(SMU.Status type)
        {
            if (!status.TryGetValue(type, out string output))
            {
                return "Unknown Status";
            }
            return output;
        }
    }
}
