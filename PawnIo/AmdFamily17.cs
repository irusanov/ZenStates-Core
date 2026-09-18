using System;
using ZenStates.Core.OHWM;

namespace ZenStates.Core.PawnIo
{
    public class AmdFamily17 : IDisposable
    {
        private const string IOCTL_READ_SMN = "ioctl_read_smn";
        private const string IOCTL_READ_MSR = "ioctl_read_msr";
        private const string IOCTL_WRITE_MSR = "ioctl_write_msr";
        private const int STATUS_SUCCESS = 0;

        private static readonly long[] _emptyOutputBuffer = new long[0];

        private readonly PawnIo _pawnIo;
        private volatile bool _disposed;

        /// <summary>
        /// Gets a value indicating whether the underlying PawnIo module is currently loaded.
        /// </summary>
        public bool IsLoaded => _pawnIo.IsLoaded;

        public AmdFamily17()
        {
            string resourceName = "ZenStates.Core.Resources.PawnIo.AMDFamily17.bin";
            _pawnIo = PawnIo.LoadModuleFromResource(typeof(AmdFamily17).Assembly, resourceName);
            //_pawnIo = PawnIo.LoadModuleFromFile("AMDFamily17.amx");
        }

        /// <summary>
        /// Reads an SMN register. Returns 0 on failure.
        /// Prefer the <c>out</c> overload when you need to distinguish a genuine zero from an error.
        /// </summary>
        public uint ReadSmnNoLock(uint offset)
        {
            ReadSmnNoLock(offset, out uint data);
            return data;
        }

        public bool ReadSmnNoLock(uint offset, out uint data)
        {
            long[] input = new long[] { offset };
            long[] output = new long[1];

            int status = _pawnIo.ExecuteHr(IOCTL_READ_SMN, input, 1, output, 1, out uint returnSize);
            if (status == STATUS_SUCCESS && returnSize > 0)
            {
                data = unchecked((uint)output[0]);
                return true;
            }

            data = 0;
            return false;
        }

        public bool ReadMsr(uint index, out ulong eaxedx)
        {
            try
            {
                long[] output = new long[1];
                int status = _pawnIo.ExecuteHr(IOCTL_READ_MSR, new long[] { index }, 1, output, 1, out uint returnSize);
#if DEBUG
                System.Diagnostics.Debug.WriteLine($"ReadMsr: index=0x{index:X}, status=0x{status:X}, returnSize={returnSize}");
#endif
                if (status == STATUS_SUCCESS && returnSize > 0)
                {
                    eaxedx = unchecked((ulong)output[0]);
                    return true;
                }

                eaxedx = 0;
                return false;
            }
            catch
            {
                eaxedx = 0;
                return false;
            }
        }

        public bool ReadMsr(uint index, out uint eax, out uint edx)
        {
            try
            {
                if (!ReadMsr(index, out ulong eaxedx))
                {
                    eax = edx = 0;
                    return false;
                }

                eax = unchecked((uint)eaxedx);
                edx = unchecked((uint)(eaxedx >> 32));
                return true;
            }
            catch
            {
                eax = edx = 0;
                return false;
            }
        }

        public bool ReadMsrTx(uint index, out uint eax, out uint edx, GroupAffinity affinity)
        {
            GroupAffinity previousAffinity = ThreadAffinity.Set(affinity);

            // Undefined means the affinity change did not take. Reading anyway would return
            // whichever core the thread happens to be on and report it as the requested one.
            if (previousAffinity == GroupAffinity.Undefined)
            {
                System.Diagnostics.Debug.WriteLine(
                    $"ReadMsrTx: could not set affinity to group {affinity.Group}/0x{affinity.Mask:X}; skipping MSR 0x{index:X}.");
                eax = edx = 0;
                return false;
            }

            try
            {
                return ReadMsr(index, out eax, out edx);
            }
            finally
            {
                RestoreAffinity(previousAffinity);
            }
        }

        private static void RestoreAffinity(GroupAffinity previousAffinity)
        {
            // A failed restore leaves the calling thread pinned to one core for good, which
            // matters most on a UI or timer thread that polls repeatedly.
            if (ThreadAffinity.Set(previousAffinity) == GroupAffinity.Undefined)
            {
                System.Diagnostics.Debug.WriteLine(
                    "Failed to restore the previous thread affinity; this thread may stay pinned.");
            }
        }

        public bool WriteMsr(uint index, ulong eaxedx)
        {
            try
            {
                int status = _pawnIo.ExecuteHr(
                    IOCTL_WRITE_MSR,
                    new long[] { index, unchecked((long)eaxedx) }, 2,
                    _emptyOutputBuffer, 0,
                    out uint _);

                return status == STATUS_SUCCESS;
            }
            catch
            {
                return false;
            }
        }

        public bool WriteMsr(uint index, uint eax, uint edx)
        {
            return WriteMsr(index, ((ulong)edx << 32) | eax);
        }

        public bool WriteMsrTx(uint index, uint eax, uint edx, GroupAffinity affinity)
        {
            GroupAffinity previousAffinity = ThreadAffinity.Set(affinity);

            // Writing to the wrong core is worse than reading from it: this path carries
            // per-core frequency and curve-optimiser changes.
            if (previousAffinity == GroupAffinity.Undefined)
            {
                System.Diagnostics.Debug.WriteLine(
                    $"WriteMsrTx: could not set affinity to group {affinity.Group}/0x{affinity.Mask:X}; refusing to write MSR 0x{index:X}.");
                return false;
            }

            try
            {
                return WriteMsr(index, eax, edx);
            }
            finally
            {
                RestoreAffinity(previousAffinity);
            }
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
