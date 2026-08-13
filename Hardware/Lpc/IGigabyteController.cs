using System;

namespace ZenStates.Core.Hardware.Lpc
{
    internal interface IGigabyteController : IDisposable
    {
        bool Enable(bool enabled);

        void Restore();
    }
}
