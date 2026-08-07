using System;

namespace ZenStates.Core
{
    internal class LpcIO : IDisposable
    {
        private const string IOCTL_SELECT_SLOT   = "ioctl_select_slot";
        private const string IOCTL_FIND_BARS     = "ioctl_find_bars";
        private const string IOCTL_PIO_INB       = "ioctl_pio_inb";
        private const string IOCTL_PIO_OUTB      = "ioctl_pio_outb";
        private const string IOCTL_SUPERIO_INB   = "ioctl_superio_inb";
        private const string IOCTL_SUPERIO_INW   = "ioctl_superio_inw";
        private const string IOCTL_SUPERIO_OUTB  = "ioctl_superio_outb";
        private const int    STATUS_SUCCESS      = 0;

        private static readonly long[] _emptyOutputBuffer = new long[0];

        private readonly PawnIo _pawnIo;
        private volatile bool _disposed;

        public bool IsLoaded => _pawnIo.IsLoaded;

        public LpcIO()
        {
            string resourceName = "ZenStates.Core.Resources.PawnIo.LpcIO.bin";
            _pawnIo = PawnIo.LoadModuleFromResource(typeof(LpcIO).Assembly, resourceName);
        }

        // NoLock methods — caller must hold the ISA bus lock.

        public bool SelectSlot(int slot)
        {
            long[] input = new long[] { slot };
            int status = _pawnIo.ExecuteHr(IOCTL_SELECT_SLOT, input, 1, _emptyOutputBuffer, 0, out uint _);
            return status == STATUS_SUCCESS;
        }

        public bool FindBars()
        {
            int status = _pawnIo.ExecuteHr(IOCTL_FIND_BARS, new long[0], 0, _emptyOutputBuffer, 0, out uint _);
            return status == STATUS_SUCCESS;
        }

        public bool ReadPort(ushort port, out byte value)
        {
            long[] input  = new long[] { port };
            long[] output = new long[1];

            int status = _pawnIo.ExecuteHr(IOCTL_PIO_INB, input, 1, output, 1, out uint returnSize);
            if (status == STATUS_SUCCESS && returnSize > 0)
            {
                value = unchecked((byte)output[0]);
                return true;
            }

            value = 0;
            return false;
        }

        public bool WritePort(ushort port, byte value)
        {
            long[] input = new long[] { port, value };
            int status = _pawnIo.ExecuteHr(IOCTL_PIO_OUTB, input, 2, _emptyOutputBuffer, 0, out uint _);
            return status == STATUS_SUCCESS;
        }

        public bool ReadByte(byte reg, out byte value)
        {
            long[] input  = new long[] { reg };
            long[] output = new long[1];

            int status = _pawnIo.ExecuteHr(IOCTL_SUPERIO_INB, input, 1, output, 1, out uint returnSize);
            if (status == STATUS_SUCCESS && returnSize > 0)
            {
                value = unchecked((byte)output[0]);
                return true;
            }

            value = 0;
            return false;
        }

        public bool ReadWord(byte reg, out ushort value)
        {
            long[] input  = new long[] { reg };
            long[] output = new long[1];

            int status = _pawnIo.ExecuteHr(IOCTL_SUPERIO_INW, input, 1, output, 1, out uint returnSize);
            if (status == STATUS_SUCCESS && returnSize > 0)
            {
                value = unchecked((ushort)output[0]);
                return true;
            }

            value = 0;
            return false;
        }

        public bool WriteByte(byte reg, byte value)
        {
            long[] input = new long[] { reg, value };
            int status = _pawnIo.ExecuteHr(IOCTL_SUPERIO_OUTB, input, 2, _emptyOutputBuffer, 0, out uint _);
            return status == STATUS_SUCCESS;
        }

        // Locking methods — acquire the ISA bus mutex for each call.

        //public bool SelectSlot(int slot)
        //{
        //    using (new IsaBusLock())
        //        return SelectSlotNoLock(slot);
        //}

        //public bool FindBars()
        //{
        //    using (new IsaBusLock())
        //        return FindBarsNoLock();
        //}

        //public bool ReadPort(ushort port, out byte value)
        //{
        //    using (new IsaBusLock())
        //        return ReadPortNoLock(port, out value);
        //}

        //public bool WritePort(ushort port, byte value)
        //{
        //    using (new IsaBusLock())
        //        return WritePortNoLock(port, value);
        //}

        //public bool ReadByte(byte reg, out byte value)
        //{
        //    using (new IsaBusLock())
        //        return ReadByteNoLock(reg, out value);
        //}

        //public bool ReadWord(byte reg, out ushort value)
        //{
        //    using (new IsaBusLock())
        //        return ReadWordNoLock(reg, out value);
        //}

        //public bool WriteByte(byte reg, byte value)
        //{
        //    using (new IsaBusLock())
        //        return WriteByteNoLock(reg, value);
        //}

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
