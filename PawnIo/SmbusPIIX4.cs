// SmbusPiix4.cs

using System;
using System.Collections.Generic;

namespace ZenStates.Core
{
    public class SmbusPiix4 : IDisposable
    {
        // ── Singleton instance and lock ──────────────────────────────────────────
        private static SmbusPiix4 _instance;
        private static readonly object _instanceLock = new object();

        // ── ioctl names from SmbusPIIX4 PawnIO module ────────────────────────
        private const string IOCTL_SMBUS_XFER = "ioctl_smbus_xfer";
        private const string IOCTL_PIIX4_PORT_SEL = "ioctl_piix4_port_sel";
        private const string IOCTL_IDENTITY = "ioctl_identity";
        private const string IOCTL_CLOCK_FREQ = "ioctl_clock_freq";

        // ── i2c_smbus_xfer read/write markers ────────────────────────────────
        private const long I2C_SMBUS_WRITE = 0;
        private const long I2C_SMBUS_READ = 1;

        // ── SMBus transaction protocol codes ─────────────────────────────────
        private const long I2C_SMBUS_QUICK = 0;
        private const long I2C_SMBUS_BYTE = 1;
        private const long I2C_SMBUS_BYTE_DATA = 2;
        private const long I2C_SMBUS_WORD_DATA = 3;
        private const long I2C_SMBUS_BLOCK_DATA = 5;

        // ── Port constants ───────────────────────────────────────────────────
        // port_to_reg[] = [0b00, 0b00, 0b01, 0b10, 0b11]
        // Port 0 = board SMBus (primary, 0x0B00)
        // Port 1 = ASF / aux   (secondary, 0x0B20)
        // Port 2 = DDR5 / TSI  (primary port 2, KernCZ reg 0b01)
        // Ports 3,4 = reserved
        public const int PORT_BOARD = 0;
        public const int PORT_ASF = 1;
        public const int PORT_DIMM = 2;

        // ── DDR5 SPD5118 hub 7-bit I2C addresses (0x50–0x57) ─────────────────
        // Module takes 7-bit address and does (addr << 1) | rw internally.
        private const int SPD_HUB_ADDR_FIRST = 0x50;
        private const int SPD_HUB_ADDR_LAST = 0x57;

        private readonly PawnIo _pawnIo;
        private volatile bool _disposed;
        private readonly object _disposeLock = new object();
        private int _currentPort = -1;

        /// <summary>
        /// Gets the singleton instance of SmbusPiix4.
        /// </summary>
        public static SmbusPiix4 Instance
        {
            get
            {
                if (_instance == null)
                {
                    lock (_instanceLock)
                    {
                        if (_instance == null)
                        {
                            _instance = new SmbusPiix4();
                        }
                    }
                }
                return _instance;
            }
        }

        private SmbusPiix4()
        {
            string resourceName = "ZenStates.Core.Resources.PawnIo.SmbusPIIX4.bin";
            _pawnIo = PawnIo.LoadModuleFromResource(typeof(SmbusPiix4).Assembly, resourceName);
        }

        public bool IsLoaded
        {
            get { return _pawnIo != null && _pawnIo.IsLoaded; }
        }

        #region IDisposable Implementation

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        /// <summary>
        /// Releases the unmanaged resources used by the SmbusPiix4 and optionally releases the managed resources.
        /// </summary>
        /// <param name="disposing">true to release both managed and unmanaged resources; false to release only unmanaged resources.</param>
        protected virtual void Dispose(bool disposing)
        {
            if (_disposed)
                return;

            lock (_disposeLock)
            {
                if (_disposed)
                    return;

                if (disposing)
                {
                    _pawnIo?.Close();
                }

                _disposed = true;
            }
        }

        /// <summary>
        /// Finalizer for SmbusPiix4 class.
        /// </summary>
        ~SmbusPiix4()
        {
            Dispose(false);
        }

        #endregion

        /// <summary>
        /// Wrapper around ExecuteHr.
        /// </summary>
        /// <param name="fn"></param>
        /// <param name="inParams"></param>
        /// <param name="outCount"></param>
        /// <param name="result"></param>
        /// <returns>bool</returns>
        private bool Execute(string fn, long[] inParams, int outCount, out long[] result)
        {
            result = new long[outCount];
            int hr = _pawnIo.ExecuteHr(
                fn,
                inParams,
                unchecked((uint)inParams.Length),
                result,
                (uint)outCount,
                out uint returnSize);


            // hr < 0 = NTSTATUS failure (device not present, timeout, etc.)
            // returnSize == 0 on reads means no data came back
            return hr == 0;
        }

