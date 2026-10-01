using System;
using System.Collections.Generic;
using System.Diagnostics;
using ZenStates.Core.Drivers;
using ZenStates.Core.OHWM;

namespace ZenStates.Core.Hardware.DRAM.DDR4.Spd
{
    /// <summary>
    /// Reads the SPD EEPROM (EE1004) of DDR4 modules over SMBus.
    ///
    /// The 512 bytes are split in two pages of 256. The page is selected for all modules at once with the
    /// SPA0 (0x36) and SPA1 (0x37) commands; nothing is ever written to the EEPROM array itself. The page
    /// is always set back to 0 afterwards, which is what the BIOS and other tools expect.
    /// </summary>
    public static class Ddr4SpdReader
    {
        private const int PAGE_SIZE = 256;
        private const byte SPD_ADDR_FIRST = 0x50;
        private const int SLOT_COUNT = 8;

        // EE1004 "set page address" commands, sent to these (otherwise unused) addresses
        private const byte SPA0 = 0x36;
        private const byte SPA1 = 0x37;

        private const byte SPD_DEVICE_TYPE = 2;

        /// <summary>Base configuration read at startup; the rest of page 0 is only read for a full SPD.</summary>
        private const int PARTIAL_PAGE0_LENGTH = 128;

        // SMBus ports the modules are looked for on, in order (see Ddr5SpdReader)
        private static readonly int[] Ports = new int[] { 0, 2 };

