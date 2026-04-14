namespace ZenStates.Core
{
    /// <summary>
    /// JEDEC JESD301-2 PMIC5100 register constants used by the expanded helper modules.
    /// This file intentionally keeps the original Ddr5PmicReader unchanged and adds a central map.
    /// </summary>
    internal static class JedecPmicRegisters
    {
        public const byte PMIC_ADDR_BASE = 0x48;
        public const byte PMIC_ADDR_LAST = 0x4F;
        public const byte SPD_PMIC_OFFSET = 0x08; // SPD_addr - PMIC_addr

        public const byte REG_STATUS_0 = 0x08;
        public const byte REG_STATUS_1 = 0x09;
        public const byte REG_STATUS_2 = 0x0A;
        public const byte REG_STATUS_3 = 0x0B;

        public const byte REG_CLEAR_0 = 0x10;
        public const byte REG_CLEAR_1 = 0x11;
        public const byte REG_CLEAR_2 = 0x12;
        public const byte REG_CLEAR_3 = 0x13;
        public const byte REG_GLOBAL_CLEAR = 0x14;
        
        public const byte REG_CURRENT_LIMIT = 0x20;  // SWA [7:6], SWB [3:2], SWC [1:0]

        // Voltage VID registers (bits [7:1] = 7-bit VID, bit [0] = PG low threshold)
        public const byte REG_SWA_VID = 0x21;
        public const byte REG_SWA_THRESH = 0x22;
        public const byte REG_SWB_VID = 0x25;
        public const byte REG_SWB_THRESH = 0x26;
        public const byte REG_SWC_VID = 0x27;
        public const byte REG_SWC_THRESH = 0x28;

        public const byte REG_VIN_BULK_OV_CFG = 0x1B;
        public const byte REG_POWER_MODE_CFG = 0x1A;

        // Rail config / LDO / ADC
        public const byte REG_RAIL_CONFIG_A = 0x29;
        public const byte REG_RAIL_CONFIG_B = 0x2A;
        public const byte REG_LDO_SETTINGS = 0x2B;
        public const byte REG_SHUTDOWN_TEMP = 0x2E;

        public const byte REG_TELEMETRY_SELECT = 0x30;
        public const byte REG_TELEMETRY_VALUE = 0x31;

        public const byte REG_VR_ENABLE = 0x32;  // [7] = VR Enable
        public const byte REG_PMIC_TEMP = 0x33;  // [7:5] = temperature code, [2] = VOUT_1.0V PG

        public const byte REG_VENDOR_PASSWORD_LSB = 0x37;
        public const byte REG_VENDOR_PASSWORD_MSB = 0x38;
        public const byte REG_VENDOR_COMMAND = 0x39;

        public const byte REG_REVISION = 0x3B;  // [5:4] = major+1, [3:1] = minor
        public const byte REG_VENDOR_BANK = 0x3C;  // VENDOR_ID_BYTE0
        public const byte REG_VENDOR_CODE = 0x3D;  // VENDOR_ID_BYTE1

        public const byte REG_SWA_CURRENT_OR_POWER = 0x0C;
        public const byte REG_SWB_CURRENT_OR_POWER = 0x0E;
        public const byte REG_SWC_CURRENT_OR_POWER = 0x0F;

        public const byte REG_NVM_LDO_SETTINGS = 0x51;
    }
}
