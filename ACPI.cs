using System;
using System.Runtime.InteropServices;
using ZenStates.Core.Drivers;

namespace ZenStates.Core
{
    public class ACPI
    {
        public static class TableSignature
        {
            public const string RSDP = "RSD PTR ";
            public const string RSDT = "RSDT";
            public const string XSDT = "XSDT";
            public const string SSDT = "SSDT";
            // Table OemId signatures
            public const string AOD_ = "AOD     ";
            public const string AAOD = "AMD AOD";
            public const string LENOVO_AOD = "CB-01   ";
            // Region signatures
            public const string AODE = "AODE";
            public const string AODT = "AODT";
        }

        internal const ushort EBDA_START_SEGMENT_PTR = 0x40e;
        internal const uint EBDA_EARLIEST_START = 0x80000;
        internal const uint EBDA_END = 0x9ffff;
        internal const uint RSDP_REGION_BASE_ADDRESS = 0x0e0000;
        internal const int RSDP_REGION_LENGTH = 0x01ffff;

        // 5.2.5.3 RSDP Structure
        // https://uefi.org/sites/default/files/resources/ACPI_5_1_Errata_B.PDF p.110
        [Serializable]
        [StructLayout(LayoutKind.Sequential, Pack = 1, Size = 36)]
        public struct RSDP
        {
            //  [FieldOffset(0)]
            public ulong Signature; // "RSD PTR " (note the space at the end)
            // [FieldOffset(8)]
            public byte Checksum; // Includes only the first 20 bytes of this table, bytes 0 to 19, including the checksum field. These bytes must sum to zero.
            // [FieldOffset(9)]
            [MarshalAs(UnmanagedType.ByValArray, SizeConst = 6)]
            public byte[] OEMID;
            // [FieldOffset(15)]
            public byte Revision;
            // [FieldOffset(16)]
            public uint RsdtAddress; // 32 bit physical address of the RSDT table
            // [FieldOffset(20)]
            public uint Length; // The length of the whole table, in bytes, including the header, starting from offset 0.
            // [FieldOffset(24)]
            public ulong XsdtAddress; // 64 bit physical address of the XSDT table
            // [FieldOffset(32)]
            public byte ExtendedChecksum; // This is a checksum of the entire table, including both checksum fields
            // [FieldOffset(33)]
            public byte Reserved1;
            // [FieldOffset(34)]
            public byte Reserved2;
            // [FieldOffset(35)]
            public byte Reserved3;
        };

        [Serializable]
        [StructLayout(LayoutKind.Sequential, Pack = 1, Size = 36)]
        public struct SDTHeader
        {
            // [FieldOffset(0)]
            public uint Signature;
            // [FieldOffset(4)]
            public uint Length;
            // [FieldOffset(8)]
            public byte Revision;
            // [FieldOffset(9)]
            public byte Checksum;
            [MarshalAs(UnmanagedType.ByValArray, SizeConst = 6)]
            // [FieldOffset(10)]
            public byte[] OEMID;
            // [FieldOffset(16)]
            public ulong OEMTableID;
            // [FieldOffset(24)]
            public uint OEMRevision;
            // [FieldOffset(28)]
            public uint CreatorID;
            // [FieldOffset(32)]
            public uint CreatorRevision;
        };

        [Serializable]
        [StructLayout(LayoutKind.Sequential)]
        public struct ParsedSDTHeader
        {
            public string Signature;
            public uint Length;
            public byte Revision;
            public byte Checksum;
            public string OEMID;
            public string OEMTableID;
            public uint OEMRevision;
            public string CreatorID;
            public uint CreatorRevision;
        };

        [Serializable]
        [StructLayout(LayoutKind.Sequential)]
        public struct ACPITable
        {
            public SDTHeader RawHeader;
            public ParsedSDTHeader Header;
            [MarshalAs(UnmanagedType.ByValArray)]
            public byte[] Data;
        };

        [Serializable]
        [StructLayout(LayoutKind.Sequential)]
        public struct RSDT
        {
            public SDTHeader Header;
            [MarshalAs(UnmanagedType.ByValArray)]
            public uint[] Data; // 32-bit physical addresses (RSDT)
        };

