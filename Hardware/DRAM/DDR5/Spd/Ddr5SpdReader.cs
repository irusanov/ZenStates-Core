using System;
using System.Collections.Generic;
using System.Diagnostics;
using ZenStates.Core.Drivers;
using ZenStates.Core.Hardware.DRAM.DDR5.Pmic;
using ZenStates.Core.Hardware.DRAM.DDR5.Thermal;
using ZenStates.Core.OHWM;

namespace ZenStates.Core.Hardware.DRAM.DDR5.Spd
{
    public static class Ddr5SpdReader
    {
        private static SmbusDriverBase smbusDriver => SmbusProvider.Instance;
        private const int PAGE_SIZE = 128; // in bytes, for DDR5 SPD
        private const int SPD_TOTAL_SIZE = 0x400; // 1024 bytes total (8 pages of 128 bytes)

        // Port constants
        // port_to_reg[] = [0b00, 0b00, 0b01, 0b10, 0b11]
        // Port 0 = board SMBus (primary, 0x0B00)
        // Port 1 = ASF / aux   (secondary, 0x0B20)
        // Port 2 = DDR5 / TSI  (primary port 2, KernCZ reg 0b01)
        // Ports 3,4 = reserved
        public const int PORT_BOARD = 0;
        public const int PORT_ASF = 1;
        public const int PORT_DIMM = 2;

        // DDR5 SPD5118 hub 7-bit I2C addresses (0x50–0x57)
        // Module takes 7-bit address and does (addr << 1) | rw internally.
        private const int SPD_HUB_ADDR_FIRST = 0x50;
        private const int SPD_HUB_ADDR_LAST = 0x57;

        // SPD5118 hub: 1024 bytes in 8 pages of 128 bytes.
        // Page selected by writing page number to register 0x0B.
        // 7-bit addresses: 0x50–0x57 (DIMM 0–7).
        internal static int SpdCalculatePage(int offset) { return offset / PAGE_SIZE; }
        internal static int SpdCalculateReg(int offset) { return 0x80 + (offset % PAGE_SIZE); }

        // SPD5118 mode registers used for identification / paging
        private const byte SPD5118_MR0 = 0x00;   // device type MSB (0x51)
        private const byte SPD5118_MR1 = 0x01;   // device type LSB (0x18)
        private const byte SPD5118_MR11 = 0x0B;  // I2C legacy mode / page select

        // SMBus port on which the SPD hubs were last found (-1 = unknown).
        // Only accessed while the SMBus mutex is held.
        private static int hubPort = -1;

        /// <summary>SMBus port the SPD5118 hubs were last found on, or -1 if not scanned yet.</summary>
        internal static int HubPort { get { return hubPort; } }

        /// <summary>
        /// Identify an SPD5118 hub by its device type in MR0/MR1 (JESD300-5: 0x51, 0x18).
        /// Read-only; performs no writes. Uses the same (byte-order tolerant) check as the
        /// thermal sensor detection.
        /// </summary>
        internal static bool IsSpd5118HubNoLock(byte addr7)
        {
            if (addr7 < SPD_HUB_ADDR_FIRST || addr7 > SPD_HUB_ADDR_LAST)
                return false;

            try
            {
                if (!smbusDriver.ReadByteDataNoLock(addr7, SPD5118_MR0, out byte mr0))
                    return false;
                if (!smbusDriver.ReadByteDataNoLock(addr7, SPD5118_MR1, out byte mr1))
                    return false;
                return Ddr5ThermalSensor.IsSpd5118DeviceType(mr0, mr1);
            }
            catch
            {
                return false;
            }
        }

