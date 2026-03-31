using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;

namespace ZenStates.Core.Drivers
{
    internal sealed class SmbusPiix4InpOut : SmbusDriverBase
    {
        private static volatile SmbusPiix4InpOut _instance;
        private static readonly object _instanceLock = new object();

        private static readonly IODriver ioDriver = IODriver.Instance;

        // PCI config mechanism #1
        private const ushort PCI_CONFIG_ADDRESS = 0xCF8;
        private const ushort PCI_CONFIG_DATA = 0xCFC;

        // AMD KernCZ SMBus
        private const byte PIIX4_PCI_BUS = 0x00;
        private const byte PIIX4_PCI_DEVICE = 0x14;
        private const byte PIIX4_PCI_FUNCTION = 0x00;

        private const ushort PCI_VENDOR_ID_AMD = 0x1022;
        private const ushort PCI_DEVICE_ID_AMD_KERNCZ_SMBUS = 0x790B;

        private const byte PCICMD = 0x04;
        private const ushort PCICMD_IOBIT = 0x0001;

        // PIIX4 protocol encodings
        private const byte PIIX4_QUICK = 0x00;
        private const byte PIIX4_BYTE = 0x04;
        private const byte PIIX4_BYTE_DATA = 0x08;
        private const byte PIIX4_WORD_DATA = 0x0C;
        private const byte PIIX4_BLOCK_DATA = 0x14;

        private const int I2C_SMBUS_BLOCK_MAX = 32;
        private const int MAX_TIMEOUT_MS = 64;

        // SMBHSTSTS bits
        private const byte SMBHSTSTS_HOST_BUSY = 0x01;
        private const byte SMBHSTSTS_INTR = 0x02;
        private const byte SMBHSTSTS_DEV_ERR = 0x04;
        private const byte SMBHSTSTS_BUS_ERR = 0x08;
        private const byte SMBHSTSTS_FAILED = 0x10;

        // SMBHSTCNT bits
        private const byte SMBHSTCNT_START = 0x40;

        // Default SMBus IO base on KernCZ in your PawnIO driver
        private readonly ushort _smba;

        private int _currentPort = -1;

        public static SmbusPiix4InpOut Instance
        {
            get
            {
                if (_instance == null)
                {
                    lock (_instanceLock)
                    {
                        if (_instance == null)
                        {
                            _instance = new SmbusPiix4InpOut();
                        }
                    }
                }
                return _instance;
            }
        }

        public SmbusPiix4InpOut(ushort smba = 0x0B00)
        {
            _smba = smba;
        }

        // Host register offsets
        private ushort SMBHSTSTS { get { return (ushort)(_smba + 0x00); } }
        private ushort SMBHSTCNT { get { return (ushort)(_smba + 0x02); } }
        private ushort SMBHSTCMD { get { return (ushort)(_smba + 0x03); } }
        private ushort SMBHSTADD { get { return (ushort)(_smba + 0x04); } }
        private ushort SMBHSTDAT0 { get { return (ushort)(_smba + 0x05); } }
        private ushort SMBHSTDAT1 { get { return (ushort)(_smba + 0x06); } }
        private ushort SMBBLKDAT { get { return (ushort)(_smba + 0x07); } }
        private ushort SMBTIMING { get { return (ushort)(_smba + 0x0E); } }

        private static byte IoIn8(ushort port)
        {
            return ioDriver.DlPortReadPortUchar(port);
        }

        private static void IoOut8(ushort port, byte value)
        {
            ioDriver.DlPortWritePortUchar(port, value);
        }

        private static ushort IoIn16(ushort port)
        {
            return ioDriver.DlPortReadPortUshort(port);
        }

        private static void IoOut16(ushort port, ushort value)
        {
            ioDriver.DlPortWritePortUshort(port, value);
        }

        private static uint IoIn32(ushort port)
        {
            return ioDriver.DlPortReadPortUlong(port);
        }

