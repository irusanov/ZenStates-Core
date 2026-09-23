namespace ZenStates.Core.Hardware.Apob
{
    internal static class ApobCcdlValidation
    {
        public const uint MinTccdl = 8;
        public const uint MaxTccdl = 22;
        public const uint MinTccdlWr2 = 16;
        public const uint MaxTccdlWr2 = 44;
        public const uint MinTccdlWr = 32;
        public const uint MaxTccdlWr = 88;

        internal static bool IsValid(uint tccdl, uint tccdlWr, uint tccdlWr2)
        {
            if (tccdl == 0 || tccdlWr == 0 || tccdlWr2 == 0)
                return false;

            if (tccdl < MinTccdl || tccdl > MaxTccdl)
                return false;

            if (tccdlWr2 < MinTccdlWr2 || tccdlWr2 > MaxTccdlWr2)
                return false;

            if (tccdlWr < MinTccdlWr || tccdlWr > MaxTccdlWr)
                return false;

            return tccdl <= tccdlWr2 && tccdlWr2 <= tccdlWr;
        }
    }
}