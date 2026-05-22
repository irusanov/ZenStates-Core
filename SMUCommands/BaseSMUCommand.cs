using System;

namespace ZenStates.Core.SMUCommands
{
    internal abstract class BaseSMUCommand : IDisposable
    {
        internal SMU smu;
        internal CmdResult result;
        private bool disposedValue;
        private readonly uint _maxArgs = Constants.DEFAULT_MAILBOX_ARGS;

        protected BaseSMUCommand(SMU smuInstance, uint maxArgs = Constants.DEFAULT_MAILBOX_ARGS)
        {
            if (smuInstance != null)
            {
                smu = smuInstance;
                _maxArgs = maxArgs;
            }
            result = new CmdResult(maxArgs);
        }

        public virtual bool CanExecute() => smu != null;
        public virtual CmdResult Execute()
        {
            // reset args to 0, to avoid getting the incoming parameter as a result in case of an error
            if (!result.Success)
                ResetArgs();

            Dispose();
            return result;
        }

        protected void ResetArgs()
        {
            result.args = Utils.MakeCmdArgs(maxArgs: _maxArgs);
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