        private static void IoOut32(ushort port, uint value)
        {
            ioDriver.DlPortWritePortUlong(port, value);
        }

        // TODO: Move back to IO Driver
        private static uint PciConfigAddress(byte bus, byte dev, byte func, byte offset)
        {
            return 0x80000000u
                 | ((uint)bus << 16)
                 | ((uint)dev << 11)
                 | ((uint)func << 8)
                 | (uint)(offset & 0xFC);
        }

        private static uint PciReadDword(byte bus, byte dev, byte func, byte offset)
        {
            IoOut32(PCI_CONFIG_ADDRESS, PciConfigAddress(bus, dev, func, offset));
            return IoIn32(PCI_CONFIG_DATA);
        }

        private static ushort PciReadWord(byte bus, byte dev, byte func, byte offset)
        {
            uint dword = PciReadDword(bus, dev, func, (byte)(offset & 0xFC));
            int shift = (offset & 2) * 8;
            return (ushort)((dword >> shift) & 0xFFFF);
        }

        private static byte PciReadByte(byte bus, byte dev, byte func, byte offset)
        {
            uint dword = PciReadDword(bus, dev, func, (byte)(offset & 0xFC));
            int shift = (offset & 3) * 8;
            return (byte)((dword >> shift) & 0xFF);
        }

        private static void PciWriteWord(byte bus, byte dev, byte func, byte offset, ushort value)
        {
            byte aligned = (byte)(offset & 0xFC);
            uint dword = PciReadDword(bus, dev, func, aligned);
            int shift = (offset & 2) * 8;
            uint mask = 0xFFFFu << shift;
            dword = (dword & ~mask) | ((uint)value << shift);

            IoOut32(PCI_CONFIG_ADDRESS, PciConfigAddress(bus, dev, func, aligned));
            IoOut32(PCI_CONFIG_DATA, dword);
        }

        private static long GetTimestamp()
        {
            return Stopwatch.GetTimestamp();
        }

        private static long MillisecondsToStopwatchTicks(int milliseconds)
        {
            return (Stopwatch.Frequency * milliseconds) / 1000L;
        }

        private static long MicrosecondsToStopwatchTicks(int microseconds)
        {
            return (Stopwatch.Frequency * microseconds) / 1000000L;
        }

        private static int DivideRoundUp(int numerator, int denominator)
        {
            if (numerator <= 0)
                return 0;

            return (numerator + denominator - 1) / denominator;
        }

        private static void DelayMicroseconds(int microseconds)
        {
            if (microseconds <= 0)
                return;

            long waitTicks = (Stopwatch.Frequency * microseconds) / 1000000L;

            if (waitTicks <= 0)
                waitTicks = 1;

            long start = Stopwatch.GetTimestamp();

            while ((Stopwatch.GetTimestamp() - start) < waitTicks)
            {
                Thread.SpinWait(10);
            }
        }

        public bool Initialize()
        {
            if (!Mutexes.WaitPciBus(5000))
            {
                Console.WriteLine("Failed to acquire PCI bus mutex for SMBus initialization.");
                return false;
            }

            try
            {
                ushort vid = PciReadWord(PIIX4_PCI_BUS, PIIX4_PCI_DEVICE, PIIX4_PCI_FUNCTION, 0x00);
                ushort did = PciReadWord(PIIX4_PCI_BUS, PIIX4_PCI_DEVICE, PIIX4_PCI_FUNCTION, 0x02);
                byte rev = PciReadByte(PIIX4_PCI_BUS, PIIX4_PCI_DEVICE, PIIX4_PCI_FUNCTION, 0x08);

                if (vid != PCI_VENDOR_ID_AMD || did != PCI_DEVICE_ID_AMD_KERNCZ_SMBUS)
                    return false;

                //if (rev < 0x51)
                //    return false;

                ushort pciCmd = PciReadWord(PIIX4_PCI_BUS, PIIX4_PCI_DEVICE, PIIX4_PCI_FUNCTION, PCICMD);
                if ((pciCmd & PCICMD_IOBIT) == 0)
                    PciWriteWord(PIIX4_PCI_BUS, PIIX4_PCI_DEVICE, PIIX4_PCI_FUNCTION, PCICMD, (ushort)(pciCmd | PCICMD_IOBIT));

                if (IoIn8(SMBHSTSTS) == 0xFF)
                    return false;

                return true;
            }
            finally
            {
                Mutexes.ReleasePciBus();
            }
        }

