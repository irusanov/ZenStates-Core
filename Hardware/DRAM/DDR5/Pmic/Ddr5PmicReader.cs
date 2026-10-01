using System;
using System.Diagnostics;
using ZenStates.Core.Drivers;
using ZenStates.Core.Hardware.DRAM.DDR5.Hub;
using ZenStates.Core.Hardware.DRAM.DDR5.Spd;

namespace ZenStates.Core.Hardware.DRAM.DDR5.Pmic
{
    /// <summary>
    /// Reads the PMIC of a module. Which PMIC to expect comes from the SPD (PMIC0 in the support device bytes); its
    /// address from the SPD hub's (same host ID, local device type 1001).
    /// </summary>
    public static class Ddr5PmicReader
    {
        // R0x3C / R0x3D, the JEP106 vendor ID every JESD301 PMIC has
        private const byte REG_VENDOR_ID_0 = 0x3C;
        private const byte REG_VENDOR_ID_1 = 0x3D;

        /// <summary>
        /// Reads the PMIC the SPD lists as PMIC0 for the module whose hub is at <paramref name="hubAddress"/>. Null when
        /// none is listed, the model is not supported or it does not answer. The bus must be on the module port and the
        /// SMBus mutex held.
        /// </summary>
        internal static Ddr5Pmic ReadNoLock(SmbusDriverBase smbus, byte hubAddress, Ddr5SpdDevice listed)
        {
            if (smbus == null || listed == null || !listed.Installed)
                return null;

            Ddr5PmicType type = listed.PmicType;
            if (!Ddr5PmicFactory.IsSupported(type))
            {
                Debug.WriteLine(string.Format("PMIC of module 0x{0:X2}: {1} is not supported.", hubAddress, listed.TypeName));
                return null;
            }

            byte address = Ddr5LocalDeviceAddress.Pmic(hubAddress);

            try
            {
                if (!smbus.ReadByteDataNoLock(address, REG_VENDOR_ID_0, out byte bank) ||
                    !smbus.ReadByteDataNoLock(address, REG_VENDOR_ID_1, out byte code) ||
                    code == 0x00 || code == 0xFF || bank == 0xFF)
                    return null;

                Ddr5Pmic pmic = Ddr5PmicFactory.Create(type, bank, code);
                pmic.I2cAddress = address;
                pmic.SpdHubAddress = hubAddress;
                return pmic.ReadNoLock(smbus) ? pmic : null;
            }
            catch (Exception ex)
            {
                Debug.WriteLine(string.Format("PMIC 0x{0:X2}: {1}", address, ex.Message));
                return null;
            }
        }
    }
}
