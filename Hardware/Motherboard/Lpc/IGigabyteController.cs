using System;

namespace ZenStates.Core.Hardware.Motherboard.Lpc
{
    internal interface IGigabyteController : IDisposable
    {
        bool Enable(bool enabled);

        void Restore();
    }
}
