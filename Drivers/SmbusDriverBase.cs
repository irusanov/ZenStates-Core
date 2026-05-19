using System;
using System.Collections.Generic;

namespace ZenStates.Core.Drivers
{
    public abstract class SmbusDriverBase : ISmbusDriver, IDisposable
    {
        private bool disposedValue;

        public const int I2C_SMBUS_WRITE = 0;
        public const int I2C_SMBUS_READ = 1;

        public const int I2C_SMBUS_QUICK = 0;
        public const int I2C_SMBUS_BYTE = 1;
        public const int I2C_SMBUS_BYTE_DATA = 2;
        public const int I2C_SMBUS_WORD_DATA = 3;
        public const int I2C_SMBUS_BLOCK_DATA = 5;

        internal abstract bool SmbusQuickNoLock(byte addr7, byte readWrite);

        public bool SmbusQuick(byte addr7, byte readWrite)
        {
            using (new SmbusLock())
            {
                return SmbusQuickNoLock(addr7, readWrite);
            }
        }

        internal abstract bool ReadByteDataNoLock(byte addr7, byte command, out byte value);

        public bool ReadByteData(byte addr7, byte command, out byte value)
        {
            using (new SmbusLock())
            {
                return ReadByteDataNoLock(addr7, command, out value);
            }
        }

        internal abstract bool WriteByteDataNoLock(byte addr7, byte command, byte value);

        public bool WriteByteData(byte addr7, byte command, byte value)
        {
            using (new SmbusLock())
            {
                return WriteByteDataNoLock(addr7, command, value);
            }
        }

        internal abstract bool ReadWordDataNoLock(byte addr7, byte command, out ushort value);

        public bool ReadWordData(byte addr7, byte command, out ushort value)
        {
            using (new SmbusLock())
            {
                return ReadWordDataNoLock(addr7, command, out value);
            }
        }

        internal abstract bool WriteWordDataNoLock(byte addr7, byte command, ushort value);

        public bool WriteWordData(byte addr7, byte command, ushort value)
        {
            using (new SmbusLock())
            {
                return WriteWordDataNoLock(addr7, command, value);
            }
        }

        internal abstract bool ReadBlockDataNoLock(byte addr7, byte command, out List<byte> data);

        public bool ReadBlockData(byte addr7, byte command, out List<byte> data)
        {
            using (new SmbusLock())
            {
                return ReadBlockDataNoLock(addr7, command, out data);
            }
        }

        internal abstract bool WriteBlockDataNoLock(byte addr7, byte command, List<byte> data);

        public bool WriteBlockData(byte addr7, byte command, List<byte> data)
        {
            using (new SmbusLock())
            {
                return WriteBlockDataNoLock(addr7, command, data);
            }
        }

        internal abstract bool ChangePortNoLock(int port, out int previousPort);

        internal bool ChangePortNoLock(int port)
        {
            return ChangePortNoLock(port, out int _);
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

        protected virtual void Dispose(bool disposing)
        {
            if (!disposedValue)
            {
                disposedValue = true;
            }
        }

        public void Dispose()
        {
            Dispose(disposing: true);
            GC.SuppressFinalize(this);
        }
    }
}
