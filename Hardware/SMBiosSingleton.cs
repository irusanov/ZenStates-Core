using System;
using ZenStates.Core.OHWM;

namespace ZenStates.Core.Hardware
{
    internal sealed class SMBiosSingleton : IDisposable
    {
        private static SMBios instance = null;
        private SMBiosSingleton() { }

        public static SMBios Instance
        {
            get
            {
                if (instance == null)
                    instance = new SMBios();

                return instance;
            }
        }

        public void Dispose()
        {
            if (instance != null)
                ((IDisposable)instance).Dispose();
        }

        ~SMBiosSingleton()
        {
            Dispose();
        }
    }
}
