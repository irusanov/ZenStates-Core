using System;
using ZenStates.Core.OHWM;

namespace ZenStates.Core.Hardware
{
    internal static class SMBiosSingleton
    {
        private static SMBios instance = null;

        public static SMBios Instance
        {
            get
            {
                if (instance == null)
                    instance = new SMBios();

                return instance;
            }
        }
    }
}
