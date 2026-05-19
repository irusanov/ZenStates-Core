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
        bool ReadBlockData(byte addr7, byte command, out System.Collections.Generic.List<byte> data);
        bool WriteBlockData(byte addr7, byte command, System.Collections.Generic.List<byte> data);

        // Port selection
        bool ChangePort(int port, out int previousPort);
        bool ChangePort(int port);
    }
}
