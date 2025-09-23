using System;

namespace ZenStates.Core
{
    public class RyzenSmu
    {
        private readonly PawnIo _pawnIO;

        public RyzenSmu()
        {
            string resourceName = "ZenStates.Core.Resources.PawnIo.RyzenSMU.bin";
            _pawnIO = PawnIo.LoadModuleFromResource(typeof(RyzenSmu).Assembly, resourceName);
        }

        public uint GetSmuVersion()
        {
            if (!Mutexes.WaitPciBus(5000))
                throw new TimeoutException("Timeout waiting for PCI bus mutex");

            uint version;
            try
            {
                long[] outArray = _pawnIO.Execute("ioctl_get_smu_version", new long[0], 1);
                version = (uint)outArray[0];
            }
            finally
            {
                Mutexes.ReleasePciBus();
            }

            return version;
        }

        public long GetCodeName()
        {
            long[] outArray = _pawnIO.Execute("ioctl_get_code_name", new long[0], 1);
            return outArray[0];
        }

        public long[] ReadPmTable(int size)
        {
            long[] outArray = _pawnIO.Execute("ioctl_read_pm_table", new long[0], size);
            return outArray;
        }

        public void UpdatePmTable()
        {
            if (!Mutexes.WaitPciBus(5000))
                throw new TimeoutException("Timeout waiting for PCI bus mutex");

            try
            {
                _pawnIO.Execute("ioctl_update_pm_table", new long[0], 0);
            }
            finally
            {
                Mutexes.ReleasePciBus();
            }
        }

        public void ResolvePmTable(out uint version, out uint tableBase)
        {
            if (!Mutexes.WaitPciBus(5000))
                throw new TimeoutException("Timeout waiting for PCI bus mutex");

            try
            {
                long[] outArray = _pawnIO.Execute("ioctl_resolve_pm_table", new long[0], 2);
                version = (uint)outArray[0];
                tableBase = (uint)outArray[1];
            }
            finally
            {
                Mutexes.ReleasePciBus();
            }
        }

        public void Close()
        {
            _pawnIO.Close();
        }
    }
}
