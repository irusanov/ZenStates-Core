using System;
using System.IO;

namespace ZenStates.Core.Drivers
{
    /// <summary>
    /// Linux hardware access: SMN through the root complex PCI config space (index/data pair)
    /// and MSRs through /dev/cpu/N/msr. Requires root and the msr kernel module.
    /// </summary>
    internal static class LinuxAmdAccess
    {
        private const string RootComplexConfig = "/sys/bus/pci/devices/0000:00:00.0/config";
        private const string MsrPathFormat = "/dev/cpu/{0}/msr";
        // Candidate SMN index/data register pairs in the root complex config space. The pair that works differs
        // between platforms; a wrong pair reads back zeros instead of failing.
        private static readonly long[][] SmnPairs = new long[][]
        {
            new long[] { 0x60, 0x64 },
            new long[] { 0xC4, 0xC8 },
            new long[] { 0xB8, 0xBC },
            new long[] { 0xB4, 0xB8 },
        };

        private const uint SmnProbeAddress = 0x50200; // UMC0 clock ratio register, non-zero while memory is trained
        private static long SmnIndexOffset = 0x60;
        private static long SmnDataOffset = 0x64;
        private static bool _smnPairResolved;

        private static bool _usePortIo;
        private static FileStream _port;

        public static string SmnPairInfo => $"{(_usePortIo ? "/dev/port " : "sysfs ")}0x{SmnIndexOffset:X}/0x{SmnDataOffset:X}";

