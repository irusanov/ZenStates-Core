using OpenHardwareMonitor.Hardware;

namespace ZenStates.Core
{
    public class AmdFamily17
    {
        private readonly PawnIo _pawnIo;

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

        public uint ReadSmn(uint offset)
        {
            long[] result = _pawnIo.Execute("ioctl_read_smn", new long[1] { offset }, 1);
            return unchecked((uint)result[0]);
        }

        public bool ReadSmn(uint offset, out uint data)
        {
            var input = new long[] { offset };
            var output = new long[1];

            int status = _pawnIo.ExecuteHr("ioctl_read_smn", input, 1, output, 1, out uint returnSize);
            // NTSTATUS_SUCCESS (0)
            if (status == 0 && returnSize > 0)
            {
                data = unchecked((uint)output[0]);
                return true;
            }

            data = 0;
            return false;
        }

        // TODO: Handle different NTSTATUS codes
        public bool ReadMsr(uint index, out ulong eaxedx)
        {
            try
            {
                var output = new long[1];
                int status = _pawnIo.ExecuteHr("ioctl_read_msr", new long[] { index }, 1, output, 1, out uint returnSize);
                System.Diagnostics.Debug.WriteLine($"ReadMsr: index=0x{index:X}, status=0x{status:X}, returnSize={returnSize}");
                if (status == 0 && returnSize > 0)
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
            try
            {
                return ReadMsr(index, out eax, out edx);
            }
            finally
            {
                ThreadAffinity.Set(previousAffinity);
            }
        }

        public bool WriteMsr(uint index, ulong eaxedx)
        {
            try
            {
                int status = _pawnIo.ExecuteHr("ioctl_write_msr", new long[] { index, (long)eaxedx }, 2, new long[0], 0, out uint _);
                if (status != 0)
                    return false;
                return true;
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
            try
            {
                return WriteMsr(index, eax, edx);
            }
            finally
            {
                ThreadAffinity.Set(previousAffinity);
            }
        }

        public void Close() => _pawnIo.Close();
    }
}
