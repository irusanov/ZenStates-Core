using OpenHardwareMonitor.Hardware;
using System;
using System.Collections.Generic;

namespace ZenStates.Core
{
    public abstract class SMU
    {
        private const ushort SMU_TIMEOUT = 8192;
        public enum MailboxType
        {
            UNSUPPORTED = 0,
            RSMU,
            MP1,
            HSMP
        }

        public enum SmuType
        {
            TYPE_CPU0 = 0x0,
            TYPE_CPU1 = 0x1,
            TYPE_CPU2 = 0x2,
            TYPE_CPU3 = 0x3,
            TYPE_CPU4 = 0x4,
            TYPE_CPU9 = 0x9,
            TYPE_APU0 = 0x10,
            TYPE_APU1 = 0x11,
            TYPE_APU2 = 0x12,
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

            Rsmu = new RSMUMailbox();
            Mp1Smu = new MP1Mailbox();
            Hsmp = new HSMPMailbox();
        }

        public uint Version { get; set; }

        public uint TableVersion { get; set; }
        //public bool ManualOverclockSupported { get; protected set; }

        public SmuType SMU_TYPE { get; protected set; }

        public uint SMU_PCI_ADDR { get; protected set; }
        public uint SMU_OFFSET_ADDR { get; protected set; }
        public uint SMU_OFFSET_DATA { get; protected set; }

        // SMU has different mailboxes, each with its own registers and command IDs
        public RSMUMailbox Rsmu { get; protected set; }
        public MP1Mailbox Mp1Smu { get; protected set; }
        public HSMPMailbox Hsmp { get; protected set; }

        private bool SmuWriteReg(uint addr, uint data)
        {
            if (Ring0.WritePciConfig(SMU_PCI_ADDR, SMU_OFFSET_ADDR, addr))
                return Ring0.WritePciConfig(SMU_PCI_ADDR, SMU_OFFSET_DATA, data);
            return false;
        }

        private bool SmuReadReg(uint addr, ref uint data)
        {
            if (Ring0.WritePciConfig(SMU_PCI_ADDR, SMU_OFFSET_ADDR, addr))
                return Ring0.ReadPciConfig(SMU_PCI_ADDR, SMU_OFFSET_DATA, out data);
            return false;
        }

        private bool SmuWaitDone(Mailbox mailbox)
        {
            bool res;
            ushort timeout = SMU_TIMEOUT;
            uint data = 0;

            // Retry until response register is non-zero and reading RSP register is successful
            do
                res = SmuReadReg(mailbox.SMU_ADDR_RSP, ref data);
            while ((!res || data == 0) && --timeout > 0);

            return timeout != 0 && data > 0;
        }

        public Status SendSmuCommand(Mailbox mailbox, uint msg, ref uint[] args)
        {
            uint status = 0xFF; // SMU.Status.FAILED;

            // Check all the arguments and don't execute if invalid
            // If the mailbox addresses are not set, they would have the default value of 0x0
            // TODO: Add custom status for not implemented command?
            if (msg == 0
                || mailbox == null
                || mailbox.SMU_ADDR_MSG == 0
                || mailbox.SMU_ADDR_ARG == 0
                || mailbox.SMU_ADDR_RSP == 0)
                return Status.UNKNOWN_CMD;

            if (Ring0.WaitPciBusMutex(10))
            {
                // Wait done
                if (!SmuWaitDone(mailbox))
                {
                    // Initial probe failed, some other command is still being processed or the PCI read failed
                    Ring0.ReleasePciBusMutex();
                    return Status.FAILED;
                }

                // Clear response register
                SmuWriteReg(mailbox.SMU_ADDR_RSP, 0);

                // Write data
                uint[] cmdArgs = Utils.MakeCmdArgs(args, mailbox.MAX_ARGS);
                for (int i = 0; i < cmdArgs.Length; ++i)
                    SmuWriteReg(mailbox.SMU_ADDR_ARG + (uint)(i * 4), cmdArgs[i]);

                // Send message
                SmuWriteReg(mailbox.SMU_ADDR_MSG, msg);

                // Wait done
                if (!SmuWaitDone(mailbox))
                {
                    // Timeout reached or PCI read failed
                    Ring0.ReleasePciBusMutex();
                    return Status.FAILED;
                }

                // If we reach this stage, read final status
                SmuReadReg(mailbox.SMU_ADDR_RSP, ref status);

                if ((Status)status == Status.OK)
                {
                    // Read back args
                    for (int i = 0; i < args.Length; ++i)
                        SmuReadReg(mailbox.SMU_ADDR_ARG + (uint)(i * 4), ref args[i]);
                }

                Ring0.ReleasePciBusMutex();
            }

            return (Status)status;
        }

