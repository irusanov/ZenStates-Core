using System;

namespace ZenStates.Core.Hardware.Apob
{
    public abstract class ApobBlockData
    {
        private readonly byte[] _rawBytes;

        protected ApobBlockData(byte[] data, uint offset, ApobBlockLayout layout)
        {
            if (data == null)
                throw new ArgumentNullException(nameof(data));
            if (layout == null)
                throw new ArgumentNullException(nameof(layout));

            Offset = offset;

            long end = (long)Offset + layout.BlockSize;
            if (end > data.Length)
                throw new ArgumentException("Buffer too small for the configured APOB block layout.", nameof(data));

            _rawBytes = new byte[layout.BlockSize];
            Buffer.BlockCopy(data, (int)Offset, _rawBytes, 0, _rawBytes.Length);
            Layout = layout;
        }

        protected uint Offset { get; private set; }

        protected ApobBlockLayout Layout { get; private set; }

        internal byte[] RawBytes
        {
            get { return (byte[])_rawBytes.Clone(); }
        }

        protected byte? ReadRawValue(int relativeOffset)
        {
            if (relativeOffset < 0 || relativeOffset >= _rawBytes.Length)
                return null;

            return _rawBytes[relativeOffset];
        }
    }
}
