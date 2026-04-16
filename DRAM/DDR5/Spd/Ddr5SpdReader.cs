using System;
using System.Collections.Generic;
using ZenStates.Core.Drivers;

namespace ZenStates.Core.DRAM
{
    internal static class Ddr5SpdReader
    {
        private static readonly SmbusPiix4 smbusDriver = SmbusPiix4.Instance;
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

        internal static bool SpdSwitchPage(byte addr7, byte page)
        {
            return smbusDriver.WriteByteDataNoLock(addr7, 0x0B, page);
        }

        /// <summary>
        /// Scan for DDR5 SPD hubs. Tries port 2 (DDR5/TSI) first, then port 0.
        /// Returns 7-bit addresses of responding hubs.
        /// </summary>
        internal static List<byte> ScanDdr5SpdHubsNoLock()
        {
            int[] ports = new int[] { PORT_DIMM, PORT_BOARD };

            for (int p = 0; p < ports.Length; p++)
            {
                smbusDriver.ChangePortNoLock(ports[p]);
                List<byte> found = new List<byte>();
                for (int i = SPD_HUB_ADDR_FIRST; i <= SPD_HUB_ADDR_LAST; i++)
                {
                    if (smbusDriver.ReadByteDataNoLock((byte)i, 0x00, out byte _))
                        found.Add((byte)i);
                }
                if (found.Count > 0)
                    return found;
            }

            return new List<byte>();
        }

        // Read full SPD info without Mutex lock
        internal static Ddr5SpdInfo ReadDdr5SpdNoLock(byte addr7)
        {
            List<byte> spd = new List<byte>(SPD_TOTAL_SIZE);
            int prevPage = -1;
            int offset = 0;

            while (offset < SPD_TOTAL_SIZE)
            {
                int page = SpdCalculatePage(offset);
                int reg = SpdCalculateReg(offset);
                int regOffset = offset % PAGE_SIZE;

                if (page != prevPage)
                {
                    SpdSwitchPage(addr7, (byte)page);
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

            return Ddr5SpdDecoder.Decode(spd);
        }

        internal static Dictionary<byte, Ddr5SpdInfo> ReadDdr5SpdAllNoLock()
        {
            Dictionary<byte, Ddr5SpdInfo> list = new Dictionary<byte, Ddr5SpdInfo>();
            List<byte> addresses = ScanDdr5SpdHubsNoLock();
            if (addresses.Count == 0)
                throw new InvalidOperationException("No DDR5 DIMMs found on any SMBus port.");

            for (int i = 0; i < addresses.Count; i++)
                list.Add(addresses[i], ReadDdr5SpdNoLock(addresses[i]));

            return list;
        }

         // Read minimal SPD info without Mutex lock
        internal static Ddr5SpdInfo ReadDdr5SpdInitInfoNoLock(byte addr7)
        {
            Ddr5SpdInfo info = new Ddr5SpdInfo();

            byte b0, b1;
            ushort w;

            if (!SpdSwitchPage(addr7, 0))
                return null;

            if (smbusDriver.ReadByteDataNoLock(addr7, (byte)SpdCalculateReg(2), out b0))
            {
                info.DeviceType = b0;
                info.IsValid = (info.DeviceType == 0x12 || info.DeviceType == 0x13);
                if (!info.IsValid)
                    return null;
                info.IsLpddr5 = (info.DeviceType == 0x13);
            }

            // Byte 4: density and die count
            if (smbusDriver.ReadByteDataNoLock(addr7, (byte)SpdCalculateReg(4), out b0))
            {
                info.FirstDieDensityMbit = Ddr5SpdDecoder.DecodeDieDensity(b0 & 0x1F);
                info.FirstDieCount = (((b0 >> 5) & 0x07) + 1);
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

            ReadLiveDevicesNoLock(addr7, info, smbusDriver);

            //byte pmicAddr = Ddr5PmicReader.CalculatePmicAddrFromSpd(addr7);
            //if (Ddr5PmicReader.DetectNoLock(smbusDriver, pmicAddr))
            //{
            //    info.PmicData = new Ddr5PmicData()
            //    {
            //        IsValid = true,
            //        I2cAddress = pmicAddr,
            //    };
            //}

            return info;
        }

        internal static Dictionary<byte, Ddr5SpdInfo> ReadDdr5SpdInitInfoAllNoLock()
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
            if (!Mutexes.WaitSmbus(5000))
                return null;
            try
            {
                return ReadDdr5SpdNoLock(addr7);
            }
            finally
            {
                Mutexes.ReleaseSmbus();
            }
        }

        // Read single DDR5 SPD minimal
        public static Ddr5SpdInfo ReadDdr5SpdInitInfo(byte addr7)
        {
            if (!Mutexes.WaitSmbus(5000))
                return null;
            try
            {
                return ReadDdr5SpdInitInfoNoLock(addr7);
            }
            finally
            {
                Mutexes.ReleaseSmbus();
            }
        }

        // Read all SPD
        public static Dictionary<byte, Ddr5SpdInfo> ReadDdr5SpdAll()
        {
            Dictionary<byte, Ddr5SpdInfo> list = new Dictionary<byte, Ddr5SpdInfo>();

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
        /// Write SPD data from all discovered DIMMs to binary files in the specified directory.
        /// </summary>
        public static bool DumpDdr5SpdToFiles(string outputDirectory)
        {
            try
            {
                if (!System.IO.Directory.Exists(outputDirectory))
                    System.IO.Directory.CreateDirectory(outputDirectory);

                Dictionary<byte, Ddr5SpdInfo> list = ReadDdr5SpdAll();

                if (list.Count == 0)
                    throw new InvalidOperationException("No DDR5 DIMMs found on any SMBus port.");

                foreach (var kvp in list)
                {
                    string filename = System.IO.Path.Combine(outputDirectory, string.Format("DIMM_0x{0:X2}.bin", kvp.Key));

                    byte[] buffer = kvp.Value.RawSpd;
                    System.IO.File.WriteAllBytes(filename, buffer);

                    Console.WriteLine("Wrote {0} bytes to {1}", buffer.Length, filename);
                }

                return true;
            }
            catch (Exception ex)
            {
                Console.WriteLine("Error dumping SPD to files: {0}", ex.Message);
                return false;
            }
        }
    }
}