        private bool IsBusyNoLock()
        {
            byte temp = IoIn8(SMBHSTSTS);
            if (temp != 0x00)
            {
                IoOut8(SMBHSTSTS, temp);
                temp = IoIn8(SMBHSTSTS);
                if (temp != 0x00)
                    return false;
            }

            return true;
        }

        private bool Transaction(int size, out byte statusByte)
        {
            statusByte = 0;

            byte timing = IoIn8(SMBTIMING);

            // Best-effort clear of stale latched status before starting.
            byte temp = IoIn8(SMBHSTSTS);
            if (temp != 0x00)
            {
                IoOut8(SMBHSTSTS, temp);
                temp = IoIn8(SMBHSTSTS);
                if (temp != 0x00)
                {
                    statusByte = temp;
                    return false;
                }
            }

            // Start transaction.
            IoOut8(SMBHSTCNT, (byte)(IoIn8(SMBHSTCNT) | SMBHSTCNT_START));

            long start = GetTimestamp();
            long timeoutTicks = MillisecondsToStopwatchTicks(MAX_TIMEOUT_MS);

            // Approximate initial transaction setup delay in microseconds.
            int initialDelayUs = 0;
            if (timing != 0)
                initialDelayUs = DivideRoundUp((10 + (9 * size)) * timing * 4, 66);

            if (initialDelayUs > 0)
                DelayMicroseconds(initialDelayUs);

            int perClockUs = 0;
            if (timing != 0)
                perClockUs = DivideRoundUp(timing * 4, 66);

            do
            {
                temp = IoIn8(SMBHSTSTS);

                if ((temp & SMBHSTSTS_HOST_BUSY) == 0)
                    break;

                if ((GetTimestamp() - start) >= timeoutTicks)
                    break;

                if (perClockUs > 0)
                    DelayMicroseconds(perClockUs);
            }
            while (true);

            statusByte = temp;

            bool success =
                ((temp & SMBHSTSTS_HOST_BUSY) == 0) &&
                ((temp & SMBHSTSTS_FAILED) == 0) &&
                ((temp & SMBHSTSTS_BUS_ERR) == 0) &&
                ((temp & SMBHSTSTS_DEV_ERR) == 0) &&
                ((temp & SMBHSTSTS_INTR) != 0);

            if (temp != 0x00)
                IoOut8(SMBHSTSTS, temp);

            return success;
        }

        internal bool ChangePortNoLock(int port, out int previousPort)
        {
            previousPort = _currentPort;

            // This backend has no explicit port-switch ioctl/register like the kernel driver.
            // Treat it as a logical port change only.
            if (port != -1)
                _currentPort = port;

            return true;
        }

        internal bool ChangePortNoLock(int port)
        {
            return ChangePortNoLock(port, out int _);
        }

        internal override bool SmbusQuickNoLock(byte addr7, byte readWrite)
        {
            if (!IsBusyNoLock())
                return false;
            IoOut8(SMBHSTADD, (byte)((addr7 << 1) | (readWrite & 0x01)));
            IoOut8(SMBHSTCNT, PIIX4_QUICK);
            return Transaction(I2C_SMBUS_QUICK + (readWrite & 0x01), out byte _);
        }

        internal override bool ReadByteDataNoLock(byte addr7, byte command, out byte value)
        {
            value = 0;

            if (!IsBusyNoLock())
                return false;

            IoOut8(SMBHSTADD, (byte)((addr7 << 1) | I2C_SMBUS_READ));
            IoOut8(SMBHSTCMD, command);
            IoOut8(SMBHSTCNT, PIIX4_BYTE_DATA);

            if (!Transaction(I2C_SMBUS_BYTE_DATA + I2C_SMBUS_READ, out byte st))
                return false;

            value = IoIn8(SMBHSTDAT0);
            return true;
        }