        // Legacy
        [Obsolete("SendSmuCommand with one argument is deprecated, please use SendSmuCommand with full 6 args")]
        public bool SendSmuCommand(Mailbox mailbox, uint msg, uint arg)
        {
            uint[] args = Utils.MakeCmdArgs(arg, mailbox.MAX_ARGS);
            return SendSmuCommand(mailbox, msg, ref args) == Status.OK;
        }

        public Status SendMp1Command(uint msg, ref uint[] args) => SendSmuCommand(Mp1Smu, msg, ref args);
        public Status SendRsmuCommand(uint msg, ref uint[] args) => SendSmuCommand(Rsmu, msg, ref args);
        public Status SendHsmpCommand(uint msg, ref uint[] args)
        {
            if (Hsmp.IsSupported && msg <= Hsmp.HighestSupportedFunction)
                return SendSmuCommand(Hsmp, msg, ref args);
            return Status.UNKNOWN_CMD;
        }
    }

    public class BristolRidgeSettings : SMU
    {
        public BristolRidgeSettings()
        {
            SMU_TYPE = SmuType.TYPE_CPU9;

            SMU_OFFSET_ADDR = 0xB8;
            SMU_OFFSET_DATA = 0xBC;

            Rsmu.SMU_ADDR_MSG = 0x13000000;
            Rsmu.SMU_ADDR_RSP = 0x13000010;
            Rsmu.SMU_ADDR_ARG = 0x13000020;
        }
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
            Rsmu.SMU_MSG_GetFastestCoreofSocket = 0x59;
            Rsmu.SMU_MSG_SetPBOScalar = 0x58;
            Rsmu.SMU_MSG_GetPBOScalar = 0x6C;
            Rsmu.SMU_MSG_ReadBoostLimit = 0x6E;
            Rsmu.SMU_MSG_IsOverclockable = 0x6F;

            // MP1
            Mp1Smu.SMU_ADDR_MSG = 0x3B10530;
            Mp1Smu.SMU_ADDR_RSP = 0x3B1057C;
            Mp1Smu.SMU_ADDR_ARG = 0x3B109C4;

            Mp1Smu.SMU_MSG_SetToolsDramAddress = 0x6;
            Mp1Smu.SMU_MSG_EnableOcMode = 0x24;
            Mp1Smu.SMU_MSG_DisableOcMode = 0x25;
            Mp1Smu.SMU_MSG_SetOverclockFrequencyPerCore = 0x27;
            Mp1Smu.SMU_MSG_SetOverclockCpuVid = 0x28;
            Mp1Smu.SMU_MSG_SetPBOScalar = 0x2F;
            Mp1Smu.SMU_MSG_SetEDCVDDLimit = 0x3C;
            Mp1Smu.SMU_MSG_SetTDCVDDLimit = 0x3B;
            Mp1Smu.SMU_MSG_SetPPTLimit = 0x3D;
            Mp1Smu.SMU_MSG_SetHTCLimit = 0x3E;

