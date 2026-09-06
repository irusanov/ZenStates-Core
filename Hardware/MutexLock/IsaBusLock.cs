using System;
namespace ZenStates.Core.Hardware.MutexLock
{
    public sealed class IsaBusLock : IDisposable
    {
        private bool _acquired;

        public IsaBusLock(int timeoutMs = 5000)
        {
            _acquired = Mutexes.WaitIsaBus(timeoutMs);
            if (!_acquired)
                throw new TimeoutException($"Timed out waiting for ISA bus lock after {timeoutMs} ms.");
        }

        public void Dispose()
        {
            if (_acquired)
            {
                Mutexes.ReleaseIsaBus();
                _acquired = false;
            }
        }
    }
}