        internal override bool WriteByteDataNoLock(byte addr7, byte command, byte value)
        {
            if (!IsBusyNoLock())
                return false;

            IoOut8(SMBHSTADD, (byte)((addr7 << 1) | I2C_SMBUS_WRITE));
            IoOut8(SMBHSTCMD, command);
            IoOut8(SMBHSTDAT0, value);
            IoOut8(SMBHSTCNT, PIIX4_BYTE_DATA);

            return Transaction(I2C_SMBUS_BYTE_DATA + I2C_SMBUS_WRITE, out byte st);
        }

        internal override bool ReadWordDataNoLock(byte addr7, byte command, out ushort value)
        {
            value = 0;

            if (!IsBusyNoLock())
                return false;

            IoOut8(SMBHSTADD, (byte)((addr7 << 1) | I2C_SMBUS_READ));
            IoOut8(SMBHSTCMD, command);
            IoOut8(SMBHSTCNT, PIIX4_WORD_DATA);

            if (!Transaction(I2C_SMBUS_WORD_DATA + I2C_SMBUS_READ, out byte st))
                return false;

            value = (ushort)(IoIn8(SMBHSTDAT0) | (IoIn8(SMBHSTDAT1) << 8));
            return true;
        }

        internal override bool WriteWordDataNoLock(byte addr7, byte command, ushort value)
        {
            if (!IsBusyNoLock())
                return false;

            IoOut8(SMBHSTADD, (byte)((addr7 << 1) | I2C_SMBUS_WRITE));
            IoOut8(SMBHSTCMD, command);

            // Write low byte first, then high byte
            IoOut8(SMBHSTDAT0, (byte)(value & 0xFF));
            IoOut8(SMBHSTDAT1, (byte)((value >> 8) & 0xFF));

            IoOut8(SMBHSTCNT, PIIX4_WORD_DATA);

            return Transaction(I2C_SMBUS_WORD_DATA + I2C_SMBUS_WRITE, out byte st);
        }

        internal override bool ReadBlockDataNoLock(byte addr7, byte command, out List<byte> data)
        {
            data = new List<byte>();

            if (!IsBusyNoLock())
                return false;

            IoOut8(SMBHSTADD, (byte)((addr7 << 1) | I2C_SMBUS_READ));
            IoOut8(SMBHSTCMD, command);
            IoOut8(SMBHSTCNT, PIIX4_BLOCK_DATA);

            if (!Transaction(2 + I2C_SMBUS_READ, out byte st))
                return false;

            int len = IoIn8(SMBHSTDAT0);
            if (len < 1 || len > I2C_SMBUS_BLOCK_MAX)
                return false;

            data = new List<byte>(len);

            // Reset SMBBLKDAT pointer
            IoIn8(SMBHSTCNT);

            for (int i = 0; i < len; i++)
                data.Add(IoIn8(SMBBLKDAT));

            return true;
        }

        internal override bool WriteBlockDataNoLock(byte addr7, byte command, List<byte> data)
        {
            if (data == null)
                return false;

            int len = data.Count;
            if (len < 1 || len > I2C_SMBUS_BLOCK_MAX)
                return false;

            if (!IsBusyNoLock())
                return false;

            IoOut8(SMBHSTADD, (byte)((addr7 << 1) | I2C_SMBUS_WRITE));
            IoOut8(SMBHSTCMD, command);
            IoOut8(SMBHSTCNT, PIIX4_BLOCK_DATA);

            // First byte is block length
            IoOut8(SMBHSTDAT0, (byte)len);

            // Reset SMBBLKDAT pointer
            IoIn8(SMBHSTCNT);

            for (int i = 0; i < len; i++)
                IoOut8(SMBBLKDAT, data[i]);

            return Transaction(2 + len + I2C_SMBUS_WRITE, out byte st);
        }

