using System;

namespace ZenStates.Core.SMUCommands
{
    internal abstract class BaseSMUCommand : IDisposable
    {
        internal SMU smu = null;
        internal CmdResult result;
        private bool disposedValue;

        public BaseSMUCommand(SMU smuInstance)
        {
            if (smuInstance != null)
            {
                smu = smuInstance;
            }
            result = new CmdResult();
        }

        public virtual bool CanExecute() => smu != null;
        public virtual CmdResult Execute()
        {
            Dispose();
            return result;
        }
        protected virtual void Dispose(bool disposing)
        {
            if (!disposedValue)
            {
                if (disposing)
                {
                    smu = null;
                }

                disposedValue = true;
            }
        }
        public void Dispose()
        {
            // Do not change this code. Put cleanup code in 'Dispose(bool disposing)' method
            Dispose(disposing: true);
            GC.SuppressFinalize(this);
        }
    }
}
