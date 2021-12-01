using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;

namespace ZenStates.Core
{
    [SuppressMessage("Naming", "CA1707:Identifiers should not contain underscores", Justification = "<Pending>")]
    public abstract class SMU
    {
        public enum MailboxType
        {
            UNSUPPORTED = 0,
            RSMU,
            MP1
        }

        public enum SmuType
        {
            TYPE_CPU0 = 0x0,
            TYPE_CPU1 = 0x1,
            TYPE_CPU2 = 0x2,
            TYPE_CPU3 = 0x3,
            TYPE_APU0 = 0x10,
            TYPE_APU1 = 0x11,
            TYPE_UNSUPPORTED = 0xFF
        }

        public enum Status : byte
        {
            OK = 0x01,
            FAILED = 0xFF,
            UNKNOWN_CMD = 0xFE,
            CMD_REJECTED_PREREQ = 0xFD,
            CMD_REJECTED_BUSY = 0xFC
        }

        protected internal SMU()
        {
            Version = 0;
            // SMU
            //ManualOverclockSupported = false;

            SMU_TYPE = SmuType.TYPE_UNSUPPORTED;

            SMU_PCI_ADDR = 0x00000000;
            SMU_OFFSET_ADDR = 0x60; // 0xC4
            SMU_OFFSET_DATA = 0x64; // 0xC8

            Rsmu = new Mailbox();
            Mp1Smu = new Mailbox();
            Hsmp = new Mailbox();
        }

        public uint Version { get; set; }

        public uint TableVersion { get; set; }
        //public bool ManualOverclockSupported { get; protected set; }

        public SmuType SMU_TYPE { get; protected set; }

        public uint SMU_PCI_ADDR { get; protected set; }
        public uint SMU_OFFSET_ADDR { get; protected set; }
        public uint SMU_OFFSET_DATA { get; protected set; }

        // SMU has different mailboxes, each with its own registers and command IDs
        public Mailbox Rsmu { get; protected set; }
        public Mailbox Mp1Smu { get; protected set; }
        public Mailbox Hsmp { get; protected set; }
    }

    // Zen (Summit Ridge), ThreadRipper (Whitehaven)
    public class SummitRidgeSettings : SMU
    {
        public SummitRidgeSettings()
        {
            SMU_TYPE = SmuType.TYPE_CPU0;

            // RSMU
            Rsmu.SMU_ADDR_MSG = 0x03B1051C;
            Rsmu.SMU_ADDR_RSP = 0x03B10568;
            Rsmu.SMU_ADDR_ARG = 0x03B10590;

            Rsmu.SMU_MSG_TransferTableToDram = 0xA;
            Rsmu.SMU_MSG_GetDramBaseAddress = 0xC;
            // Rsmu.SMU_MSG_EnableOcMode = 0x63; // Disable PROCHOT?

            // MP1
            Mp1Smu.SMU_ADDR_MSG = 0x03B10528;
            Mp1Smu.SMU_ADDR_RSP = 0x03B10564;
            Mp1Smu.SMU_ADDR_ARG = 0x03B10598;

            //Mp1Smu.SMU_MSG_TransferTableToDram = 0x21; // ?
            Mp1Smu.SMU_MSG_EnableOcMode = 0x23;
            Mp1Smu.SMU_MSG_DisableOcMode = 0x24; // is this still working?
            Mp1Smu.SMU_MSG_SetOverclockFrequencyAllCores = 0x26;
            Mp1Smu.SMU_MSG_SetOverclockFrequencyPerCore = 0x27;
            Mp1Smu.SMU_MSG_SetOverclockCpuVid = 0x28;
        }
    }

