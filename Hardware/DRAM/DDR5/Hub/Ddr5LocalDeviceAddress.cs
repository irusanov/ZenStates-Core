namespace ZenStates.Core.Hardware.DRAM.DDR5.Hub
{
    /// <summary>
    /// 7-bit addresses of the devices on a DDR5 module (JESD300-5). Each address is a 4-bit local device type (LID)
    /// followed by the 3-bit host ID (HID) of the module slot, which all devices of a module share with its SPD hub.
    /// </summary>
    public static class Ddr5LocalDeviceAddress
    {
        private const byte LID_TS0 = 0x10;   // 0010
        private const byte LID_TS1 = 0x30;   // 0110
        private const byte LID_PMIC = 0x48;  // 1001
        private const byte LID_SPD = 0x50;   // 1010
        private const byte LID_RCD = 0x58;   // 1011

        public static int HostId(byte hubAddress)
        {
            return hubAddress & 0x07;
        }

        public static byte SpdHub(int hostId)
        {
            return (byte)(LID_SPD | (hostId & 0x07));
        }

        /// <summary>PMIC0 of the module whose SPD hub is at <paramref name="hubAddress"/>.</summary>
        public static byte Pmic(byte hubAddress)
        {
            return (byte)(LID_PMIC | HostId(hubAddress));
        }

        public static byte Rcd(byte hubAddress)
        {
            return (byte)(LID_RCD | HostId(hubAddress));
        }

        public static byte ThermalSensor0(byte hubAddress)
        {
            return (byte)(LID_TS0 | HostId(hubAddress));
        }

        public static byte ThermalSensor1(byte hubAddress)
        {
            return (byte)(LID_TS1 | HostId(hubAddress));
        }
    }
}
