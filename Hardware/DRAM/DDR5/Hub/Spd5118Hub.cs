using System;
using System.Collections.Generic;
using System.Diagnostics;
using ZenStates.Core.Drivers;
using static ZenStates.Core.Hardware.DRAM.DDR5.Hub.Spd5118Registers;

namespace ZenStates.Core.Hardware.DRAM.DDR5.Hub
{
    /// <summary>
    /// SMBus access to SPD5118 hubs (JESD300-5): finding them, reading their registers and reading their
    /// non-volatile memory (the SPD) page by page. Used by DDR5 and LPDDR5/5X modules alike.
    ///
    /// The only register ever written is the page pointer MR11[2:0], and only on a device identified as a hub that
    /// uses 1-byte addressing. Each NVM read puts the hub back on page 0 when it is done:
    /// that is what the BIOS and other software expect, and the volatile registers (the thermal sensor) are only
    /// reachable on page 0.
    ///
    /// All methods expect the SMBus mutex to be held.
    /// </summary>
    public static class Spd5118Hub
    {
        // SMBus ports the hubs are looked for on, in order: the board SMBus, then the DIMM port (see Ddr4SpdReader)
        private static readonly int[] Ports = new int[] { 0, 2 };

        // Port the hubs were last found on, -1 when not scanned yet. Only accessed with the SMBus mutex held.
        private static int hubPort = -1;

        /// <summary>SMBus port the hubs were last found on, or -1 when not scanned yet.</summary>
        public static int HubPort
        {
            get { return hubPort; }
        }

        /// <summary>
        /// Identifies a hub by its device type (MR0, MR1). A hub left on a nonzero page by other software is put back
        /// on page 0 (see <see cref="TryRecoverPage0NoLock"/>); otherwise read-only.
        /// </summary>
        internal static bool IsHubNoLock(SmbusDriverBase smbus, byte addr7)
        {
            return ReadDeviceTypeNoLock(smbus, addr7, out _, out _);
        }