            // HSMP
            Hsmp.SMU_ADDR_MSG = 0x3B10534;
            Hsmp.SMU_ADDR_RSP = 0x3B10980;
            Hsmp.SMU_ADDR_ARG = 0x3B109E0;
        }
    }

    // Ryzen 5000 (Vermeer), TR 5000 (Chagall)?
    public class Zen3Settings : Zen2Settings
    {
        public Zen3Settings()
        {
            SMU_TYPE = SmuType.TYPE_CPU3;

            Rsmu.SMU_MSG_SetDldoPsmMargin = 0xA;
            Rsmu.SMU_MSG_SetAllDldoPsmMargin = 0xB;
            Rsmu.SMU_MSG_GetDldoPsmMargin = 0x7C;

            Mp1Smu.SMU_MSG_SetDldoPsmMargin = 0x35;
            Mp1Smu.SMU_MSG_SetAllDldoPsmMargin = 0x36;
            Mp1Smu.SMU_MSG_GetDldoPsmMargin = 0x48;
        }
    }

    // Ryzen 7000 (Raphael)
    // Seems to be similar to Zen2 and Zen3
    public class Zen4Settings : Zen3Settings
    {
        public Zen4Settings()
        {
            SMU_TYPE = SmuType.TYPE_CPU4;

            // MP1
            Mp1Smu.SMU_MSG_SetTDCVDDLimit = 0x3C;
            Mp1Smu.SMU_MSG_SetEDCVDDLimit = 0x3D;
            Mp1Smu.SMU_MSG_SetPPTLimit = 0x3E;
            Mp1Smu.SMU_MSG_SetHTCLimit = 0x3F;

            // Unknown
            Mp1Smu.SMU_MSG_SetDldoPsmMargin = 0;
            Mp1Smu.SMU_MSG_SetAllDldoPsmMargin = 0;
            Mp1Smu.SMU_MSG_GetDldoPsmMargin = 0;

            // RSMU
            Rsmu.SMU_ADDR_MSG = 0x03B10524;
            Rsmu.SMU_ADDR_RSP = 0x03B10570;
            Rsmu.SMU_ADDR_ARG = 0x03B10A40;

            Rsmu.SMU_MSG_TransferTableToDram = 0x3;
            Rsmu.SMU_MSG_GetDramBaseAddress = 0x4;
            Rsmu.SMU_MSG_GetTableVersion = 0x5;
            Rsmu.SMU_MSG_EnableOcMode = 0x5D;
            Rsmu.SMU_MSG_DisableOcMode = 0x5E;
            Rsmu.SMU_MSG_SetOverclockFrequencyAllCores = 0x5F;
            Rsmu.SMU_MSG_SetOverclockFrequencyPerCore = 0x60;
            Rsmu.SMU_MSG_SetOverclockCpuVid = 0x61;
            Rsmu.SMU_MSG_SetPPTLimit = 0x56;
            Rsmu.SMU_MSG_SetTDCVDDLimit = 0x57;
            Rsmu.SMU_MSG_SetEDCVDDLimit = 0x58;
            Rsmu.SMU_MSG_SetHTCLimit = 0x59;
            Rsmu.SMU_MSG_SetPBOScalar = 0x5B;
            Rsmu.SMU_MSG_GetPBOScalar = 0x6D;

            Rsmu.SMU_MSG_SetDldoPsmMargin = 0x6;
            Rsmu.SMU_MSG_SetAllDldoPsmMargin = 0x7;
            Rsmu.SMU_MSG_GetDldoPsmMargin = 0xD5;
            Rsmu.SMU_MSG_GetLN2Mode = 0xDD;

            // HSMP
            Hsmp.SMU_ADDR_MSG = 0x3B10534;
            Hsmp.SMU_ADDR_RSP = 0x3B10980;
            Hsmp.SMU_ADDR_ARG = 0x3B109E0;
        }
    }

    public class Zen5Settings: Zen4Settings
    {
        public Zen5Settings()
        {
            // HSMP
            Hsmp.SMU_ADDR_MSG = 0x3B10934;
            Hsmp.SMU_ADDR_RSP = 0x3B10980;
            Hsmp.SMU_ADDR_ARG = 0x3B109E0;
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
            Rsmu.SMU_MSG_IsOverclockable = 0x88;

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
            Rsmu.SMU_MSG_IsOverclockable = 0x87;
        }
    }

    // Renoir, Cezanne, Rembrandt
    public class APUSettings1 : SMU
    {
        public APUSettings1()
        {
            SMU_TYPE = SmuType.TYPE_APU1;

            Rsmu.SMU_ADDR_MSG = 0x03B10A20;
            Rsmu.SMU_ADDR_RSP = 0x03B10A80;
            Rsmu.SMU_ADDR_ARG = 0x03B10A88;

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
            Rsmu.SMU_MSG_GetPBOScalar = 0xF;

            // MP1
            Mp1Smu.SMU_ADDR_MSG = 0x03B10528;
            Mp1Smu.SMU_ADDR_RSP = 0x03B10564;
            Mp1Smu.SMU_ADDR_ARG = 0x03B10998;

            Mp1Smu.SMU_MSG_EnableOcMode = 0x2F;
            Mp1Smu.SMU_MSG_DisableOcMode = 0x30;
            Mp1Smu.SMU_MSG_SetOverclockFrequencyPerCore = 0x32;
            Mp1Smu.SMU_MSG_SetOverclockCpuVid = 0x33;
            Mp1Smu.SMU_MSG_SetHTCLimit = 0x3E;
            Mp1Smu.SMU_MSG_SetPBOScalar = 0x49;
        }
    }

    public class APUSettings1_Cezanne : APUSettings1
    {
        public APUSettings1_Cezanne()
        {
            Rsmu.SMU_MSG_SetDldoPsmMargin = 0x52;
            Rsmu.SMU_MSG_SetAllDldoPsmMargin = 0xB1;
            Rsmu.SMU_MSG_GetDldoPsmMargin = 0xC3;
            Rsmu.SMU_MSG_SetGpuPsmMargin = 0x53;
            Rsmu.SMU_MSG_GetGpuPsmMargin = 0xC6;
        }
    }

    public class APUSettings1_Rembrandt : APUSettings1
    {
        public APUSettings1_Rembrandt()
        {
            SMU_TYPE = SmuType.TYPE_APU2;

            Rsmu.SMU_MSG_SetPBOScalar = 0x3E;

            Rsmu.SMU_MSG_SetDldoPsmMargin = 0x53;
            Rsmu.SMU_MSG_SetAllDldoPsmMargin = 0x5D;
            Rsmu.SMU_MSG_GetDldoPsmMargin = 0x2F;
            Rsmu.SMU_MSG_SetGpuPsmMargin = 0xB7;
            Rsmu.SMU_MSG_GetGpuPsmMargin = 0x30;

            // MP1
            // https://github.com/FlyGoat/RyzenAdj/blob/master/lib/nb_smu_ops.h#L45
            Mp1Smu.SMU_ADDR_MSG = 0x03B10528;
            Mp1Smu.SMU_ADDR_RSP = 0x03B10578;
            Mp1Smu.SMU_ADDR_ARG = 0x03B10998;

            Mp1Smu.SMU_MSG_SetDldoPsmMargin = 0x4B;
            Mp1Smu.SMU_MSG_SetAllDldoPsmMargin = 0x4C;
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
            { Cpu.CodeName.BristolRidge, new BristolRidgeSettings() },
            { Cpu.CodeName.Vishera, new UnsupportedSettings() },

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
            { Cpu.CodeName.Chagall, new Zen3Settings() },
            { Cpu.CodeName.Milan, new Zen3Settings() },

            // Zen4
            { Cpu.CodeName.Raphael, new Zen4Settings() },
            { Cpu.CodeName.Genoa, new Zen4Settings() },
            { Cpu.CodeName.StormPeak, new Zen4Settings() },
            { Cpu.CodeName.DragonRange, new Zen4Settings() },

            // Zen5
            { Cpu.CodeName.GraniteRidge, new Zen5Settings() },
            { Cpu.CodeName.Bergamo, new Zen5Settings() },
            { Cpu.CodeName.Turin, new Zen5Settings() },

            // APU
            { Cpu.CodeName.RavenRidge, new APUSettings0() },
            { Cpu.CodeName.FireFlight, new APUSettings0() },
            { Cpu.CodeName.Dali, new APUSettings0_Picasso() },
            { Cpu.CodeName.Picasso, new APUSettings0_Picasso() },

            { Cpu.CodeName.Renoir, new APUSettings1() },
            { Cpu.CodeName.Lucienne, new APUSettings1() },
            { Cpu.CodeName.Cezanne, new APUSettings1_Cezanne() },

            { Cpu.CodeName.Mero, new APUSettings1() }, // unknown, presumably based on VanGogh
            { Cpu.CodeName.VanGogh, new APUSettings1() }, // experimental
            { Cpu.CodeName.Rembrandt, new APUSettings1_Rembrandt() },
            // Still unknown. The MP1 addresses are the same as on Rembrand according to coreboot
            // https://github.com/coreboot/coreboot/blob/master/src/soc/amd/mendocino/include/soc/smu.h
            // https://github.com/coreboot/coreboot/blob/master/src/soc/amd/phoenix/include/soc/smu.h
            { Cpu.CodeName.Phoenix, new APUSettings1_Rembrandt() },
            { Cpu.CodeName.Phoenix2, new APUSettings1_Rembrandt() },
            { Cpu.CodeName.HawkPoint, new APUSettings1_Rembrandt() },
            { Cpu.CodeName.Mendocino, new APUSettings1_Rembrandt() },

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