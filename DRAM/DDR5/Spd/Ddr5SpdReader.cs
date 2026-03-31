using System;
using System.Collections.Generic;
using ZenStates.Core.Drivers;

namespace ZenStates.Core.DRAM.DDR5.Spd
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
            return smbusDriver.WriteByteData(addr7, 0x0B, page);
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
                    if (smbusDriver.ReadByteData((byte)i, 0x00, out byte _))
                        found.Add((byte)i);
                }
                if (found.Count > 0)
                    return found;
            }

            return new List<byte>();
        }

        internal static Ddr5SpdInfo ReadNoLock(byte addr7)
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

        public static Ddr5SpdInfo Read(byte addr7)
        {
            if (!Mutexes.WaitSmbus(5000))
                return null;
            try
            {
                return ReadNoLock(addr7);
            }
            finally
            {
                Mutexes.ReleaseSmbus();
            }
        }

        public static Dictionary<byte, Ddr5SpdInfo> ReadAll()
        {
            Dictionary<byte, Ddr5SpdInfo> list = new Dictionary<byte, Ddr5SpdInfo>();

            if (!Mutexes.WaitSmbus(5000))
                return list;

            try
            {
                List<byte> addresses = ScanDdr5SpdHubsNoLock();
                if (addresses.Count == 0)
                    throw new InvalidOperationException("No DDR5 DIMMs found on any SMBus port.");

                for (int i = 0; i < addresses.Count; i++)
                    list.Add(addresses[i], ReadNoLock(addresses[i]));
            }
            finally
            {
                Mutexes.ReleaseSmbus();
            }
            return list;
        }

        public static Ddr5SpdInfo ReadDdr5SpdInitInfo(byte addr7)
        {
            byte[] spd = new byte[SPD_TOTAL_SIZE];
            Ddr5SpdInfo info = new Ddr5SpdInfo();

            if (!Mutexes.WaitSmbus(5000))
                return null;

            try
            {
                byte b0, b1;
                ushort w;

                if (!SpdSwitchPage(addr7, 0))
                    return null;

                if (smbusDriver.ReadByteData(addr7, (byte)SpdCalculateReg(2), out b0))
                {
                    info.DeviceType = b0;
                    info.IsValid = (info.DeviceType == 0x12 || info.DeviceType == 0x13);
                    if (info.IsValid)
                        return null;
                    info.IsLpddr5 = (info.DeviceType == 0x13);
                }

                // -------------------------------------------------
                // Page 4: module manufacturer / part number
                // -------------------------------------------------
                if (!SpdSwitchPage(addr7, 4))
                    return null;

                // Module manufacturer: bytes 512-513
                if (smbusDriver.ReadWordDataNoLock(addr7, (byte)SpdCalculateReg(552), out w))
                {
                    b0 = (byte)(w & 0xFF);
                    b1 = (byte)((w >> 8) & 0xFF);
                    info.DramMfgIdBank = b0;
                    info.DramMfgIdMfr = b1;
                    info.DramManufacturer = ManufacturerMapping.Lookup(info.DramMfgIdBank, info.DramMfgIdMfr);
                }

                ReadPmic(addr7, info, smbusDriver);

                return info;
            }
            finally
            {
                Mutexes.ReleaseSmbus();
            }
        }

        public static Dictionary<byte, Ddr5SpdInfo> ReadDdr5SpdInitInfoAll()
        {
            Dictionary<byte, Ddr5SpdInfo> result = new Dictionary<byte, Ddr5SpdInfo>();

            List<byte> addresses = ScanDdr5SpdHubsNoLock();
            for (int i = 0; i < addresses.Count; i++)
            {
                byte addr = addresses[i];
                Ddr5SpdInfo info = ReadDdr5SpdInitInfo(addr);
                if (info != null)
                    result[addr] = info;
            }

            return result;
        }

        public static void ReadThermal(byte addr7, Ddr5SpdInfo info, SmbusDriverBase smbus)
        {
            if (info == null || smbus == null || info.IsLpddr5)
                return;

            try
            {
                if (Ddr5ThermalSensor.Detect(smbus, addr7))
                    info.ThermalData = Ddr5ThermalSensor.ReadAll(smbus, addr7);
            }
            catch
            {
                // Thermal sensor not accessible - not critical
            }
        }

        public static void ReadPmic(byte addr7, Ddr5SpdInfo info, SmbusDriverBase smbus)
        {
            if (info == null || smbus == null || info.IsLpddr5)
                return;

            try
            {
                byte pmicAddr = Ddr5PmicReader.CalculatePmicAddrFromSpd(addr7);
                if (Ddr5PmicReader.Detect(smbus, pmicAddr))
                    info.PmicData = Ddr5PmicReader.ReadAll(smbus, pmicAddr);
            }
            catch
            {
                // PMIC not accessible - not critical
            }
        }

        public static void ReadLiveDevices(byte addr7, Ddr5SpdInfo info, SmbusDriverBase smbus)
        {
            if (info == null || smbus == null)
                return;

            ReadThermal(addr7, info, smbus);
            ReadPmic(addr7, info, smbus);
        }

        //public List<byte> DumpDdr5Spd(byte addr7)
        //{
        //    const int totalSize = 0x400;
        //    const int blockSize = 32;

        //    List<byte> spd = new List<byte>(totalSize);
        //    int prevPage = -1;

        //    if (!Mutexes.WaitSmbus(5000))
        //        return spd;

        //    long data = 0;
        //    long[] output = new long[6];

        //    int ret = _pawnIo.ExecuteHr(IOCTL_SMBUS_XFER,
        //        new long[] { 0x50, I2C_SMBUS_READ, 0x80, I2C_SMBUS_BLOCK_DATA, data }, 5, output, 5, out uint oSize);

        //    //SmbusReadBlockDataFixedNoLock(0x51, 0x80, 32, out byte[] data);

        //    try
        //    {
        //        int offset = 0;

        //        while (offset < totalSize)
        //        {
        //            int page = SpdGetPage(offset);
        //            int reg = SpdGetReg(offset);
        //            int regOffset = offset % PAGE_SIZE;

        //            if (page != prevPage)
        //            {
        //                SpdSwitchPage(addr7, (byte)page);
        //                prevPage = page;
        //            }

        //            int bytesLeftTotal = totalSize - offset;
        //            int bytesLeftInPage = PAGE_SIZE - regOffset;
        //            int count = Math.Min(blockSize, Math.Min(bytesLeftTotal, bytesLeftInPage));

        //            if (SmbusReadBlockDataFixedNoLock(addr7, (byte)reg, count, out byte[] block))
        //            {
        //                spd.AddRange(block);
        //            }
        //            else
        //            {
        //                for (int i = 0; i < count; i++)
        //                {
        //                    if (!SmbusReadByteDataNoLock(addr7, (byte)(reg + i), out byte b))
        //                        b = 0xFF;
        //                    spd.Add(b);
        //                }
        //            }

        //            offset += count;
        //        }
        //    }
        //    finally
        //    {
        //        Mutexes.ReleaseSmbus();
        //    }

        //    return spd;
        //}

        /// <summary>
        /// Write SPD data from all discovered DIMMs to binary files in the specified directory.
        /// </summary>
        public static bool DumpDdr5SpdToFiles(string outputDirectory)
        {
            try
            {
                if (!System.IO.Directory.Exists(outputDirectory))
                    System.IO.Directory.CreateDirectory(outputDirectory);

                Dictionary<byte, Ddr5SpdInfo> list = ReadAll();

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
