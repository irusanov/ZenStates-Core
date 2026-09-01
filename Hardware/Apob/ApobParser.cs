using System;

namespace ZenStates.Core.Hardware.Apob
{
    internal static class ApobDataReader
    {
        internal static bool TryRead(byte[] data, uint offset, ApobBlockLayout layout, out ApobData result)
        {
            result = null;
            if (data == null || layout == null)
                return false;

            try
            {
                result = new ApobData(data, offset, layout);
                return true;
            }
            catch (ArgumentException)
            {
                return false;
            }
        }

        internal static ApobData Read(byte[] data, uint offset, ApobBlockLayout layout)
        {
            return new ApobData(data, offset, layout);
        }

        internal static bool TryReadCcdl(byte[] data, ApobCcdlLayout layout, out uint ccdl, out uint ccdlrw, out uint ccdlrw2)
        {
            ccdl = 0;
            ccdlrw = 0;
            ccdlrw2 = 0;

            if (data == null)
                throw new ArgumentNullException(nameof(data));
            if (layout == null)
                throw new ArgumentNullException(nameof(layout));

            int matchIndex = Utils.FindSequence(data, 0, layout.Magic);
            if (matchIndex < 0)
                return false;

            long offset = (long)matchIndex + layout.Magic.Length + layout.CcdlBlockOffset;
            long requiredSize = layout.ValueWidth == ApobValueWidth.UInt16 ? 6 : 12;
            if (offset < 0 || offset + requiredSize > data.Length)
                return false;

            if (layout.ValueWidth == ApobValueWidth.UInt16)
            {
                ccdl = Utils.ReadUInt16(data, (uint)offset);
                ccdlrw = Utils.ReadUInt16(data, (uint)(offset + 2));
                ccdlrw2 = Utils.ReadUInt16(data, (uint)(offset + 4));
            }
            else
            {
                ccdl = Utils.ReadUInt32(data, (uint)offset);
                ccdlrw = Utils.ReadUInt32(data, (uint)(offset + 4));
                ccdlrw2 = Utils.ReadUInt32(data, (uint)(offset + 8));
            }

            return true;
        }
    }
}