        [Serializable]
        [StructLayout(LayoutKind.Sequential)]
        public struct XSDT
        {
            public SDTHeader Header;
            [MarshalAs(UnmanagedType.ByValArray)]
            public ulong[] Data; // 64-bit physical addresses (XSDT)
        };

        // 5.2.9 Fixed ACPI Description Table (FADT)
        // https://uefi.org/sites/default/files/resources/ACPI_5_1_Errata_B.PDF p.116
        [Serializable]
        [StructLayout(LayoutKind.Explicit, Pack = 1)]
        public struct FADT
        {
            [FieldOffset(0)]
            public SDTHeader Header;
            [FieldOffset(36)]
            public uint FIRMWARE_CTRL; // Physical memory address of the FACS table
            [FieldOffset(40)]
            public uint DSDT; // Physical memory address of the DSDT table
            [FieldOffset(132)]
            public ulong X_FIRMWARE_CTRL;
            [FieldOffset(140)]
            public ulong X_DSDT;
        }

        // https://github.com/rust-osdev/acpi/blob/main/acpi/src/address.rs
        public enum AddressSpace : byte
        {
            SystemMemory,
            SystemIo,
            PciConfigSpace,
            EmbeddedController,
            SMBus,
            SystemCmos,
            PciBarTarget,
            Ipmi,
            GeneralIo,
            GenericSerialBus,
            PlatformCommunicationsChannel,
            FunctionalFixedHardware,
            OemDefined,
        }

        [Serializable]
        [StructLayout(LayoutKind.Sequential, Pack = 1, Size = 16)]
        public struct OperationRegion
        {
            public uint RegionName;
            public AddressSpace RegionSpace;
            public byte _unknown1;
            public uint Offset;
            public byte _unknown2;
            [MarshalAs(UnmanagedType.ByValArray, SizeConst = 2)]
            public byte[] Length;
            public byte _unknown3;
            public byte _unknown4;
            public byte _unknown5;
        };

        private readonly IODriver io;

        public ACPI(IODriver io)
        {
            this.io = io ?? throw new ArgumentNullException(nameof(io));
        }

        public static ParsedSDTHeader ParseRawHeader(SDTHeader rawHeader)
        {
            return new ParsedSDTHeader()
            {
                Signature = Utils.GetStringFromBytes(rawHeader.Signature),
                Length = rawHeader.Length,
                Revision = rawHeader.Revision,
                Checksum = rawHeader.Checksum,
                OEMID = Utils.GetStringFromBytes(rawHeader.OEMID),
                OEMTableID = Utils.GetStringFromBytes(rawHeader.OEMTableID),
                OEMRevision = rawHeader.OEMRevision,
                CreatorID = Utils.GetStringFromBytes(rawHeader.CreatorID),
                CreatorRevision = rawHeader.CreatorRevision,
            };
        }

        // ASCII string to Little-Endian uint, used for table signatures and OEM ID
        public static uint Signature(string ascii)
        {
            uint val = 0x0;
            int length = Math.Min(ascii.Length, 4);

            for (int i = 0; i < length; i++)
            {
                val |= (uint)ascii[i] << (i * 8);
            }
            return val;
        }

        public static ulong SignatureUL(string ascii)
        {
            ulong val = 0x0;
            int length = Math.Min(ascii.Length, 8);

            for (int i = 0; i < length; i++)
            {
                val |= (ulong)ascii[i] << (i * 8);
            }
            return val;
        }

        public static byte[] ByteSignature(string ascii) => BitConverter.GetBytes(Signature(ascii));
        public static byte[] ByteSignatureUL(string ascii) => BitConverter.GetBytes(SignatureUL(ascii));

        public T GetHeader<T>(uint address, int length = 36) where T : new()
        {
            byte[] bytes = io.ReadMemory(new IntPtr(address), length);
            return Utils.ByteArrayToStructure<T>(bytes);
        }

        public T GetHeader<T>(ulong address, int length = 36) where T : new()
        {
            byte[] bytes = io.ReadMemory(new IntPtr((long)address), length);
            return Utils.ByteArrayToStructure<T>(bytes);
        }

