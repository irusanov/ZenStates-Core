using System;
using System.Runtime.InteropServices;

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
            // Region signatures
            public const string AODE = "AODE";
            public const string AODT = "AODT";
        }

        internal const uint RSDP_REGION_BASE_ADDRESS = 0x0E0000;
        internal const int RSDP_REGION_LENGTH = 0x01FFFF;

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
            public uint[] Data;
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

        // https://github.com/irusanov/acpi/blob/main/acpi/src/address.rs
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

        private readonly IOModule io;

        public ACPI(IOModule io)
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
                val |= (uint)ascii[i] << i * 8;
            }
            return val;
        }

        public static ulong SignatureUL(string ascii)
        {
            ulong val = 0x0;
            int length = Math.Min(ascii.Length, 8);

            for (int i = 0; i < length; i++)
            {
                val |= (ulong)ascii[i] << i * 8;
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

        public RSDP GetRsdp()
        {
            byte[] bytes = io.ReadMemory(new IntPtr(RSDP_REGION_BASE_ADDRESS), RSDP_REGION_LENGTH);
            int rsdpOffset = Utils.FindSequence(bytes, 0, ByteSignatureUL(TableSignature.RSDP));

            if (rsdpOffset < 0)
                throw new SystemException("ACPI: Could not find RSDP signature");

            return Utils.ByteArrayToStructure<RSDP>(io.ReadMemory(new IntPtr(RSDP_REGION_BASE_ADDRESS + rsdpOffset), 36));
        }

        public RSDT GetRSDT()
        {
            RSDT rsdtTable;
            RSDP rsdp = GetRsdp();
            SDTHeader rsdtHeader = GetHeader<SDTHeader>(rsdp.RsdtAddress);
            byte[] rawTable = io.ReadMemory(new IntPtr(rsdp.RsdtAddress), (int)rsdtHeader.Length);
            GCHandle handle = GCHandle.Alloc(rawTable, GCHandleType.Pinned);
            try
            {
                int headerSize = Marshal.SizeOf(rsdtHeader);
                int dataSize = (int)rsdtHeader.Length - headerSize;
                rsdtTable = new RSDT()
                {
                    Header = rsdtHeader,
                    Data = new uint[dataSize],
                };
                Buffer.BlockCopy(rawTable, headerSize, rsdtTable.Data, 0, dataSize);
            }
            finally
            {
                handle.Free();
            }
            return rsdtTable;
        }

        public static ACPITable ParseSdtTable(byte[] rawTable)
        {
            ACPITable acpiTable;
            GCHandle handle = GCHandle.Alloc(rawTable, GCHandleType.Pinned);
            try
            {
                SDTHeader rawHeader = (SDTHeader)Marshal.PtrToStructure(handle.AddrOfPinnedObject(), typeof(SDTHeader));
                int headerSize = Marshal.SizeOf(rawHeader);
                int dataSize = (int)rawHeader.Length - headerSize;
                acpiTable = new ACPITable()
                {
                    RawHeader = rawHeader,
                    Header = ParseRawHeader(rawHeader),
                    Data = new byte[dataSize],
                };
                Buffer.BlockCopy(rawTable, headerSize, acpiTable.Data, 0, dataSize);
            }
            finally
            {
                handle.Free();
            }
            return acpiTable;
        }
    }
}