    // Zen+ (Pinnacle Ridge)
    public class ZenPSettings : SMU
    {
        public ZenPSettings()
        {
            SMU_TYPE = SmuType.TYPE_CPU1;

            // RSMU
            Rsmu.SMU_ADDR_MSG = 0x03B1051C;
            Rsmu.SMU_ADDR_RSP = 0x03B10568;
            Rsmu.SMU_ADDR_ARG = 0x03B10590;

            Rsmu.SMU_MSG_TransferTableToDram = 0xA;
            Rsmu.SMU_MSG_GetDramBaseAddress = 0xC;
            Rsmu.SMU_MSG_EnableOcMode = 0x6B; //0x63; <-- Disable PROCHOT?
            //SMU_MSG_DisableOcMode = 0x64;
            Rsmu.SMU_MSG_SetOverclockFrequencyAllCores = 0x6C;
            Rsmu.SMU_MSG_SetOverclockFrequencyPerCore = 0x6D;
            Rsmu.SMU_MSG_SetOverclockCpuVid = 0x6E;

            Rsmu.SMU_MSG_SetPPTLimit = 0x64; // ?
            Rsmu.SMU_MSG_SetTDCVDDLimit = 0x65; // ?
            Rsmu.SMU_MSG_SetEDCVDDLimit = 0x66;
            Rsmu.SMU_MSG_SetHTCLimit = 0x68;

            Rsmu.SMU_MSG_SetPBOScalar = 0x6A;
            Rsmu.SMU_MSG_GetPBOScalar = 0x6F;

            // MP1
            Mp1Smu.SMU_ADDR_MSG = 0x03B10528;
            Mp1Smu.SMU_ADDR_RSP = 0x03B10564;
            Mp1Smu.SMU_ADDR_ARG = 0x03B10598;
        }
    }

    // TR2 (Colfax) 
    public class ColfaxSettings : SMU
    {
        public ColfaxSettings()
        {
            SMU_TYPE = SmuType.TYPE_CPU1;

            // RSMU
            Rsmu.SMU_ADDR_MSG = 0x03B1051C;
            Rsmu.SMU_ADDR_RSP = 0x03B10568;
            Rsmu.SMU_ADDR_ARG = 0x03B10590;

            Rsmu.SMU_MSG_TransferTableToDram = 0xA;
            Rsmu.SMU_MSG_GetDramBaseAddress = 0xC;
            Rsmu.SMU_MSG_EnableOcMode = 0x63;
            Rsmu.SMU_MSG_DisableOcMode = 0x64;
            Rsmu.SMU_MSG_SetOverclockFrequencyAllCores = 0x68;
            Rsmu.SMU_MSG_SetOverclockFrequencyPerCore = 0x69;
            Rsmu.SMU_MSG_SetOverclockCpuVid = 0x6A;

            Rsmu.SMU_MSG_SetTDCVDDLimit = 0x6B; // ?
            Rsmu.SMU_MSG_SetEDCVDDLimit = 0x6C; // ?
            Rsmu.SMU_MSG_SetHTCLimit = 0x6E;

            Rsmu.SMU_MSG_SetPBOScalar = 0x6F;
            Rsmu.SMU_MSG_GetPBOScalar = 0x70;

            // MP1
            Mp1Smu.SMU_ADDR_MSG = 0x03B10528;
            Mp1Smu.SMU_ADDR_RSP = 0x03B10564;
            Mp1Smu.SMU_ADDR_ARG = 0x03B10598;
        }
    }

