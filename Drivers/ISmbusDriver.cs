using System;

namespace ZenStates.Core.Drivers
{
    internal interface ISmbusDriver: IDisposable
    {
        bool IsBusy();
        bool Quick(byte addr7, byte readWrite);

        // Byte functions
        bool ReadByteDataNoLock(byte addr7, byte command, out byte value);
        bool ReadByteData(byte addr7, byte command, out byte value);
        bool WriteByteData(byte addr7, byte command, byte value);
        bool WriteByteDataNoLock(byte addr7, byte command, byte value);

        // Word functions
        bool ReadWordDataNoLock(byte addr7, byte command, out ushort value);
        bool ReadWordData(byte addr7, byte command, out ushort value);
        bool WriteWordDataNoLock(byte addr7, byte command, ushort value);
        bool WriteWordData(byte addr7, byte command, ushort value);

        // Block functions
    }
}
