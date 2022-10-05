using System;

namespace ZenStates.Core.SMUCommands
{
    internal abstract class BaseSMUCommand : IDisposable
    {
        internal SMU smu;
        internal CmdResult result;
        private bool disposedValue;

        protected BaseSMUCommand(SMU smuInstance)
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
            // reset args to 0, to avoid getting the incoming parameter as a result in case of an error
            if (!result.Success)
                result.args = Utils.MakeCmdArgs();

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
