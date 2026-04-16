using System;

namespace ZenStates.Core.Drivers
{
    internal interface ISmbusDriver : IDisposable
    {
        bool SmbusQuick(byte addr7, byte readWrite);

        // Byte functions

        bool ReadByteData(byte addr7, byte command, out byte value);
        bool WriteByteData(byte addr7, byte command, byte value);

        // Word functions
        bool ReadWordData(byte addr7, byte command, out ushort value);
        bool WriteWordData(byte addr7, byte command, ushort value);

        // Block functions
    }
}
