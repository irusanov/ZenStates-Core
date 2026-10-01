using System.Text;

namespace ZenStates.Core.Hardware.DRAM.DDR5.Hub
{
    /// <summary>Identification and configuration registers of an SPD5118 hub (MR0~MR18).</summary>
    public class Spd5118HubInfo
    {
        /// <summary>7-bit SMBus address of the hub.</summary>
        public byte Address;

        /// <summary>MR0 / MR1 as read.</summary>
        public byte DeviceTypeMsb;
        public byte DeviceTypeLsb;

        /// <summary>MR2, e.g. "1.0".</summary>
        public string Revision;

        /// <summary>MR3 / MR4: JEP106 vendor ID of the hub.</summary>
        public byte VendorIdBank;
        public byte VendorIdCode;
        public string Vendor;

        /// <summary>MR5 [1]: the hub has a temperature sensor.</summary>
        public bool TempSensorSupported;

        /// <summary>MR5 [0]: the hub function (local bus to the PMIC, RCD and thermal sensors) is supported.</summary>
        public bool HubSupported;

        /// <summary>MR11 [3]: 2-byte addressing; the page pointer is not used and the NVM is not read in this mode.</summary>
        public bool TwoByteAddressing;

        /// <summary>MR12 / MR13: one bit per write-protected 64-byte NVM block, block 0 in bit 0.</summary>
        public int WriteProtectedBlocks;

        /// <summary>MR18 [5]: the hub is in I3C basic mode.</summary>
        public bool IsI3c;

        /// <summary>Host ID of the module slot: the low 3 bits of the address, shared by all devices of the module.</summary>
        public int HostId
        {
            get { return Address & 0x07; }
        }

        public override string ToString()
        {
            StringBuilder sb = new StringBuilder();
            sb.AppendFormat("  Address            : 0x{0:X2}\n", Address);
            sb.AppendFormat("  Device Type        : 0x{0:X2}{1:X2}\n", DeviceTypeMsb, DeviceTypeLsb);
            sb.AppendFormat("  Revision           : {0}\n", Revision);
            sb.AppendFormat("  Vendor             : {0}\n", Vendor);
            sb.AppendFormat("  Temperature Sensor : {0}\n", TempSensorSupported ? "Supported" : "Not supported");
            sb.AppendFormat("  Hub Function       : {0}\n", HubSupported ? "Supported" : "Not supported");
            sb.AppendFormat("  Addressing         : {0}\n", TwoByteAddressing ? "2-byte" : "1-byte");
            sb.AppendFormat("  Interface          : {0}\n", IsI3c ? "I3C basic" : "I2C");
            sb.AppendFormat("  Write Protection   : 0x{0:X4}\n", WriteProtectedBlocks);
            return sb.ToString();
        }
    }
}
