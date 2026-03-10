namespace ZenStates.Core
{
    /// <summary>
    /// JEDEC JESD301-2 PMIC5100 register constants used by the expanded helper modules.
    /// This file intentionally keeps the original Ddr5PmicReader unchanged and adds a central map.
    /// </summary>
    public static class JedecPmicRegisters
    {
        public const byte STATUS_0 = 0x08;
        public const byte STATUS_1 = 0x09;
        public const byte STATUS_2 = 0x0A;
        public const byte STATUS_3 = 0x0B;

        public const byte CLEAR_0 = 0x10;
        public const byte CLEAR_1 = 0x11;
        public const byte CLEAR_2 = 0x12;
        public const byte CLEAR_3 = 0x13;
        public const byte GLOBAL_CLEAR = 0x14;

        public const byte CURRENT_LIMIT = 0x20;
        public const byte SWA_VID = 0x21;
        public const byte SWA_THRESH = 0x22;
        public const byte SWB_VID = 0x25;
        public const byte SWB_THRESH = 0x26;
        public const byte SWC_VID = 0x27;
        public const byte SWC_THRESH = 0x28;

        public const byte VIN_BULK_OV_CFG = 0x1B;
        public const byte POWER_MODE_CFG = 0x1A;

        public const byte TELEMETRY_SELECT = 0x30;
        public const byte TELEMETRY_VALUE = 0x31;
        public const byte VR_ENABLE = 0x32;
        public const byte PMIC_TEMP = 0x33;

        public const byte VENDOR_PASSWORD_LSB = 0x37;
        public const byte VENDOR_PASSWORD_MSB = 0x38;
        public const byte VENDOR_COMMAND = 0x39;

        public const byte REVISION = 0x3B;
        public const byte VENDOR_BANK = 0x3C;
        public const byte VENDOR_CODE = 0x3D;

        // Telemetry payload registers called out in the JESD301-2 text.
        public const byte SWA_CURRENT_OR_POWER = 0x0C;
        public const byte SWB_CURRENT_OR_POWER = 0x0E;
        public const byte SWC_CURRENT_OR_POWER = 0x0F;
    }
}
