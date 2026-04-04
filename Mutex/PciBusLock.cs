using System;
namespace ZenStates.Core
{
    public sealed class PciBusLock : IDisposable
    {
        private bool _acquired;

        public PciBusLock(int timeoutMs = 5000)
        {
            _acquired = Mutexes.WaitPciBus(timeoutMs);
            if (!_acquired)
                throw new TimeoutException($"Timed out waiting for PciBus lock after {timeoutMs} ms.");
        }

        public void Dispose()
        {
            if (_acquired)
            {
                Mutexes.ReleasePciBus();
                _acquired = false;
            }
        }
    }
}
