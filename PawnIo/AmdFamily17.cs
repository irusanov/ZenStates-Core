using OpenHardwareMonitor.Hardware;
using System;

namespace ZenStates.Core
{
    public class AmdFamily17
    {
        private readonly PawnIo _pawnIo;

        public AmdFamily17()
        {
            //string resourceName = "ZenStates.Core.Resources.PawnIo.AMDFamily17.bin";
            //_pawnIo = PawnIo.LoadModuleFromResource(typeof(AmdFamily17).Assembly, resourceName);
            _pawnIo = PawnIo.LoadModuleFromFile("AMDFamily17.amx");
        }

        public uint ReadSmn(uint offset)
        {
            long[] input = new long[1];
            input[0] = offset;

            long[] result = _pawnIo.Execute("ioctl_read_smn", input, 1);
            return Convert.ToUInt32(result[0] & 0xffffffff);
        }

        public bool ReadSmn(uint offset, out uint data)
        {
            long[] input = new long[1];
            long[] output = new long[1];
            input[0] = offset;

            uint returnSize;
            int result = _pawnIo.ExecuteHr("ioctl_read_smn", input, 1, output, 1, out returnSize);

            // NTSTATUS_SUCCESS
            if (result == 0 && returnSize > 0)
            {
                data = Convert.ToUInt32(output[0] & 0xffffffff);
                return true;
            }

            data = 0;
            return false;
        }

        public bool ReadMsr(uint index, out uint eax, out uint edx)
        {
            long[] inArray = new long[1];
            inArray[0] = index;
            eax = 0;
            edx = 0;

            try
            {
                long[] outArray = _pawnIo.Execute("ioctl_read_msr", inArray, 1);
                eax = Convert.ToUInt32(outArray[0] & 0xffffffff);
                edx = Convert.ToUInt32((outArray[0] >> 32) & 0xffffffff);
            }
            catch
            {
                return false;
            }

            return true;
        }

        public bool ReadMsr(uint index, out ulong eaxedx)
        {
            long[] inArray = new long[1];
            inArray[0] = index;
            eaxedx = 0;

            try
            {
                long[] outArray = _pawnIo.Execute("ioctl_read_msr", inArray, 1);
                eaxedx = (ulong)outArray[0];
            }
            catch
            {
                return false;
            }

            return true;
        }

        public bool ReadMsrTx(uint index, out uint eax, out uint edx, GroupAffinity affinity)
        {
            var previousAffinity = ThreadAffinity.Set(affinity);

            bool result = ReadMsr(index, out eax, out edx);

            ThreadAffinity.Set(previousAffinity);
            return result;
        }

        public void Close()
        {
            _pawnIo.Close();
        }
    }
}
