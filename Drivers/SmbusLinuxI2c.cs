using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;

namespace ZenStates.Core.Drivers
{
    /// <summary>
    /// Linux SMBus driver on top of /dev/i2c-N (i2c-dev). Port N maps to the N-th PIIX4 adapter.
    /// Requires root and the i2c-dev and i2c-piix4 kernel modules.
    /// </summary>
    internal sealed class SmbusLinuxI2c : SmbusDriverBase
    {
        private const int O_RDWR = 2;
        private const ulong I2C_SLAVE_FORCE = 0x0706;
        private const ulong I2C_SMBUS = 0x0720;
        private const int DATA_SIZE = 34;

        private static volatile SmbusLinuxI2c _instance;
        private static readonly object _instanceLock = new object();

        private readonly object _ioLock = new object();
        private int _fd = -1;
        private int _openBus = -1;
        private int _currentPort;
        private int _currentAddr = -1;
        private bool _disposed;

        [StructLayout(LayoutKind.Sequential)]
        private struct I2cSmbusIoctlData
        {
            public byte ReadWrite;
            public byte Command;
            public int Size;
            public IntPtr Data;
        }

        [DllImport("libc", SetLastError = true)]
        private static extern int open([MarshalAs(UnmanagedType.LPStr)] string path, int flags);

        [DllImport("libc", SetLastError = true)]
        private static extern int close(int fd);

        [DllImport("libc", SetLastError = true, EntryPoint = "ioctl")]
        private static extern int ioctl_addr(int fd, ulong request, IntPtr arg);

        public static SmbusLinuxI2c Instance
        {
            get
            {
                if (_instance == null)
                {
                    lock (_instanceLock)
                    {
                        if (_instance == null)
                            _instance = new SmbusLinuxI2c();
                    }
                }
                return _instance;
            }
        }

        private SmbusLinuxI2c() { }