        /// <summary>
        /// Byte packing (little-endian, 8 bytes per cell)
        /// </summary>
        /// <param name="data"></param>
        /// <param name="offset"></param>
        /// <param name="count"></param>
        /// <returns></returns>
        private static long PackBytes(byte[] data, int offset, int count)
        {
            long cell = 0;
            int end = Math.Min(offset + count, data.Length);
            for (int i = offset; i < end; i++)
                cell |= (long)((ulong)data[i] << ((i - offset) * 8));
            return cell;
        }

        // Unpack pack_bytes_le output: each cell holds 8 bytes little-endian.
        // startCell = first cell index containing data (after any length cell).
        private static List<byte> UnpackCells(long[] cells, int startCell, int byteCount)
        {
            List<byte> result = new List<byte>(byteCount);
            for (int i = 0; i < byteCount; i++)
            {
                int cellIdx = startCell + i / 8;
                int bitShift = (i % 8) * 8;
                if (cellIdx < cells.Length)
                    result.Add((byte)(((ulong)cells[cellIdx] >> bitShift) & 0xFF));
            }
            return result;
        }

        /// <summary>
        /// Switch to the given port. Pass -1 to query without changing.
        /// Returns the previous port number.
        /// </summary>
        public bool ChangePort(int port, out int previousPort)
        {
            previousPort = -1;
            if (!Execute(IOCTL_PIIX4_PORT_SEL, new long[] { port }, 1, out long[] result))
                return false;
            previousPort = (int)result[0];
            if (port != -1) _currentPort = port;
            return true;
        }

        public bool ChangePort(int port)
        {
            return ChangePort(port, out int prev);
        }

        /// <summary>
        /// Returns [0]=type identifier, [1]=IO base address, [2]=PCI vendor+device ID.
        /// </summary>
        public bool GetIdentity(out long[] identity)
        {
            return Execute(IOCTL_IDENTITY, new long[] { }, 3, out identity);
        }

        public bool GetIoBase(out int ioBase)
        {
            ioBase = 0;
            if (!GetIdentity(out long[] id)) return false;
            ioBase = (int)id[1];
            return true;
        }

        /// <summary>
        /// Get current SMBus clock frequency in Hz.
        /// Pass -1 as newFreqHz to query without changing.
        /// </summary>
        public bool GetSetClockFreq(int newFreqHz, out int previousFreqHz)
        {
            previousFreqHz = 0;
            if (!Execute(IOCTL_CLOCK_FREQ, new long[] { newFreqHz }, 1, out long[] result))
                return false;
            previousFreqHz = (int)result[0];
            return true;
        }

        /// <summary>Tests if a device ACKs its 7-bit address.</summary>
        /// <param name="addr7">7-bit I2C device address.</param>
        /// <param name="readWrite">I2C_SMBUS_READ (1) for read, I2C_SMBUS_WRITE (0) for write.</param>
        /// <returns>true if the device ACKs; false otherwise.</returns>
        public bool SmbusQuick(byte addr7, byte readWrite)
        {
            return Execute(IOCTL_SMBUS_XFER,
                           new long[4] { addr7, readWrite, 0L, I2C_SMBUS_QUICK },
                           0, out long[] result);
        }

        /// <summary>Read a single byte from a device without a register address.</summary>
        /// <param name="addr7">7-bit I2C device address.</param>
        /// <param name="result">The byte read from the device.</param>
        /// <returns>true if successful; false if the read failed.</returns>
        public bool SmbusReadByte(byte addr7, out byte result)
        {
            result = 0;
            if (!Execute(IOCTL_SMBUS_XFER,
                         new long[] { addr7, I2C_SMBUS_READ, 0L, I2C_SMBUS_BYTE },
                         1, out long[] raw))
                return false;
            result = (byte)(raw[0] & 0xFF);
            return true;
        }

