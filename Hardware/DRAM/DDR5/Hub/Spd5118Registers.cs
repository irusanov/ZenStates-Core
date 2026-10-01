namespace ZenStates.Core.Hardware.DRAM.DDR5.Hub
{
    /// <summary>
    /// SPD5118 hub register map and the pure helpers that go with it (JESD300-5). No bus access here; see
    /// <see cref="Spd5118Hub"/> for that.
    ///
    /// The hub has volatile mode registers MR0~MR127 at offsets 0x00~0x7F and a 1024-byte non-volatile memory (the SPD)
    /// that is read through a 128-byte window at offsets 0x80~0xFF. In I2C legacy mode with 1-byte addressing the window
    /// shows one of 8 pages, selected by MR11[2:0].
    /// </summary>
    public static class Spd5118Registers
    {
        // Identification
        public const byte MR0_DEVICE_TYPE_MSB = 0x00;   // 0x51
        public const byte MR1_DEVICE_TYPE_LSB = 0x01;   // 0x18 = hub with temperature sensor
        public const byte MR2_REVISION = 0x02;          // [5:4] major - 1, [3:1] minor
        public const byte MR3_VENDOR_ID_0 = 0x03;       // JEP106 continuation count (with parity)
        public const byte MR4_VENDOR_ID_1 = 0x04;       // JEP106 manufacturer code (with parity)
        public const byte MR5_CAPABILITY = 0x05;        // [1] temperature sensor, [0] hub function

        // Configuration
        public const byte MR11_LEGACY_MODE = 0x0B;      // [3] 1 = 2-byte addressing, [2:0] NVM page
        public const byte MR12_WRITE_PROTECT_0 = 0x0C;  // write protection of NVM blocks 7~0
        public const byte MR13_WRITE_PROTECT_1 = 0x0D;  // write protection of NVM blocks 15~8
        public const byte MR18_DEVICE_CONFIG = 0x12;    // [5] interface: 0 = I2C, 1 = I3C basic

        // Temperature sensor (only on a hub with MR1 = 0x18)
        public const byte MR19_CLEAR_TS_STATUS = 0x13;  // write 1 to clear MR51 bits [3:0]
        public const byte MR26_TS_CONFIG = 0x1A;        // [0] 1 = sensor disabled
        public const byte MR28_TS_HIGH_LIMIT = 0x1C;    // MR28~MR29
        public const byte MR30_TS_LOW_LIMIT = 0x1E;     // MR30~MR31
        public const byte MR32_TS_CRIT_HIGH = 0x20;     // MR32~MR33
        public const byte MR34_TS_CRIT_LOW = 0x22;      // MR34~MR35
        public const byte MR49_TS_TEMPERATURE = 0x31;   // MR49~MR50
        public const byte MR51_TS_STATUS = 0x33;        // [3] crit low, [2] crit high, [1] low, [0] high

        public const byte DEVICE_TYPE_MSB = 0x51;
        public const byte DEVICE_TYPE_LSB = 0x18;

        public const byte MR5_TS_SUPPORT = 0x02;
        public const byte MR5_HUB_SUPPORT = 0x01;
        public const byte MR11_TWO_BYTE_ADDRESSING = 0x08;
        public const byte MR11_PAGE_MASK = 0x07;
        public const byte MR18_I3C = 0x20;
        public const byte MR26_TS_DISABLE = 0x01;

        public const byte MR51_HIGH = 0x01;
        public const byte MR51_LOW = 0x02;
        public const byte MR51_CRIT_HIGH = 0x04;
        public const byte MR51_CRIT_LOW = 0x08;

        /// <summary>Hub 7-bit addresses: local device type 1010 and the 3-bit host ID (HID) of the module slot.</summary>
        public const byte ADDRESS_FIRST = 0x50;
        public const byte ADDRESS_LAST = 0x57;

        /// <summary>Size of the non-volatile memory and of one page of it.</summary>
        public const int NVM_SIZE = 1024;
        public const int PAGE_SIZE = 128;

        /// <summary>Offset of the NVM window in the register space.</summary>
        public const byte NVM_WINDOW = 0x80;

        /// <summary>
        /// True when MR0/MR1 identify an SPD5118 hub (0x51, 0x18). Some hubs and SMBus controllers return the two bytes
        /// swapped, and the low nibble of the LSB is what marks the hub, so both orders are accepted.
        /// </summary>
        public static bool IsHubDeviceType(byte mr0, byte mr1)
        {
            if (mr0 == DEVICE_TYPE_MSB && (mr1 & 0x0F) == 0x08)
                return true;
            return (mr0 & 0x0F) == 0x08 && mr1 == DEVICE_TYPE_MSB;
        }

        public static bool IsHubAddress(byte addr7)
        {
            return addr7 >= ADDRESS_FIRST && addr7 <= ADDRESS_LAST;
        }

        /// <summary>NVM page that holds <paramref name="nvmOffset"/>.</summary>
        public static int NvmPage(int nvmOffset)
        {
            return nvmOffset / PAGE_SIZE;
        }

        /// <summary>Register that shows <paramref name="nvmOffset"/> once its page is selected.</summary>
        public static byte NvmRegister(int nvmOffset)
        {
            return (byte)(NVM_WINDOW + nvmOffset % PAGE_SIZE);
        }

        /// <summary>
        /// Temperature register (low byte first) to millidegrees Celsius: bits [12:2] are a signed value in 0.25 C steps.
        /// </summary>
        public static int TemperatureToMilliC(int raw16)
        {
            int value = (raw16 >> 2) & 0x7FF;
            if ((value & 0x400) != 0)
                value -= 0x800;
            return value * 250;
        }
    }
}