    // Ryzen 3000 (Matisse), TR 3000 (Castle Peak)
    public class Zen2Settings : SMU
    {
        public Zen2Settings()
        {
            SMU_TYPE = SmuType.TYPE_CPU2;

            // RSMU
            Rsmu.SMU_ADDR_MSG = 0x03B10524;
            Rsmu.SMU_ADDR_RSP = 0x03B10570;
            Rsmu.SMU_ADDR_ARG = 0x03B10A40;

            Rsmu.SMU_MSG_TransferTableToDram = 0x5;
            Rsmu.SMU_MSG_GetDramBaseAddress = 0x6;
            Rsmu.SMU_MSG_GetTableVersion = 0x8;
            Rsmu.SMU_MSG_EnableOcMode = 0x5A;
            Rsmu.SMU_MSG_DisableOcMode = 0x5B;
            Rsmu.SMU_MSG_SetOverclockFrequencyAllCores = 0x5C;
            Rsmu.SMU_MSG_SetOverclockFrequencyPerCore = 0x5D;
            Rsmu.SMU_MSG_SetOverclockCpuVid = 0x61;
            Rsmu.SMU_MSG_SetPPTLimit = 0x53;
            Rsmu.SMU_MSG_SetTDCVDDLimit = 0x54;
            Rsmu.SMU_MSG_SetEDCVDDLimit = 0x55;
            Rsmu.SMU_MSG_SetHTCLimit = 0x56;
            Rsmu.SMU_MSG_SetPBOScalar = 0x58;
            Rsmu.SMU_MSG_GetPBOScalar = 0x6C;
            //Rsmu.ReadBoostLimit = 0x6E;

            // MP1
            Mp1Smu.SMU_ADDR_MSG = 0x3B10530;
            Mp1Smu.SMU_ADDR_RSP = 0x3B1057C;
            Mp1Smu.SMU_ADDR_ARG = 0x3B109C4;

            // HSMP
            Hsmp.SMU_ADDR_MSG = 0x3B10534;
            Hsmp.SMU_ADDR_RSP = 0x3B10980;
            Hsmp.SMU_ADDR_ARG = 0x3B109E0;

            Hsmp.GetInterfaceVersion = 0x3;
            Hsmp.ReadSocketPower = 0x4;
            Hsmp.WriteSocketPowerLimit = 0x5;
            Hsmp.ReadSocketPowerLimit = 0x6;
            Hsmp.ReadMaxSocketPowerLimit = 0x7;
            Hsmp.WriteBoostLimit = 0x8;
            Hsmp.WriteBoostLimitAllCores = 0x9;
            Hsmp.ReadBoostLimit = 0xA;
            Hsmp.ReadProchotStatus = 0xB;
            Hsmp.SetXgmiLinkWidthRange = 0xC;
            Hsmp.APBDisable = 0xD;
            Hsmp.APBEnable = 0xE;
            Hsmp.ReadCurrentFclkMemclk = 0xF;
            Hsmp.ReadCclkFrequencyLimit = 0x10;
            Hsmp.ReadSocketC0Residency = 0x11;
            Hsmp.SetLclkDpmLevelRange = 0x12;
            //Hsmp.Reserved = 0x13;
            Hsmp.GetMaxDDRBandwidthAndUtilization = 0x14;
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

            // RSMU
            Rsmu.SMU_ADDR_MSG = 0x03B10524;
            Rsmu.SMU_ADDR_RSP = 0x03B10570;
            Rsmu.SMU_ADDR_ARG = 0x03B10A40;

            Rsmu.SMU_MSG_TransferTableToDram = 0x5;
            Rsmu.SMU_MSG_GetDramBaseAddress = 0x6;
            Rsmu.SMU_MSG_GetTableVersion = 0x8;
            Rsmu.SMU_MSG_SetOverclockFrequencyAllCores = 0x18;
            // SMU_MSG_SetOverclockFrequencyPerCore = 0x19;
            Rsmu.SMU_MSG_SetOverclockCpuVid = 0x12;

            // MP1
            Mp1Smu.SMU_ADDR_MSG = 0x3B10530;
            Mp1Smu.SMU_ADDR_RSP = 0x3B1057C;
            Mp1Smu.SMU_ADDR_ARG = 0x3B109C4;
        }
    }

    // RavenRidge, RavenRidge 2, FireFlight, Picasso
    public class APUSettings0 : SMU
    {
        public APUSettings0()
        {
            SMU_TYPE = SmuType.TYPE_APU0;

            Rsmu.SMU_ADDR_MSG = 0x03B10A20;
            Rsmu.SMU_ADDR_RSP = 0x03B10A80;
            Rsmu.SMU_ADDR_ARG = 0x03B10A88;

            Rsmu.SMU_MSG_GetDramBaseAddress = 0xB;
            Rsmu.SMU_MSG_GetTableVersion = 0xC;
            Rsmu.SMU_MSG_TransferTableToDram = 0x3D;
            Rsmu.SMU_MSG_EnableOcMode = 0x69;
            Rsmu.SMU_MSG_DisableOcMode = 0x6A;
            Rsmu.SMU_MSG_SetOverclockFrequencyAllCores = 0x7D;
            Rsmu.SMU_MSG_SetOverclockFrequencyPerCore = 0x7E;
            Rsmu.SMU_MSG_SetOverclockCpuVid = 0x7F;
            Rsmu.SMU_MSG_GetPBOScalar = 0x68;

            // MP1
            Mp1Smu.SMU_ADDR_MSG = 0x03B10528;
            Mp1Smu.SMU_ADDR_RSP = 0x03B10564;
            Mp1Smu.SMU_ADDR_ARG = 0x03B10998;
        }
    }

