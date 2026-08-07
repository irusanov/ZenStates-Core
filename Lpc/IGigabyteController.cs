using System;

namespace ZenStates.Core.Lpc
{
    internal interface IGigabyteController : IDisposable
    {
        bool Enable(bool enabled);

        void Restore();
    }
}