        /// <summary>Write a single byte to a device without a register address.</summary>
        /// <param name="addr7">7-bit I2C device address.</param>
        /// <param name="command">The byte value to write.</param>
        /// <returns>true if successful; false if the write failed.</returns>
        public bool SmbusWriteByte(byte addr7, byte command)
        {
            return Execute(IOCTL_SMBUS_XFER,
                           new long[] { addr7, I2C_SMBUS_WRITE, command, I2C_SMBUS_BYTE },
                           0, out long[] raw);
        }

        // ── No-lock helpers for batching under a single external mutex ───────

        internal bool SmbusReadByteDataNoLock(byte addr7, byte command, out byte result)
        {
            result = 0;
            if (!Execute(IOCTL_SMBUS_XFER,
                         new long[] { (long)addr7, I2C_SMBUS_READ, (long)command, I2C_SMBUS_BYTE_DATA },
                         1, out long[] raw))
                return false;
            result = (byte)(raw[0] & 0xFF);
            return true;
        }

        internal bool SmbusWriteByteDataNoLock(byte addr7, byte command, byte value)
        {
            return Execute(IOCTL_SMBUS_XFER,
                           new long[] { (long)addr7, I2C_SMBUS_WRITE, (long)command, I2C_SMBUS_BYTE_DATA, (long)value },
                           0, out long[] raw);
        }

        internal bool SmbusReadBlockDataNoLock(byte addr7, byte command, out List<byte> data)
        {
            data = new List<byte>();
            long[] raw;
            if (!Execute(IOCTL_SMBUS_XFER,
                         new long[] { addr7, I2C_SMBUS_READ, command, I2C_SMBUS_BLOCK_DATA },
                         5, out raw))
                return false;

            int count = (int)(raw[0] & 0xFF);
            if (count < 0) count = 0;
            if (count > 32) count = 32;

            for (int i = 0; i < count; i++)
            {
                int byteInCells = i + 1;
                int cellIdx = byteInCells / 8;
                int bitShift = (byteInCells % 8) * 8;
                data.Add((byte)(((ulong)raw[cellIdx] >> bitShift) & 0xFF));
            }
            return true;
        }


        /// <summary>Read one byte from register <paramref name="command"/> of 7-bit address <paramref name="addr7"/>.</summary>
        /// <param name="addr7">7-bit I2C device address.</param>
        /// <param name="command">Register address to read from.</param>
        /// <param name="result">The byte read from the register.</param>
        /// <returns>true if successful; false if the read failed or mutex timeout occurred.</returns>
        public bool SmbusReadByteData(byte addr7, byte command, out byte result)
        {
            result = 0;
            if (Mutexes.WaitSmbus(5000))
            {
                try
                {
                    return SmbusReadByteDataNoLock(addr7, command, out result);
                }
                finally
                {
                    Mutexes.ReleaseSmbus();
                }
            }
            return false;
        }

        /// <summary>Write one byte to register <paramref name="command"/> of 7-bit address <paramref name="addr7"/>.</summary>
        /// <param name="addr7">7-bit I2C device address.</param>
        /// <param name="command">Register address to write to.</param>
        /// <param name="value">The byte value to write.</param>
        /// <returns>true if successful; false if the write failed.</returns>
        public bool SmbusWriteByteData(byte addr7, byte command, byte value)
        {
            if (Mutexes.WaitSmbus(5000))
            {
                try
                {
                    return SmbusWriteByteDataNoLock(addr7, command, value);
                }
                finally
                {
                    Mutexes.ReleaseSmbus();
                }
            }
            return false;
        }

        /// <summary>Read a 16-bit word from a register of a device.</summary>
        /// <param name="addr7">7-bit I2C device address.</param>
        /// <param name="command">Register address to read from.</param>
        /// <param name="result">The 16-bit word read from the register (little-endian).</param>
        /// <returns>true if successful; false if the read failed.</returns>
        public bool SmbusReadWordData(byte addr7, byte command, out ushort result)
        {
            result = 0;
            if (!Execute(IOCTL_SMBUS_XFER,
                         new long[] { addr7, I2C_SMBUS_READ, command, I2C_SMBUS_WORD_DATA },
                         1, out long[] raw))
                return false;
            result = (ushort)(raw[0] & 0xFFFF);
            return true;
        }