        private static void ResolveSmnPair(FileStream fs)
        {
            _smnPairResolved = true;

            foreach (long[] pair in SmnPairs)
            {
                WriteConfigDword(fs, pair[0], SmnProbeAddress);
                if (ReadConfigDword(fs, pair[1], out uint value) && value != 0 && value != 0xFFFFFFFF)
                {
                    SmnIndexOffset = pair[0];
                    SmnDataOffset = pair[1];
                    return;
                }
            }

            // The sysfs config space returned nothing usable: fall back to PCI configuration mechanism #1 through
            // /dev/port (0xCF8/0xCFC), which is what other Linux tools use for the same registers.
            try
            {
                foreach (long[] pair in SmnPairs)
                {
                    PortWriteConfig(pair[0], SmnProbeAddress);
                    if (PortReadConfig(pair[1], out uint value) && value != 0 && value != 0xFFFFFFFF)
                    {
                        SmnIndexOffset = pair[0];
                        SmnDataOffset = pair[1];
                        _usePortIo = true;
                        return;
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine("Port I/O SMN probe failed: " + ex.Message);
            }
        }

        [System.Runtime.InteropServices.DllImport("libc", EntryPoint = "geteuid")]
        private static extern uint GetEffectiveUserId();

        public static string Diagnose()
        {
            System.Text.StringBuilder sb = new System.Text.StringBuilder();

            try
            {
                sb.Append("euid=").Append(GetEffectiveUserId()).Append(". ");
            }
            catch (Exception ex)
            {
                sb.Append("euid: ").Append(ex.GetType().Name).Append(". ");
            }

            try
            {
                const string lockdownPath = "/sys/kernel/security/lockdown";
                sb.Append("lockdown: ").Append(File.Exists(lockdownPath) ? File.ReadAllText(lockdownPath).Trim() : "n/a").Append(". ");
            }
            catch (Exception ex)
            {
                sb.Append("lockdown: ").Append(ex.GetType().Name).Append(". ");
            }

            lock (_smnLock)
            {
                try
                {
                    using (FileStream fs = new FileStream(RootComplexConfig, FileMode.Open, FileAccess.ReadWrite, FileShare.ReadWrite, 1))
                    {
                        ReadConfigDword(fs, 0, out uint id);
                        sb.Append($"sysfs id={id:X8}:");
                        foreach (long[] pair in SmnPairs)
                        {
                            try
                            {
                                WriteConfigDword(fs, pair[0], SmnProbeAddress);
                                ReadConfigDword(fs, pair[1], out uint value);
                                sb.Append($" {pair[0]:X}/{pair[1]:X}={value:X8}");
                            }
                            catch (Exception ex)
                            {
                                sb.Append($" {pair[0]:X}/{pair[1]:X}={ex.GetType().Name}({ex.Message})");
                            }
                        }
                    }
                }
                catch (Exception ex)
                {
                    sb.Append("sysfs: ").Append(ex.GetType().Name).Append(": ").Append(ex.Message);
                }

                sb.Append(". /dev/port:");
                foreach (long[] pair in SmnPairs)
                {
                    try
                    {
                        PortWriteConfig(pair[0], SmnProbeAddress);
                        PortReadConfig(pair[1], out uint value);
                        sb.Append($" {pair[0]:X}/{pair[1]:X}={value:X8}");
                    }
                    catch (Exception ex)
                    {
                        sb.Append(" ").Append(ex.GetType().Name).Append(": ").Append(ex.Message);
                        break;
                    }
                }
            }

            sb.Append("ryzen_smu: ").Append(IsRyzenSmuAvailable ? "loaded" : "not loaded").Append(". ");
            sb.Append(". Selected: ").Append(IsRyzenSmuAvailable ? "ryzen_smu" : SmnPairInfo).Append('.');
            return sb.ToString();
        }

        private static FileStream GetPort()
        {
            if (_port == null)
                _port = new FileStream("/dev/port", FileMode.Open, FileAccess.ReadWrite, FileShare.ReadWrite, 1);
            return _port;
        }

        // Bus 0, device 0, function 0 of the root complex.
        private static void PortWriteConfig(long reg, uint value)
        {
            FileStream p = GetPort();
            p.Seek(0xCF8, SeekOrigin.Begin);
            p.Write(BitConverter.GetBytes(0x80000000u | (uint)(reg & 0xFC)), 0, 4);
            p.Flush();
            p.Seek(0xCFC, SeekOrigin.Begin);
            p.Write(BitConverter.GetBytes(value), 0, 4);
            p.Flush();
        }

        private static bool PortReadConfig(long reg, out uint value)
        {
            FileStream p = GetPort();
            p.Seek(0xCF8, SeekOrigin.Begin);
            p.Write(BitConverter.GetBytes(0x80000000u | (uint)(reg & 0xFC)), 0, 4);
            p.Flush();
            byte[] buf = new byte[4];
            p.Seek(0xCFC, SeekOrigin.Begin);
            int read = p.Read(buf, 0, 4);
            value = read == 4 ? BitConverter.ToUInt32(buf, 0) : 0;
            return read == 4;
        }

        private static readonly object _smnLock = new object();
        private static FileStream _pciConfig;

        public static bool IsLinux
        {
            get
            {
                int p = (int)Environment.OSVersion.Platform;
                return p == 4 || p == 128;
            }
        }

        public static bool IsAvailable => File.Exists(RootComplexConfig);

        private static FileStream GetPciConfig()
        {
            if (_pciConfig == null)
                _pciConfig = new FileStream(RootComplexConfig, FileMode.Open, FileAccess.ReadWrite, FileShare.ReadWrite, 1);
            return _pciConfig;
        }

        private static void WriteConfigDword(FileStream fs, long offset, uint value)
        {
            fs.Seek(offset, SeekOrigin.Begin);
            fs.Write(BitConverter.GetBytes(value), 0, 4);
            fs.Flush();
        }

        private static bool ReadConfigDword(FileStream fs, long offset, out uint value)
        {
            byte[] buf = new byte[4];
            fs.Seek(offset, SeekOrigin.Begin);
            int read = fs.Read(buf, 0, 4);
            value = read == 4 ? BitConverter.ToUInt32(buf, 0) : 0;
            return read == 4;
        }

        private const string RyzenSmuSmnPath = "/sys/kernel/ryzen_smu_drv/smn";

        /// <summary>True when the ryzen_smu kernel module is loaded. The kernel then does the SMN access, which keeps
        /// working under kernel lockdown (Secure Boot) as long as the module is signed.</summary>
        public static bool IsRyzenSmuAvailable
        {
            get { return File.Exists(RyzenSmuSmnPath); }
        }

        private static bool RyzenSmuReadSmn(uint address, out uint data)
        {
            data = 0;
            using (FileStream fs = new FileStream(RyzenSmuSmnPath, FileMode.Open, FileAccess.ReadWrite, FileShare.ReadWrite, 1))
            {
                fs.Write(BitConverter.GetBytes(address), 0, 4);
                fs.Flush();
                fs.Seek(0, SeekOrigin.Begin);
                byte[] buf = new byte[4];
                int read = fs.Read(buf, 0, 4);
                data = read == 4 ? BitConverter.ToUInt32(buf, 0) : 0;
                return read == 4;
            }
        }

        private static bool RyzenSmuWriteSmn(uint address, uint data)
        {
            using (FileStream fs = new FileStream(RyzenSmuSmnPath, FileMode.Open, FileAccess.ReadWrite, FileShare.ReadWrite, 1))
            {
                byte[] buf = new byte[8];
                Array.Copy(BitConverter.GetBytes(address), 0, buf, 0, 4);
                Array.Copy(BitConverter.GetBytes(data), 0, buf, 4, 4);
                fs.Write(buf, 0, 8);
                fs.Flush();
                return true;
            }
        }

        public static bool ReadSmn(uint address, out uint data)
        {
            data = 0;
            try
            {
                lock (_smnLock)
                {
                    if (IsRyzenSmuAvailable)
                        return RyzenSmuReadSmn(address, out data);

                    FileStream fs = GetPciConfig();
                    if (!_smnPairResolved)
                        ResolveSmnPair(fs);
                    if (_usePortIo)
                    {
                        PortWriteConfig(SmnIndexOffset, address);
                        return PortReadConfig(SmnDataOffset, out data);
                    }
                    WriteConfigDword(fs, SmnIndexOffset, address);
                    return ReadConfigDword(fs, SmnDataOffset, out data);
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine("ReadSmn failed: " + ex.Message);
                return false;
            }
        }

        public static bool WriteSmn(uint address, uint data)
        {
            try
            {
                lock (_smnLock)
                {
                    if (IsRyzenSmuAvailable)
                        return RyzenSmuWriteSmn(address, data);

                    FileStream fs = GetPciConfig();
                    if (!_smnPairResolved)
                        ResolveSmnPair(fs);
                    if (_usePortIo)
                    {
                        PortWriteConfig(SmnIndexOffset, address);
                        PortWriteConfig(SmnDataOffset, data);
                        return true;
                    }
                    WriteConfigDword(fs, SmnIndexOffset, address);
                    WriteConfigDword(fs, SmnDataOffset, data);
                    return true;
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine("WriteSmn failed: " + ex.Message);
                return false;
            }
        }

        public static bool ReadMsr(int cpu, uint index, out ulong value)
        {
            value = 0;
            try
            {
                using (var fs = new FileStream(string.Format(MsrPathFormat, cpu), FileMode.Open, FileAccess.Read, FileShare.ReadWrite, 1))
                {
                    byte[] buf = new byte[8];
                    fs.Seek(index, SeekOrigin.Begin);
                    if (fs.Read(buf, 0, 8) != 8)
                        return false;
                    value = BitConverter.ToUInt64(buf, 0);
                    return true;
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"ReadMsr(cpu {cpu}, 0x{index:X}) failed: {ex.Message}");
                return false;
            }
        }

        public static bool WriteMsr(int cpu, uint index, ulong value)
        {
            try
            {
                using (var fs = new FileStream(string.Format(MsrPathFormat, cpu), FileMode.Open, FileAccess.Write, FileShare.ReadWrite, 1))
                {
                    fs.Seek(index, SeekOrigin.Begin);
                    fs.Write(BitConverter.GetBytes(value), 0, 8);
                    fs.Flush();
                    return true;
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"WriteMsr(cpu {cpu}, 0x{index:X}) failed: {ex.Message}");
                return false;
            }
        }

        public static bool ReadPhysical(long address, byte[] buffer)
        {
            try
            {
                using (var fs = new FileStream("/dev/mem", FileMode.Open, FileAccess.Read, FileShare.ReadWrite, 1))
                {
                    fs.Seek(address, SeekOrigin.Begin);
                    int total = 0;
                    while (total < buffer.Length)
                    {
                        int read = fs.Read(buffer, total, buffer.Length - total);
                        if (read <= 0)
                            return false;
                        total += read;
                    }
                    return true;
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"ReadPhysical(0x{address:X}) failed: {ex.Message}");
                return false;
            }
        }

        /// <summary>Maps a group/mask
        public static int CpuFromAffinity(ushort group, ulong mask)
        {
            int bit = 0;
            while (bit < 64 && (mask & (1UL << bit)) == 0)
                bit++;
            return group * 64 + (bit == 64 ? 0 : bit);
        }
    }
}
