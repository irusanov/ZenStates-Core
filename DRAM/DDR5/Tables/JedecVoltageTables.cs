namespace ZenStates.Core
{
    /// <summary>
    /// Shared JEDEC voltage conversion helpers used by the PMIC expansion modules.
    /// </summary>
    public static class JedecVoltageTables
    {
        public const int SwaSwbBaseMv = 800;
        public const int SwcBaseMv = 1500;
        public const int VidStepMv = 5;

        public static int DecodeSwabVid7ToMv(byte reg)
        {
            return SwaSwbBaseMv + (((int)reg >> 1) & 0x7F) * VidStepMv;
        }

        public static int DecodeSwcVid7ToMv(byte reg)
        {
            return SwcBaseMv + (((int)reg >> 1) & 0x7F) * VidStepMv;
        }

        public static int DecodeSwabVid8ToMv(byte reg)
        {
            return SwaSwbBaseMv + ((int)reg) * VidStepMv;
        }

        public static int DecodeSwcVid8ToMv(byte reg)
        {
            return SwcBaseMv + ((int)reg) * VidStepMv;
        }

        public static int AdcLsbsToMv(byte raw, int scaleMvPerLsb)
        {
            return ((int)raw) * scaleMvPerLsb;
        }
    }
}
