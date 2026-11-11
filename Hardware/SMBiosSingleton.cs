using ZenStates.Core.OHWM;

namespace ZenStates.Core.Hardware
{
    internal static class SMBiosSingleton
    {
        private static readonly object instanceLock = new object();
        private static volatile SMBios instance;

        public static SMBios Instance
        {
            get
            {
                // Two threads racing here would each parse the whole SMBIOS and keep a second copy of its tables
                if (instance == null)
                {
                    lock (instanceLock)
                    {
                        if (instance == null)
                            instance = new SMBios(CoreOptions.Current);
                    }
                }

                return instance;
            }
        }
    }
}