        public RSDP GetRsdp()
        {
            byte[] bytes = io.ReadMemory(new IntPtr(RSDP_REGION_BASE_ADDRESS), RSDP_REGION_LENGTH);
            int rsdpOffset = Utils.FindSequence(bytes, 0, ByteSignatureUL(TableSignature.RSDP));

            if (rsdpOffset < 0)
                throw new SystemException("ACPI: Could not find RSDP signature");

            RSDP rsdp = Utils.ByteArrayToStructure<RSDP>(
                io.ReadMemory(new IntPtr(RSDP_REGION_BASE_ADDRESS + rsdpOffset), Marshal.SizeOf(typeof(RSDP))));

            if (!VerifyChecksum(bytes, rsdpOffset, 20))
                throw new SystemException("ACPI: RSDP checksum validation failed");

            return rsdp;
        }

        /// <summary>
        /// Returns true when the byte sum of <paramref name="length"/> bytes
        /// starting at <paramref name="offset"/> equals zero (ACPI checksum rule).
        /// </summary>
        public static bool VerifyChecksum(byte[] data, int offset, int length)
        {
            if (data == null) throw new ArgumentNullException(nameof(data));
            if (offset < 0 || offset + length > data.Length) throw new ArgumentOutOfRangeException(nameof(offset));

            byte sum = 0;
            for (int i = offset; i < offset + length; i++)
                sum += data[i];
            return sum == 0;
        }

        public RSDT GetRsdt()
        {
            RSDP rsdp = GetRsdp();
            uint rsdtAddress = rsdp.RsdtAddress;

            if (rsdtAddress == 0)
                return new RSDT();

            SDTHeader rsdtHeader = GetHeader<SDTHeader>(rsdtAddress);
            byte[] rawTable = io.ReadMemory(new IntPtr(rsdtAddress), (int)rsdtHeader.Length);

            if (rawTable == null)
                return new RSDT();

            int headerSize = Marshal.SizeOf(typeof(SDTHeader));
            int dataSize = (int)rsdtHeader.Length - headerSize;
            RSDT rsdtTable = new RSDT
            {
                Header = rsdtHeader,
                Data = new uint[dataSize / sizeof(uint)],
            };
            Buffer.BlockCopy(rawTable, headerSize, rsdtTable.Data, 0, dataSize);
            return rsdtTable;
        }

        public XSDT GetXsdt()
        {
            RSDP rsdp = GetRsdp();
            ulong xsdtAddress = rsdp.XsdtAddress;

            if (xsdtAddress == 0)
                return new XSDT();

            SDTHeader xsdtHeader = GetHeader<SDTHeader>(xsdtAddress);
            byte[] rawTable = io.ReadMemory(new IntPtr((long)xsdtAddress), (int)xsdtHeader.Length);

            if (rawTable == null)
                return new XSDT();

            int headerSize = Marshal.SizeOf(typeof(SDTHeader));
            int dataSize = (int)xsdtHeader.Length - headerSize;
            XSDT xsdtTable = new XSDT
            {
                Header = xsdtHeader,
                Data = new ulong[dataSize / sizeof(ulong)],
            };
            Buffer.BlockCopy(rawTable, headerSize, xsdtTable.Data, 0, dataSize);
            return xsdtTable;
        }

        public static ACPITable ParseSdtTable(byte[] rawTable)
        {
            if (rawTable == null) throw new ArgumentNullException(nameof(rawTable));

            GCHandle handle = GCHandle.Alloc(rawTable, GCHandleType.Pinned);
            SDTHeader rawHeader;
            try
            {
                rawHeader = (SDTHeader)Marshal.PtrToStructure(handle.AddrOfPinnedObject(), typeof(SDTHeader));
            }
            finally
            {
                handle.Free();
            }

            int headerSize = Marshal.SizeOf(typeof(SDTHeader));
            int dataSize = Math.Max(0, (int)rawHeader.Length - headerSize);
            ACPITable acpiTable = new ACPITable
            {
                RawHeader = rawHeader,
                Header = ParseRawHeader(rawHeader),
                Data = new byte[dataSize],
            };
            if (dataSize > 0)
                Buffer.BlockCopy(rawTable, headerSize, acpiTable.Data, 0, dataSize);
            return acpiTable;
        }

        private static T ReadMemory<T>(IntPtr address)
        {
            T result = default;
            Marshal.PtrToStructure(address, result);
            return result;
        }
    }
}