        /// <summary>Write a 16-bit word to a register of a device.</summary>
        /// <param name="addr7">7-bit I2C device address.</param>
        /// <param name="command">Register address to write to.</param>
        /// <param name="value">The 16-bit word value to write (little-endian).</param>
        /// <returns>true if successful; false if the write failed.</returns>
        public bool SmbusWriteWordData(byte addr7, byte command, ushort value)
        {
            return Execute(IOCTL_SMBUS_XFER,
                           new long[] { addr7, I2C_SMBUS_WRITE, command, I2C_SMBUS_WORD_DATA, value },
                           0, out long[] raw);
        }

        /// <summary>Read up to 32 bytes of block data from a device register.</summary>
        /// <param name="addr7">7-bit I2C device address.</param>
        /// <param name="command">Register address to read from.</param>
        /// <param name="data">The bytes read from the device. The first byte is the count (0-32).</param>
        /// <returns>true if successful; false if the read failed.</returns>
        /// <remarks>
        /// Block data format: The device returns [count, data[0]..data[count-1]] where count is in the first byte.
        /// Output is packed into 5 cells: out[0] bits [7:0] = count, bits [15:8] = data[0], etc.
        /// </remarks>
        public bool SmbusReadBlockData(byte addr7, byte command, out List<byte> data)
        {
            data = new List<byte>();
            if (!Mutexes.WaitSmbus(5000))
                return false;
            try
            {
                return SmbusReadBlockDataNoLock(addr7, command, out data);
            }
            finally
            {
                Mutexes.ReleaseSmbus();
            }
        }

        public bool SmbusReadBlockDataFixed(byte addr7, byte command, int expectedCount, out byte[] data)
        {
            data = null;
            if (expectedCount < 1 || expectedCount > 32)
                return false;

            if (!Mutexes.WaitSmbus(5000))
                return false;

            try
            {
                List<byte> tmp;
                if (!SmbusReadBlockDataNoLock(addr7, command, out tmp))
                    return false;

                if (tmp.Count < expectedCount)
                    return false;

                data = new byte[expectedCount];
                for (int i = 0; i < expectedCount; i++)
                    data[i] = tmp[i];
                return true;
            }
            finally
            {
                Mutexes.ReleaseSmbus();
            }
        }

        internal bool SmbusReadBlockDataFixedNoLock(byte addr7, byte command, int expectedCount, out byte[] data)
        {
            data = null;
            if (expectedCount < 1 || expectedCount > 32)
                return false;

            List<byte> tmp;
            if (!SmbusReadBlockDataNoLock(addr7, command, out tmp))
                return false;

            if (tmp.Count < expectedCount)
                return false;

            data = new byte[expectedCount];
            for (int i = 0; i < expectedCount; i++)
                data[i] = tmp[i];
            return true;
        }

        public bool SmbusWriteBlockData(byte addr7, byte command, List<byte> data)
        {
            if (data == null || data.Count < 1 || data.Count > 32)
                return false;

            // in: [addr, rw, cmd, protocol, packed_data(5 cells)]
            // pack_bytes_le expects [count, data[0]..data[N]] → 5 cells
            byte[] raw = data.ToArray();
            int cells = (data.Count + 1 + 7) / 8; // +1 for count byte
            long[] packed = new long[cells];

            // Build [count, data...] byte array to pack
            byte[] blob = new byte[data.Count + 1];
            blob[0] = (byte)data.Count;
            for (int i = 0; i < data.Count; i++)
                blob[i + 1] = raw[i];

            for (int i = 0; i < cells; i++)
                packed[i] = PackBytes(blob, i * 8, Math.Min(8, blob.Length - i * 8));

            long[] inParams = new long[4 + cells];
            inParams[0] = (long)addr7;
            inParams[1] = I2C_SMBUS_WRITE;
            inParams[2] = (long)command;
            inParams[3] = I2C_SMBUS_BLOCK_DATA;
            for (int i = 0; i < cells; i++)
                inParams[4 + i] = packed[i];

            return Execute(IOCTL_SMBUS_XFER, inParams, 0, out long[] result);
        }

        // SPD5118 hub: 1024 bytes in 8 pages of 128 bytes.
        // Page selected by writing page number to register 0x0B.
        // 7-bit addresses: 0x50–0x57 (DIMM 0–7).
        private static int SpdGetPage(int offset) { return offset / 128; }
        private static int SpdGetReg(int offset) { return 0x80 + (offset % 128); }