        /// <summary>
        /// Reads MR0 / MR1 and tells whether they identify a hub, recovering page 0 first when needed. The values given
        /// back are the ones read after recovery.
        /// </summary>
        private static bool ReadDeviceTypeNoLock(SmbusDriverBase smbus, byte addr7, out byte mr0, out byte mr1)
        {
            mr0 = 0;
            mr1 = 0;
            if (smbus == null || !IsHubAddress(addr7))
                return false;

            try
            {
                if (!smbus.ReadByteDataNoLock(addr7, MR0_DEVICE_TYPE_MSB, out mr0) ||
                    !smbus.ReadByteDataNoLock(addr7, MR1_DEVICE_TYPE_LSB, out mr1))
                    return false;

                if (IsHubDeviceType(mr0, mr1))
                    return true;

                return mr0 == 0 && mr1 == 0 && TryRecoverPage0NoLock(smbus, addr7, out mr0, out mr1);
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// Same as the Linux spd5118 driver: when the device type reads 0, the chip may have a nonzero page selected
        /// and hide the volatile registers besides MR11 (Renesas/ITD hubs). Requires the vendor ID to read 0 too and
        /// MR11 to hold only the addressing and page bits with a nonzero page. Selects page 0 and checks the device
        /// type again; restores the original MR11 value when it is still not a hub.
        /// </summary>
        private static bool TryRecoverPage0NoLock(SmbusDriverBase smbus, byte addr7, out byte mr0, out byte mr1)
        {
            mr0 = 0;
            mr1 = 0;
            if (!smbus.ReadByteDataNoLock(addr7, MR3_VENDOR_ID_0, out byte mr3) ||
                !smbus.ReadByteDataNoLock(addr7, MR4_VENDOR_ID_1, out byte mr4) ||
                mr3 != 0 || mr4 != 0)
                return false;

            if (!smbus.ReadByteDataNoLock(addr7, MR11_LEGACY_MODE, out byte mode) ||
                (mode & ~(MR11_TWO_BYTE_ADDRESSING | MR11_PAGE_MASK)) != 0 ||
                (mode & MR11_PAGE_MASK) == 0)
                return false;

            if (!smbus.WriteByteDataNoLock(addr7, MR11_LEGACY_MODE, (byte)(mode & MR11_TWO_BYTE_ADDRESSING)))
                return false;

            if (smbus.ReadByteDataNoLock(addr7, MR0_DEVICE_TYPE_MSB, out mr0) &&
                smbus.ReadByteDataNoLock(addr7, MR1_DEVICE_TYPE_LSB, out mr1) &&
                IsHubDeviceType(mr0, mr1))
            {
                Debug.WriteLine(string.Format("SPD5118 0x{0:X2}: hub was left on page {1}, restored page 0.", addr7, mode & MR11_PAGE_MASK));
                return true;
            }

            smbus.WriteByteDataNoLock(addr7, MR11_LEGACY_MODE, mode);
            return false;
        }

        /// <summary>
        /// Finds the hubs. Tries the ports in order and stops at the first one with hubs on it; the bus is left on that
        /// port and it is remembered in <see cref="HubPort"/>. Read-only.
        /// </summary>
        internal static List<byte> ScanNoLock(SmbusDriverBase smbus)
        {
            for (int p = 0; p < Ports.Length; p++)
            {
                if (!smbus.ChangePortNoLock(Ports[p]))
                    continue;

                List<byte> found = new List<byte>();
                for (int addr = ADDRESS_FIRST; addr <= ADDRESS_LAST; addr++)
                {
                    if (IsHubNoLock(smbus, (byte)addr))
                        found.Add((byte)addr);
                }

                if (found.Count > 0)
                {
                    hubPort = Ports[p];
                    return found;
                }
            }

            hubPort = -1;
            return new List<byte>();
        }

        /// <summary>
        /// Switches the bus to the port the hubs were found on. When it is not known yet and <paramref name="scanIfUnknown"/>
        /// is set, scans first. The caller restores the previous port.
        /// </summary>
        internal static bool SelectHubPortNoLock(SmbusDriverBase smbus, bool scanIfUnknown)
        {
            if (hubPort >= 0)
                return smbus.ChangePortNoLock(hubPort);

            return scanIfUnknown && ScanNoLock(smbus).Count > 0;
        }

        /// <summary>Reads the identification and configuration registers. Null when the device is not a hub.</summary>
        internal static Spd5118HubInfo ReadInfoNoLock(SmbusDriverBase smbus, byte addr7)
        {
            try
            {
                if (!ReadDeviceTypeNoLock(smbus, addr7, out byte mr0, out byte mr1))
                    return null;

                Spd5118HubInfo info = new Spd5118HubInfo
                {
                    Address = addr7,
                    DeviceTypeMsb = mr0,
                    DeviceTypeLsb = mr1,
                };

                if (smbus.ReadByteDataNoLock(addr7, MR2_REVISION, out byte mr2))
                    info.Revision = string.Format("{0}.{1}", ((mr2 >> 4) & 0x03) + 1, (mr2 >> 1) & 0x07);

                if (smbus.ReadByteDataNoLock(addr7, MR3_VENDOR_ID_0, out byte mr3) &&
                    smbus.ReadByteDataNoLock(addr7, MR4_VENDOR_ID_1, out byte mr4))
                {
                    info.VendorIdBank = mr3;
                    info.VendorIdCode = mr4;
                    info.Vendor = ManufacturerMapping.Lookup(mr3, mr4);
                }

                if (smbus.ReadByteDataNoLock(addr7, MR5_CAPABILITY, out byte mr5))
                {
                    info.TempSensorSupported = (mr5 & MR5_TS_SUPPORT) != 0;
                    info.HubSupported = (mr5 & MR5_HUB_SUPPORT) != 0;
                }

                if (smbus.ReadByteDataNoLock(addr7, MR11_LEGACY_MODE, out byte mr11))
                    info.TwoByteAddressing = (mr11 & MR11_TWO_BYTE_ADDRESSING) != 0;

                if (smbus.ReadByteDataNoLock(addr7, MR12_WRITE_PROTECT_0, out byte mr12) &&
                    smbus.ReadByteDataNoLock(addr7, MR13_WRITE_PROTECT_1, out byte mr13))
                    info.WriteProtectedBlocks = (mr13 << 8) | mr12;

                if (smbus.ReadByteDataNoLock(addr7, MR18_DEVICE_CONFIG, out byte mr18))
                    info.IsI3c = (mr18 & MR18_I3C) != 0;

                return info;
            }
            catch (Exception ex)
            {
                Debug.WriteLine(string.Format("SPD5118 0x{0:X2}: {1}", addr7, ex.Message));
                return null;
            }
        }

        /// <summary>
        /// Identifies the hub (MR0, MR1) and checks that it uses 1-byte addressing (MR11 [3] = 0), the only mode the
        /// SMBus byte and word transactions used here can address. Gives the page currently selected. Read-only.
        /// </summary>
        private static bool CanPageNoLock(SmbusDriverBase smbus, byte addr7, out int currentPage)
        {
            currentPage = -1;
            if (!IsHubNoLock(smbus, addr7) || !smbus.ReadByteDataNoLock(addr7, MR11_LEGACY_MODE, out byte mr11))
                return false;

            if ((mr11 & MR11_TWO_BYTE_ADDRESSING) != 0)
            {
                Debug.WriteLine(string.Format("SPD5118 0x{0:X2}: 2-byte addressing mode, the SPD is not read.", addr7));
                return false;
            }

            currentPage = mr11 & MR11_PAGE_MASK;
            return true;
        }

        /// <summary>Writes the page pointer and reads it back. Only on a device <see cref="CanPageNoLock"/> accepted.</summary>
        private static bool WritePageNoLock(SmbusDriverBase smbus, byte addr7, int page)
        {
            if (!smbus.ReadByteDataNoLock(addr7, MR11_LEGACY_MODE, out byte mr11) ||
                (mr11 & MR11_TWO_BYTE_ADDRESSING) != 0)
                return false;

            if ((mr11 & MR11_PAGE_MASK) == page)
                return true;

            byte value = (byte)((mr11 & ~MR11_PAGE_MASK) | page);
            if (!smbus.WriteByteDataNoLock(addr7, MR11_LEGACY_MODE, value))
                return false;

            return smbus.ReadByteDataNoLock(addr7, MR11_LEGACY_MODE, out byte verify) &&
                   (verify & (MR11_PAGE_MASK | MR11_TWO_BYTE_ADDRESSING)) ==
                   (value & (MR11_PAGE_MASK | MR11_TWO_BYTE_ADDRESSING));
        }

        /// <summary>Best effort, never throws: puts a previously identified hub back on page 0.</summary>
        private static void RestorePage0NoLock(SmbusDriverBase smbus, byte addr7)
        {
            try
            {
                // Renesas hubs hide MR0/MR1 on nonzero pages; only MR11 can be used for cleanup.
                if (!WritePageNoLock(smbus, addr7, 0))
                    Debug.WriteLine(string.Format("SPD5118 0x{0:X2}: failed to restore page 0.", addr7));
            }
            catch (Exception ex)
            {
                Debug.WriteLine(string.Format("SPD5118 0x{0:X2}: error restoring page 0: {1}", addr7, ex.Message));
            }
        }

        /// <summary>
        /// Reads <paramref name="count"/> NVM bytes starting at <paramref name="nvmOffset"/> into the same positions of
        /// <paramref name="dest"/>, switching pages as needed. Bytes that can't be read are set to 0xFF. Restores page 0
        /// after every range, including failures and exceptions, so later reads can identify the hub again.
        /// </summary>
        /// <returns>False when a page could not be selected or a byte could not be read.</returns>
        internal static bool ReadNvmNoLock(SmbusDriverBase smbus, byte addr7, byte[] dest, int nvmOffset, int count)
        {
            if (dest == null || nvmOffset < 0 || count <= 0 || nvmOffset + count > NVM_SIZE || nvmOffset + count > dest.Length)
                return false;

            // The hub is identified once, before the first page write, rather than on every page change
            if (!CanPageNoLock(smbus, addr7, out int currentPage))
                return false;

            int failures = 0;
            int offset = nvmOffset;
            int end = nvmOffset + count;

            try
            {
                while (offset < end)
                {
                    int page = NvmPage(offset);
                    if (page != currentPage)
                    {
                        if (!WritePageNoLock(smbus, addr7, page))
                        {
                            Debug.WriteLine(string.Format("SPD5118 0x{0:X2}: failed to select page {1}.", addr7, page));
                            return false;
                        }
                        currentPage = page;
                    }

                    int pageEnd = Math.Min(end, (page + 1) * PAGE_SIZE);
                    while (offset < pageEnd)
                    {
                        byte reg = NvmRegister(offset);

                        // Word reads only from an even offset, so a read never crosses a page or 16-byte block boundary
                        if ((offset & 1) == 0 && offset + 1 < pageEnd && smbus.ReadWordDataNoLock(addr7, reg, out ushort word))
                        {
                            dest[offset] = (byte)(word & 0xFF);
                            dest[offset + 1] = (byte)(word >> 8);
                            offset += 2;
                            continue;
                        }

                        if (smbus.ReadByteDataNoLock(addr7, reg, out byte value))
                        {
                            dest[offset] = value;
                        }
                        else
                        {
                            dest[offset] = 0xFF;
                            failures++;
                        }

                        offset++;
                    }
                }
            }
            finally
            {
                RestorePage0NoLock(smbus, addr7);
            }

            if (failures > 0)
                Debug.WriteLine(string.Format("SPD5118 0x{0:X2}: {1} byte(s) from offset {2} could not be read.", addr7, failures, nvmOffset));

            return failures == 0;
        }
    }
}