        private static List<int> FindBuses()
        {
            var buses = new List<int>();
            try
            {
                foreach (string dir in Directory.GetDirectories("/sys/class/i2c-dev"))
                {
                    string leaf = Path.GetFileName(dir);
                    if (!leaf.StartsWith("i2c-"))
                        continue;

                    string name = File.ReadAllText(Path.Combine(dir, "name"));
                    if (name.IndexOf("PIIX4", StringComparison.OrdinalIgnoreCase) >= 0
                        && int.TryParse(leaf.Substring(4), out int bus))
                    {
                        buses.Add(bus);
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine("SMBus adapter scan failed: " + ex.Message);
            }

            buses.Sort();
            return buses;
        }

        private bool EnsureOpen(byte addr7)
        {
            if (_disposed)
                return false;

            List<int> buses = FindBuses();
            if (_currentPort < 0 || _currentPort >= buses.Count)
                return false;

            int bus = buses[_currentPort];
            if (_fd < 0 || _openBus != bus)
            {
                CloseFd();
                int fd = open("/dev/i2c-" + bus, O_RDWR);
                if (fd < 0)
                    return false;
                _fd = fd;
                _openBus = bus;
                _currentAddr = -1;
            }

            if (_currentAddr != addr7)
            {
                if (ioctl_addr(_fd, I2C_SLAVE_FORCE, (IntPtr)addr7) < 0)
                    return false;
                _currentAddr = addr7;
            }

            return true;
        }

        private void CloseFd()
        {
            if (_fd >= 0)
                close(_fd);
            _fd = -1;
            _openBus = -1;
            _currentAddr = -1;
        }

        private bool Xfer(byte addr7, byte rw, byte command, int size, byte[] buffer)
        {
            lock (_ioLock)
            {
                try
                {
                    if (!EnsureOpen(addr7))
                        return false;

                    IntPtr data = IntPtr.Zero;
                    IntPtr args = Marshal.AllocHGlobal(Marshal.SizeOf(typeof(I2cSmbusIoctlData)));
                    try
                    {
                        if (buffer != null)
                        {
                            data = Marshal.AllocHGlobal(DATA_SIZE);
                            byte[] zero = new byte[DATA_SIZE];
                            Array.Copy(buffer, zero, Math.Min(buffer.Length, DATA_SIZE));
                            Marshal.Copy(zero, 0, data, DATA_SIZE);
                        }

                        var s = new I2cSmbusIoctlData { ReadWrite = rw, Command = command, Size = size, Data = data };
                        Marshal.StructureToPtr(s, args, false);

                        if (ioctl_addr(_fd, I2C_SMBUS, args) < 0)
                            return false;

                        if (buffer != null)
                        {
                            byte[] result = new byte[DATA_SIZE];
                            Marshal.Copy(data, result, 0, DATA_SIZE);
                            Array.Copy(result, buffer, Math.Min(buffer.Length, DATA_SIZE));
                        }

                        return true;
                    }
                    finally
                    {
                        if (data != IntPtr.Zero)
                            Marshal.FreeHGlobal(data);
                        Marshal.FreeHGlobal(args);
                    }
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine("SMBus xfer failed: " + ex.Message);
                    return false;
                }
            }
        }

        internal override bool SmbusQuickNoLock(byte addr7, byte readWrite)
        {
            return Xfer(addr7, readWrite, 0, I2C_SMBUS_QUICK, null);
        }

        internal override bool ReadByteDataNoLock(byte addr7, byte command, out byte value)
        {
            byte[] buf = new byte[DATA_SIZE];
            bool ok = Xfer(addr7, I2C_SMBUS_READ, command, I2C_SMBUS_BYTE_DATA, buf);
            value = buf[0];
            return ok;
        }

        internal override bool WriteByteDataNoLock(byte addr7, byte command, byte value)
        {
            byte[] buf = new byte[DATA_SIZE];
            buf[0] = value;
            return Xfer(addr7, I2C_SMBUS_WRITE, command, I2C_SMBUS_BYTE_DATA, buf);
        }

        internal override bool ReadWordDataNoLock(byte addr7, byte command, out ushort value)
        {
            byte[] buf = new byte[DATA_SIZE];
            bool ok = Xfer(addr7, I2C_SMBUS_READ, command, I2C_SMBUS_WORD_DATA, buf);
            value = (ushort)(buf[0] | (buf[1] << 8));
            return ok;
        }

        internal override bool WriteWordDataNoLock(byte addr7, byte command, ushort value)
        {
            byte[] buf = new byte[DATA_SIZE];
            buf[0] = (byte)(value & 0xFF);
            buf[1] = (byte)(value >> 8);
            return Xfer(addr7, I2C_SMBUS_WRITE, command, I2C_SMBUS_WORD_DATA, buf);
        }

        internal override bool ReadBlockDataNoLock(byte addr7, byte command, out List<byte> data)
        {
            data = new List<byte>();
            byte[] buf = new byte[DATA_SIZE];
            if (!Xfer(addr7, I2C_SMBUS_READ, command, I2C_SMBUS_BLOCK_DATA, buf))
                return false;

            int len = Math.Min((int)buf[0], 32);
            for (int i = 0; i < len; i++)
                data.Add(buf[1 + i]);
            return true;
        }

        internal override bool WriteBlockDataNoLock(byte addr7, byte command, List<byte> data)
        {
            if (data == null || data.Count > 32)
                return false;

            byte[] buf = new byte[DATA_SIZE];
            buf[0] = (byte)data.Count;
            for (int i = 0; i < data.Count; i++)
                buf[1 + i] = data[i];
            return Xfer(addr7, I2C_SMBUS_WRITE, command, I2C_SMBUS_BLOCK_DATA, buf);
        }

        internal override bool ChangePortNoLock(int port, out int previousPort)
        {
            previousPort = _currentPort;
            if (port < 0)
                return true;

            lock (_ioLock)
            {
                _currentPort = port;
            }
            return true;
        }

        protected override void Dispose(bool disposing)
        {
            if (_disposed)
                return;

            lock (_ioLock)
            {
                _disposed = true;
                CloseFd();
            }

            lock (_instanceLock)
            {
                if (ReferenceEquals(_instance, this))
                    _instance = null;
            }

            base.Dispose(disposing);
        }
    }
}
