using System;
namespace ZenStates.Core
{
    public sealed class SmbusLock : IDisposable
    {
        private bool _acquired;

        public SmbusLock(int timeoutMs = 5000)
        {
            _acquired = Mutexes.WaitSmbus(timeoutMs);
            if (!_acquired)
                throw new TimeoutException($"Timed out waiting for SMBus lock after {timeoutMs} ms.");
        }

        public void Dispose()
        {
            if (_acquired)
            {
                Mutexes.ReleaseSmbus();
                _acquired = false;
            }
        }
    }
}