        internal bool SpdSwitchPage(byte addr7, byte page)
        {
            return SmbusWriteByteData(addr7, 0x0B, page);
        }

        /// <summary>
        /// Scan for DDR5 SPD hubs. Tries port 2 (DDR5/TSI) first, then port 0.
        /// Returns 7-bit addresses of responding hubs.
        /// </summary>
        internal List<byte> DramPop()
        {
            int[] ports = new int[] { PORT_DIMM, PORT_BOARD };

            for (int p = 0; p < ports.Length; p++)
            {
                ChangePort(ports[p]);
                List<byte> found = new List<byte>();
                for (int i = SPD_HUB_ADDR_FIRST; i <= SPD_HUB_ADDR_LAST; i++)
                {
                    if (SmbusReadByteData((byte)i, 0x00, out byte dummy))
                        found.Add((byte)i);
                }
                if (found.Count > 0)
                    return found;
            }

            return new List<byte>();
        }

        /// <summary>Dump all 1024 SPD bytes from a single DIMM (7-bit address).</summary>
        public List<byte> DumpDdr5Spd(byte addr7)
        {
            List<byte> spd = new List<byte>();
            int prev = -1;
            byte res;

            if (Mutexes.WaitSmbus(5000))
            {
                try
                {
                    for (int i = 0; i <= 0x3FF; i++)
                    {
                        int page = SpdGetPage(i);
                        int reg = SpdGetReg(i);
                        if (page != prev)
                        {
                            SpdSwitchPage(addr7, (byte)page);
                            prev = page;
                        }
                        if (!SmbusReadByteData(addr7, (byte)reg, out res))
                            res = 0xFF;
                        spd.Add(res);
                    }
                }
                finally
                {
                    Mutexes.ReleaseSmbus();
                }
            }

            return spd;
        }

        /// <summary>Dump SPD from all discovered DIMMs. Key = 7-bit address, Value = 1024-byte SPD.</summary>
        public Dictionary<byte, List<byte>> DumpDdr5Spd()
        {
            Dictionary<byte, List<byte>> result = new Dictionary<byte, List<byte>>();

            List<byte> addresses = DramPop();
            if (addresses.Count == 0)
                throw new InvalidOperationException("No DDR5 DIMMs found on any SMBus port.");

            for (int i = 0; i < addresses.Count; i++)
                result.Add(addresses[i], DumpDdr5Spd(addresses[i]));

            foreach (KeyValuePair<byte, List<byte>> kvp in result)
            {
                byte address = kvp.Key;
                List<byte> spdData = kvp.Value;

                Console.WriteLine("DIMM at address 0x{0:X2}:", address);

                for (int offset = 0; offset < spdData.Count; offset += 16)
                {
                    Console.Write("  {0:X4}: ", offset);

                    for (int i = 0; i < 16 && offset + i < spdData.Count; i++)
                    {
                        if (i > 0)
                            Console.Write(" ");
                        Console.Write("{0:X2}", spdData[offset + i]);
                    }

                    Console.WriteLine();
                }

                Console.WriteLine();
            }

            return result;
        }

        /// <summary>
        /// Write SPD data from all discovered DIMMs to binary files in the specified directory.
        /// </summary>
        public bool DumpDdr5SpdToFiles(string outputDirectory)
        {
            try
            {
                if (!System.IO.Directory.Exists(outputDirectory))
                    System.IO.Directory.CreateDirectory(outputDirectory);

                List<byte> addresses = DramPop();
                if (addresses.Count == 0)
                    throw new InvalidOperationException("No DDR5 DIMMs found on any SMBus port.");

                for (int i = 0; i < addresses.Count; i++)
                {
                    byte addr = addresses[i];
                    List<byte> spdData = DumpDdr5Spd(addr);
                    string filename = System.IO.Path.Combine(outputDirectory, string.Format("DIMM_0x{0:X2}.bin", addr));

                    byte[] buffer = spdData.ToArray();
                    System.IO.File.WriteAllBytes(filename, buffer);

                    Console.WriteLine("Wrote {0} bytes to {1}", buffer.Length, filename);
                }

                return true;
            }
            catch (Exception ex)
            {
                Console.WriteLine("Error dumping SPD to files: {0}", ex.Message);
                return false;
            }
        }
    }
}