        //public List<byte> DumpDdr5SpdInBlocks(byte addr7)
        //{
        //    const int totalSize = 0x400;

        //    List<byte> spd = new List<byte>(totalSize);
        //    int prevPage = -1;

        //    int offset = 0;
        //    while (offset < totalSize)
        //    {
        //        int page = SpdGetPage(offset);
        //        int reg = SpdGetReg(offset);
        //        int regOffset = offset % 128;

        //        if (page != prevPage)
        //        {
        //            if (!WriteByteDataNoLock(addr7, 0x0B, (byte)page))
        //            {
        //                spd.Add(0xFF);
        //                offset++;
        //                continue;
        //            }

        //            prevPage = page;
        //        }

        //        int bytesLeftTotal = totalSize - offset;
        //        int bytesLeftInPage = 128 - regOffset;
        //        int maxUsable = Math.Min(bytesLeftTotal, bytesLeftInPage);

        //        List<byte> block;
        //        if (ReadBlockDataNoLock(addr7, (byte)reg, out block) &&
        //            block != null &&
        //            block.Count > 0)
        //        {
        //            int used = Math.Min(block.Count, maxUsable);

        //            for (int i = 0; i < used; i++)
        //                spd.Add(block[i]);

        //            offset += used;
        //        }
        //        else
        //        {
        //            if (!ReadByteDataNoLock(addr7, (byte)reg, out byte value))
        //                value = 0xFF;

        //            spd.Add(value);
        //            offset++;
        //        }
        //    }

        //    return spd;
        //}

        //public List<byte> DumpDdr5Spd(byte addr7)
        //{
        //    const int totalSize = 0x400;

        //    List<byte> spd = new List<byte>(totalSize);
        //    int prevPage = -1;

        //    bool tryBlock = false;

        //    // One quick probe only
        //    if (WriteByteDataNoLock(addr7, 0x0B, 0x00))
        //    {
        //        List<byte> probe;
        //        if (ReadBlockDataNoLock(addr7, 0x80, out probe) && probe != null && probe.Count > 1)
        //            tryBlock = true;
        //    }

        //    int offset = 0;
        //    while (offset < totalSize)
        //    {
        //        int page = SpdGetPage(offset);
        //        int reg = SpdGetReg(offset);
        //        int regOffset = offset % 128;

        //        if (page != prevPage)
        //        {
        //            if (!WriteByteDataNoLock(addr7, 0x0B, (byte)page))
        //            {
        //                spd.Add(0xFF);
        //                offset++;
        //                continue;
        //            }

        //            prevPage = page;
        //        }

        //        int bytesLeftTotal = totalSize - offset;
        //        int bytesLeftInPage = 128 - regOffset;
        //        int maxUsable = Math.Min(bytesLeftTotal, bytesLeftInPage);

        //        bool usedBlock = false;

        //        if (tryBlock)
        //        {
        //            List<byte> block;
        //            if (ReadBlockDataNoLock(addr7, (byte)reg, out block) &&
        //                block != null &&
        //                block.Count > 1)
        //            {
        //                int used = Math.Min(block.Count, maxUsable);

        //                for (int i = 0; i < used; i++)
        //                    spd.Add(block[i]);

        //                offset += used;
        //                usedBlock = true;
        //            }
        //            else
        //            {
        //                tryBlock = false;
        //            }
        //        }

        //        if (!usedBlock)
        //        {
        //            if (!ReadByteDataNoLock(addr7, (byte)reg, out byte value))
        //                value = 0xFF;

        //            spd.Add(value);
        //            offset++;
        //        }
        //    }

        //    return spd;
        //}

        //private static int SpdGetPage(int offset)
        //{
        //    return offset / 128;
        //}

        //private static int SpdGetReg(int offset)
        //{
        //    return 0x80 + (offset % 128);
        //}
    }
}