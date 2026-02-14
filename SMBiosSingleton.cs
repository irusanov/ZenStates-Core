using OpenHardwareMonitor.Hardware;
using System;

namespace ZenStates.Core
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
            ((IDisposable)instance).Dispose();
        }

        ~SMBiosSingleton()
        {
            Dispose();
        }
    }
}