    public class APUSettings0_Picasso : APUSettings0
    {
        public APUSettings0_Picasso()
        {
            Rsmu.SMU_MSG_GetPBOScalar = 0x62;
        }
    }

    // Renoir, Cezanne
    public class APUSettings1 : SMU
    {
        public APUSettings1()
        {
            SMU_TYPE = SmuType.TYPE_APU1;

            Rsmu.SMU_ADDR_MSG = 0x03B10A20;
            Rsmu.SMU_ADDR_RSP = 0x03B10A80;
            Rsmu.SMU_ADDR_ARG = 0x03B10A88;

            //SMU_MSG_GetPBOScalar = 0xF;
            Rsmu.SMU_MSG_GetTableVersion = 0x6;
            Rsmu.SMU_MSG_TransferTableToDram = 0x65;
            Rsmu.SMU_MSG_GetDramBaseAddress = 0x66;
            Rsmu.SMU_MSG_EnableOcMode = 0x17;
            Rsmu.SMU_MSG_DisableOcMode = 0x18;
            Rsmu.SMU_MSG_SetOverclockFrequencyAllCores = 0x19;
            Rsmu.SMU_MSG_SetOverclockFrequencyPerCore = 0x1A;
            Rsmu.SMU_MSG_SetOverclockCpuVid = 0x1B;
            Rsmu.SMU_MSG_SetPPTLimit = 0x33;
            Rsmu.SMU_MSG_SetHTCLimit = 0x37;
            Rsmu.SMU_MSG_SetTDCVDDLimit = 0x38;
            Rsmu.SMU_MSG_SetTDCSOCLimit = 0x39;
            Rsmu.SMU_MSG_SetEDCVDDLimit = 0x3A;
            Rsmu.SMU_MSG_SetEDCSOCLimit = 0x3B;
            Rsmu.SMU_MSG_SetPBOScalar = 0x3F;
            Rsmu.SMU_MSG_GetPBOScalar = 0x0F;

            // MP1
            Mp1Smu.SMU_ADDR_MSG = 0x03B10528;
            Mp1Smu.SMU_ADDR_RSP = 0x03B10564;
            Mp1Smu.SMU_ADDR_ARG = 0x03B10598;
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
        private static readonly Dictionary<Cpu.CodeName, SMU> settings = new Dictionary<Cpu.CodeName, SMU>
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
            // Chagall is unknown for now
            { Cpu.CodeName.Chagall, new UnsupportedSettings() },
            { Cpu.CodeName.Milan, new Zen3Settings() },

            // APU
            { Cpu.CodeName.RavenRidge, new APUSettings0() },
            { Cpu.CodeName.Dali, new APUSettings0() },
            { Cpu.CodeName.FireFlight, new APUSettings0() },
            { Cpu.CodeName.Picasso, new APUSettings0_Picasso() },

            { Cpu.CodeName.Renoir, new APUSettings1() },
            { Cpu.CodeName.Lucienne, new APUSettings1() },
            { Cpu.CodeName.Cezanne, new APUSettings1() },

            { Cpu.CodeName.VanGogh, new UnsupportedSettings() },
            { Cpu.CodeName.Rembrandt, new UnsupportedSettings() },

            { Cpu.CodeName.Unsupported, new UnsupportedSettings() },
        };

        public static SMU GetByType(Cpu.CodeName type)
        {
            if (!settings.TryGetValue(type, out SMU output))
            {
                return new UnsupportedSettings();
            }
            return output;
        }
    }

    public static class GetSMUStatus
    {
        private static readonly Dictionary<SMU.Status, string> status = new Dictionary<SMU.Status, string>
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