        /// <summary>True when SMBIOS reports DDR4 memory; the page commands are only sent then.</summary>
        internal static bool IsDdr4MemoryInstalled()
        {
            try
            {
                MemoryDevice[] devices = SMBiosSingleton.Instance.MemoryDevices;
                if (devices == null)
                    return false;

                for (int i = 0; i < devices.Length; i++)
                {
                    if (devices[i] == null || devices[i].Size == 0)
                        continue;

                    return MemoryConfig.SMBiosDramTypeToMemType(devices[i].Type) == MemType.DDR4;
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine(string.Format("IsDdr4MemoryInstalled: {0}", ex.Message));
            }

            return false;
        }

        // Page 0 has the device type (0x0C / 0x0E) at offset 2; the same offset of page 1 (byte 258) is reserved.
        private static bool IsOnPageNoLock(SmbusDriverBase smbus, byte addr7, int page)
        {
            if (!smbus.ReadByteDataNoLock(addr7, SPD_DEVICE_TYPE, out byte value))
                return false;

            bool page0 = Ddr4SpdDecoder.IsDdr4DeviceType(value);
            return page == 0 ? page0 : !page0;
        }

        /// <summary>
        /// Selects an SPD page on all modules and checks it on <paramref name="verifyAddr"/>.
        /// </summary>
        internal static bool SelectPageNoLock(SmbusDriverBase smbus, int page, byte verifyAddr)
        {
            if (IsOnPageNoLock(smbus, verifyAddr, page))
                return true;

            byte spa = page == 0 ? SPA0 : SPA1;

            // The EE1004 acknowledges its SPA address, but the data bytes are "don't care" and many don't
            // acknowledge them, so the transfer is reported as failed even when the page changed: check the page instead.
            smbus.WriteByteDataNoLock(spa, 0x00, 0x00);
            if (IsOnPageNoLock(smbus, verifyAddr, page))
                return true;

            // Some take the command without data
            smbus.SmbusQuickNoLock(spa, SmbusDriverBase.I2C_SMBUS_WRITE);
            return IsOnPageNoLock(smbus, verifyAddr, page);
        }

        /// <summary>
        /// Finds the DDR4 SPD EEPROMs. Tries the SMBus ports in order and stops at the first one with modules on it.
        /// Leaves the bus on that port with page 0 selected.
        /// </summary>
        internal static List<byte> ScanNoLock(SmbusDriverBase smbus)
        {
            for (int p = 0; p < Ports.Length; p++)
            {
                if (!smbus.ChangePortNoLock(Ports[p]))
                    continue;

                List<byte> responding = new List<byte>();
                for (int slot = 0; slot < SLOT_COUNT; slot++)
                {
                    byte addr = (byte)(SPD_ADDR_FIRST + slot);
                    if (smbus.ReadByteDataNoLock(addr, 0, out byte _))
                        responding.Add(addr);
                }

                if (responding.Count == 0)
                    continue;

                bool onPage0 = false;
                for (int i = 0; i < responding.Count && !onPage0; i++)
                    onPage0 = SelectPageNoLock(smbus, 0, responding[i]);

                if (!onPage0)
                {
                    Debug.WriteLine(string.Format("DDR4 SPD: could not select page 0 on port {0}.", Ports[p]));
                    continue;
                }

                List<byte> found = new List<byte>();
                for (int i = 0; i < responding.Count; i++)
                {
                    if (IsOnPageNoLock(smbus, responding[i], 0))
                        found.Add(responding[i]);
                }

                if (found.Count > 0)
                    return found;
            }

            return new List<byte>();
        }

        /// <summary>Reads <paramref name="count"/> bytes of the current page, two at a time where possible.</summary>
        private static bool ReadRangeNoLock(SmbusDriverBase smbus, byte addr7, byte[] dest, int destOffset, int pageOffset, int count)
        {
            int failures = 0;
            int i = 0;

            while (i < count)
            {
                int reg = pageOffset + i;

                if (i + 1 < count && smbus.ReadWordDataNoLock(addr7, (byte)reg, out ushort word))
                {
                    dest[destOffset + i] = (byte)(word & 0xFF);
                    dest[destOffset + i + 1] = (byte)(word >> 8);
                    i += 2;
                    continue;
                }

                if (smbus.ReadByteDataNoLock(addr7, (byte)reg, out byte value))
                {
                    dest[destOffset + i] = value;
                }
                else
                {
                    dest[destOffset + i] = 0xFF;
                    failures++;
                }

                i++;
            }

            if (failures > 0)
                Debug.WriteLine(string.Format("DDR4 SPD 0x{0:X2}: {1} byte(s) at page offset {2} could not be read.", addr7, failures, pageOffset));

            return failures == 0;
        }

        // CRC-protected blocks of page 0: bytes 0~125 with the CRC at 126, and 128~253 with the CRC at 254
        private const int BLOCK_SIZE = 128;
        private const int REREAD_PASSES = 2;

        private static bool BlockCrcValid(byte[] image, int start)
        {
            int crc = Ddr4SpdDecoder.Crc16(image, start, BLOCK_SIZE - 2);
            return crc == (image[start + BLOCK_SIZE - 2] | (image[start + BLOCK_SIZE - 1] << 8));
        }

        /// <summary>
        /// Reads a 128-byte block of page 0 again when its CRC does not match: twice more, a byte at a time, keeping for
        /// each byte the value two of the three reads agree on. SMBus reads of SPD EEPROMs are seen to return a wrong
        /// byte now and then, which would otherwise be decoded as is. A module whose CRC is really wrong reads the
        /// same and keeps its bytes. The page must be selected.
        /// </summary>
        private static void VerifyBlockNoLock(SmbusDriverBase smbus, byte addr7, byte[] image, int start)
        {
            if (BlockCrcValid(image, start))
                return;

            byte[][] passes = new byte[REREAD_PASSES][];
            for (int p = 0; p < REREAD_PASSES; p++)
            {
                passes[p] = new byte[BLOCK_SIZE];
                for (int i = 0; i < BLOCK_SIZE; i++)
                {
                    if (!smbus.ReadByteDataNoLock(addr7, (byte)(start + i), out passes[p][i]))
                        passes[p][i] = image[start + i];
                }
            }

            int changed = 0;
            for (int i = 0; i < BLOCK_SIZE; i++)
            {
                byte first = image[start + i], second = passes[0][i], third = passes[1][i];
                byte voted = first == second || first == third ? first : second == third ? second : first;
                if (voted != first)
                {
                    image[start + i] = voted;
                    changed++;
                }
            }

            Debug.WriteLine(string.Format("DDR4 SPD 0x{0:X2}: CRC of bytes {1}~{2} did not match; {3} byte(s) corrected on re-read, CRC {4}.",
                addr7, start, start + BLOCK_SIZE - 1, changed, BlockCrcValid(image, start) ? "now valid" : "still invalid"));
        }

        /// <summary>
        /// Reads and decodes the SPD of every module; restores page 0 and the previously selected port.
        /// </summary>
        /// <param name="full">All 512 bytes, or only the base configuration and manufacturing bytes.</param>
        internal static Dictionary<byte, Ddr4SpdInfo> ReadAllNoLock(SmbusDriverBase smbus, bool full)
        {
            Dictionary<byte, byte[]> images = new Dictionary<byte, byte[]>();
            Dictionary<byte, bool> page1Read = new Dictionary<byte, bool>();
            List<byte> addresses = new List<byte>();

            if (smbus == null || !smbus.ChangePortNoLock(-1, out int savedPort))
                return new Dictionary<byte, Ddr4SpdInfo>();

            try
            {
                addresses = ScanNoLock(smbus);

                for (int i = 0; i < addresses.Count; i++)
                {
                    byte[] image = new byte[Ddr4SpdDecoder.SPD_SIZE];
                    if (ReadRangeNoLock(smbus, addresses[i], image, 0, 0, full ? PAGE_SIZE : PARTIAL_PAGE0_LENGTH))
                    {
                        VerifyBlockNoLock(smbus, addresses[i], image, 0);
                        if (full)
                            VerifyBlockNoLock(smbus, addresses[i], image, BLOCK_SIZE);
                        images[addresses[i]] = image;
                    }
                }

                if (images.Count > 0 && SelectPageNoLock(smbus, 1, addresses[0]))
                {
                    foreach (KeyValuePair<byte, byte[]> entry in images)
                    {
                        page1Read[entry.Key] = full
                            ? ReadRangeNoLock(smbus, entry.Key, entry.Value, PAGE_SIZE, 0, PAGE_SIZE)
                            : ReadRangeNoLock(smbus, entry.Key, entry.Value, Ddr4SpdDecoder.MANUFACTURING_FIRST,
                                Ddr4SpdDecoder.MANUFACTURING_FIRST - PAGE_SIZE, Ddr4SpdDecoder.MANUFACTURING_LENGTH);
                    }
                }
            }
            finally
            {
                RestorePage0NoLock(smbus, addresses);

                if (savedPort >= 0)
                    smbus.ChangePortNoLock(savedPort);
            }

            Dictionary<byte, Ddr4SpdInfo> result = new Dictionary<byte, Ddr4SpdInfo>();
            foreach (KeyValuePair<byte, byte[]> entry in images)
            {
                bool page1 = page1Read.TryGetValue(entry.Key, out bool ok) && ok;
                if (full && !page1)
                    Debug.WriteLine(string.Format("DDR4 SPD 0x{0:X2}: page 1 was not read, the SPD is partial.", entry.Key));

                result[entry.Key] = Ddr4SpdDecoder.Decode(entry.Value, !full || !page1);
            }

            return result;
        }

        // Best effort, never throws: page 0 is what the BIOS and other software expect.
        private static void RestorePage0NoLock(SmbusDriverBase smbus, List<byte> addresses)
        {
            if (addresses == null || addresses.Count == 0)
                return;

            try
            {
                if (!SelectPageNoLock(smbus, 0, addresses[0]))
                    Debug.WriteLine("DDR4 SPD: failed to restore page 0.");
            }
            catch (Exception ex)
            {
                Debug.WriteLine(string.Format("DDR4 SPD: error restoring page 0: {0}", ex.Message));
            }
        }

        private static Dictionary<byte, Ddr4SpdInfo> ReadAllLocked(bool full)
        {
            Dictionary<byte, Ddr4SpdInfo> empty = new Dictionary<byte, Ddr4SpdInfo>();

            if (!IsDdr4MemoryInstalled())
                return empty;

            if (!Mutexes.WaitSmbus(5000))
            {
                Debug.WriteLine("DDR4 SPD: timeout waiting for the SMBus mutex.");
                return empty;
            }

            try
            {
                return ReadAllNoLock(SmbusProvider.Instance, full);
            }
            catch (Exception ex)
            {
                Debug.WriteLine(string.Format("DDR4 SPD: {0}", ex.Message));
                return empty;
            }
            finally
            {
                Mutexes.ReleaseSmbus();
            }
        }

        /// <summary>Reads and decodes the full SPD of all DDR4 modules, keyed by SPD address in module order.</summary>
        public static Dictionary<byte, Ddr4SpdInfo> ReadAll()
        {
            return ReadAllLocked(true);
        }

        /// <summary>
        /// Reads only what is needed at startup (identity, organisation, JEDEC timings), a fraction of the
        /// SMBus traffic of <see cref="ReadAll"/>. The entries have <see cref="Ddr4SpdInfo.IsPartial"/> set.
        /// </summary>
        public static Dictionary<byte, Ddr4SpdInfo> ReadInitInfoAll()
        {
            return ReadAllLocked(false);
        }

        /// <summary>Writes the 512-byte SPD of the module at <paramref name="addr7"/> to a file.</summary>
        public static bool DumpToFile(byte addr7, string filePath)
        {
            try
            {
                Dictionary<byte, Ddr4SpdInfo> all = ReadAll();
                if (!all.TryGetValue(addr7, out Ddr4SpdInfo info) || info == null)
                    throw new InvalidOperationException(string.Format("Could not read SPD from DIMM at address 0x{0:X2}.", addr7));
                if (info.IsPartial)
                    throw new InvalidOperationException(string.Format("Could not read the whole SPD of DIMM at address 0x{0:X2}.", addr7));

                string dir = System.IO.Path.GetDirectoryName(filePath);
                if (!string.IsNullOrEmpty(dir) && !System.IO.Directory.Exists(dir))
                    System.IO.Directory.CreateDirectory(dir);

                System.IO.File.WriteAllBytes(filePath, info.RawSpd);
                return true;
            }
            catch (Exception ex)
            {
                Debug.WriteLine(string.Format("Error dumping DDR4 SPD to file: {0}", ex.Message));
                return false;
            }
        }
    }
}
