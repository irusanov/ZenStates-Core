using System;
using System.Collections.Generic;
using ZenStates.Core.SMUSettings;

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
            CMD_REJECTED_BUSY = 0xFC,
            // custom status codes
            TIMEOUT_MUTEX_LOCK = 0x30,
            TIMEOUT_MAILBOX_READY = 0x31,
            TIMEOUT_MAILBOX_MSG_WRITE = 0x32,
            PCI_FAILED = 0x33,
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

        protected SMU()
        {
            Initialize();
        }

        private void Initialize()
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

        private static RyzenSmu _ryzenSmu;

        public static void SetRyzenSmu(RyzenSmu ryzenSmu)
        {
            _ryzenSmu = ryzenSmu;
        }

        private bool SmuWriteReg(uint addr, uint data)
        {
            if (addr > uint.MaxValue) return false;

            return _ryzenSmu.SmuWriteRegInternal(addr, data);
        }

        private bool SmuReadReg(uint addr, ref uint data)
        {
            if (addr > uint.MaxValue) return false;

            return _ryzenSmu.SmuReadRegInternal(addr, out data);
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

        // Check all the arguments and don't execute if invalid
        // If the mailbox addresses are not set, they would have the default value of 0x0
        // TODO: Add custom status for not implemented command?
        private static bool ValidateMailbox(Mailbox mailbox, uint msg)
        {
            return mailbox != null &&
                   mailbox.SMU_ADDR_MSG != 0 &&
                   mailbox.SMU_ADDR_ARG != 0 &&
                   mailbox.SMU_ADDR_RSP != 0 &&
                   msg != 0;
        }

        public Status SendSmuCommand(Mailbox mailbox, uint msg, ref uint[] args)
        {
            if (!ValidateMailbox(mailbox, msg))
                return Status.UNKNOWN_CMD;

            if (!Mutexes.WaitPciBus(5000))
                return Status.TIMEOUT_MUTEX_LOCK;

            try
            {
                // Wait done
                if (!SmuWaitDone(mailbox))
                {
                    // Initial probe failed, some other command is still being processed or the PCI read failed
                    return Status.TIMEOUT_MAILBOX_READY;
                }

                uint maxValidArgAddress = uint.MaxValue - mailbox.MAX_ARGS * 4;

                // Clear response register
                if (!SmuWriteReg(mailbox.SMU_ADDR_RSP, 0))
                {
                    // PCI write failed
                    return Status.PCI_FAILED;
                }

                // Write data
                uint[] cmdArgs = Utils.MakeCmdArgs(args, mailbox.MAX_ARGS);

                for (int i = 0; i < cmdArgs.Length; ++i)
                {
                    if (mailbox.SMU_ADDR_ARG > maxValidArgAddress)
                        continue;

                    if (!SmuWriteReg(mailbox.SMU_ADDR_ARG + (uint)(i * 4), cmdArgs[i]))
                    {
                        // PCI write failed
                        return Status.PCI_FAILED;
                    }
                }

                // Send message
                if (!SmuWriteReg(mailbox.SMU_ADDR_MSG, msg))
                {
                    // PCI write failed
                    return Status.PCI_FAILED;
                }

                // Wait done
                if (!SmuWaitDone(mailbox))
                {
                    // Timeout reached or PCI read failed
                    return Status.TIMEOUT_MAILBOX_MSG_WRITE;
                }

                uint status = 0;
                // If we reach this stage, read final status
                if (!SmuReadReg(mailbox.SMU_ADDR_RSP, ref status))
                {
                    // PCI read failed
                    return Status.PCI_FAILED;
                }

                if (status > byte.MaxValue)
                {
                    // Invalid status code
                    return Status.FAILED;
                }

                if (unchecked((Status)status) == Status.OK)
                {
                    // Read back args
                    for (int i = 0; i < args.Length; ++i)
                    {
                        if (mailbox.SMU_ADDR_ARG > maxValidArgAddress)
                            continue;

                        if (!SmuReadReg(mailbox.SMU_ADDR_ARG + (uint)(i * 4), ref args[i]))
                        {
                            // PCI read failed
                            return Status.PCI_FAILED;
                        }
                    }
                }
                return unchecked((Status)status);
            }
            catch
            {
                return Status.FAILED;
            }
            finally
            {
                Mutexes.ReleasePciBus();
            }
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

    public static class GetMaintainedSettings
    {
        private static readonly Dictionary<Cpu.CodeName, SMU> settings = new Dictionary<Cpu.CodeName, SMU>
        {
            { Cpu.CodeName.BristolRidge, new BristolRidgeSettings() },

            // Zen
            { Cpu.CodeName.SummitRidge, new ZenSettings() },
            { Cpu.CodeName.Naples, new ZenSettings() },
            { Cpu.CodeName.Whitehaven, new ZenSettings() },

            // Zen+
            { Cpu.CodeName.PinnacleRidge, new ZenPSettings() },
            { Cpu.CodeName.Colfax, new ZenPSettings_Colfax() },

            // Zen2
            { Cpu.CodeName.Matisse, new Zen2Settings() },
            { Cpu.CodeName.CastlePeak, new Zen2Settings() },
            { Cpu.CodeName.Rome, new Zen2Settings_Rome() },

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
            // Experimental
            { Cpu.CodeName.Turin, new Zen5Settings() },
            { Cpu.CodeName.TurinD, new Zen5Settings() },
            { Cpu.CodeName.ShimadaPeak, new Zen5Settings_ShimadaPeak() },

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
            { Cpu.CodeName.Rembrandt, new APUSettings1_Phoenix() },
            // https://github.com/coreboot/coreboot/blob/master/src/soc/amd/mendocino/include/soc/smu.h
            // https://github.com/coreboot/coreboot/blob/master/src/soc/amd/phoenix/include/soc/smu.h
            { Cpu.CodeName.Phoenix, new APUSettings1_Phoenix() },
            { Cpu.CodeName.Phoenix2, new APUSettings1_Phoenix() },
            { Cpu.CodeName.HawkPoint, new APUSettings1_Phoenix() },
            { Cpu.CodeName.Mendocino, new APUSettings1_Phoenix() },

            { Cpu.CodeName.StrixPoint, new APUSettings1_Phoenix() },
            { Cpu.CodeName.StrixHalo, new APUSettings1_Phoenix() },
            { Cpu.CodeName.KrackanPoint, new APUSettings1_Phoenix() },

            { Cpu.CodeName.KrackanPoint2, new Zen4Settings() },

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