        internal static bool SpdSwitchPage(byte addr7, byte page)
        {
            // Never write MR11 on a device that has not been identified as an SPD5118 hub.
            if (!IsSpd5118HubNoLock(addr7))
                return false;

            // JESD406 MR11: bit[3] = I2C addressing mode (set by BIOS, must be preserved),
            // bits[2:0] = page select. Read-modify-write to avoid clearing the addressing mode bit.
            if (!smbusDriver.ReadByteDataNoLock(addr7, SPD5118_MR11, out byte mr11))
                return false;
            if (!smbusDriver.WriteByteDataNoLock(addr7, SPD5118_MR11, (byte)((mr11 & 0x08) | (page & 0x07))))
                return false;

            // Verify the page actually switched, so callers never decode bytes from the wrong page.
            if (!smbusDriver.ReadByteDataNoLock(addr7, SPD5118_MR11, out byte verify))
                return false;
            return (verify & 0x07) == (page & 0x07);
        }

        /// <summary>
        /// Best-effort return of the hub to page 0. Never throws; safe to call from finally blocks.
        /// </summary>
        private static void RestorePage0NoLock(byte addr7)
        {
            try
            {
                if (!SpdSwitchPage(addr7, 0))
                    Debug.WriteLine(string.Format("SPD hub 0x{0:X2}: failed to restore page 0.", addr7));
            }
            catch (Exception ex)
            {
                Debug.WriteLine(string.Format("SPD hub 0x{0:X2}: error restoring page 0: {1}", addr7, ex.Message));
            }
        }

        /// <summary>
        /// Scan for DDR5 SPD hubs. Tries port 0 (BOARD) first, then port 2 (DIMM).
        /// Only addresses identified as SPD5118 hubs (MR0/MR1) are returned; the scan is read-only.
        /// Leaves the bus on the port the hubs were found on and remembers it in <see cref="HubPort"/>.
        /// </summary>
        internal static List<byte> ScanDdr5SpdHubsNoLock()
        {
            int[] ports = new int[] { PORT_BOARD, PORT_DIMM };

            for (int p = 0; p < ports.Length; p++)
            {
                if (!smbusDriver.ChangePortNoLock(ports[p]))
                    continue;

                List<byte> found = new List<byte>();

                for (int i = SPD_HUB_ADDR_FIRST; i <= SPD_HUB_ADDR_LAST; i++)
                {
                    if (IsSpd5118HubNoLock((byte)i))
                        found.Add((byte)i);
                }

                if (found.Count > 0)
                {
                    hubPort = ports[p];
                    return found;
                }
            }

            hubPort = -1;
            return new List<byte>();
        }

        /// <summary>
        /// Switch the bus to the port the SPD hubs live on. If the port is not known yet and
        /// <paramref name="scanIfUnknown"/> is true, a (read-only) hub scan is done first.
        /// The caller is responsible for restoring the previous port.
        /// </summary>
        internal static bool SelectHubPortNoLock(bool scanIfUnknown)
        {
            if (hubPort < 0)
            {
                if (!scanIfUnknown)
                    return false;

                // The scan leaves the bus on the port where hubs were found.
                return ScanDdr5SpdHubsNoLock().Count > 0;
            }

            return smbusDriver.ChangePortNoLock(hubPort);
        }

        /// <summary>
        /// True when SMBIOS reports DDR5 or LPDDR5 memory (same source/logic as <see cref="MemoryConfig"/>).
        /// SPD5118 hub access is refused on any other memory type.
        /// </summary>
        internal static bool IsDdr5MemoryInstalled()
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

                    MemType type = MemoryConfig.SMBiosDramTypeToMemType(devices[i].Type);
                    return type == MemType.DDR5 || type == MemType.LPDDR5;
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine(string.Format("IsDdr5MemoryInstalled: {0}", ex.Message));
            }

