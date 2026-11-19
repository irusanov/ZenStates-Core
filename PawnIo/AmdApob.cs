using System;
using System.IO;

namespace ZenStates.Core.PawnIo
{
    /// <summary>
    /// Read-only access to the AGESA PSP Output Block (APOB) through the AMDAPOB PawnIO module.
    /// The module locates the table when it is loaded (the address recorded in the firmware image first, then
    /// the known addresses) and fails to load when there is none; parsing is left to the caller.
    /// </summary>
    public class AmdApob : IDisposable
    {
        // ioctl_get_apob_info: in = none, out = [address, version, table size, header size] (exactly 4 cells)
        private const string IOCTL_GET_APOB_INFO = "ioctl_get_apob_info";
        // ioctl_read_apob: in = [byte offset, a multiple of 8] (optional), out = table contents, 8 bytes per cell;
        // the read stops at the end of the table
        private const string IOCTL_READ_APOB = "ioctl_read_apob";
        // ioctl_get_apob_debug: in = none, out = [source, firmware address count, EFS address] (exactly 3 cells)
        private const string IOCTL_GET_APOB_DEBUG = "ioctl_get_apob_debug";
        private const int STATUS_SUCCESS = 0;
        private const int INFO_CELLS = 4;
        private const int DEBUG_CELLS = 3;

        /// <summary>The address came from the APOB entry of a BIOS directory in the firmware image.</summary>
        public const int SOURCE_FIRMWARE = 1;
        /// <summary>The address is one of the known addresses the module probes.</summary>
        public const int SOURCE_KNOWN_ADDRESS = 2;
        private const int CELL_SIZE = 8;
        private const int MAX_CELLS_PER_READ = 4096; // 32 KiB per call

        /// <summary>File name of the module, looked up next to the application.</summary>
        public const string ModuleFileName = "AMDAPOB.amx";

        private static readonly long[] _emptyInput = new long[0];

        private readonly PawnIo _pawnIo;
        private volatile bool _disposed;

        /// <summary>
        /// Gets a value indicating whether the underlying PawnIo module is currently loaded. The module only
        /// loads on a supported CPU with an APOB it could find.
        /// </summary>
        public bool IsLoaded => _pawnIo.IsLoaded;

        public AmdApob()
        {
            //string resourceName = "ZenStates.Core.Resources.PawnIo.AMDAPOB.bin";
            //_pawnIo = PawnIo.LoadModuleFromResource(typeof(AmdApob).Assembly, resourceName);
            // The module is not signed yet: it is loaded from a file next to the application and needs the
            // unrestricted PawnIO driver. Switch to the embedded resource above once it ships with PawnIO.
            _pawnIo = PawnIo.LoadModuleFromFile(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, ModuleFileName));
        }

        /// <summary>
        /// Gets the APOB location and header fields.
        /// </summary>
        /// <param name="address">Physical address of the table.</param>
        /// <param name="version">APOB header version.</param>
        /// <param name="tableSize">Size of the whole table in bytes, header included.</param>
        /// <param name="headerSize">Header size in bytes, also the offset of the first entry.</param>
        public bool GetApobInfo(out uint address, out uint version, out uint tableSize, out uint headerSize)
        {
            address = version = tableSize = headerSize = 0;

            try
            {
                long[] output = new long[INFO_CELLS];
                int status = _pawnIo.ExecuteHr(IOCTL_GET_APOB_INFO, _emptyInput, 0, output, INFO_CELLS, out uint returnSize);
                if (status != STATUS_SUCCESS || returnSize < INFO_CELLS)
                    return false;

                address = unchecked((uint)output[0]);
                version = unchecked((uint)output[1]);
                tableSize = unchecked((uint)output[2]);
                headerSize = unchecked((uint)output[3]);
                return address != 0 && tableSize != 0;
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// Gets how the module located the table, for diagnostics. False with a module build without this ioctl.
        /// </summary>
        /// <param name="source"><see cref="SOURCE_FIRMWARE"/> or <see cref="SOURCE_KNOWN_ADDRESS"/>.</param>
        /// <param name="firmwareAddresses">Number of APOB addresses found in the firmware image.</param>
        /// <param name="efsAddress">Physical address of the last Embedded Firmware Structure found, 0 when none.</param>
        public bool GetApobDebugInfo(out int source, out int firmwareAddresses, out ulong efsAddress)
        {
            source = firmwareAddresses = 0;
            efsAddress = 0;

            try
            {
                long[] output = new long[DEBUG_CELLS];
                int status = _pawnIo.ExecuteHr(IOCTL_GET_APOB_DEBUG, _emptyInput, 0, output, DEBUG_CELLS, out uint returnSize);
                if (status != STATUS_SUCCESS || returnSize < DEBUG_CELLS)
                    return false;

                source = unchecked((int)output[0]);
                firmwareAddresses = unchecked((int)output[1]);
                efsAddress = unchecked((ulong)output[2]);
                return true;
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// Reads the whole APOB table. Returns null on failure.
        /// </summary>
        public byte[] ReadTable()
        {
            if (!GetApobInfo(out uint address, out uint version, out uint tableSize, out uint headerSize))
                return null;

            return ReadTable(0, tableSize, tableSize);
        }

        /// <summary>
        /// Reads <paramref name="length"/> bytes of the APOB table starting at <paramref name="offset"/>.
        /// The offset must be a multiple of 8 and the range must be within the table. Returns null on failure.
        /// </summary>
        public byte[] ReadTable(uint offset, uint length)
        {
            if (!GetApobInfo(out uint address, out uint version, out uint tableSize, out uint headerSize))
                return null;

            return ReadTable(offset, length, tableSize);
        }

        private byte[] ReadTable(uint offset, uint length, uint tableSize)
        {
            // The module rejects an unaligned offset or one past the table, and stops reading at its end
            if ((offset & 0x7) != 0 || length == 0 || offset >= tableSize || length > tableSize - offset)
                return null;

            try
            {
                byte[] result = new byte[length];
                int done = 0;

                while (done < result.Length)
                {
                    int left = result.Length - done;
                    int count = Math.Min(MAX_CELLS_PER_READ, (left + CELL_SIZE - 1) / CELL_SIZE);
                    long[] cells = new long[count];

                    if (!ReadCells(offset + (uint)done, cells, count))
                        return null;

                    int bytes = Math.Min(count * CELL_SIZE, left);
                    Buffer.BlockCopy(cells, 0, result, done, bytes);
                    done += bytes;
                }

                return result;
            }
            catch
            {
                return null;
            }
        }

        private bool ReadCells(uint offset, long[] cells, int count)
        {
            int status = _pawnIo.ExecuteHr(IOCTL_READ_APOB, new long[] { offset }, 1, cells, (uint)count, out uint returnSize);
            return status == STATUS_SUCCESS && returnSize > 0;
        }

        public void Close()
        {
            if (_disposed)
                return;

            _disposed = true;
            _pawnIo.Close();
        }

        public void Dispose() => Close();
    }
}
