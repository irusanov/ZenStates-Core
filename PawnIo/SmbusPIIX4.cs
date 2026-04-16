using System;
using System.Collections.Generic;
using System.Diagnostics;
using ZenStates.Core.Drivers;

namespace ZenStates.Core
{
    internal class SmbusPiix4 : SmbusDriverBase
    {
        private static volatile SmbusPiix4 _instance;
        private static readonly object _instanceLock = new object();

        // ioctl names from SmbusPIIX4 PawnIO module
        private const string IOCTL_SMBUS_XFER = "ioctl_smbus_xfer";
        private const string IOCTL_PIIX4_PORT_SEL = "ioctl_piix4_port_sel";
        private const string IOCTL_IDENTITY = "ioctl_identity";
        private const string IOCTL_CLOCK_FREQ = "ioctl_clock_freq";

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

        /// <summary>
        /// Releases the unmanaged resources used by the SmbusPiix4 and optionally releases the managed resources.
        /// </summary>
        /// <param name="disposing">true to release both managed and unmanaged resources; false to release only unmanaged resources.</param>
        protected new virtual void Dispose(bool disposing)
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
            Dispose(true);
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
            {
                int shift = (i - offset) * 8;
                long part = unchecked((long)((ulong)data[i] << shift));
                cell = unchecked(cell | part);
            }

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
                {
                    ulong cell = unchecked((ulong)cells[cellIdx]);
                    result.Add((byte)((cell >> bitShift) & 0xFF));
                }
            }

            return result;
        }

        /// <summary>
        /// Switch to the given port. Pass -1 to query without changing.
        /// Returns the previous port number.
        /// </summary>
        internal override bool ChangePortNoLock(int port, out int previousPort)
        {
            previousPort = -1;
            if (!Execute(IOCTL_PIIX4_PORT_SEL, new long[] { port }, 1, out long[] result))
                return false;
            if (result[0] < int.MinValue || result[0] > int.MaxValue)
                return false;
            previousPort = (int)result[0];
            if (port != -1) _currentPort = port;
            return true;
        }

        public bool ChangePort(int port, out int previousPort)
        {
            using (new SmbusLock())
            {
                return ChangePortNoLock(port, out previousPort);
            }
        }

        public bool ChangePort(int port)
        {
            using (new SmbusLock())
            {
                return ChangePortNoLock(port);
            }
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

            if (id[1] < int.MinValue || id[1] > int.MaxValue)
                return false;

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

            if (result[0] < int.MinValue || result[0] > int.MaxValue)
                return false;

            previousFreqHz = (int)result[0];

            return true;
        }

        /// <summary>Tests if a device ACKs its 7-bit address.</summary>
        /// <param name="addr7">7-bit I2C device address.</param>
        /// <param name="readWrite">I2C_SMBUS_READ (1) for read, I2C_SMBUS_WRITE (0) for write.</param>
        /// <returns>true if the device ACKs; false otherwise.</returns>
        internal override bool SmbusQuickNoLock(byte addr7, byte readWrite)
        {
            return Execute(IOCTL_SMBUS_XFER,
                           new long[4] { addr7, readWrite, 0L, I2C_SMBUS_QUICK },
                           0, out long[] result);
        }


        // BYTE data
        internal override bool ReadByteDataNoLock(byte addr7, byte command, out byte result)
        {
            result = 0;
            if (!Execute(IOCTL_SMBUS_XFER,
                         new long[] { (long)addr7, I2C_SMBUS_READ, (long)command, I2C_SMBUS_BYTE_DATA },
                         1, out long[] raw))
                return false;

            result = (byte)(raw[0] & 0xFF);

            return true;
        }

        internal override bool WriteByteDataNoLock(byte addr7, byte command, byte value)
        {
            return Execute(IOCTL_SMBUS_XFER,
                           new long[] { (long)addr7, I2C_SMBUS_WRITE, (long)command, I2C_SMBUS_BYTE_DATA, (long)value },
                           0, out long[] raw);
        }

        // WORD data

        /// <summary>Read a 16-bit word from a register of a device.</summary>
        /// <param name="addr7">7-bit I2C device address.</param>
        /// <param name="command">Register address to read from.</param>
        /// <param name="result">The 16-bit word read from the register (little-endian).</param>
        /// <returns>true if successful; false if the read failed.</returns>
        internal override bool ReadWordDataNoLock(byte addr7, byte command, out ushort result)
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
        internal override bool WriteWordDataNoLock(byte addr7, byte command, ushort value)
        {
            return Execute(IOCTL_SMBUS_XFER,
                           new long[] { addr7, I2C_SMBUS_WRITE, command, I2C_SMBUS_WORD_DATA, value },
                           0, out long[] raw);
        }

        // BLOCK data

        /// <summary>Read up to 32 bytes of block data from a device register.</summary>
        /// <param name="addr7">7-bit I2C device address.</param>
        /// <param name="command">Register address to read from.</param>
        /// <param name="data">The bytes read from the device. The first byte is the count (0-32).</param>
        /// <returns>true if successful; false if the read failed.</returns>
        /// <remarks>
        /// Block data format: The device returns [count, data[0]..data[count-1]] where count is in the first byte.
        /// Output is packed into 5 cells: out[0] bits [7:0] = count, bits [15:8] = data[0], etc.
        /// </remarks>
        internal override bool ReadBlockDataNoLock(byte addr7, byte command, out List<byte> data)
        {
            data = new List<byte>();

            bool ok = Execute(IOCTL_SMBUS_XFER, new long[] { addr7, I2C_SMBUS_READ, command, I2C_SMBUS_BLOCK_DATA }, 5, out long[] raw);

            if (!ok)
            {
                Debug.WriteLine(string.Format("Block read failed: addr=0x{0:X2}, cmd=0x{1:X2}", addr7, command));
                return false;
            }

            int count = (int)(raw[0] & 0xFF);
            if (count > 32)
                count = 32;

            for (int i = 0; i < count; i++)
            {
                int packedIndex = i + 1;
                int cellIdx = packedIndex >> 3;
                int bitShift = (packedIndex & 7) << 3;

                ulong cell = unchecked((ulong)raw[cellIdx]);
                data.Add((byte)((cell >> bitShift) & 0xFF));
            }

            return true;
        }

        internal override bool WriteBlockDataNoLock(byte addr7, byte command, List<byte> data)
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
    }
}