            return false;
        }

        // Read full SPD info without Mutex lock. Returns null if the address is not an
        // SPD5118 hub or a page switch fails. The caller must have selected the hub port.
        internal static Ddr5SpdInfo ReadDdr5SpdNoLock(byte addr7)
        {
            if (!IsSpd5118HubNoLock(addr7))
                return null;

            List<byte> spd = new List<byte>(SPD_TOTAL_SIZE);
            int prevPage = -1;
            int offset = 0;

            try
            {
                while (offset < SPD_TOTAL_SIZE)
                {
                    int page = SpdCalculatePage(offset);
                    int reg = SpdCalculateReg(offset);
                    int regOffset = offset % PAGE_SIZE;

                    if (page != prevPage)
                    {
                        // Stop rather than decode bytes from the wrong page.
                        if (!SpdSwitchPage(addr7, (byte)page))
                        {
                            Debug.WriteLine(string.Format("SPD hub 0x{0:X2}: failed to switch to page {1}.", addr7, page));
                            return null;
                        }
                        prevPage = page;
                    }

                    // Stay within the 128-byte SPD page window
                    if (regOffset <= PAGE_SIZE - 2)
                    {
                        ushort word;
                        if (smbusDriver.ReadWordDataNoLock(addr7, (byte)reg, out word))
                        {
                            spd.Add((byte)(word & 0xFF));
                            spd.Add((byte)((word >> 8) & 0xFF));
                            offset += 2;
                            continue;
                        }
                    }

                    if (!smbusDriver.ReadByteDataNoLock(addr7, (byte)reg, out byte b))
                        b = 0xFF;

                    spd.Add(b);
                    offset++;
                }
            }
            finally
            {
                RestorePage0NoLock(addr7);
            }

            return Ddr5SpdDecoder.Decode(spd);
        }

        internal static Dictionary<byte, Ddr5SpdInfo> ReadDdr5SpdAllNoLock()
        {
            return ReadDdr5SpdAllNoLock(false);
        }

        // Scans for hubs, reads full SPD of each (and optionally the live thermal/PMIC data)
        // while switched to the hub port, then restores the previously selected port.
        internal static Dictionary<byte, Ddr5SpdInfo> ReadDdr5SpdAllNoLock(bool readLiveDevices)
        {
            smbusDriver.ChangePortNoLock(-1, out int savedPort);

            try
            {
                Dictionary<byte, Ddr5SpdInfo> list = new Dictionary<byte, Ddr5SpdInfo>();
                List<byte> addresses = ScanDdr5SpdHubsNoLock();

                if (addresses.Count == 0)
                    throw new InvalidOperationException("No DDR5 DIMMs found on any SMBus port.");

                for (int i = 0; i < addresses.Count; i++)
                {
                    Ddr5SpdInfo info = ReadDdr5SpdNoLock(addresses[i]);
                    if (info == null)
                        continue;

                    if (readLiveDevices)
                        ReadLiveDevicesNoLock(addresses[i], info, smbusDriver);

                    list.Add(addresses[i], info);
                }

                return list;
            }
            finally
            {
                if (savedPort >= 0)
                    smbusDriver.ChangePortNoLock(savedPort);
            }
        }

        // Read minimal SPD info without Mutex lock
        internal static Ddr5SpdInfo ReadDdr5SpdInitInfoNoLock(byte addr7)
        {
            // Identify the hub before any MR11 (page select) write.
            if (!IsSpd5118HubNoLock(addr7))
                return null;

            Ddr5SpdInfo info = new Ddr5SpdInfo();

            byte b0, b1;
            ushort w;

            try
            {
                if (!SpdSwitchPage(addr7, 0))
                    return null;

                if (!smbusDriver.ReadByteDataNoLock(addr7, (byte)SpdCalculateReg(2), out b0))
                    return null;

                info.DeviceType = b0;
                info.IsValid = (info.DeviceType == 0x12 || info.DeviceType == 0x13);
                if (!info.IsValid)
                    return null;
                info.IsLpddr5 = (info.DeviceType == 0x13);

                // Byte 4: density and die count
                if (smbusDriver.ReadByteDataNoLock(addr7, (byte)SpdCalculateReg(4), out b0))
                {
                    info.FirstDieDensityMbit = Ddr5SpdDecoder.DecodeDieDensity(b0 & 0x1F);
                    info.FirstDieCount = Ddr5SpdDecoder.DecodeDieCount((b0 >> 5) & 0x07);
                }

                // Byte 6: device width
                if (smbusDriver.ReadByteDataNoLock(addr7, (byte)SpdCalculateReg(6), out b0))
                {
                    int widthCode = (b0 >> 5) & 0x07;
                    switch (widthCode)
                    {
                        case 0: info.FirstDeviceWidthBits = 4; break;
                        case 1: info.FirstDeviceWidthBits = 8; break;
                        case 2: info.FirstDeviceWidthBits = 16; break;
                        case 3: info.FirstDeviceWidthBits = 32; break;
                        default: info.FirstDeviceWidthBits = 8; break;
                    }
                }

                // -------------------------------------------------
                // Page 1: module organization (bytes 234-235)
                // -------------------------------------------------
                if (SpdSwitchPage(addr7, 1))
                {
                    // Byte 234: ranks per channel
                    if (smbusDriver.ReadByteDataNoLock(addr7, (byte)SpdCalculateReg(234), out b0))
                    {
                        int rankCode = (b0 >> 3) & 0x07;
                        info.RanksPerChannel = rankCode + 1;
                    }

                    // Byte 235: bus width and sub-channels
                    if (smbusDriver.ReadByteDataNoLock(addr7, (byte)SpdCalculateReg(235), out b0))
                    {
                        int subChBit = (b0 >> 5) & 0x01;
                        info.SubChannelsPerDimm = (subChBit == 1) ? 2 : 1;

                        int busCode = b0 & 0x07;
                        switch (busCode)
                        {
                            case 0x00: info.PrimaryBusWidthBits = 8; break;
                            case 0x01: info.PrimaryBusWidthBits = 16; break;
                            case 0x02: info.PrimaryBusWidthBits = 32; break;
                            case 0x03: info.PrimaryBusWidthBits = 64; break;
                            default: info.PrimaryBusWidthBits = 32; break;
                        }

                        info.ChannelCount = info.SubChannelsPerDimm;
                    }
                }

                // Calculate capacity from the gathered fields
                if (info.FirstDieDensityMbit > 0 && info.FirstDeviceWidthBits > 0 && info.PrimaryBusWidthBits > 0)
                {
                    long densityMB = (long)info.FirstDieDensityMbit / 8;
                    int devicesPerSubCh = info.PrimaryBusWidthBits / info.FirstDeviceWidthBits;
                    info.TotalCapacityMB = densityMB
                        * info.FirstDieCount
                        * devicesPerSubCh
                        * info.RanksPerChannel
                        * info.SubChannelsPerDimm;
                }

                // -------------------------------------------------
                // Page 4: module manufacturer / part number / DRAM manufacturer
                // -------------------------------------------------
                if (!SpdSwitchPage(addr7, 4))
                    return null;

                // Module manufacturer: bytes 512-513
                if (smbusDriver.ReadWordDataNoLock(addr7, (byte)SpdCalculateReg(512), out w))
                {
                    b0 = (byte)(w & 0xFF);
                    b1 = (byte)((w >> 8) & 0xFF);
                    info.ModuleMfgIdBank = b0;
                    info.ModuleMfgIdMfr = b1;
                    info.ModuleManufacturer = ManufacturerMapping.Lookup(info.ModuleMfgIdBank, info.ModuleMfgIdMfr);
                }

                // Module part number: bytes 521-550 (30 bytes, read as 15 words)
                {
                    var partno = new System.Text.StringBuilder();
                    for (int i = 0; i < 30; i += 2)
                    {
                        if (smbusDriver.ReadWordDataNoLock(addr7, (byte)SpdCalculateReg(521 + i), out w))
                        {
                            byte lo = (byte)(w & 0xFF);
                            byte hi = (byte)((w >> 8) & 0xFF);
                            if (lo >= 0x20 && lo <= 0x7E) partno.Append((char)lo);
                            if (hi >= 0x20 && hi <= 0x7E) partno.Append((char)hi);
                        }
                    }
                    info.ModulePartNumber = partno.ToString().Trim();
                }

                // DRAM manufacturer: bytes 552-553, stepping: byte 554
                if (smbusDriver.ReadWordDataNoLock(addr7, (byte)SpdCalculateReg(552), out w))
                {
                    info.DramMfgIdBank = (byte)(w & 0xFF);
                    info.DramMfgIdMfr = (byte)((w >> 8) & 0xFF);
                    info.DramManufacturer = ManufacturerMapping.Lookup(info.DramMfgIdBank, info.DramMfgIdMfr);
                }

                if (smbusDriver.ReadByteDataNoLock(addr7, (byte)SpdCalculateReg(554), out b0))
                {
                    info.DramStepping = b0;
                }

                info.IsPartial = true;
            }
            finally
            {
                // Always return the hub to page 0, including early returns and exceptions.
                RestorePage0NoLock(addr7);
            }

            ReadLiveDevicesNoLock(addr7, info, smbusDriver);

            return info;
        }

        internal static Dictionary<byte, Ddr5SpdInfo> ReadDdr5SpdInitInfoAllNoLock()
        {
            smbusDriver.ChangePortNoLock(-1, out int savedPort);

            try
            {
                Dictionary<byte, Ddr5SpdInfo> result = new Dictionary<byte, Ddr5SpdInfo>();

                List<byte> addresses = ScanDdr5SpdHubsNoLock();
                for (int i = 0; i < addresses.Count; i++)
                {
                    byte addr = addresses[i];
                    Ddr5SpdInfo info = ReadDdr5SpdInitInfoNoLock(addr);
                    if (info != null)
                        result.Add(addr, info);
                }

                return result;
            }
            finally
            {
                if (savedPort >= 0)
                    smbusDriver.ChangePortNoLock(savedPort);
            }
        }

        internal static void ReadThermalNoLock(byte addr7, Ddr5SpdInfo info, SmbusDriverBase smbus)
        {
            if (info == null || smbus == null || info.IsLpddr5)
                return;

            try
            {
                if (Ddr5ThermalSensor.DetectNoLock(smbus, addr7))
                    info.ThermalData = Ddr5ThermalSensor.ReadAllNoLock(smbus, addr7);
            }
            catch
            {
                // Thermal sensor not accessible - not critical
            }
        }

        internal static void ReadPmicNoLock(byte addr7, Ddr5SpdInfo info, SmbusDriverBase smbus)
        {
            if (info == null || smbus == null || info.IsLpddr5)
                return;

            try
            {
                byte pmicAddr = Ddr5PmicReader.CalculatePmicAddrFromSpd(addr7);

                if (Ddr5PmicReader.DetectNoLock(smbus, pmicAddr))
                    info.PmicData = Ddr5PmicReader.ReadPmicNoLock(smbus, pmicAddr);
            }
            catch
            {
                // PMIC not accessible - not critical
            }
        }

        internal static void ReadLiveDevicesNoLock(byte addr7, Ddr5SpdInfo info, SmbusDriverBase smbus)
        {
            if (info == null || smbus == null)
                return;

            ReadThermalNoLock(addr7, info, smbus);
            ReadPmicNoLock(addr7, info, smbus);
        }

        // Public methods with Mutex lock
        // Read single DDR5 SPD full
        public static Ddr5SpdInfo ReadDdr5Spd(byte addr7)
        {
            if (!IsDdr5MemoryInstalled())
                return null;

            if (!Mutexes.WaitSmbus(5000))
                return null;
            try
            {
                smbusDriver.ChangePortNoLock(-1, out int savedPort);
                try
                {
                    // Read on the port the hubs live on, not whatever port is currently selected.
                    if (!SelectHubPortNoLock(true))
                        return null;

                    return ReadDdr5SpdNoLock(addr7);
                }
                finally
                {
                    if (savedPort >= 0)
                        smbusDriver.ChangePortNoLock(savedPort);
                }
            }
            finally
            {
                Mutexes.ReleaseSmbus();
            }
        }

        // Read single DDR5 SPD minimal
        //public static Ddr5SpdInfo ReadDdr5SpdInitInfo(byte addr7)
        //{
        //    if (!Mutexes.WaitSmbus(5000))
        //        return null;

        //    try
        //    {
        //        return ReadDdr5SpdInitInfoNoLock(addr7);
        //    }
        //    finally
        //    {
        //        Mutexes.ReleaseSmbus();
        //    }
        //}

        // Read all SPD
        public static Dictionary<byte, Ddr5SpdInfo> ReadDdr5SpdAll()
        {
            Dictionary<byte, Ddr5SpdInfo> list = new Dictionary<byte, Ddr5SpdInfo>();

            if (!IsDdr5MemoryInstalled())
                return list;

            if (!Mutexes.WaitSmbus(5000))
                return list;

            try
            {
                list = ReadDdr5SpdAllNoLock();
            }
            finally
            {
                Mutexes.ReleaseSmbus();
            }
            return list;
        }

        public static Dictionary<byte, Ddr5SpdInfo> ReadDdr5SpdInitInfoAll()
        {
            Dictionary<byte, Ddr5SpdInfo> list = new Dictionary<byte, Ddr5SpdInfo>();

            if (!IsDdr5MemoryInstalled())
                return list;

            if (!Mutexes.WaitSmbus(5000))
                return list;

            try
            {
                list = ReadDdr5SpdInitInfoAllNoLock();
            }
            finally
            {
                Mutexes.ReleaseSmbus();
            }
            return list;
        }

        /// <summary>
        /// Write SPD data for a single DIMM to a binary file at the specified path.
        /// </summary>
        public static bool DumpDdr5SpdToFile(byte addr7, string filePath)
        {
            if (!IsDdr5MemoryInstalled())
                return false;

            try
            {
                string dir = System.IO.Path.GetDirectoryName(filePath);
                if (!string.IsNullOrEmpty(dir) && !System.IO.Directory.Exists(dir))
                    System.IO.Directory.CreateDirectory(dir);

                Ddr5SpdInfo info = ReadDdr5Spd(addr7);
                if (info == null)
                    throw new InvalidOperationException(string.Format("Could not read SPD from DIMM at address 0x{0:X2}.", addr7));

                byte[] buffer = info.RawSpd;
                System.IO.File.WriteAllBytes(filePath, buffer);
                Debug.WriteLine(String.Format("Wrote {0} bytes to {1}", buffer.Length, filePath));

                return true;
            }
            catch (Exception ex)
            {
                Debug.WriteLine(String.Format("Error dumping SPD to file: {0}", ex.Message));
                return false;
            }
        }

        /// <summary>
        /// Write SPD data from all discovered DIMMs to binary files in the specified directory.
        /// </summary>
        public static bool DumpDdr5SpdToFiles(string outputDirectory)
        {
            if (!IsDdr5MemoryInstalled())
                return false;

            try
            {
                if (!System.IO.Directory.Exists(outputDirectory))
                    System.IO.Directory.CreateDirectory(outputDirectory);

                Dictionary<byte, Ddr5SpdInfo> list = ReadDdr5SpdAll();

                if (list.Count == 0)
                    throw new InvalidOperationException("No DDR5 DIMMs found on any SMBus port.");

                bool allOk = true;
                foreach (var kvp in list)
                {
                    string filePath = System.IO.Path.Combine(outputDirectory, string.Format("DIMM_0x{0:X2}.bin", kvp.Key));
                    byte[] buffer = kvp.Value.RawSpd;

                    try
                    {
                        System.IO.File.WriteAllBytes(filePath, buffer);
                        Debug.WriteLine(String.Format("Wrote {0} bytes to {1}", buffer.Length, filePath));
                    }
                    catch (Exception ex)
                    {
                        Debug.WriteLine(String.Format("Error writing {0}: {1}", filePath, ex.Message));
                        allOk = false;
                    }
                }

                return allOk;
            }
            catch (Exception ex)
            {
                Debug.WriteLine(string.Format("Error dumping SPD to files: {0}", ex.Message));
                return false;
            }
        }
    }
}
